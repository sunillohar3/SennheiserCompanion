using System;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SennheiserMomentum4.Models;
using SennheiserMomentum4.Services.Logging;
using SennheiserMomentum4.Services.Settings;
using SennheiserMomentum4.Services.Theme;
using SennheiserMomentum4.Services.Windows;

namespace SennheiserMomentum4.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeManager _themeManager;
    private readonly IStartupService _startupService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IAppLogger _logger;

    [ObservableProperty]
    private AppTheme _selectedTheme;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _closeToTray;

    [ObservableProperty]
    private bool _showNotifications;

    [ObservableProperty]
    private bool _autoConnect;

    [ObservableProperty]
    private bool _rememberLastDevice;

    [ObservableProperty]
    private bool _demoMode;

    [ObservableProperty]
    private string _hotkeyToggleAnc = "Ctrl+Alt+A";

    [ObservableProperty]
    private string _hotkeyToggleTransparency = "Ctrl+Alt+T";

    [ObservableProperty]
    private string _hotkeyPlayPause = "Ctrl+Alt+P";

    [ObservableProperty]
    private string _logContent = string.Empty;

    [ObservableProperty]
    private string _selectedLogCategory = "App";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public string AppVersion => "1.0.0";

    public SettingsViewModel(
        ISettingsService settingsService,
        IThemeManager themeManager,
        IStartupService startupService,
        IGlobalHotkeyService hotkeyService,
        IAppLogger logger)
    {
        _settingsService = settingsService;
        _themeManager = themeManager;
        _startupService = startupService;
        _hotkeyService = hotkeyService;
        _logger = logger;

        LoadSettings();
        RefreshLogs();
    }

    private void LoadSettings()
    {
        var current = _settingsService.Current;
        SelectedTheme = current.Theme;
        StartWithWindows = _startupService.IsStartWithWindowsEnabled();
        StartMinimized = current.StartMinimized;
        MinimizeToTray = current.MinimizeToTray;
        CloseToTray = current.CloseToTray;
        ShowNotifications = current.ShowNotifications;
        AutoConnect = current.AutoConnect;
        RememberLastDevice = current.RememberLastDevice;
        DemoMode = current.DemoMode;

        HotkeyToggleAnc = current.HotkeyToggleAnc;
        HotkeyToggleTransparency = current.HotkeyToggleTransparency;
        HotkeyPlayPause = current.HotkeyPlayPause;
    }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        _themeManager.ApplyTheme(value);
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        _startupService.SetStartWithWindows(value);
        _settingsService.Current.StartWithWindows = value;
        _settingsService.Save();
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        _settingsService.Current.StartMinimized = value;
        _settingsService.Save();
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        _settingsService.Current.MinimizeToTray = value;
        _settingsService.Save();
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        _settingsService.Current.CloseToTray = value;
        _settingsService.Save();
    }

    partial void OnShowNotificationsChanged(bool value)
    {
        _settingsService.Current.ShowNotifications = value;
        _settingsService.Save();
    }

    partial void OnAutoConnectChanged(bool value)
    {
        _settingsService.Current.AutoConnect = value;
        _settingsService.Save();
    }

    partial void OnRememberLastDeviceChanged(bool value)
    {
        _settingsService.Current.RememberLastDevice = value;
        _settingsService.Save();
    }

    partial void OnDemoModeChanged(bool value)
    {
        _settingsService.Current.DemoMode = value;
        _settingsService.Save();
        StatusMessage = value ? "Demo Mode ENABLED. Showing simulated MOMENTUM 4." : "Demo Mode DISABLED. Connecting to real Bluetooth.";
    }

    [RelayCommand]
    private void SaveHotkeys()
    {
        _settingsService.Current.HotkeyToggleAnc = HotkeyToggleAnc;
        _settingsService.Current.HotkeyToggleTransparency = HotkeyToggleTransparency;
        _settingsService.Current.HotkeyPlayPause = HotkeyPlayPause;
        _settingsService.Save();

        _hotkeyService.RegisterHotkeys(HotkeyToggleAnc, HotkeyToggleTransparency, HotkeyPlayPause);
        StatusMessage = "Keyboard shortcuts updated and registered.";
    }

    [RelayCommand]
    private void ResetHotkeys()
    {
        HotkeyToggleAnc = "Ctrl+Alt+A";
        HotkeyToggleTransparency = "Ctrl+Alt+T";
        HotkeyPlayPause = "Ctrl+Alt+P";
        SaveHotkeys();
    }

    [RelayCommand]
    private void RefreshLogs()
    {
        var cat = SelectedLogCategory switch
        {
            "Device" => LogCategory.Device,
            "Bluetooth" => LogCategory.Bluetooth,
            _ => LogCategory.App
        };

        LogContent = _logger.GetLogContent(cat, 150);
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        try
        {
            var dir = _logger.GetLogDirectory();
            if (Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", dir));
            }
        }
        catch { }
    }

    [RelayCommand]
    private void ResetAllSettings()
    {
        _settingsService.Reset();
        LoadSettings();
        _themeManager.ApplyTheme(AppTheme.System);
        StatusMessage = "Settings have been reset to factory defaults.";
    }
}
