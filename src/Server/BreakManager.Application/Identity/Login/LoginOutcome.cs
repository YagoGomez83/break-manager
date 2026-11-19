namespace BreakManager.Application.Identity.Login;

/// <summary>
/// Resultado de un paso del login del agente: la sesión abierta, o los pasos que faltan con un ticket.
/// Los <c>ToString</c> no muestran tokens ni tickets.
/// </summary>
public abstract record LoginOutcome
{
    private LoginOutcome()
    {
    }

    /// <summary>Sesión abierta. El token en claro se entrega una sola vez.</summary>
    public sealed record Opened(Guid SessionId, string SessionToken, DateTimeOffset ExpiresAt, Guid OperatorId) : LoginOutcome
    {
        public override string ToString() => $"Opened {{ SessionId = {SessionId}, OperatorId = {OperatorId} }}";
    }

    /// <summary>Faltan pasos (aceptar el aviso, cambiar el PIN) antes de abrir la sesión.</summary>
    public sealed record StepsRequired(string Ticket, DateTimeOffset ExpiresAt, NoticeToAccept? Notice, bool RequiresPinChange)
        : LoginOutcome
    {
        public override string ToString() =>
            $"StepsRequired {{ NoticeVersion = {Notice?.Version}, RequiresPinChange = {RequiresPinChange} }}";
    }
}

public sealed record NoticeToAccept(int Version, string Text);
