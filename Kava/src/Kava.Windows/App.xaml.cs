using Microsoft.UI.Xaml;

namespace Kava.Windows;

public partial class App : Application
{
    private TrayIconManager? _trayManager;
    private Window? _hiddenWindow;

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Create a hidden window to bootstrap WinUI XAML infrastructure.
        // Without this, creating windows later from tray icon callbacks
        // fails with RPC_E_WRONG_THREAD because the XAML thread context
        // isn't established.
        _hiddenWindow = new Window();
        _hiddenWindow.AppWindow.Hide();

        _trayManager = new TrayIconManager();
        _trayManager.Initialize();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        CrashLog.Write("UNHANDLED EXCEPTION", e.Exception);
        e.Handled = true; // prevent crash, log instead
    }
}
