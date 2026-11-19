using BreakManager.Application.Abstractions;
using BreakManager.Application.Common;
using BreakManager.Application.Identity;
using BreakManager.Application.Identity.Login;
using BreakManager.Application.Identity.Workstations;
using BreakManager.Contracts.Agent;
using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;
using BreakManager.Web.Hosting;
using BreakManager.Web.Security;
using System.Security.Claims;

namespace BreakManager.Web.Endpoints;

/// <summary>
/// API del agente (HU-01, HU-02, HU-04; ADR-0010). Mapea manualmente los resultados a HTTP.
/// </summary>
internal static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(AgentApiRoutes.Enrollment, EnrollAsync)
            .AllowAnonymous()
            .RequireRateLimiting(AuthenticationExtensions.AuthRateLimitPolicy)
            .DisableAntiforgery();

        endpoints.MapPost(AgentApiRoutes.Sessions, StartLoginAsync)
            .RequireAuthorization(AuthorizationPolicies.Agent)
            .RequireRateLimiting(AuthenticationExtensions.AuthRateLimitPolicy)
            .DisableAntiforgery();

        endpoints.MapPost(AgentApiRoutes.SessionCompletion, CompleteLoginAsync)
            .RequireAuthorization(AuthorizationPolicies.Agent)
            .RequireRateLimiting(AuthenticationExtensions.AuthRateLimitPolicy)
            .DisableAntiforgery();

        endpoints.MapDelete(AgentApiRoutes.CurrentSession, CloseSessionAsync)
            .RequireAuthorization(AuthorizationPolicies.AgentSession)
            .DisableAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> EnrollAsync(
        EnrollRequest request,
        HttpContext context,
        ICommandHandler<RedeemEnrollmentCodeCommand, Result<EnrolledDevice>> handler,
        CancellationToken cancellationToken)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
        return await RunAsync(async () =>
        {
            var result = await handler.HandleAsync(new RedeemEnrollmentCodeCommand(request.Code, clientIp), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(new EnrollResponse(result.Value.WorkstationId, result.Value.WorkstationNumber, result.Value.DeviceToken))
                : ErrorResult(result.Error);
        });
    }

    private static Task<IResult> StartLoginAsync(
        StartLoginRequest request,
        ClaimsPrincipal device,
        ICommandHandler<StartOperatorLoginCommand, Result<LoginOutcome>> handler,
        CancellationToken cancellationToken) =>
        RunAsync(async () =>
        {
            var command = new StartOperatorLoginCommand(
                device.GetGuid(BreakManagerClaims.WorkstationId),
                device.GetGuid(BreakManagerClaims.DeviceCredentialId),
                request.EmployeeNumber,
                request.Pin);
            return ToResponse(await handler.HandleAsync(command, cancellationToken));
        });

    private static Task<IResult> CompleteLoginAsync(
        CompleteLoginRequest request,
        ClaimsPrincipal device,
        ICommandHandler<CompleteOperatorLoginCommand, Result<LoginOutcome>> handler,
        CancellationToken cancellationToken) =>
        RunAsync(async () =>
        {
            var command = new CompleteOperatorLoginCommand(
                device.GetGuid(BreakManagerClaims.WorkstationId),
                device.GetGuid(BreakManagerClaims.DeviceCredentialId),
                request.Ticket,
                request.AcceptedNoticeVersion,
                request.NewPin);
            return ToResponse(await handler.HandleAsync(command, cancellationToken));
        });

    private static Task<IResult> CloseSessionAsync(
        ClaimsPrincipal device,
        ICommandHandler<CloseOperatorSessionCommand, Result> handler,
        CancellationToken cancellationToken) =>
        RunAsync(async () =>
        {
            var result = await handler.HandleAsync(
                new CloseOperatorSessionCommand(device.GetGuid(BreakManagerClaims.SessionId)), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : ErrorResult(result.Error);
        });

    private static IResult ToResponse(Result<LoginOutcome> result) =>
        result.IsFailure
            ? ErrorResult(result.Error)
            : result.Value switch
            {
                LoginOutcome.Opened opened => Results.Ok(new LoginResponse(
                    LoginStatus.Opened,
                    new SessionDto(opened.SessionId, opened.SessionToken, opened.ExpiresAt),
                    Steps: null)),
                LoginOutcome.StepsRequired steps => Results.Ok(new LoginResponse(
                    LoginStatus.StepsRequired,
                    Session: null,
                    new PendingStepsDto(
                        steps.Ticket,
                        steps.ExpiresAt,
                        steps.Notice is null ? null : new PrivacyNoticeDto(steps.Notice.Version, steps.Notice.Text),
                        steps.RequiresPinChange))),
                _ => throw new InvalidOperationException("Resultado de login desconocido."),
            };

    private static async Task<IResult> RunAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ConcurrencyConflictException)
        {
            return Results.Json(
                new AgentError("Concurrency.Conflict", "Otra operación modificó los mismos datos. Volvé a intentar."),
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static IResult ErrorResult(Error error)
    {
        var status = error.Code switch
        {
            var code when code == IdentityErrors.OperatorLockedOut.Code => StatusCodes.Status423Locked,
            var code when code == PrivacyNoticeErrors.NonePublished.Code => StatusCodes.Status503ServiceUnavailable,
            var code when code == IdentityErrors.InvalidSession.Code => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status400BadRequest,
        };

        return Results.Json(new AgentError(error.Code, error.Message), statusCode: status);
    }
}
