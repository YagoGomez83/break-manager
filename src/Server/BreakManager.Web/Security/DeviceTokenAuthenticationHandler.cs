using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BreakManager.Application.Abstractions;
using BreakManager.Application.Identity.Workstations;
using BreakManager.Contracts.Agent;
using BreakManager.Domain.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace BreakManager.Web.Security;

/// <summary>
/// Esquema <c>DeviceToken</c> (ADR-0010): <c>Authorization: Device &lt;token&gt;</c> y, opcionalmente,
/// <c>X-Session-Token</c>. Delega en el caso de uso <see cref="AuthenticateDeviceCommand"/>, que registra
/// el último uso, el cambio de IP y los tokens inválidos.
/// </summary>
internal sealed class DeviceTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ICommandHandler<AuthenticateDeviceCommand, Result<AuthenticatedDevice>> authenticate)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DeviceToken";

    private const string SchemePrefix = AgentHeaders.DeviceScheme + " ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers[HeaderNames.Authorization].ToString();
        if (!authorization.StartsWith(SchemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var deviceToken = authorization[SchemePrefix.Length..].Trim();
        if (deviceToken.Length == 0)
        {
            return AuthenticateResult.Fail("Token de dispositivo vacío.");
        }

        var sessionToken = Request.Headers[AgentHeaders.SessionToken].ToString();
        var clientIp = Context.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

        var result = await authenticate.HandleAsync(
            new AuthenticateDeviceCommand(deviceToken, string.IsNullOrWhiteSpace(sessionToken) ? null : sessionToken, clientIp),
            Context.RequestAborted).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return AuthenticateResult.Fail(result.Error.Code);
        }

        var device = result.Value;
        var claims = new List<Claim>
        {
            new(BreakManagerClaims.WorkstationId, device.WorkstationId.ToString()),
            new(BreakManagerClaims.DeviceCredentialId, device.CredentialId.ToString()),
            new(BreakManagerClaims.WorkstationNumber, device.WorkstationNumber.ToString(CultureInfo.InvariantCulture)),
        };

        if (device.Session is { } session)
        {
            claims.Add(new Claim(BreakManagerClaims.SessionId, session.SessionId.ToString()));
            claims.Add(new Claim(BreakManagerClaims.OperatorId, session.OperatorId.ToString()));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Response.WriteAsJsonAsync(new AgentError("Identity.Unauthorized", "El puesto o la sesión no están autorizados."));
    }
}
