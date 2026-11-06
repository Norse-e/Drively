using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Drively.App.Services;
using Drively.App.ViewModels;
using Drively.Backup;
using Drively.Core.Interfaces;
using Drively.Core.Utilities;
using Drively.Data;
using Drively.Data.Repositories;
using Drively.DriverDetection;
using Drively.DriverDetection.Mock;
using Drively.DriverInstallation;
using Drively.DriverUpdates;
using Drively.DriverUpdates.Mock;
using Microsoft.Extensions.DependencyInjection;

namespace Drively.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private ILog _log = NullLog.Instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var demo = e.Args.Any(a => a.Equals("--demo", StringComparison.OrdinalIgnoreCase));
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

        _log = new FileLog(AppPaths.LogDirectory);
        _log.Info($"DRIVELY {version} starting{(demo ? " in demo mode" : "")}.");
        DispatcherUnhandledException += OnUnhandledException;

        try
        {
            _services = ConfigureServices(new AppEnvironment(demo, version));
        }
        catch (Exception ex)
        {
            _log.Error("Startup failed.", ex);
            MessageBox.Show($"DRIVELY could not start: {ex.Message}", "DRIVELY", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        EnsureDefaultDirectories(_services.GetRequiredService<ISettingsRepository>());

        var theme = _services.GetRequiredService<ThemeService>();
        theme.Apply(_services.GetRequiredService<ISettingsRepository>().Load().Theme);

        var main = _services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = main };
        MainWindow = window;
        theme.ApplyTitleBar(window);
        window.Show();

        _ = main.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _log.Info("DRIVELY exiting.");
        _services?.Dispose();
        base.OnExit(e);
    }

    private ServiceProvider ConfigureServices(AppEnvironment environment)
    {
        var services = new ServiceCollection();
        services.AddSingleton(environment);
        services.AddSingleton(_log);

        // Storage
        services.AddSingleton(_ => new DatabaseContext(AppPaths.DatabasePath));
        services.AddSingleton<IHistoryRepository, HistoryRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();

        // Platform
        services.AddSingleton<IElevationService, ElevationService>();
        services.AddSingleton<INetworkStatus, NetworkStatus>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<ISignatureVerifier, WinTrustSignatureVerifier>();

        if (environment.IsDemoMode)
        {
            services.AddSingleton<IHardwareProvider, MockHardwareProvider>();
            services.AddSingleton<ISystemInfoProvider, MockSystemInfoProvider>();
            services.AddSingleton<IDriverStoreReader, MockDriverStoreReader>();
            services.AddSingleton<IUpdateProvider, MockUpdateProvider>();
        }
        else
        {
            services.AddSingleton<IHardwareProvider, WmiHardwareProvider>();
            services.AddSingleton<ISystemInfoProvider, WmiSystemInfoProvider>();
            services.AddSingleton<IDriverStoreReader>(sp => new DriverStoreReader(sp.GetRequiredService<ILog>()));
            services.AddSingleton<IUpdateProvider, WindowsUpdateProvider>();
        }

        // Updates / installation / backup
        services.AddSingleton<DriverRepository>();
        services.AddSingleton<UpdateChecker>();
        services.AddSingleton<ManufacturerProvider>();
        services.AddSingleton<DriverValidator>();
        services.AddSingleton<PnpUtilClient>();
        services.AddSingleton<DriverInstaller>();
        services.AddSingleton(sp => new InstallationManager(
            sp.GetServices<IUpdateProvider>(),
            // Demo mode simulates installs, so it does not need real administrator rights.
            environment.IsDemoMode ? new DemoElevation() : sp.GetRequiredService<IElevationService>(),
            sp.GetRequiredService<IHistoryRepository>(),
            sp.GetRequiredService<ILog>()));
        services.AddSingleton<BackupManager>();
        services.AddSingleton<RestoreManager>();

        // UI
        services.AddSingleton<ThemeService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<DriverSession>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<DriversViewModel>();
        services.AddSingleton<BackupViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }

    private void EnsureDefaultDirectories(ISettingsRepository repository)
    {
        try
        {
            var settings = repository.Load();
            var changed = false;
            if (string.IsNullOrWhiteSpace(settings.BackupDirectory)) { settings.BackupDirectory = AppPaths.DefaultBackupDirectory; changed = true; }
            if (string.IsNullOrWhiteSpace(settings.DownloadDirectory)) { settings.DownloadDirectory = AppPaths.DefaultDownloadDirectory; changed = true; }
            if (changed) repository.Save(settings);
        }
        catch (Exception ex)
        {
            _log.Warn("Could not initialize default directories.", ex);
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log.Error("Unhandled exception.", e.Exception);
        MessageBox.Show($"Something went wrong: {e.Exception.Message}\n\nDetails were written to the local log.",
            "DRIVELY", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private sealed class DemoElevation : IElevationService
    {
        public bool IsElevated => true;
        public bool TryRestartElevated() => false;
    }
}
