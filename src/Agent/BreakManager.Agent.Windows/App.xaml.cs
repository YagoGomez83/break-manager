using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using BreakManager.Agent.Core.Identity;
using BreakManager.Agent.Windows.Identity;
using H.NotifyIcon;

namespace BreakManager.Agent.Windows;

/// <summary>
/// Agente del puesto: arranca en la bandeja con una sola instancia por PC y muestra la ventana de
/// enrolamiento o de login hasta que el operador abre sesión (Fase 2).
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF no dispone la Application; los recursos se liberan en OnExit.")]
public partial class App : Application
{
    // Global\: una instancia por PC aunque haya más de una sesión de Windows abierta.
    private const string SingleInstanceMutexName = @"Global\BreakManager.Agent";

    private Mutex? _singleInstanceMutex;
    private TaskbarIcon? _trayIcon;
    private HttpClient? _http;
    private LoginFlow? _flow;
    private AgentWindow? _window;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            // Ya hay una instancia corriendo: salir sin mostrar nada.
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);

        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        _trayIcon.ForceCreate(enablesEfficiencyMode: false);

        // Validación de certificado por defecto: nunca se desactiva (ADR-0010).
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _flow = new LoginFlow(new AgentApiClient(_http), new DpapiCredentialStore());
        _flow.Changed += (_, _) => Dispatcher.Invoke(UpdateTray);
        _window = new AgentWindow(_flow);

        await _flow.InitializeAsync(CancellationToken.None);
        ShowWindow();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _http?.Dispose();

        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }

    private void ShowWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.Show();
        _window.Activate();
    }

    private void UpdateTray()
    {
        if (_trayIcon?.ContextMenu is null || _flow is null)
        {
            return;
        }

        var sessionOpen = _flow.Step == LoginStep.SessionOpen;
        foreach (var item in _trayIcon.ContextMenu.Items.OfType<MenuItem>().Where(i => Equals(i.Tag, "Logout")))
        {
            item.IsEnabled = sessionOpen;
        }

        _trayIcon.ToolTipText = sessionOpen
            ? $"BreakManager — sesión iniciada (puesto {_flow.WorkstationNumber})"
            : "BreakManager — sin sesión";
    }

    private void OnOpenClick(object sender, RoutedEventArgs e) => ShowWindow();

    private async void OnLogoutClick(object sender, RoutedEventArgs e)
    {
        if (_flow is not null)
        {
            await _flow.LogoutAsync(CancellationToken.None);
            ShowWindow();
        }
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
        MessageBox.Show(
            $"BreakManager — Agente del puesto\nVersión {version}",
            "Acerca de BreakManager",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Shutdown();
}
