using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SennheiserMomentum4.Services.Device;
using SennheiserMomentum4.Services.Logging;
using SennheiserMomentum4.Services.Settings;
using SennheiserMomentum4.Services.Theme;
using SennheiserMomentum4.Services.Windows;
using SennheiserMomentum4.ViewModels;
using SennheiserMomentum4.Views;

namespace SennheiserMomentum4;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    public static new App Current => (App)Application.Current;
    public IServiceProvider Services => _serviceProvider!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var logger = _serviceProvider.GetRequiredService<IAppLogger>();

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            logger.App(LogLevel.Error, "Unhandled AppDomain exception", ex?.ToString());
        };

        AppDomain.CurrentDomain.ProcessExit += (s, args) =>
        {
            logger.App(LogLevel.Info, "ProcessExit triggered! Stack trace: " + Environment.StackTrace);
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            logger.App(LogLevel.Error, "UnobservedTaskException", args.Exception.ToString());
        };

        DispatcherUnhandledException += (s, args) =>
        {
            logger.App(LogLevel.Error, "Unhandled UI exception", args.Exception?.ToString());
            args.Handled = true; // Prevent abrupt crash
        };

        logger.App(LogLevel.Info, "Starting Sennheiser Companion application...");

        // Set explicit shutdown mode for system tray persistence
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Apply saved theme
        var themeManager = _serviceProvider.GetRequiredService<IThemeManager>();
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        themeManager.ApplyTheme(settingsService.Current.Theme);

        // Show Main Window immediately and register as Application MainWindow
        var mainWindow = _serviceProvider.GetRequiredService<Views.MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

        // Initialize Windows media controls session in background without blocking UI
        _ = Task.Run(async () =>
        {
            try
            {
                var mediaService = _serviceProvider.GetRequiredService<IMediaTransportService>();
                await mediaService.InitializeAsync();
            }
            catch (Exception ex)
            {
                logger.App(LogLevel.Warn, "MediaTransportService init warning", ex.Message);
            }
        });
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<IAppLogger, AppLogger>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IThemeManager, ThemeManager>();

        // Device Service
        services.AddSingleton<IMomentum4DeviceService, Momentum4DeviceManager>();

        // Windows Integration Services
        services.AddSingleton<IMediaTransportService, MediaTransportService>();
        services.AddSingleton<IWindowsAudioService, WindowsAudioService>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<ISystemTrayService, SystemTrayService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddSingleton<Views.MainWindow>();
    }
}
