using System.ComponentModel;
using System.Globalization;
using System.Windows;
using BreakManager.Agent.Core.Identity;

namespace BreakManager.Agent.Windows;

/// <summary>
/// Ventana del agente: muestra el paso actual de <see cref="LoginFlow"/> y le pasa lo que carga el operador.
/// Toda la lógica está en Agent.Core; acá solo hay presentación.
/// </summary>
public partial class AgentWindow : Window
{
    private readonly LoginFlow _flow;

    internal AgentWindow(LoginFlow flow)
    {
        _flow = flow;
        InitializeComponent();
        _flow.Changed += (_, _) => Dispatcher.Invoke(Render);
        Render();
    }

    /// <summary>Cerrar la ventana solo la oculta: el agente sigue en la bandeja.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        e.Cancel = true;
        Hide();
    }

    private void Render()
    {
        EnrollPanel.Visibility = Show(LoginStep.NotEnrolled);
        LoginPanel.Visibility = Show(LoginStep.Login);
        NoticePanel.Visibility = Show(LoginStep.NoticePending);
        PinChangePanel.Visibility = Show(LoginStep.PinChangePending);
        SessionPanel.Visibility = Show(LoginStep.SessionOpen);

        var number = _flow.WorkstationNumber?.ToString(CultureInfo.InvariantCulture) ?? "?";
        LoginTitle.Text = $"Ingresar al puesto {number}";
        SessionText.Text = $"Sesión iniciada: puesto {number}";
        NoticeText.Text = _flow.Notice?.Text ?? string.Empty;

        ErrorText.Text = _flow.ErrorMessage ?? string.Empty;
        ErrorText.Visibility = string.IsNullOrEmpty(_flow.ErrorMessage) ? Visibility.Collapsed : Visibility.Visible;

        // Los secretos no quedan en los controles después de usarlos.
        if (_flow.Step != LoginStep.Login)
        {
            PinBox.Clear();
        }

        if (_flow.Step != LoginStep.PinChangePending)
        {
            NewPinBox.Clear();
            ConfirmPinBox.Clear();
        }
    }

    private Visibility Show(LoginStep step) => _flow.Step == step ? Visibility.Visible : Visibility.Collapsed;

    private async void OnEnrollClick(object sender, RoutedEventArgs e) =>
        await RunAsync(() => _flow.EnrollAsync(ServerUrlBox.Text, EnrollmentCodeBox.Text, CancellationToken.None));

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        await RunAsync(() => _flow.LoginAsync(EmployeeNumberBox.Text, PinBox.Password, CancellationToken.None));
        PinBox.Clear();
    }

    private async void OnAcceptNoticeClick(object sender, RoutedEventArgs e) =>
        await RunAsync(() => _flow.AcceptNoticeAsync(CancellationToken.None));

    private void OnRejectNoticeClick(object sender, RoutedEventArgs e) => _flow.RejectNotice();

    private async void OnChangePinClick(object sender, RoutedEventArgs e) =>
        await RunAsync(() => _flow.ChangePinAsync(NewPinBox.Password, ConfirmPinBox.Password, CancellationToken.None));

    private async void OnLogoutClick(object sender, RoutedEventArgs e) =>
        await RunAsync(() => _flow.LogoutAsync(CancellationToken.None));

    private async Task RunAsync(Func<Task> action)
    {
        IsEnabled = false;
        try
        {
            await action();
        }
        finally
        {
            IsEnabled = true;
        }
    }
}
