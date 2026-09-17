using System.Threading.Tasks;
using Xunit;
using SennheiserMomentum4.Models;
using SennheiserMomentum4.Services.Device;
using SennheiserMomentum4.Services.Logging;

namespace SennheiserMomentum4.Tests;

public class DeviceServiceTests
{
    private class TestLogger : IAppLogger
    {
        public void App(LogLevel level, string message, string? details = null) { }
        public void Bluetooth(LogLevel level, string message, string? details = null) { }
        public void Device(LogLevel level, string message, string? details = null) { }
        public string GetLogContent(LogCategory category, int maxLines = 100) => string.Empty;
        public string GetLogDirectory() => string.Empty;
        public void Log(LogCategory category, LogLevel level, string message, string? details = null) { }
    }

    [Fact]
    public async Task MockDeviceService_ConnectAsync_SetsConnectedState()
    {
        var logger = new TestLogger();
        var service = new MockMomentum4DeviceService(logger);

        var result = await service.ConnectAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(ConnectionState.Connected, service.DeviceInfo.State);
        Assert.Equal(78, service.DeviceInfo.BatteryPercentage);
        Assert.Equal("MOMENTUM 4", service.DeviceInfo.DeviceName);
    }

    [Fact]
    public async Task MockDeviceService_DisconnectAsync_SetsDisconnectedState()
    {
        var logger = new TestLogger();
        var service = new MockMomentum4DeviceService(logger);

        await service.ConnectAsync();
        var result = await service.DisconnectAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(ConnectionState.Disconnected, service.DeviceInfo.State);
    }

    [Fact]
    public async Task MockDeviceService_SetNoiseControl_UpdatesModeAndIntensity()
    {
        var logger = new TestLogger();
        var service = new MockMomentum4DeviceService(logger);

        var newState = new NoiseControlState
        {
            Mode = NoiseControlMode.Transparency,
            AncIntensity = 50.0,
            TransparencyIntensity = 85.0,
            WindNoiseReduction = true
        };

        var result = await service.SetNoiseControlAsync(newState);

        Assert.True(result.IsSuccess);
        Assert.Equal(NoiseControlMode.Transparency, service.NoiseControl.Mode);
        Assert.Equal(85.0, service.NoiseControl.TransparencyIntensity);
    }

    [Fact]
    public async Task MockDeviceService_SetEqualizer_AppliesPreset()
    {
        var logger = new TestLogger();
        var service = new MockMomentum4DeviceService(logger);

        var rockPreset = EqualizerPreset.CreateDefaultPresets()[1]; // Rock
        var result = await service.SetEqualizerAsync(rockPreset);

        Assert.True(result.IsSuccess);
        Assert.Equal("Rock", service.CurrentEqualizer.Name);
        Assert.Equal(5, service.CurrentEqualizer.Bands.Count);
    }

    [Fact]
    public async Task BleDeviceService_SetNoiseControl_EnforcesNoFakeRuleWhenDisconnected()
    {
        var logger = new TestLogger();
        var bleService = new Momentum4BleDeviceService(logger);

        var newState = new NoiseControlState { Mode = NoiseControlMode.AdaptiveAnc };
        var result = await bleService.SetNoiseControlAsync(newState);

        // Must fail or report unavailable - never fake success!
        Assert.False(result.IsSuccess);
        Assert.Contains("unavailable", result.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CanReadWindowsBluetoothDevice()
    {
        var selector = Windows.Devices.Bluetooth.BluetoothDevice.GetDeviceSelectorFromPairingState(true);
        var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(selector);
        var target = System.Linq.Enumerable.FirstOrDefault(devices, d => d.Name.Contains("MOMENTUM", System.StringComparison.OrdinalIgnoreCase));
        if (target == null) return; // Skip if no physical MOMENTUM 4 is paired on this test host
        
        var btDevice = await Windows.Devices.Bluetooth.BluetoothDevice.FromIdAsync(target.Id);
        Assert.NotNull(btDevice);

        // Check battery property query on Device (DevNode) instead of DeviceInterface
        var pnpSelector = "System.Devices.DeviceInstanceId:~~\"80C3BAAD157F\"";
        var requestedProps = new[] { "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2" };
        var pnpDevices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(
            pnpSelector, 
            requestedProps, 
            Windows.Devices.Enumeration.DeviceInformationKind.Device);
        
        int? battery = null;
        foreach (var dev in pnpDevices)
        {
            if (dev.Properties.TryGetValue("{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2", out var val) && val is byte b)
            {
                battery = b;
                break;
            }
        }
        if (btDevice.ConnectionStatus == Windows.Devices.Bluetooth.BluetoothConnectionStatus.Connected)
        {
            Assert.NotNull(battery);
            Assert.True(battery.Value >= 0 && battery.Value <= 100);
        }
    }

    [Fact]
    public async Task MockDeviceService_SetBassBoost_TogglesState()
    {
        var logger = new TestLogger();
        var service = new MockMomentum4DeviceService(logger);

        Assert.False(service.IsBassBoostEnabled);
        var res = await service.SetBassBoostAsync(true);
        Assert.True(res.IsSuccess);
        Assert.True(service.IsBassBoostEnabled);

        var resOff = await service.SetBassBoostAsync(false);
        Assert.True(resOff.IsSuccess);
        Assert.False(service.IsBassBoostEnabled);
    }

    [Fact]
    public async Task Momentum4BleDeviceService_SetBassBoost_WhenDisconnected_FailsGracefully()
    {
        var logger = new TestLogger();
        var service = new Momentum4BleDeviceService(logger);

        var res = await service.SetBassBoostAsync(true);
        Assert.False(res.IsSuccess);
        Assert.Contains("not connected", res.Message, System.StringComparison.OrdinalIgnoreCase);
    }
}
