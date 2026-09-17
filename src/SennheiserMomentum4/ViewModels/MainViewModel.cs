using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SennheiserMomentum4.Models;
using SennheiserMomentum4.Services.Device;
using SennheiserMomentum4.Services.Logging;
using SennheiserMomentum4.Services.Settings;
using SennheiserMomentum4.Services.Theme;
using SennheiserMomentum4.Services.Windows;

namespace SennheiserMomentum4.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IMomentum4DeviceService _deviceService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeManager _themeManager;
    private readonly IMediaTransportService _mediaService;
    private readonly IWindowsAudioService _audioService;
    private readonly IStartupService _startupService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly INotificationService _notificationService;
    private readonly IAppLogger _logger;

    [ObservableProperty]
    private ViewModelBase? _currentPageViewModel;

    [ObservableProperty]
    private string _currentNavKey = "Dashboard";

    [ObservableProperty]
    private DeviceInfo _deviceInfo;

    [ObservableProperty]
    private bool _isDemoMode;

    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private string _statusBannerMessage = string.Empty;

    public DashboardViewModel DashboardVM { get; }
    public SoundViewModel SoundVM { get; }
    public NoiseControlViewModel NoiseControlVM { get; }
    public DeviceViewModel DeviceVM { get; }
    public SettingsViewModel SettingsVM { get; }

    public MainViewModel(
        IMomentum4DeviceService deviceService,
        ISettingsService settingsService,
        IThemeManager themeManager,
        IMediaTransportService mediaService,
        IWindowsAudioService audioService,
        IStartupService startupService,
        IGlobalHotkeyService hotkeyService,
        INotificationService notificationService,
        IAppLogger logger)
    {
        _deviceService = deviceService;
        _settingsService = settingsService;
        _themeManager = themeManager;
        _mediaService = mediaService;
        _audioService = audioService;
        _startupService = startupService;
        _hotkeyService = hotkeyService;
        _notificationService = notificationService;
        _logger = logger;

        _deviceInfo = _deviceService.DeviceInfo.Clone();
        _isDemoMode = _deviceService.IsDemoMode;
        _isDarkTheme = _themeManager.IsDark;

        // Initialize child ViewModels
        DashboardVM = new DashboardViewModel(_deviceService, _mediaService, _audioService, _settingsService, _logger, NavigateTo);
        SoundVM = new SoundViewModel(_deviceService, _settingsService, _logger);
        NoiseControlVM = new NoiseControlViewModel(_deviceService, _logger);
        DeviceVM = new DeviceViewModel(_deviceService, _logger);
        SettingsVM = new SettingsViewModel(_settingsService, _themeManager, _startupService, _hotkeyService, _logger);

        // Default page
        CurrentPageViewModel = DashboardVM;

        // Hook device events
        _deviceService.DeviceInfoChanged += (s, info) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                DeviceInfo = info.Clone();
                OnPropertyChanged(nameof(DeviceInfo));
            });
        };

        if (_deviceService is Momentum4DeviceManager manager)
        {
            manager.ModeSwitched += (s, isDemo) =>
            {
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    IsDemoMode = isDemo;
                });
            };
        }

        _themeManager.ThemeChanged += (s, isDark) =>
        {
            IsDarkTheme = isDark;
        };

        // Wire global hotkeys
        _hotkeyService.ToggleAncRequested += OnHotkeyToggleAnc;
        _hotkeyService.ToggleTransparencyRequested += OnHotkeyToggleTransparency;
        _hotkeyService.PlayPauseRequested += OnHotkeyPlayPause;

        // Auto-connect if enabled
        if (_settingsService.Current.AutoConnect)
        {
            _ = _deviceService.ConnectAsync();
        }
    }

    [RelayCommand]
    public void NavigateTo(string navKey)
    {
        CurrentNavKey = navKey;
        CurrentPageViewModel = navKey switch
        {
            "Dashboard" => DashboardVM,
            "Sound" => SoundVM,
            "NoiseControl" => NoiseControlVM,
            "Device" => DeviceVM,
            "Settings" => SettingsVM,
            _ => DashboardVM
        };
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _themeManager.ToggleTheme();
    }

    private async void OnHotkeyToggleAnc()
    {
        _logger.App(LogLevel.Info, "Hotkey triggered: Toggle ANC");
        var current = _deviceService.NoiseControl;
        var newMode = current.Mode == NoiseControlMode.AdaptiveAnc ? NoiseControlMode.Off : NoiseControlMode.AdaptiveAnc;
        var newState = new NoiseControlState
        {
            Mode = newMode,
            AncIntensity = current.AncIntensity,
            TransparencyIntensity = current.TransparencyIntensity,
            WindNoiseReduction = current.WindNoiseReduction
        };
        await _deviceService.SetNoiseControlAsync(newState);
    }

    private async void OnHotkeyToggleTransparency()
    {
        _logger.App(LogLevel.Info, "Hotkey triggered: Toggle Transparency");
        var current = _deviceService.NoiseControl;
        var newMode = current.Mode == NoiseControlMode.Transparency ? NoiseControlMode.Off : NoiseControlMode.Transparency;
        var newState = new NoiseControlState
        {
            Mode = newMode,
            AncIntensity = current.AncIntensity,
            TransparencyIntensity = current.TransparencyIntensity,
            WindNoiseReduction = current.WindNoiseReduction
        };
        await _deviceService.SetNoiseControlAsync(newState);
    }

    private async void OnHotkeyPlayPause()
    {
        _logger.App(LogLevel.Info, "Hotkey triggered: Play/Pause");
        await _mediaService.TogglePlayPauseAsync();
    }
}
