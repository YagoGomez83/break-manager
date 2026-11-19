using BreakManager.Application.Abstractions;
using BreakManager.Application.Common;
using BreakManager.Application.Common.Security;
using BreakManager.Application.Presence;
using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;
using BreakManager.Domain.Presence;
using Microsoft.Extensions.Options;

namespace BreakManager.Application.Identity.Login;

/// <summary>Paso 1 del login del agente: legajo + PIN en un puesto autenticado (HU-01).</summary>
public sealed record StartOperatorLoginCommand(Guid WorkstationId, Guid DeviceCredentialId, string EmployeeNumber, string Pin)
{
    public override string ToString() =>
        $"StartOperatorLoginCommand {{ WorkstationId = {WorkstationId}, DeviceCredentialId = {DeviceCredentialId} }}";
}

internal sealed class StartOperatorLoginHandler(
    IOperatorIdentityProvider identityProvider,
    IOperatorRepository operators,
    IPrivacyNoticeRepository notices,
    ILoginTicketRepository tickets,
    IWorkstationSessionService sessions,
    ITokenService tokens,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    IOptions<IdentityParameters> parameters) : ICommandHandler<StartOperatorLoginCommand, Result<LoginOutcome>>
{
    public async Task<Result<LoginOutcome>> HandleAsync(StartOperatorLoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var notice = await notices.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (notice is null)
        {
            return PrivacyNoticeErrors.NonePublished;
        }

        var authenticated = await identityProvider
            .AuthenticateAsync(new OperatorCredentials(command.EmployeeNumber, command.Pin), command.WorkstationId, cancellationToken)
            .ConfigureAwait(false);
        if (authenticated.IsFailure)
        {
            return authenticated.Error;
        }

        var op = await operators.GetByIdAsync(authenticated.Value, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("El operador autenticado no existe.");

        var noticeToAccept = await notices.HasAcceptedAsync(op.Id, notice.Version, cancellationToken).ConfigureAwait(false)
            ? null
            : notice;

        if (noticeToAccept is null && !op.MustChangePin)
        {
            var opened = await sessions.OpenAsync(command.WorkstationId, op.Id, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new LoginOutcome.Opened(opened.SessionId, opened.Token, opened.ExpiresAt, op.Id);
        }

        var pending = LoginSteps.IssueTicket(
            tickets, tokens, time, parameters.Value, op, command.DeviceCredentialId, noticeToAccept);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return pending;
    }
}

/// <summary>
/// Paso 2: con el ticket, la versión del aviso aceptada y el PIN nuevo (si se pidieron), abre la sesión.
/// Consumir el ticket, registrar la aceptación, cambiar el PIN y abrir la sesión se guardan juntos.
/// </summary>
public sealed record CompleteOperatorLoginCommand(
    Guid WorkstationId,
    Guid DeviceCredentialId,
    string Ticket,
    int? AcceptedNoticeVersion,
    string? NewPin)
{
    public override string ToString() =>
        $"CompleteOperatorLoginCommand {{ WorkstationId = {WorkstationId}, DeviceCredentialId = {DeviceCredentialId}, "
        + $"AcceptedNoticeVersion = {AcceptedNoticeVersion} }}";
}

internal sealed class CompleteOperatorLoginHandler(
    IOperatorRepository operators,
    IPrivacyNoticeRepository notices,
    ILoginTicketRepository tickets,
    IWorkstationSessionService sessions,
    ITokenService tokens,
    IPinHasher pinHasher,
    ISecurityEventLog securityEvents,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    IOptions<IdentityParameters> parameters) : ICommandHandler<CompleteOperatorLoginCommand, Result<LoginOutcome>>
{
    public async Task<Result<LoginOutcome>> HandleAsync(CompleteOperatorLoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = time.GetUtcNow();
        var ticket = string.IsNullOrWhiteSpace(command.Ticket)
            ? null
            : await tickets.GetByHashAsync(tokens.Hash(command.Ticket), cancellationToken).ConfigureAwait(false);
        if (ticket is null)
        {
            return await RejectAsync(null, command.WorkstationId, "LoginTicket.Unknown", now, cancellationToken).ConfigureAwait(false);
        }

        var validation = ticket.Validate(now, command.DeviceCredentialId, command.AcceptedNoticeVersion);
        if (validation.IsFailure)
        {
            return validation.Error == LoginTicketErrors.NoticeVersionMismatch
                ? validation.Error
                : await RejectAsync(ticket.OperatorId, command.WorkstationId, validation.Error.Code, now, cancellationToken)
                    .ConfigureAwait(false);
        }

        var op = await operators.GetByIdAsync(ticket.OperatorId, cancellationToken).ConfigureAwait(false);
        if (op is null || !op.Active)
        {
            return IdentityErrors.InvalidCredentials;
        }

        // Si mientras tanto se publicó otra versión del aviso, se pide de nuevo con un ticket nuevo.
        var notice = await notices.GetCurrentAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Hay un ticket pendiente pero no hay aviso publicado.");
        var noticeToAccept = await notices.HasAcceptedAsync(op.Id, notice.Version, cancellationToken).ConfigureAwait(false)
            ? null
            : notice;
        if (noticeToAccept?.Version != ticket.NoticeVersion)
        {
            ticket.Consume(now, command.DeviceCredentialId, command.AcceptedNoticeVersion);
            var again = LoginSteps.IssueTicket(tickets, tokens, time, parameters.Value, op, command.DeviceCredentialId, noticeToAccept);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return again;
        }

        if (op.MustChangePin)
        {
            var changed = ChangePin(op, command.NewPin, command.WorkstationId, now);
            if (changed.IsFailure)
            {
                return changed.Error;
            }
        }

        ticket.Consume(now, command.DeviceCredentialId, command.AcceptedNoticeVersion);

        if (noticeToAccept is not null)
        {
            notices.AddAcceptance(noticeToAccept.Accept(Guid.CreateVersion7(now), op.Id, command.WorkstationId, now));
        }

        var opened = await sessions.OpenAsync(command.WorkstationId, op.Id, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new LoginOutcome.Opened(opened.SessionId, opened.Token, opened.ExpiresAt, op.Id);
    }

    private Result ChangePin(Operator op, string? newPin, Guid workstationId, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(newPin))
        {
            return Result.Failure(IdentityErrors.NewPinRequired);
        }

        var pin = Pin.Create(newPin, op.EmployeeNumber);
        if (pin.IsFailure)
        {
            return Result.Failure(pin.Error);
        }

        var changed = op.ChangePin(pin.Value, pinHasher);
        if (changed.IsSuccess)
        {
            securityEvents.Record(new SecurityEvent(
                Guid.CreateVersion7(now), SecurityEventType.OperatorPinChanged, now, SecurityActorType.Operator, op.Id, workstationId));
        }

        return changed;
    }

    private async Task<Result<LoginOutcome>> RejectAsync(
        Guid? operatorId,
        Guid workstationId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        securityEvents.Record(new SecurityEvent(
            Guid.CreateVersion7(now),
            SecurityEventType.LoginTicketRejected,
            now,
            SecurityActorType.Device,
            operatorId,
            workstationId,
            new Dictionary<string, string> { ["reason"] = reason }));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return IdentityErrors.InvalidLoginTicket;
    }
}

public sealed record CloseOperatorSessionCommand(Guid SessionId);

internal sealed class CloseOperatorSessionHandler(IWorkstationSessionService sessions, IUnitOfWork unitOfWork)
    : ICommandHandler<CloseOperatorSessionCommand, Result>
{
    public async Task<Result> HandleAsync(CloseOperatorSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var closed = await sessions.CloseAsync(command.SessionId, SessionEndReason.Logout, cancellationToken).ConfigureAwait(false);
        if (closed.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return closed;
    }
}

internal static class LoginSteps
{
    public static LoginOutcome.StepsRequired IssueTicket(
        ILoginTicketRepository tickets,
        ITokenService tokens,
        TimeProvider time,
        IdentityParameters parameters,
        Operator op,
        Guid deviceCredentialId,
        PrivacyNotice? noticeToAccept)
    {
        var now = time.GetUtcNow();
        var plain = tokens.GenerateToken();
        var ticket = LoginTicket.Issue(
            Guid.CreateVersion7(now),
            op.Id,
            deviceCredentialId,
            tokens.Hash(plain),
            noticeToAccept?.Version,
            op.MustChangePin,
            now,
            parameters.LoginTicketLifetime);
        tickets.Add(ticket);

        var notice = noticeToAccept is null ? null : new NoticeToAccept(noticeToAccept.Version, noticeToAccept.Text);
        return new LoginOutcome.StepsRequired(plain, ticket.ExpiresAt, notice, op.MustChangePin);
    }
}
