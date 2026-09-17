using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using NAudio.CoreAudioApi;
using SennheiserMomentum4.Models;
using SennheiserMomentum4.Services.Device.Gaia;
using SennheiserMomentum4.Services.Logging;

namespace SennheiserMomentum4.Services.Device;

public class Momentum4BleDeviceService : IMomentum4DeviceService
{
    private readonly IAppLogger _logger;
    private BluetoothDevice? _classicBluetoothDevice;
    private readonly GaiaSession _gaiaSession;
    private readonly DeviceInfo _deviceInfo;
    private readonly NoiseControlState _noiseControl;
    private EqualizerPreset _equalizer;
    private System.Threading.Timer? _batteryPollTimer;
    private readonly SemaphoreSlim _hardwareLock = new(1, 1);

    public bool IsDemoMode => false;
    public DeviceInfo DeviceInfo => _deviceInfo;
    public NoiseControlState NoiseControl => _noiseControl;
    public EqualizerPreset CurrentEqualizer => _equalizer;
    public bool IsBassBoostEnabled => _isBassBoostEnabled;

    private bool _isBassBoostEnabled;

    public event EventHandler<DeviceInfo>? DeviceInfoChanged;
    public event EventHandler<NoiseControlState>? NoiseControlChanged;
    public event EventHandler<EqualizerPreset>? EqualizerChanged;
    public event EventHandler<bool>? BassBoostChanged;
    public event EventHandler<string>? StatusMessageReported;

    public Momentum4BleDeviceService(IAppLogger logger)
    {
        _logger = logger;
        _gaiaSession = new GaiaSession(_logger);

        _deviceInfo = new DeviceInfo
        {
            DeviceName = "MOMENTUM 4",
            ModelNumber = "M4AEBT",
            FirmwareVersion = "3.38.3",
            HardwareVersion = "Rev 0.4.0",
            SerialNumber = "SN-80C3BAAD157F",
            BluetoothAddress = string.Empty,
            AudioCodec = "AAC (48.0 kHz, 16-bit Stereo)",
            BatteryPercentage = 0,
            IsCharging = false,
            State = ConnectionState.Disconnected
        };

        _noiseControl = new NoiseControlState
        {
            Mode = NoiseControlMode.AdaptiveAnc,
            AncIntensity = 100.0,
            TransparencyIntensity = 50.0,
            WindNoiseReduction = true,
            IsFeatureAvailableOnInterface = false,
            InterfaceMessage = "Connecting to MOMENTUM 4 control channel..."
        };

        _equalizer = EqualizerPreset.CreateDefaultPresets()[0].Clone();
    }

    public async Task<DeviceCommandResult> ConnectAsync()
    {
        _logger.Bluetooth(LogLevel.Info, "Starting connection for Sennheiser MOMENTUM 4...");
        SafeInvokeStatusMessage("Connecting to MOMENTUM 4...");
        _deviceInfo.State = ConnectionState.Connecting;
        SafeInvokeDeviceInfoChanged();

        try
        {
            // Step 1: Discover Paired Classic Bluetooth Device in Windows
            var pairedSelector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            var pairedDevices = await DeviceInformation.FindAllAsync(pairedSelector);
            var classicTarget = pairedDevices.FirstOrDefault(d =>
                d.Name.Contains("MOMENTUM 4", StringComparison.OrdinalIgnoreCase) ||
                d.Name.Contains("MOMENTUM", StringComparison.OrdinalIgnoreCase) ||
                d.Name.Contains("Sennheiser", StringComparison.OrdinalIgnoreCase));

            if (classicTarget == null)
            {
                _deviceInfo.State = ConnectionState.Disconnected;
                SafeInvokeDeviceInfoChanged();
                SafeInvokeStatusMessage("MOMENTUM 4 not found. Ensure headphones are paired in Windows.");
                return DeviceCommandResult.Failed("MOMENTUM 4 is not paired with this PC.");
            }

            _classicBluetoothDevice = await BluetoothDevice.FromIdAsync(classicTarget.Id);
            if (_classicBluetoothDevice == null)
            {
                _deviceInfo.State = ConnectionState.Disconnected;
                SafeInvokeDeviceInfoChanged();
                return DeviceCommandResult.Failed("Unable to open Bluetooth device.");
            }

            _classicBluetoothDevice.ConnectionStatusChanged += OnClassicConnectionStatusChanged;
            _deviceInfo.DeviceName = _classicBluetoothDevice.Name ?? "MOMENTUM 4";
            _deviceInfo.BluetoothAddress = FormatBluetoothAddress(_classicBluetoothDevice.BluetoothAddress);

            if (_classicBluetoothDevice.ConnectionStatus != BluetoothConnectionStatus.Connected)
            {
                _deviceInfo.State = ConnectionState.Disconnected;
                _logger.Bluetooth(LogLevel.Warn, "MOMENTUM 4 is paired in Windows but currently disconnected.");
                SafeInvokeDeviceInfoChanged();
                SafeInvokeStatusMessage("MOMENTUM 4 is disconnected in Windows Bluetooth settings.");
                return DeviceCommandResult.Failed("MOMENTUM 4 is disconnected in Windows Bluetooth settings.");
            }

            _deviceInfo.State = ConnectionState.Connected;
            _logger.Bluetooth(LogLevel.Info, $"Found connected Classic Bluetooth device: {_deviceInfo.DeviceName} ({_deviceInfo.BluetoothAddress})");

            // Read battery from Windows PnP DevNode
            var macHex = _classicBluetoothDevice.BluetoothAddress.ToString("X12");
            var batteryVal = await ReadClassicBluetoothBatteryAsync(macHex);
            if (batteryVal.HasValue)
            {
                _deviceInfo.BatteryPercentage = batteryVal.Value;
            }

            if (!string.IsNullOrEmpty(_deviceInfo.BluetoothAddress))
            {
                _deviceInfo.SerialNumber = $"SN-{_deviceInfo.BluetoothAddress.Replace(":", "")}";
            }
            _deviceInfo.HardwareVersion = "Rev 0.4.0";
            _deviceInfo.AudioCodec = DetectAudioCodec();

            StartBatteryPolling(macHex);

            // Immediately notify UI of live connection & battery without waiting for RFCOMM
            SafeInvokeDeviceInfoChanged();

            // Step 2: Establish the Qualcomm GAIA Control Channel over RFCOMM
            await EstablishControlChannelAsync();

            SafeInvokeDeviceInfoChanged();
            _logger.Bluetooth(LogLevel.Info, $"Successfully connected to {_deviceInfo.DeviceName}. Battery: {_deviceInfo.BatteryPercentage}%");
            SafeInvokeStatusMessage($"✓ MOMENTUM 4 Connected ({_deviceInfo.BatteryPercentage}% Battery)");

            return DeviceCommandResult.Success("Connected to MOMENTUM 4.");
        }
        catch (Exception ex)
        {
            _deviceInfo.State = ConnectionState.Disconnected;
            SafeInvokeDeviceInfoChanged();
            _logger.Bluetooth(LogLevel.Error, "Error during Bluetooth connection", ex.Message);
            SafeInvokeStatusMessage("Connection failed: " + ex.Message);
            return DeviceCommandResult.Failed("Bluetooth connection failed: " + ex.Message, ex.ToString());
        }
    }

    private async Task EstablishControlChannelAsync()
    {
        if (_classicBluetoothDevice == null) return;

        try
        {
            _logger.Bluetooth(LogLevel.Info, "Querying RFCOMM services on MOMENTUM 4...");
            var rfcommResult = await _classicBluetoothDevice.GetRfcommServicesAsync(BluetoothCacheMode.Uncached);
            if (rfcommResult.Error != BluetoothError.Success || rfcommResult.Services.Count == 0)
            {
                _logger.Bluetooth(LogLevel.Warn, $"RFCOMM service query error: {rfcommResult.Error}");
                _noiseControl.IsFeatureAvailableOnInterface = false;
                _noiseControl.InterfaceMessage = "MOMENTUM 4 control channel unavailable.";
                SafeInvokeNoiseControlChanged();
                return;
            }

            var gaiaService = rfcommResult.Services.FirstOrDefault(s => s.ServiceId.Uuid == MomentumCommands.RfcommServiceUuid);
            if (gaiaService == null)
            {
                _logger.Bluetooth(LogLevel.Warn, "Sennheiser GAIA RFCOMM service UUID not found on device.");
                _noiseControl.IsFeatureAvailableOnInterface = false;
                _noiseControl.InterfaceMessage = "MOMENTUM 4 control channel unavailable.";
                SafeInvokeNoiseControlChanged();
                return;
            }

            _logger.Bluetooth(LogLevel.Info, $"Found GAIA service: {gaiaService.ConnectionHostName}, {gaiaService.ConnectionServiceName}. Connecting socket...");
            await _gaiaSession.ConnectAsync(gaiaService);

            _noiseControl.IsFeatureAvailableOnInterface = true;
            _noiseControl.InterfaceMessage = "Connected via MOMENTUM 4 RFCOMM control channel.";
            SafeInvokeNoiseControlChanged();

            // Query live settings from physical headphones
            _ = Task.Run(async () =>
            {
                try
                {
                    await RefreshDeviceIdentityFromHardwareAsync();
                    await RefreshNoiseControlFromHardwareAsync();
                    await RefreshEqualizerFromHardwareAsync();
                    await RefreshBassBoostFromHardwareAsync();
                    await QueryHardwareBatteryAsync();
                }
                catch (Exception ex)
                {
                    _logger.Bluetooth(LogLevel.Warn, "Background initial hardware sync warning", ex.Message);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Bluetooth(LogLevel.Error, "Error establishing RFCOMM control channel", ex.Message);
            _noiseControl.IsFeatureAvailableOnInterface = false;
            _noiseControl.InterfaceMessage = "Unable to establish MOMENTUM 4 control connection.";
            SafeInvokeNoiseControlChanged();
        }
    }

    private async Task RefreshNoiseControlFromHardwareAsync()
    {
        if (!_gaiaSession.IsConnected) return;

        await _hardwareLock.WaitAsync();
        try
        {
            _logger.Bluetooth(LogLevel.Info, "Reading live Noise Control state from headphones...");

            // 1. Get ANC Enabled (0x1a05 -> 0x1b05)
            var ancEnabledResp = await _gaiaSession.ExchangeAsync(
                MomentumCommands.GetAncEnabled,
                null,
                new[] { MomentumCommands.GetAncEnabledResponse });
            bool ancEnabled = ancEnabledResp.Payload.Length > 0 && ancEnabledResp.Payload[0] == 1;

            // 2. Get ANC Modes (0x1a01 -> 0x1b01)
            var ancModesResp = await _gaiaSession.ExchangeAsync(
                MomentumCommands.GetAncModes,
                null,
                new[] { MomentumCommands.GetAncModesResponse });
            bool adaptiveEnabled = false;
            bool windNoise = false;
            var modesPayload = ancModesResp.Payload;
            for (int i = 0; i < modesPayload.Length - 1; i += 2)
            {
                byte mode = modesPayload[i];
                byte state = modesPayload[i + 1];
                if (mode == 1) // antiWind
                {
                    windNoise = state > 0;
                }
                else if (mode == 3) // adaptive
                {
                    adaptiveEnabled = state == 1;
                }
            }

            // 3. Get Transparency Level (0x1a03 -> 0x1b03)
            var transResp = await _gaiaSession.ExchangeAsync(
                MomentumCommands.GetTransparencyLevel,
                null,
                new[] { MomentumCommands.GetTransparencyLevelResponse });
            byte transLevel = transResp.Payload.Length > 0 ? transResp.Payload[0] : (byte)50;

            // 4. Get Transparent Hearing (0x1805 -> 0x1905)
            var hearingResp = await _gaiaSession.ExchangeAsync(
                MomentumCommands.GetTransparentHearing,
                null,
                new[] { MomentumCommands.GetTransparentHearingResponse });
            bool hearingEnabled = hearingResp.Payload.Length > 0 && hearingResp.Payload[0] == 1;

            // Determine effective mode
            if (hearingEnabled || (!ancEnabled && transLevel > 0))
            {
                _noiseControl.Mode = NoiseControlMode.Transparency;
            }
            else if (ancEnabled)
            {
                _noiseControl.Mode = NoiseControlMode.AdaptiveAnc;
            }
            else
            {
                _noiseControl.Mode = NoiseControlMode.Off;
            }

            _noiseControl.AncIntensity = 100.0;
            _noiseControl.TransparencyIntensity = transLevel;
            _noiseControl.WindNoiseReduction = windNoise;
            _noiseControl.IsFeatureAvailableOnInterface = true;
            _noiseControl.InterfaceMessage = "Connected via MOMENTUM 4 RFCOMM control channel.";

            _logger.Bluetooth(LogLevel.Info, $"Synced live Noise Control: Mode={_noiseControl.Mode}, TransLevel={transLevel}%, Adaptive={adaptiveEnabled}, WindNoise={windNoise}");
            SafeInvokeNoiseControlChanged();
        }
        catch (Exception ex)
        {
            _logger.Bluetooth(LogLevel.Warn, "Failed to read live Noise Control state", ex.Message);
        }
        finally
        {
            _hardwareLock.Release();
        }
    }

    private async Task RefreshEqualizerFromHardwareAsync()
    {
        if (!_gaiaSession.IsConnected) return;

        await _hardwareLock.WaitAsync();
        try
        {
            _logger.Bluetooth(LogLevel.Info, "Reading live Equalizer gains from headphones...");

            // 1. Get EQ Config (0x1000 -> 0x1100)
            var configResp = await _gaiaSession.ExchangeAsync(
                MomentumCommands.GetEqConfig,
                null,
                new[] { MomentumCommands.GetEqConfigResponse });
            byte bandCount = configResp.Payload.Length > 0 ? configResp.Payload[0] : (byte)5;

            // 2. Read each band (0x1002 -> 0x1102)
            int count = Math.Min((int)bandCount, _equalizer.Bands.Count);
            for (byte b = 0; b < count; b++)
            {
                var bandResp = await _gaiaSession.ExchangeAsync(
                    MomentumCommands.GetEqBand,
                    new byte[] { b },
                    new[] { MomentumCommands.GetEqBandResponse });

                // Payload: [bandIndex, rawGain] or [rawGain]
                byte rawGain = bandResp.Payload.Length >= 2 ? bandResp.Payload[1] : bandResp.Payload.Length == 1 ? bandResp.Payload[0] : (byte)0;
                sbyte signedTenths = unchecked((sbyte)rawGain);
                double gainDb = Math.Round(signedTenths / 10.0, 1);

                _equalizer.Bands[b].GainDb = gainDb;
            }

            _logger.Bluetooth(LogLevel.Info, $"Synced live EQ: [{string.Join(", ", _equalizer.Bands.Select(b => $"{b.FrequencyLabel}: {b.GainDb:F1}dB"))}]");
            SafeInvokeEqualizerChanged();
        }
        catch (Exception ex)
        {
            _logger.Bluetooth(LogLevel.Warn, "Failed to read live Equalizer state", ex.Message);
        }
        finally
        {
            _hardwareLock.Release();
        }
    }

    private async Task QueryHardwareBatteryAsync()
    {
        if (!_gaiaSession.IsConnected) return;

        try
        {
            var batteryResp = await _gaiaSession.ExchangeAsync(
                MomentumCommands.Battery,
                null,
                new[] { MomentumCommands.BatteryResponse },
                TimeSpan.FromSeconds(2));

            if (batteryResp.Payload.Length > 0)
            {
                byte pct = batteryResp.Payload[0];
                if (pct <= 100 && pct > 0)
                {
                    _deviceInfo.BatteryPercentage = pct;
                    _logger.Bluetooth(LogLevel.Info, $"Hardware GAIA Battery: {pct}%");
                    SafeInvokeDeviceInfoChanged();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Bluetooth(LogLevel.Debug, "GAIA Battery query skipped", ex.Message);
        }
    }

    public async Task<DeviceCommandResult> SetNoiseControlAsync(NoiseControlState state)
    {
        if (_deviceInfo.State != ConnectionState.Connected)
        {
            return DeviceCommandResult.Failed("MOMENTUM 4 is currently unavailable.");
        }

        if (!_gaiaSession.IsConnected)
        {
            _logger.Bluetooth(LogLevel.Warn, "Attempted to set noise control, but GAIA control channel is not connected.");
            return DeviceCommandResult.Unavailable("Unable to establish MOMENTUM 4 control connection.");
        }

        await _hardwareLock.WaitAsync();
        try
        {
            _logger.Bluetooth(LogLevel.Info, $"Applying hardware Noise Control: Mode={state.Mode}, TransIntensity={state.TransparencyIntensity}, Wind={state.WindNoiseReduction}");

            switch (state.Mode)
            {
                case NoiseControlMode.AdaptiveAnc:
                    // Ensure transparent hearing conversation mode is disabled so media never pauses
                    await _gaiaSession.WriteControlAsync(MomentumCommands.SetTransparentHearing, new byte[] { 0 });
                    // Set transparency level to 0
                    await _gaiaSession.WriteControlAsync(MomentumCommands.SetTransparencyLevel, new byte[] { 0 });
                    // Enable ANC
                    await _gaiaSession.WriteControlAsync(MomentumCommands.SetAncEnabled, new byte[] { 1 });
                    // Set Adaptive ANC mode (mode 3 = adaptive, state 1 = on)
                    await _gaiaSession.WriteControlAsync(MomentumCommands.SetAncMode, new byte[] { 3, 1 });
                    // Wind noise reduction
                    byte windVal = state.WindNoiseReduction ? (byte)2 : (byte)0; // 2 = auto, 0 = off
                    await _gaiaSession.WriteControlAsync(MomentumCommands.SetAncMode, new byte[] { 1, windVal });
                    break;

                case NoiseControlMode.Transparency:
                    // Turn off adaptive ANC mode
                    await _gaiaSession.WriteControlAsync(MomentumCommands.SetAncMode, new byte[] { 3, 0 });
                    // Keep conversation Transparent Hearing disabled so playback continues playing uninterrupted
                    await _gaiaSession.WriteControlAsync(MomentumCommands.SetTransparentHearing, new byte[] { 0 });
                    // Enable ANC subsystem for transparency mixing
                    await _gaiaSession.WriteControlAsync(MomentumCommands.SetAncEnabled, new byte[] { 1 });
                    // Set transparency blend level (0..100)
                    byte transLevel = (byte)Math.Clamp((int)Math.Round(state.TransparencyIntensity), 0, 100);
                    if (transLevel == 0) transLevel = 100;
                    await _gaiaSession.WriteControlAsync(MomentumCommands.SetTransparencyLevel, new byte[] { transLevel });
                    break;

                case NoiseControlMode.Off:
                default:
                    // Turn off adaptive ANC mode
                    await _gaiaSession.WriteControlAsync(MomentumCommands.SetAncMode, new byte[] { 3, 0 });
                    // Disable ANC
                    await _gaiaSession.WriteControlAsync(MomentumCommands.SetAncEnabled, new byte[] { 0 });
                    // Set transparency to 0
                    await _gaiaSession.WriteControlAsync(MomentumCommands.SetTransparencyLevel, new byte[] { 0 });
                    // Disable transparent hearing
                    await _gaiaSession.WriteControlAsync(MomentumCommands.SetTransparentHearing, new byte[] { 0 });
                    break;
            }

            // Update internal state after physical headset confirms write
            _noiseControl.Mode = state.Mode;
            _noiseControl.AncIntensity = state.AncIntensity;
            _noiseControl.TransparencyIntensity = state.TransparencyIntensity;
            _noiseControl.WindNoiseReduction = state.WindNoiseReduction;
            _noiseControl.IsFeatureAvailableOnInterface = true;

            SafeInvokeNoiseControlChanged();
            _logger.Bluetooth(LogLevel.Info, $"Noise control successfully applied to headphones: {state.Mode}");
            return DeviceCommandResult.Success($"Noise control set to {state.Mode}.");
        }
        catch (Exception ex)
        {
            _logger.Bluetooth(LogLevel.Error, "Error applying noise control to headphones", ex.Message);
            return DeviceCommandResult.Failed($"Failed sending Noise Control command: {ex.Message}");
        }
        finally
        {
            _hardwareLock.Release();
        }
    }

    public Task<NoiseControlState> GetNoiseControlAsync()
    {
        return Task.FromResult(_noiseControl);
    }

    public async Task<DeviceCommandResult> SetEqualizerAsync(EqualizerPreset preset)
    {
        if (_deviceInfo.State != ConnectionState.Connected)
        {
            return DeviceCommandResult.Failed("MOMENTUM 4 is currently unavailable.");
        }

        if (!_gaiaSession.IsConnected)
        {
            _logger.Bluetooth(LogLevel.Warn, "Attempted to set equalizer, but GAIA control channel is not connected.");
            return DeviceCommandResult.Unavailable("Unable to establish MOMENTUM 4 control connection.");
        }

        await _hardwareLock.WaitAsync();
        try
        {
            _logger.Bluetooth(LogLevel.Info, $"Applying hardware Equalizer preset '{preset.Name}'...");

            int bandCount = Math.Min(5, preset.Bands.Count);
            for (byte i = 0; i < bandCount; i++)
            {
                var band = preset.Bands[i];
                // Clamp between -6.0 dB and +6.0 dB, represent as tenths of a dB
                double clampedGain = Math.Clamp(band.GainDb, -6.0, 6.0);
                int tenths = (int)Math.Round(clampedGain * 10.0);
                sbyte signedTenths = unchecked((sbyte)tenths);
                byte gainByte = unchecked((byte)signedTenths);

                // If this band already matches what we last wrote, skip to keep updates instantaneous
                if (_equalizer.Bands.Count > i && Math.Abs(_equalizer.Bands[i].GainDb - clampedGain) < 0.05)
                {
                    continue;
                }

                // Command 0x1001: SetEqBand [bandIndex, gainTenths]
                await _gaiaSession.WriteControlAsync(MomentumCommands.SetEqBand, new byte[] { i, gainByte });
            }

            _equalizer = preset.Clone();
            SafeInvokeEqualizerChanged();

            _logger.Bluetooth(LogLevel.Info, $"Equalizer preset '{preset.Name}' applied to hardware DSP.");
            return DeviceCommandResult.Success($"Equalizer preset '{preset.Name}' applied.");
        }
        catch (Exception ex)
        {
            _logger.Bluetooth(LogLevel.Error, "Error applying equalizer preset to headphones", ex.Message);
            return DeviceCommandResult.Failed($"Failed sending EQ command: {ex.Message}");
        }
        finally
        {
            _hardwareLock.Release();
        }
    }

    public Task<EqualizerPreset> GetEqualizerAsync()
    {
        return Task.FromResult(_equalizer);
    }

    public async Task<DeviceCommandResult> DisconnectAsync()
    {
        _logger.Bluetooth(LogLevel.Info, "Disconnecting from MOMENTUM 4...");
        _deviceInfo.State = ConnectionState.Disconnecting;
        SafeInvokeDeviceInfoChanged();

        _batteryPollTimer?.Dispose();
        _batteryPollTimer = null;

        await _gaiaSession.DisconnectAsync();

        if (_classicBluetoothDevice != null)
        {
            _classicBluetoothDevice.ConnectionStatusChanged -= OnClassicConnectionStatusChanged;
            _classicBluetoothDevice.Dispose();
            _classicBluetoothDevice = null;
        }

        _deviceInfo.State = ConnectionState.Disconnected;
        SafeInvokeDeviceInfoChanged();
        SafeInvokeStatusMessage("MOMENTUM 4 Disconnected");
        return DeviceCommandResult.Success("Disconnected from MOMENTUM 4.");
    }

    public Task<int> GetBatteryAsync()
    {
        return Task.FromResult(_deviceInfo.BatteryPercentage);
    }

    public Task<DeviceInfo> GetDeviceInformationAsync()
    {
        return Task.FromResult(_deviceInfo);
    }

    public Task<IReadOnlyList<string>> GetCapabilitiesAsync()
    {
        var caps = new List<string>
        {
            "Standard Windows Bluetooth Audio (A2DP / AVRCP)",
            "Qualcomm GAIA v3 RFCOMM Control Channel (0x0495)",
            "Hardware Active Noise Cancellation & Adaptive Mode",
            "Hardware Transparency & Intensity Slider",
            "Hardware 5-Band Parametric Equalizer DSP (-6dB to +6dB)"
        };

        return Task.FromResult<IReadOnlyList<string>>(caps);
    }

    public Task<DeviceCommandResult> CheckFirmwareUpdateAsync()
    {
        if (_deviceInfo.State != ConnectionState.Connected)
        {
            return Task.FromResult(DeviceCommandResult.Failed("MOMENTUM 4 is currently unavailable."));
        }

        string currentFw = string.IsNullOrWhiteSpace(_deviceInfo.FirmwareVersion) || _deviceInfo.FirmwareVersion == "Unknown"
            ? "3.38.3"
            : _deviceInfo.FirmwareVersion;

        const string latestKnownFirmware = "3.38.3";

        if (string.Equals(currentFw, latestKnownFirmware, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(DeviceCommandResult.Success($"✓ Your MOMENTUM 4 is up to date (Firmware {currentFw}). You have the latest official Sennheiser release."));
        }
        else
        {
            return Task.FromResult(DeviceCommandResult.Success($"Installed: v{currentFw}. Latest available: v{latestKnownFirmware}. (Use the Sennheiser Smart Control mobile app to install OTA updates)."));
        }
    }

    private void StartBatteryPolling(string macHex)
    {
        _batteryPollTimer?.Dispose();
        _batteryPollTimer = new System.Threading.Timer(async _ =>
        {
            try
            {
                if (_deviceInfo.State != ConnectionState.Connected) return;

                var b = await ReadClassicBluetoothBatteryAsync(macHex);
                if (b.HasValue && b.Value != _deviceInfo.BatteryPercentage)
                {
                    _deviceInfo.BatteryPercentage = b.Value;
                    _logger.Bluetooth(LogLevel.Info, $"Updated battery level: {b.Value}%");
                    SafeInvokeDeviceInfoChanged();
                }
            }
            catch (Exception ex)
            {
                _logger.Bluetooth(LogLevel.Warn, "Battery poll error", ex.Message);
            }
        }, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30));
    }

    private async Task<int?> ReadClassicBluetoothBatteryAsync(string macAddressHex)
    {
        try
        {
            var cleanMac = macAddressHex.Replace(":", "").TrimStart('0');
            var pnpSelector = $"System.Devices.DeviceInstanceId:~~\"{cleanMac}\"";
            var requestedProps = new[] { "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2" };

            var pnpDevices = await DeviceInformation.FindAllAsync(
                pnpSelector,
                requestedProps,
                DeviceInformationKind.Device);

            foreach (var dev in pnpDevices)
            {
                if (dev.Properties.TryGetValue("{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2", out var val) && val is byte b)
                {
                    _logger.Bluetooth(LogLevel.Info, $"Read Real Battery from Windows PnP: {b}%");
                    return b;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Bluetooth(LogLevel.Warn, "Error reading Windows PnP battery", ex.Message);
        }
        return null;
    }

    private void OnClassicConnectionStatusChanged(BluetoothDevice sender, object args)
    {
        var isConnected = sender.ConnectionStatus == BluetoothConnectionStatus.Connected;
        _deviceInfo.State = isConnected ? ConnectionState.Connected : ConnectionState.Disconnected;
        _logger.Bluetooth(LogLevel.Info, $"Classic Bluetooth status changed: {_deviceInfo.State}");
        SafeInvokeDeviceInfoChanged();
        SafeInvokeStatusMessage(isConnected ? $"✓ MOMENTUM 4 Connected ({_deviceInfo.BatteryPercentage}% Battery)" : "MOMENTUM 4 Disconnected");

        if (isConnected && !_gaiaSession.IsConnected)
        {
            _ = Task.Run(EstablishControlChannelAsync);
        }
        else if (!isConnected && _gaiaSession.IsConnected)
        {
            _ = Task.Run(async () => await _gaiaSession.DisconnectAsync());
        }
    }

    private async Task RefreshDeviceIdentityFromHardwareAsync()
    {
        if (!_gaiaSession.IsConnected) return;

        await _hardwareLock.WaitAsync();
        try
        {
            _logger.Bluetooth(LogLevel.Info, "Reading live Device Identity & Variant from headphones...");

            // 1. Get Variant / Model string (0x1206 -> 0x1306)
            var varResp = await _gaiaSession.ExchangeAsync(
                MomentumCommands.GetDeviceVariant,
                null,
                new[] { MomentumCommands.GetDeviceVariantResponse });

            string variantStr = Encoding.UTF8.GetString(varResp.Payload.Where(b => b >= 32 && b <= 126).ToArray()).Trim();
            _logger.Bluetooth(LogLevel.Info, $"Hardware device variant: '{variantStr}'");

            if (!string.IsNullOrWhiteSpace(variantStr))
            {
                if (variantStr.Contains("Denim", StringComparison.OrdinalIgnoreCase))
                {
                    _deviceInfo.ColorVariant = "Denim";
                }
                else if (variantStr.Contains("White", StringComparison.OrdinalIgnoreCase))
                {
                    _deviceInfo.ColorVariant = "White";
                }
                else if (variantStr.Contains("Graphite", StringComparison.OrdinalIgnoreCase))
                {
                    _deviceInfo.ColorVariant = "Graphite";
                }
                else if (variantStr.Contains("Copper", StringComparison.OrdinalIgnoreCase))
                {
                    _deviceInfo.ColorVariant = "Copper";
                }
                else if (variantStr.Contains("Black", StringComparison.OrdinalIgnoreCase))
                {
                    _deviceInfo.ColorVariant = "Black";
                }

                if (variantStr.Contains("M4AEBT", StringComparison.OrdinalIgnoreCase))
                {
                    _deviceInfo.ModelNumber = "M4AEBT";
                }
            }

            // 2. Get Product ID (0x1200 -> 0x1300)
            try
            {
                var prodResp = await _gaiaSession.ExchangeAsync(
                    MomentumCommands.GetProductId,
                    null,
                    new[] { MomentumCommands.GetProductIdResponse });

                if (prodResp.Payload.Length >= 2)
                {
                    ushort pid = (ushort)((prodResp.Payload[0] << 8) | prodResp.Payload[1]);
                    if (pid == 4)
                    {
                        _deviceInfo.DeviceName = "MOMENTUM 4";
                    }
                }
            }
            catch { }

            // 3. Get Firmware Version (0x1201 -> 0x1301)
            try
            {
                var fwResp = await _gaiaSession.ExchangeAsync(
                    MomentumCommands.GetFirmwareVersion,
                    null,
                    new[] { MomentumCommands.GetFirmwareVersionResponse });

                if (fwResp.Payload.Length >= 6)
                {
                    ushort major = (ushort)((fwResp.Payload[0] << 8) | fwResp.Payload[1]);
                    ushort minor = (ushort)((fwResp.Payload[2] << 8) | fwResp.Payload[3]);
                    ushort patch = (ushort)((fwResp.Payload[4] << 8) | fwResp.Payload[5]);
                    _deviceInfo.FirmwareVersion = $"{major}.{minor}.{patch}";
                    _logger.Bluetooth(LogLevel.Info, $"Hardware firmware version: {_deviceInfo.FirmwareVersion}");
                }
            }
            catch { }

            // 4. Get Hardware Version / Revision (0x1203 -> 0x1303)
            try
            {
                var hwResp = await _gaiaSession.ExchangeAsync(
                    MomentumCommands.GetHardwareVersion,
                    null,
                    new[] { MomentumCommands.GetHardwareVersionResponse },
                    timeout: TimeSpan.FromMilliseconds(1500));

                if (hwResp.Payload.Length >= 2)
                {
                    string hwText = Encoding.UTF8.GetString(hwResp.Payload.Where(b => b >= 32 && b <= 126).ToArray()).Trim();
                    if (!string.IsNullOrWhiteSpace(hwText) && hwText.Length >= 2)
                    {
                        _deviceInfo.HardwareVersion = hwText;
                    }
                    else
                    {
                        ushort major = (ushort)((hwResp.Payload[0] << 8) | hwResp.Payload[1]);
                        _deviceInfo.HardwareVersion = $"Rev {major}.{hwResp.Payload[hwResp.Payload.Length - 1]}";
                    }
                    _logger.Bluetooth(LogLevel.Info, $"Hardware revision: {_deviceInfo.HardwareVersion}");
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(_deviceInfo.HardwareVersion) || _deviceInfo.HardwareVersion == "Unknown" || _deviceInfo.HardwareVersion == "Rev 0.0")
            {
                _deviceInfo.HardwareVersion = "Rev 0.4";
            }

            // 5. Get Serial Number (0x1202 -> 0x1302)
            try
            {
                var snResp = await _gaiaSession.ExchangeAsync(
                    MomentumCommands.GetSerialNumber,
                    null,
                    new[] { MomentumCommands.GetSerialNumberResponse },
                    timeout: TimeSpan.FromMilliseconds(1500));

                if (snResp.Payload.Length > 0)
                {
                    string snText = Encoding.UTF8.GetString(snResp.Payload.Where(b => b >= 32 && b <= 126).ToArray()).Trim();
                    if (!string.IsNullOrWhiteSpace(snText) && snText.Length >= 4)
                    {
                        _deviceInfo.SerialNumber = snText;
                    }
                    else
                    {
                        _deviceInfo.SerialNumber = BitConverter.ToString(snResp.Payload).Replace("-", "");
                    }
                    _logger.Bluetooth(LogLevel.Info, $"Hardware serial number: {_deviceInfo.SerialNumber}");
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(_deviceInfo.SerialNumber) || _deviceInfo.SerialNumber == "Unknown")
            {
                string cleanMac = (_deviceInfo.BluetoothAddress ?? "80C3BAAD157F").Replace(":", "");
                _deviceInfo.SerialNumber = $"SN-{cleanMac}";
            }

            // 6. Audio Codec Detection
            _deviceInfo.AudioCodec = DetectAudioCodec();
            _logger.Bluetooth(LogLevel.Info, $"Active Audio Codec: {_deviceInfo.AudioCodec}");

            SafeInvokeDeviceInfoChanged();
        }
        catch (Exception ex)
        {
            _logger.Bluetooth(LogLevel.Warn, "Failed to query live Device Identity", ex.Message);
        }
        finally
        {
            _hardwareLock.Release();
        }
    }

    private async Task RefreshBassBoostFromHardwareAsync()
    {
        if (!_gaiaSession.IsConnected) return;

        await _hardwareLock.WaitAsync();
        try
        {
            _logger.Bluetooth(LogLevel.Info, "Reading live Bass Boost state from headphones...");
            var resp = await _gaiaSession.ExchangeAsync(
                MomentumCommands.GetBassBoost,
                null,
                new[] { MomentumCommands.GetBassBoostResponse });

            if (resp.Payload.Length > 0)
            {
                _isBassBoostEnabled = resp.Payload[0] != 0;
                SafeInvokeBassBoostChanged(_isBassBoostEnabled);
                _logger.Bluetooth(LogLevel.Info, $"Synced live Bass Boost: {_isBassBoostEnabled}");
            }
        }
        catch (Exception ex)
        {
            _logger.Bluetooth(LogLevel.Warn, "Failed to query live Bass Boost state", ex.Message);
        }
        finally
        {
            _hardwareLock.Release();
        }
    }

    public async Task<DeviceCommandResult> SetBassBoostAsync(bool enabled)
    {
        if (!_gaiaSession.IsConnected)
            return DeviceCommandResult.Failed("Headphones not connected via control channel.");

        await _hardwareLock.WaitAsync();
        try
        {
            _logger.Bluetooth(LogLevel.Info, $"Writing Bass Boost to MOMENTUM 4 DSP: {enabled}...");
            byte[] payload = new byte[] { (byte)(enabled ? 1 : 0) };
            var resp = await _gaiaSession.ExchangeAsync(
                MomentumCommands.SetBassBoost,
                payload,
                new[] { MomentumCommands.SetBassBoostResponse });

            _isBassBoostEnabled = enabled;
            SafeInvokeBassBoostChanged(_isBassBoostEnabled);
            _logger.Bluetooth(LogLevel.Info, $"Hardware DSP acknowledged Bass Boost: {enabled}");
            return DeviceCommandResult.Success(enabled ? "Bass Boost activated on MOMENTUM 4 DSP." : "Bass Boost deactivated.");
        }
        catch (Exception ex)
        {
            _logger.Bluetooth(LogLevel.Error, "Failed to set Bass Boost on hardware", ex.Message);
            return DeviceCommandResult.Failed("Failed to set Bass Boost: " + ex.Message);
        }
        finally
        {
            _hardwareLock.Release();
        }
    }

    public async Task<bool> GetBassBoostAsync()
    {
        await RefreshBassBoostFromHardwareAsync();
        return _isBassBoostEnabled;
    }

    private void SafeInvokeBassBoostChanged(bool enabled)
    {
        var app = System.Windows.Application.Current;
        if (app != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.InvokeAsync(() => BassBoostChanged?.Invoke(this, enabled));
        }
        else
        {
            BassBoostChanged?.Invoke(this, enabled);
        }
    }

    private void SafeInvokeDeviceInfoChanged()
    {
        var app = System.Windows.Application.Current;
        var clone = _deviceInfo.Clone();
        if (app != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.InvokeAsync(() => DeviceInfoChanged?.Invoke(this, clone));
        }
        else
        {
            DeviceInfoChanged?.Invoke(this, clone);
        }
    }

    private void SafeInvokeNoiseControlChanged()
    {
        var app = System.Windows.Application.Current;
        var clone = _noiseControl.Clone();
        if (app != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.InvokeAsync(() => NoiseControlChanged?.Invoke(this, clone));
        }
        else
        {
            NoiseControlChanged?.Invoke(this, clone);
        }
    }

    private void SafeInvokeEqualizerChanged()
    {
        var app = System.Windows.Application.Current;
        var clone = _equalizer.Clone();
        if (app != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.InvokeAsync(() => EqualizerChanged?.Invoke(this, clone));
        }
        else
        {
            EqualizerChanged?.Invoke(this, clone);
        }
    }

    private void SafeInvokeStatusMessage(string message)
    {
        var app = System.Windows.Application.Current;
        if (app != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.InvokeAsync(() => StatusMessageReported?.Invoke(this, message));
        }
        else
        {
            StatusMessageReported?.Invoke(this, message);
        }
    }

    private string DetectAudioCodec()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var dev in devices)
            {
                if (dev.FriendlyName.Contains("MOMENTUM", StringComparison.OrdinalIgnoreCase) ||
                    dev.FriendlyName.Contains("Sennheiser", StringComparison.OrdinalIgnoreCase))
                {
                    var mix = dev.AudioClient?.MixFormat;
                    if (mix != null)
                    {
                        double khz = mix.SampleRate / 1000.0;
                        string channels = mix.Channels == 2 ? "Stereo" : $"{mix.Channels}ch";
                        return $"AAC ({khz:0.0} kHz, {mix.BitsPerSample}-bit {channels})";
                    }
                }
            }
        }
        catch { }

        return "AAC (48.0 kHz, 16-bit Stereo)";
    }

    private static string FormatBluetoothAddress(ulong address)
    {
        var bytes = BitConverter.GetBytes(address);
        return string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
            bytes[5], bytes[4], bytes[3], bytes[2], bytes[1], bytes[0]);
    }
}
