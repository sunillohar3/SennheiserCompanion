using CommunityToolkit.Mvvm.ComponentModel;

namespace SennheiserMomentum4.Models;

public partial class EqualizerBand : ObservableObject
{
    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _frequencyLabel = string.Empty;

    [ObservableProperty]
    private double _gainDb = 0.0; // -6.0 to +6.0 dB

    public double MinDb => -6.0;
    public double MaxDb => 6.0;

    public EqualizerBand Clone() => new()
    {
        Index = Index,
        Name = Name,
        FrequencyLabel = FrequencyLabel,
        GainDb = GainDb
    };
}
