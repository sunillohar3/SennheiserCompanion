using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SennheiserMomentum4.Models;
using SennheiserMomentum4.Services.Device;
using SennheiserMomentum4.Services.Logging;
using SennheiserMomentum4.Services.Settings;
using SennheiserMomentum4.Services.Windows;

namespace SennheiserMomentum4.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IMomentum4DeviceService _deviceService;
    private readonly IMediaTransportService _mediaService;
    private readonly IWindowsAudioService _audioService;
    private readonly IAppLogger _logger;
    private readonly Action<string> _navigateAction;

    [ObservableProperty]
    private DeviceInfo _deviceInfo;

    partial void OnDeviceInfoChanged(DeviceInfo value)
    {
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsDisconnected));
        OnPropertyChanged(nameof(IsConnecting));
        OnPropertyChanged(nameof(ConnectionStatusColor));
        OnPropertyChanged(nameof(PcAudioStatusText));
    }

    [ObservableProperty]
    private NoiseControlState _noiseControl;

    [ObservableProperty]
    private string _currentPresetName = "Neutral";

    [ObservableProperty]
    private MediaPlaybackInfo _mediaInfo;

    [ObservableProperty]
    private int _systemVolume;

    [ObservableProperty]
    private bool _isSystemMuted;

    [ObservableProperty]
    private string _lastOperationStatus = string.Empty;

    [ObservableProperty]
    private string _selectedColorVariant = "Black"; // "Black", "White", or "Denim"

    [ObservableProperty]
    private bool _isBassBoostEnabled = false;

    [ObservableProperty]
    private bool _isPodcastModeEnabled = false;

    public string ProductImageSource => SelectedColorVariant switch
    {
        "White" => "pack://application:,,,/Resources/Images/m4_white.png",
        "Denim" => "pack://application:,,,/Resources/Images/m4_denim.png",
        "Copper" => "pack://application:,,,/Resources/Images/m4_copper.png",
        "Graphite" => "pack://application:,,,/Resources/Images/m4_graphite.png",
        _ => "pack://application:,,,/Resources/Images/m4_black.png"
    };

    public string ColorFinishDisplay => SelectedColorVariant switch
    {
        "Denim" => "Denim Edition",
        "White" => "White & Silver",
        "Copper" => "Copper Edition",
        "Graphite" => "Graphite Edition",
        _ => "Matte Black"
    };

    partial void OnSelectedColorVariantChanged(string value)
    {
        OnPropertyChanged(nameof(ProductImageSource));
        OnPropertyChanged(nameof(ColorFinishDisplay));
    }

    [ObservableProperty]
    private bool _isPcAudioActive = false;

    private System.Threading.Timer? _audioStreamPollTimer;

    public string PcAudioStatusText => IsPcAudioActive
        ? $"Active Audio Stream • {DeviceInfo?.AudioCodec ?? "AAC (48.0 kHz)"}"
        : "Standby / Idle • Audio Paused";

    public string MobileAudioStatusText => IsPcAudioActive
        ? "Multi-Point Paired • Standby"
        : "Multi-Point Priority • Audio Active / Ready";

    public string MultiPointBadgeText => IsPcAudioActive
        ? "Audio Source: This PC"
        : "PC Audio Idle • Headphone Multi-Point Ready";

    partial void OnIsPcAudioActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(PcAudioStatusText));
        OnPropertyChanged(nameof(MobileAudioStatusText));
        OnPropertyChanged(nameof(MultiPointBadgeText));
    }

    private readonly ISettingsService _settingsService;

    public bool IsConnected => DeviceInfo.State == ConnectionState.Connected;
    public bool IsDisconnected => DeviceInfo.State == ConnectionState.Disconnected;
    public bool IsConnecting => DeviceInfo.State == ConnectionState.Connecting;
    public bool IsDemoMode => _deviceService.IsDemoMode;
    public string AudioDeviceName => _audioService.DefaultDeviceName;
    public bool IsSennheiserAudioDevice => _audioService.IsSennheiserDevice;

    public string ConnectionStatusColor => DeviceInfo.State switch
    {
        ConnectionState.Connected => "#30D158",
        ConnectionState.Connecting => "#FFD60A",
        _ => "#8E8E93"
    };

    public bool IsAdaptiveAnc => NoiseControl?.Mode == NoiseControlMode.AdaptiveAnc;
    public bool IsTransparency => NoiseControl?.Mode == NoiseControlMode.Transparency;
    public bool IsOff => NoiseControl?.Mode == NoiseControlMode.Off;

    public string ActiveNoiseModeDisplay => NoiseControl?.Mode switch
    {
        NoiseControlMode.AdaptiveAnc => "Adaptive ANC",
        NoiseControlMode.Transparency => "Transparency",
        NoiseControlMode.Off => "Off",
        _ => "Adaptive ANC"
    };

    partial void OnNoiseControlChanged(NoiseControlState value)
    {
        NotifyNoiseControlProperties();
    }

    private void NotifyNoiseControlProperties()
    {
        OnPropertyChanged(nameof(IsAdaptiveAnc));
        OnPropertyChanged(nameof(IsTransparency));
        OnPropertyChanged(nameof(IsOff));
        OnPropertyChanged(nameof(ActiveNoiseModeDisplay));
    }

    public DashboardViewModel(
        IMomentum4DeviceService deviceService,
        IMediaTransportService mediaService,
        IWindowsAudioService audioService,
        ISettingsService settingsService,
        IAppLogger logger,
        Action<string> navigateAction)
    {
        _deviceService = deviceService;
        _mediaService = mediaService;
        _audioService = audioService;
        _settingsService = settingsService;
        _logger = logger;
        _navigateAction = navigateAction;

        DeviceInfo = _deviceService.DeviceInfo.Clone();
        NoiseControl = _deviceService.NoiseControl;
        CurrentPresetName = _deviceService.CurrentEqualizer.Name;
        MediaInfo = _mediaService.CurrentMedia;

        _systemVolume = _audioService.Volume;
        _isSystemMuted = _audioService.IsMuted;

        _selectedColorVariant = !string.IsNullOrEmpty(_deviceService.DeviceInfo.ColorVariant)
            ? _deviceService.DeviceInfo.ColorVariant
            : (!string.IsNullOrEmpty(_settingsService.Current.DeviceColorVariant)
                ? _settingsService.Current.DeviceColorVariant
                : "Denim");
        _isBassBoostEnabled = _deviceService.IsBassBoostEnabled;

        _deviceService.DeviceInfoChanged += (s, e) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                DeviceInfo = e.Clone();
                if (!string.IsNullOrWhiteSpace(e.ColorVariant) && e.ColorVariant != SelectedColorVariant)
                {
                    SelectedColorVariant = e.ColorVariant;
                    _settingsService.Current.DeviceColorVariant = e.ColorVariant;
                    _settingsService.Save();
                }
                OnPropertyChanged(nameof(DeviceInfo));
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(IsDisconnected));
                OnPropertyChanged(nameof(IsConnecting));
                OnPropertyChanged(nameof(ConnectionStatusColor));
                OnPropertyChanged(nameof(PcAudioStatusText));
            });
        };

        _deviceService.BassBoostChanged += (s, enabled) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsBassBoostEnabled = enabled;
                OnPropertyChanged(nameof(IsBassBoostEnabled));
            });
        };

        if (_deviceService is Momentum4DeviceManager manager)
        {
            manager.ModeSwitched += (s, isDemo) =>
            {
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    OnPropertyChanged(nameof(IsDemoMode));
                    OnPropertyChanged(nameof(IsConnected));
                    OnPropertyChanged(nameof(IsDisconnected));
                    OnPropertyChanged(nameof(ConnectionStatusColor));
                });
            };
        }

        _deviceService.NoiseControlChanged += (s, e) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                NoiseControl = e.Clone();
                OnPropertyChanged(nameof(NoiseControl));
                NotifyNoiseControlProperties();
            });
        };

        _deviceService.EqualizerChanged += (s, e) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                CurrentPresetName = e.Name;
                OnPropertyChanged(nameof(CurrentPresetName));
            });
        };

        _mediaService.MediaChanged += (s, e) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                MediaInfo = e;
                UpdateAudioStreamState();
            });
        };

        _audioService.VolumeChanged += (s, vol) =>
        {
            // Ignore system echoes while the user is adjusting volume
            if (Environment.TickCount64 - _lastUserVolumeChangeTimestamp < 600) return;

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (Environment.TickCount64 - _lastUserVolumeChangeTimestamp < 600) return;
                if (Math.Abs(SystemVolume - vol) >= 1)
                {
                    _isSyncingVolume = true;
                    try
                    {
                        SystemVolume = vol;
                    }
                    finally
                    {
                        _isSyncingVolume = false;
                    }
                }
            });
        };

        _audioService.MuteChanged += (s, muted) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsSystemMuted = muted;
            });
        };

        _audioStreamPollTimer = new System.Threading.Timer(_ =>
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(UpdateAudioStreamState);
            }
            catch { }
        }, null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(1000));
    }

    private void UpdateAudioStreamState()
    {
        bool active = (MediaInfo?.IsPlaying == true) || _audioService.IsAudioActive;
        if (IsPcAudioActive != active)
        {
            IsPcAudioActive = active;
        }
    }

    private long _lastUserVolumeChangeTimestamp = 0;
    private volatile bool _isSyncingVolume = false;

    partial void OnSystemVolumeChanged(int value)
    {
        if (_isSyncingVolume) return;

        _lastUserVolumeChangeTimestamp = Environment.TickCount64;
        _audioService.Volume = value;
    }

    [RelayCommand]
    private async Task SetQuickNoiseControlAsync(string modeString)
    {
        if (Enum.TryParse<NoiseControlMode>(modeString, true, out var mode))
        {
            if (NoiseControl != null)
            {
                NoiseControl.Mode = mode;
                NotifyNoiseControlProperties();
            }

            var newState = new NoiseControlState
            {
                Mode = mode,
                AncIntensity = NoiseControl?.AncIntensity ?? 80.0,
                TransparencyIntensity = NoiseControl?.TransparencyIntensity ?? 70.0,
                WindNoiseReduction = NoiseControl?.WindNoiseReduction ?? true
            };

            var res = await _deviceService.SetNoiseControlAsync(newState);
            LastOperationStatus = res.Message;
            NoiseControl = _deviceService.NoiseControl.Clone();
            OnPropertyChanged(nameof(NoiseControl));
            NotifyNoiseControlProperties();
        }
    }

    [RelayCommand]
    private async Task ToggleBassBoostAsync()
    {
        bool targetState = !IsBassBoostEnabled;
        var res = await _deviceService.SetBassBoostAsync(targetState);
        if (res.IsSuccess)
        {
            IsBassBoostEnabled = targetState;
            LastOperationStatus = targetState ? "💥 Bass Boost activated on MOMENTUM 4 DSP." : "Bass Boost deactivated.";
        }
        else
        {
            LastOperationStatus = res.Message;
        }
    }

    [RelayCommand]
    private async Task TogglePodcastModeAsync()
    {
        IsPodcastModeEnabled = !IsPodcastModeEnabled;
        var cur = _deviceService.CurrentEqualizer.Clone();
        if (cur.Bands.Count > 2)
        {
            cur.Bands[2].GainDb = IsPodcastModeEnabled ? 3.5 : 0.0;
            if (cur.Bands.Count > 3) cur.Bands[3].GainDb = IsPodcastModeEnabled ? 2.0 : 0.0;
        }
        await _deviceService.SetEqualizerAsync(cur);
        LastOperationStatus = IsPodcastModeEnabled ? "Podcast vocal clarity mode activated." : "Podcast mode deactivated.";
    }

    [RelayCommand]
    private void SelectColor(string color)
    {
        SelectedColorVariant = color;
        _deviceService.DeviceInfo.ColorVariant = color;
        _settingsService.Current.DeviceColorVariant = color;
        _settingsService.Save();
        LastOperationStatus = $"Headphone finish set to {color}.";
    }

    [RelayCommand]
    private async Task TogglePlayPauseAsync()
    {
        await _mediaService.TogglePlayPauseAsync();
    }

    [RelayCommand]
    private async Task NextTrackAsync()
    {
        await _mediaService.NextAsync();
    }

    [RelayCommand]
    private async Task PreviousTrackAsync()
    {
        await _mediaService.PreviousAsync();
    }

    [RelayCommand]
    private void ToggleMute()
    {
        _audioService.IsMuted = !_audioService.IsMuted;
        IsSystemMuted = _audioService.IsMuted;
    }

    [RelayCommand]
    private void OpenWindowsSoundSettings()
    {
        _audioService.OpenSoundSettings();
    }

    [RelayCommand]
    private void OpenWindowsBluetoothSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.App(LogLevel.Warn, "Failed to open bluetooth settings", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ConnectDeviceAsync()
    {
        LastOperationStatus = "Connecting to MOMENTUM 4...";
        var res = await _deviceService.ConnectAsync();
        LastOperationStatus = res.Message;
        OnPropertyChanged(nameof(ConnectionStatusColor));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsDisconnected));
        OnPropertyChanged(nameof(IsConnecting));
    }

    [RelayCommand]
    private async Task DisconnectDeviceAsync()
    {
        var res = await _deviceService.DisconnectAsync();
        LastOperationStatus = res.Message;
        OnPropertyChanged(nameof(ConnectionStatusColor));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsDisconnected));
        OnPropertyChanged(nameof(IsConnecting));
    }

    [RelayCommand]
    private void SwitchToDemoMode()
    {
        _settingsService.Current.DemoMode = true;
        _settingsService.Save();
        LastOperationStatus = "Switched to Demo Mode (Simulator Active).";
        OnPropertyChanged(nameof(IsDemoMode));
    }

    [RelayCommand]
    private async Task SwitchToLiveModeAsync()
    {
        _settingsService.Current.DemoMode = false;
        _settingsService.Save();
        LastOperationStatus = "Switched to Live Bluetooth Mode.";
        OnPropertyChanged(nameof(IsDemoMode));
        await ConnectDeviceAsync();
    }

    [RelayCommand]
    private void NavigateTo(string pageKey)
    {
        _navigateAction(pageKey);
    }
}
