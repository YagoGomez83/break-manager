using BreakManager.Contracts.Agent;

namespace BreakManager.Agent.Core.Identity;

/// <summary>Paso en el que está el agente (spec de la Fase 2 §7).</summary>
public enum LoginStep
{
    NotEnrolled,
    Login,
    NoticePending,
    PinChangePending,
    SessionOpen,
}

/// <summary>
/// Flujo de enrolamiento y login del agente: login → aviso (si corresponde) → cambio de PIN (si corresponde) → sesión.
/// No depende de WPF: la ventana solo muestra el paso actual y llama a estos métodos.
/// </summary>
public sealed class LoginFlow(IAgentApi api, IDeviceCredentialStore store)
{
    private DeviceCredential? _credential;
    private PendingStepsDto? _pending;
    private SessionDto? _session;
    private string _employeeNumber = string.Empty;
    private bool _noticeAccepted;

    public event EventHandler? Changed;

    public LoginStep Step { get; private set; } = LoginStep.NotEnrolled;

    /// <summary>Último error para mostrar al operador; se limpia en cada acción.</summary>
    public string? ErrorMessage { get; private set; }

    public int? WorkstationNumber => _credential?.WorkstationNumber;

    /// <summary>Aviso a aceptar en <see cref="LoginStep.NoticePending"/>.</summary>
    public PrivacyNoticeDto? Notice => Step == LoginStep.NoticePending ? _pending?.Notice : null;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _credential = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        MoveTo(_credential is null ? LoginStep.NotEnrolled : LoginStep.Login);
    }

    public async Task EnrollAsync(string serverUrl, string code, CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        if (!Uri.TryCreate(serverUrl?.Trim(), UriKind.Absolute, out var server) || server.Scheme != Uri.UriSchemeHttps)
        {
            Fail("La dirección del servidor tiene que empezar con https://");
            return;
        }

        var result = await api.EnrollAsync(server, new EnrollRequest(code ?? string.Empty), cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            Fail(result.Error!.Message);
            return;
        }

        _credential = new DeviceCredential(server, result.Value!.DeviceToken, result.Value.WorkstationNumber);
        await store.SaveAsync(_credential, cancellationToken).ConfigureAwait(false);
        MoveTo(LoginStep.Login);
    }

    public async Task LoginAsync(string employeeNumber, string pin, CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        _employeeNumber = employeeNumber?.Trim() ?? string.Empty;
        var result = await api.StartLoginAsync(Credential(), new StartLoginRequest(_employeeNumber, pin ?? string.Empty), cancellationToken)
            .ConfigureAwait(false);
        await ApplyAsync(result, cancellationToken).ConfigureAwait(false);
    }

    public async Task AcceptNoticeAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        _noticeAccepted = true;
        if (_pending!.RequiresPinChange)
        {
            MoveTo(LoginStep.PinChangePending);
            return;
        }

        await CompleteAsync(newPin: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sin aceptar el aviso no se puede operar: se vuelve al login.</summary>
    public void RejectNotice()
    {
        _pending = null;
        ErrorMessage = "Para usar el puesto tenés que aceptar el aviso.";
        MoveTo(LoginStep.Login);
    }

    public async Task ChangePinAsync(string newPin, string confirmation, CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        if (!string.Equals(newPin, confirmation, StringComparison.Ordinal))
        {
            Fail("Los dos PIN no coinciden.");
            return;
        }

        if (PinRules.Check(newPin, _employeeNumber) is { } problem)
        {
            Fail(problem);
            return;
        }

        await CompleteAsync(newPin, cancellationToken).ConfigureAwait(false);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        if (_session is not null)
        {
            await api.LogoutAsync(Credential(), _session.SessionToken, cancellationToken).ConfigureAwait(false);
        }

        _session = null;
        MoveTo(LoginStep.Login);
    }

    private async Task CompleteAsync(string? newPin, CancellationToken cancellationToken)
    {
        var request = new CompleteLoginRequest(_pending!.Ticket, _noticeAccepted ? _pending.Notice?.Version : null, newPin);
        var result = await api.CompleteLoginAsync(Credential(), request, cancellationToken).ConfigureAwait(false);
        await ApplyAsync(result, cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyAsync(AgentApiResult<LoginResponse> result, CancellationToken cancellationToken)
    {
        if (!result.IsSuccess)
        {
            if (result.StatusCode == 401)
            {
                // Token revocado o reemplazado: hay que volver a enrolar el puesto.
                await store.ClearAsync(cancellationToken).ConfigureAwait(false);
                _credential = null;
                _pending = null;
                ErrorMessage = "El puesto no está autorizado. Pedile al administrador un código de enrolamiento nuevo.";
                MoveTo(LoginStep.NotEnrolled);
                return;
            }

            if (Step is LoginStep.NoticePending or LoginStep.PinChangePending && result.Error?.Code == "Identity.InvalidLoginTicket")
            {
                _pending = null;
                ErrorMessage = result.Error.Message;
                MoveTo(LoginStep.Login);
                return;
            }

            Fail(result.Error!.Message);
            return;
        }

        var response = result.Value!;
        if (response.Status == LoginStatus.Opened)
        {
            _session = response.Session;
            _pending = null;
            MoveTo(LoginStep.SessionOpen);
            return;
        }

        _pending = response.Steps;
        _noticeAccepted = false;
        MoveTo(_pending!.Notice is not null ? LoginStep.NoticePending : LoginStep.PinChangePending);
    }

    private DeviceCredential Credential() =>
        _credential ?? throw new InvalidOperationException("El puesto no está enrolado.");

    private void Fail(string message)
    {
        ErrorMessage = message;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void MoveTo(LoginStep step)
    {
        Step = step;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
