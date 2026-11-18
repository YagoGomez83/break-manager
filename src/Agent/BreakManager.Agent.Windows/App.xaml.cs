using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Windows;
using H.NotifyIcon;

namespace BreakManager.Agent.Windows;

/// <summary>
/// Cascarón del agente: arranca minimizado en la bandeja, sin ventana principal, con una sola instancia por PC.
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

    protected override void OnStartup(StartupEventArgs e)
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
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();

        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
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
