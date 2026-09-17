using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using SennheiserMomentum4.Services.Logging;
using SennheiserMomentum4.Services.Settings;
using SennheiserMomentum4.Services.Windows;
using SennheiserMomentum4.ViewModels;

namespace SennheiserMomentum4.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISystemTrayService _trayService;
    private readonly IAppLogger _logger;
    private bool _isExplicitExit = false;

    public MainWindow(
        MainViewModel viewModel,
        ISettingsService settingsService,
        IGlobalHotkeyService hotkeyService,
        ISystemTrayService trayService,
        IAppLogger logger)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;
        _trayService = trayService;
        _logger = logger;

        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _hotkeyService.Initialize(handle);

        var current = _settingsService.Current;
        _hotkeyService.RegisterHotkeys(current.HotkeyToggleAnc, current.HotkeyToggleTransparency, current.HotkeyPlayPause);

        _trayService.Initialize(
            handle,
            showAppAction: () =>
            {
                Dispatcher.Invoke(() =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                });
            },
            showSettingsAction: () =>
            {
                Dispatcher.Invoke(() =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    _viewModel.NavigateTo("Settings");
                });
            },
            exitAction: () =>
            {
                Dispatcher.Invoke(ExitApplication);
            });

        if (current.StartMinimized)
        {
            WindowState = WindowState.Minimized;
            Hide();
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settingsService.Current.MinimizeToTray)
        {
            Hide();
            _logger.App(LogLevel.Info, "Window minimized to system tray.");
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_isExplicitExit && _settingsService.Current.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            _logger.App(LogLevel.Info, "Window close intercepted; minimizing to system tray.");
        }
        else
        {
            _trayService.Dispose();
            _hotkeyService.UnregisterHotkeys();
        }
    }

    public void ExitApplication()
    {
        _isExplicitExit = true;
        _trayService.Dispose();
        _hotkeyService.UnregisterHotkeys();
        Application.Current.Shutdown();
    }
}
