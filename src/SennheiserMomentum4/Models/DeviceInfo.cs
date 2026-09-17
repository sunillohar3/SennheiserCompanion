using CommunityToolkit.Mvvm.ComponentModel;

namespace SennheiserMomentum4.Models;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting
}

public partial class DeviceInfo : ObservableObject
{
    [ObservableProperty]
    private string _deviceName = "MOMENTUM 4";

    [ObservableProperty]
    private string _modelNumber = "M4AEBT";

    [ObservableProperty]
    private string _firmwareVersion = "2.13.28";

    [ObservableProperty]
    private string _hardwareVersion = "0.4.0";

    [ObservableProperty]
    private string _serialNumber = "M4-2024-884920";

    [ObservableProperty]
    private string _colorVariant = "Denim"; // Auto-detected: "Black", "White", "Denim"

    [ObservableProperty]
    private string _bluetoothAddress = "00:1B:66:8A:4F:92";

    [ObservableProperty]
    private string _audioCodec = "aptX Adaptive";

    [ObservableProperty]
    private int _batteryPercentage = 78;

    [ObservableProperty]
    private bool _isCharging = false;

    [ObservableProperty]
    private string? _estimatedRemainingTime = null; // null if not reliably exposed

    [ObservableProperty]
    private ConnectionState _state = ConnectionState.Disconnected;

    public DeviceInfo Clone() => (DeviceInfo)MemberwiseClone();
}
