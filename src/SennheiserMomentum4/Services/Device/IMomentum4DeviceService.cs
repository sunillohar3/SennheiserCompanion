using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SennheiserMomentum4.Models;

namespace SennheiserMomentum4.Services.Device;

public interface IMomentum4DeviceService
{
    bool IsDemoMode { get; }
    DeviceInfo DeviceInfo { get; }
    NoiseControlState NoiseControl { get; }
    EqualizerPreset CurrentEqualizer { get; }
    bool IsBassBoostEnabled { get; }

    event EventHandler<DeviceInfo>? DeviceInfoChanged;
    event EventHandler<NoiseControlState>? NoiseControlChanged;
    event EventHandler<EqualizerPreset>? EqualizerChanged;
    event EventHandler<bool>? BassBoostChanged;
    event EventHandler<string>? StatusMessageReported;

    Task<DeviceCommandResult> ConnectAsync();
    Task<DeviceCommandResult> DisconnectAsync();
    Task<int> GetBatteryAsync();
    Task<DeviceInfo> GetDeviceInformationAsync();
    Task<IReadOnlyList<string>> GetCapabilitiesAsync();

    Task<DeviceCommandResult> SetNoiseControlAsync(NoiseControlState state);
    Task<NoiseControlState> GetNoiseControlAsync();

    Task<DeviceCommandResult> SetEqualizerAsync(EqualizerPreset preset);
    Task<EqualizerPreset> GetEqualizerAsync();

    Task<DeviceCommandResult> SetBassBoostAsync(bool enabled);
    Task<bool> GetBassBoostAsync();

    Task<DeviceCommandResult> CheckFirmwareUpdateAsync();
}
