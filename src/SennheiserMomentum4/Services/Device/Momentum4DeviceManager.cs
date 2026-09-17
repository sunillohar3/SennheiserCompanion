using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SennheiserMomentum4.Models;
using SennheiserMomentum4.Services.Logging;
using SennheiserMomentum4.Services.Settings;

namespace SennheiserMomentum4.Services.Device;

public class Momentum4DeviceManager : IMomentum4DeviceService
{
    private readonly ISettingsService _settingsService;
    private readonly IAppLogger _logger;
    private readonly MockMomentum4DeviceService _mockService;
    private readonly Momentum4BleDeviceService _bleService;
    private IMomentum4DeviceService _activeService;

    public bool IsDemoMode => _activeService.IsDemoMode;
    public DeviceInfo DeviceInfo => _activeService.DeviceInfo;
    public NoiseControlState NoiseControl => _activeService.NoiseControl;
    public EqualizerPreset CurrentEqualizer => _activeService.CurrentEqualizer;
    public bool IsBassBoostEnabled => _activeService.IsBassBoostEnabled;

    public event EventHandler<DeviceInfo>? DeviceInfoChanged;
    public event EventHandler<NoiseControlState>? NoiseControlChanged;
    public event EventHandler<EqualizerPreset>? EqualizerChanged;
    public event EventHandler<bool>? BassBoostChanged;
    public event EventHandler<string>? StatusMessageReported;
    public event EventHandler<bool>? ModeSwitched;

    public Momentum4DeviceManager(ISettingsService settingsService, IAppLogger logger)
    {
        _settingsService = settingsService;
        _logger = logger;

        _mockService = new MockMomentum4DeviceService(_logger);
        _bleService = new Momentum4BleDeviceService(_logger);

        HookEvents(_mockService);
        HookEvents(_bleService);

        _activeService = _settingsService.Current.DemoMode ? _mockService : _bleService;

        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void HookEvents(IMomentum4DeviceService service)
    {
        service.DeviceInfoChanged += (s, e) =>
        {
            if (ReferenceEquals(s, _activeService))
                DeviceInfoChanged?.Invoke(this, e);
        };

        service.NoiseControlChanged += (s, e) =>
        {
            if (ReferenceEquals(s, _activeService))
                NoiseControlChanged?.Invoke(this, e);
        };

        service.EqualizerChanged += (s, e) =>
        {
            if (ReferenceEquals(s, _activeService))
                EqualizerChanged?.Invoke(this, e);
        };

        service.BassBoostChanged += (s, e) =>
        {
            if (ReferenceEquals(s, _activeService))
                BassBoostChanged?.Invoke(this, e);
        };

        service.StatusMessageReported += (s, e) =>
        {
            if (ReferenceEquals(s, _activeService))
                StatusMessageReported?.Invoke(this, e);
        };
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        var targetModeIsDemo = settings.DemoMode;
        if (targetModeIsDemo != IsDemoMode)
        {
            _activeService = targetModeIsDemo ? _mockService : _bleService;
            _logger.App(LogLevel.Info, $"Device mode switched to: {(targetModeIsDemo ? "DEMO MODE" : "LIVE BLUETOOTH")}");

            DeviceInfoChanged?.Invoke(this, _activeService.DeviceInfo);
            NoiseControlChanged?.Invoke(this, _activeService.NoiseControl);
            EqualizerChanged?.Invoke(this, _activeService.CurrentEqualizer);
            BassBoostChanged?.Invoke(this, _activeService.IsBassBoostEnabled);
            ModeSwitched?.Invoke(this, targetModeIsDemo);
        }
    }

    public Task<DeviceCommandResult> ConnectAsync() => _activeService.ConnectAsync();
    public Task<DeviceCommandResult> DisconnectAsync() => _activeService.DisconnectAsync();
    public Task<int> GetBatteryAsync() => _activeService.GetBatteryAsync();
    public Task<DeviceInfo> GetDeviceInformationAsync() => _activeService.GetDeviceInformationAsync();
    public Task<IReadOnlyList<string>> GetCapabilitiesAsync() => _activeService.GetCapabilitiesAsync();
    public Task<DeviceCommandResult> SetNoiseControlAsync(NoiseControlState state) => _activeService.SetNoiseControlAsync(state);
    public Task<NoiseControlState> GetNoiseControlAsync() => _activeService.GetNoiseControlAsync();
    public Task<DeviceCommandResult> SetEqualizerAsync(EqualizerPreset preset) => _activeService.SetEqualizerAsync(preset);
    public Task<EqualizerPreset> GetEqualizerAsync() => _activeService.GetEqualizerAsync();
    public Task<DeviceCommandResult> SetBassBoostAsync(bool enabled) => _activeService.SetBassBoostAsync(enabled);
    public Task<bool> GetBassBoostAsync() => _activeService.GetBassBoostAsync();
    public Task<DeviceCommandResult> CheckFirmwareUpdateAsync() => _activeService.CheckFirmwareUpdateAsync();
}
