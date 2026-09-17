using CommunityToolkit.Mvvm.ComponentModel;

namespace SennheiserMomentum4.Models;

public enum NoiseControlMode
{
    AdaptiveAnc,
    Transparency,
    Off
}

public partial class NoiseControlState : ObservableObject
{
    [ObservableProperty]
    private NoiseControlMode _mode = NoiseControlMode.AdaptiveAnc;

    [ObservableProperty]
    private double _ancIntensity = 80.0; // 0 to 100%

    [ObservableProperty]
    private double _transparencyIntensity = 70.0; // 0 to 100%

    [ObservableProperty]
    private bool _windNoiseReduction = true;

    [ObservableProperty]
    private bool _isFeatureAvailableOnInterface = true;

    [ObservableProperty]
    private string? _interfaceMessage;

    public NoiseControlState Clone() => new()
    {
        Mode = Mode,
        AncIntensity = AncIntensity,
        TransparencyIntensity = TransparencyIntensity,
        WindNoiseReduction = WindNoiseReduction,
        IsFeatureAvailableOnInterface = IsFeatureAvailableOnInterface,
        InterfaceMessage = InterfaceMessage
    };
}
