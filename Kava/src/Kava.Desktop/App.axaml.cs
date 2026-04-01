using System;
using System.IO;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Kava.Persistence;

namespace Kava.Desktop;

public class App : Application
{
    public static CalDavAccountService? AccountService { get; private set; }
    public static SyncService? Sync { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

#if !SAMPLE_DATA
            InitializeServices();
#endif

            var trayManager = new TrayIconManager(desktop);
            trayManager.Initialize();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void InitializeServices()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kava");
        Directory.CreateDirectory(appDataPath);

        var dbPath = Path.Combine(appDataPath, "kava.db");
        var database = new KavaDatabase(dbPath);

#pragma warning disable CA1416 // Windows-only desktop app
        var credentialStore = new FileCredentialStore(appDataPath);
#pragma warning restore CA1416
        AccountService = new CalDavAccountService(database, credentialStore);

        Sync = new SyncService(AccountService, TimeSpan.FromMinutes(15));
        Sync.Start();
    }
}
