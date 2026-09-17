using System.Collections.Generic;
using System.Linq;

namespace SennheiserMomentum4.Models;

public class EqualizerPreset
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }
    public List<EqualizerBand> Bands { get; set; } = new();

    public static List<EqualizerPreset> CreateDefaultPresets()
    {
        return new List<EqualizerPreset>
        {
            new()
            {
                Id = "neutral",
                Name = "Neutral",
                IsBuiltIn = true,
                Bands = new List<EqualizerBand>
                {
                    new() { Index = 0, Name = "Bass", FrequencyLabel = "63 Hz", GainDb = 0.0 },
                    new() { Index = 1, Name = "Low Mid", FrequencyLabel = "250 Hz", GainDb = 0.0 },
                    new() { Index = 2, Name = "Mid", FrequencyLabel = "1 kHz", GainDb = 0.0 },
                    new() { Index = 3, Name = "High Mid", FrequencyLabel = "4 kHz", GainDb = 0.0 },
                    new() { Index = 4, Name = "Treble", FrequencyLabel = "8 kHz", GainDb = 0.0 }
                }
            },
            new()
            {
                Id = "rock",
                Name = "Rock",
                IsBuiltIn = true,
                Bands = new List<EqualizerBand>
                {
                    new() { Index = 0, Name = "Bass", FrequencyLabel = "63 Hz", GainDb = 3.5 },
                    new() { Index = 1, Name = "Low Mid", FrequencyLabel = "250 Hz", GainDb = 1.5 },
                    new() { Index = 2, Name = "Mid", FrequencyLabel = "1 kHz", GainDb = -1.0 },
                    new() { Index = 3, Name = "High Mid", FrequencyLabel = "4 kHz", GainDb = 2.0 },
                    new() { Index = 4, Name = "Treble", FrequencyLabel = "8 kHz", GainDb = 3.0 }
                }
            },
            new()
            {
                Id = "pop",
                Name = "Pop",
                IsBuiltIn = true,
                Bands = new List<EqualizerBand>
                {
                    new() { Index = 0, Name = "Bass", FrequencyLabel = "63 Hz", GainDb = 2.0 },
                    new() { Index = 1, Name = "Low Mid", FrequencyLabel = "250 Hz", GainDb = 1.0 },
                    new() { Index = 2, Name = "Mid", FrequencyLabel = "1 kHz", GainDb = 2.0 },
                    new() { Index = 3, Name = "High Mid", FrequencyLabel = "4 kHz", GainDb = 1.5 },
                    new() { Index = 4, Name = "Treble", FrequencyLabel = "8 kHz", GainDb = 2.5 }
                }
            },
            new()
            {
                Id = "movie",
                Name = "Movie",
                IsBuiltIn = true,
                Bands = new List<EqualizerBand>
                {
                    new() { Index = 0, Name = "Bass", FrequencyLabel = "63 Hz", GainDb = 4.0 },
                    new() { Index = 1, Name = "Low Mid", FrequencyLabel = "250 Hz", GainDb = 0.5 },
                    new() { Index = 2, Name = "Mid", FrequencyLabel = "1 kHz", GainDb = 1.5 },
                    new() { Index = 3, Name = "High Mid", FrequencyLabel = "4 kHz", GainDb = 2.0 },
                    new() { Index = 4, Name = "Treble", FrequencyLabel = "8 kHz", GainDb = 1.0 }
                }
            },
            new()
            {
                Id = "podcast",
                Name = "Podcast",
                IsBuiltIn = true,
                Bands = new List<EqualizerBand>
                {
                    new() { Index = 0, Name = "Bass", FrequencyLabel = "63 Hz", GainDb = -2.0 },
                    new() { Index = 1, Name = "Low Mid", FrequencyLabel = "250 Hz", GainDb = 1.0 },
                    new() { Index = 2, Name = "Mid", FrequencyLabel = "1 kHz", GainDb = 3.5 },
                    new() { Index = 3, Name = "High Mid", FrequencyLabel = "4 kHz", GainDb = 2.0 },
                    new() { Index = 4, Name = "Treble", FrequencyLabel = "8 kHz", GainDb = -1.0 }
                }
            },
            new()
            {
                Id = "custom_1",
                Name = "Custom",
                IsBuiltIn = false,
                Bands = new List<EqualizerBand>
                {
                    new() { Index = 0, Name = "Bass", FrequencyLabel = "63 Hz", GainDb = 1.5 },
                    new() { Index = 1, Name = "Low Mid", FrequencyLabel = "250 Hz", GainDb = 0.5 },
                    new() { Index = 2, Name = "Mid", FrequencyLabel = "1 kHz", GainDb = 0.0 },
                    new() { Index = 3, Name = "High Mid", FrequencyLabel = "4 kHz", GainDb = 1.0 },
                    new() { Index = 4, Name = "Treble", FrequencyLabel = "8 kHz", GainDb = 2.0 }
                }
            }
        };
    }

    public EqualizerPreset Clone() => new()
    {
        Id = Id,
        Name = Name,
        IsBuiltIn = IsBuiltIn,
        Bands = Bands.Select(b => b.Clone()).ToList()
    };
}
