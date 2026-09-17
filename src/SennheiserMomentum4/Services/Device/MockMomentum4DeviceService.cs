using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SennheiserMomentum4.Models;
using SennheiserMomentum4.Services.Logging;

namespace SennheiserMomentum4.Services.Device;

public class MockMomentum4DeviceService : IMomentum4DeviceService
{
    private readonly IAppLogger _logger;
    private readonly DeviceInfo _deviceInfo;
    private readonly NoiseControlState _noiseControl;
    private EqualizerPreset _equalizer;

    public bool IsDemoMode => true;
    public DeviceInfo DeviceInfo => _deviceInfo;
    public NoiseControlState NoiseControl => _noiseControl;
    public EqualizerPreset CurrentEqualizer => _equalizer;
    public bool IsBassBoostEnabled => _isBassBoostEnabled;

    private bool _isBassBoostEnabled = false;

    public event EventHandler<DeviceInfo>? DeviceInfoChanged;
    public event EventHandler<NoiseControlState>? NoiseControlChanged;
    public event EventHandler<EqualizerPreset>? EqualizerChanged;
    public event EventHandler<bool>? BassBoostChanged;
    public event EventHandler<string>? StatusMessageReported;

    public MockMomentum4DeviceService(IAppLogger logger)
    {
        _logger = logger;
        _deviceInfo = new DeviceInfo
        {
            DeviceName = "MOMENTUM 4",
            ModelNumber = "M4AEBT",
            FirmwareVersion = "2.13.28",
            HardwareVersion = "0.4.0",
            SerialNumber = "M4-2024-884920",
            BluetoothAddress = "00:1B:66:8A:4F:92",
            AudioCodec = "aptX Adaptive",
            BatteryPercentage = 78,
            IsCharging = false,
            EstimatedRemainingTime = "42 hours",
            State = ConnectionState.Connected
        };

        _noiseControl = new NoiseControlState
        {
            Mode = NoiseControlMode.AdaptiveAnc,
            AncIntensity = 80.0,
            TransparencyIntensity = 70.0,
            WindNoiseReduction = true,
            IsFeatureAvailableOnInterface = true,
            InterfaceMessage = "Demo Mode (Simulated DSP response)"
        };

        _equalizer = EqualizerPreset.CreateDefaultPresets()[0].Clone();
    }

    public async Task<DeviceCommandResult> ConnectAsync()
    {
        _logger.Device(LogLevel.Info, "[Demo Mode] Connecting to simulated MOMENTUM 4...");
        _deviceInfo.State = ConnectionState.Connecting;
        DeviceInfoChanged?.Invoke(this, _deviceInfo.Clone());
        StatusMessageReported?.Invoke(this, "Connecting to MOMENTUM 4 (Demo)...");

        await Task.Delay(500);

        _deviceInfo.State = ConnectionState.Connected;
        DeviceInfoChanged?.Invoke(this, _deviceInfo.Clone());
        StatusMessageReported?.Invoke(this, "MOMENTUM 4 Connected (Demo Mode)");
        _logger.Device(LogLevel.Info, "[Demo Mode] MOMENTUM 4 Connected successfully.");
        return DeviceCommandResult.Success("Connected to MOMENTUM 4 (Demo Mode).");
    }

    public async Task<DeviceCommandResult> DisconnectAsync()
    {
        _logger.Device(LogLevel.Info, "[Demo Mode] Disconnecting from simulated MOMENTUM 4...");
        _deviceInfo.State = ConnectionState.Disconnecting;
        DeviceInfoChanged?.Invoke(this, _deviceInfo.Clone());

        await Task.Delay(300);

        _deviceInfo.State = ConnectionState.Disconnected;
        DeviceInfoChanged?.Invoke(this, _deviceInfo.Clone());
        StatusMessageReported?.Invoke(this, "MOMENTUM 4 Disconnected");
        _logger.Device(LogLevel.Info, "[Demo Mode] MOMENTUM 4 Disconnected.");
        return DeviceCommandResult.Success("Disconnected.");
    }

    public Task<int> GetBatteryAsync()
    {
        _logger.Device(LogLevel.Info, $"[Demo Mode] Battery: {_deviceInfo.BatteryPercentage}%");
        return Task.FromResult(_deviceInfo.BatteryPercentage);
    }

    public Task<DeviceInfo> GetDeviceInformationAsync()
    {
        return Task.FromResult(_deviceInfo);
    }

    public Task<IReadOnlyList<string>> GetCapabilitiesAsync()
    {
        IReadOnlyList<string> caps = new List<string>
        {
            "Bluetooth LE Standard Battery Service (0x180F)",
            "Bluetooth LE Device Information Service (0x180A)",
            "Adaptive Active Noise Cancellation",
            "Transparency Mode with adjustable slider",
            "Wind Noise Reduction",
            "5-Band Hardware DSP Equalizer (63Hz, 250Hz, 1kHz, 4kHz, 8kHz)",
            "aptX Adaptive / AAC / SBC Codecs",
            "On-Head Detection & Auto-Pause"
        };
        return Task.FromResult(caps);
    }

    public async Task<DeviceCommandResult> SetNoiseControlAsync(NoiseControlState state)
    {
        await Task.Delay(150);
        _noiseControl.Mode = state.Mode;
        _noiseControl.AncIntensity = state.AncIntensity;
        _noiseControl.TransparencyIntensity = state.TransparencyIntensity;
        _noiseControl.WindNoiseReduction = state.WindNoiseReduction;
        _noiseControl.IsFeatureAvailableOnInterface = true;
        _noiseControl.InterfaceMessage = "Demo Mode (Simulated DSP response)";

        NoiseControlChanged?.Invoke(this, _noiseControl);
        _logger.Device(LogLevel.Info, $"[Demo Mode] Noise control changed to {state.Mode} (ANC: {state.AncIntensity}%, Transparency: {state.TransparencyIntensity}%)");
        return DeviceCommandResult.Success($"Noise control set to {state.Mode}.");
    }

    public Task<NoiseControlState> GetNoiseControlAsync()
    {
        return Task.FromResult(_noiseControl);
    }

    public async Task<DeviceCommandResult> SetEqualizerAsync(EqualizerPreset preset)
    {
        await Task.Delay(150);
        _equalizer = preset.Clone();
        EqualizerChanged?.Invoke(this, _equalizer);
        _logger.Device(LogLevel.Info, $"[Demo Mode] Equalizer set to preset '{preset.Name}'");
        return DeviceCommandResult.Success($"Equalizer preset '{preset.Name}' applied to MOMENTUM 4 DSP.");
    }

    public Task<EqualizerPreset> GetEqualizerAsync()
    {
        return Task.FromResult(_equalizer);
    }

    public async Task<DeviceCommandResult> SetBassBoostAsync(bool enabled)
    {
        await Task.Delay(100);
        _isBassBoostEnabled = enabled;
        BassBoostChanged?.Invoke(this, enabled);
        _logger.Device(LogLevel.Info, $"[Demo Mode] Bass Boost set to: {enabled}");
        return DeviceCommandResult.Success(enabled ? "Bass Boost activated (Demo)." : "Bass Boost deactivated.");
    }

    public Task<bool> GetBassBoostAsync()
    {
        return Task.FromResult(_isBassBoostEnabled);
    }

    public async Task<DeviceCommandResult> CheckFirmwareUpdateAsync()
    {
        await Task.Delay(600);
        _logger.Device(LogLevel.Info, "[Demo Mode] Firmware checked: Current 2.13.28 is latest.");
        return DeviceCommandResult.Success("MOMENTUM 4 firmware is up to date (Version 2.13.28).");
    }
}
