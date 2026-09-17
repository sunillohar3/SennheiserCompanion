using Xunit;
using SennheiserMomentum4.Models;

namespace SennheiserMomentum4.Tests;

public class EqualizerPresetTests
{
    [Fact]
    public void DefaultPresets_ContainsRequiredSennheiserPresets()
    {
        var presets = EqualizerPreset.CreateDefaultPresets();

        Assert.Contains(presets, p => p.Name == "Neutral");
        Assert.Contains(presets, p => p.Name == "Rock");
        Assert.Contains(presets, p => p.Name == "Pop");
        Assert.Contains(presets, p => p.Name == "Movie");
        Assert.Contains(presets, p => p.Name == "Podcast");
        Assert.Contains(presets, p => p.Name == "Custom");
    }

    [Fact]
    public void Presets_HaveFiveSupportedBands()
    {
        var presets = EqualizerPreset.CreateDefaultPresets();

        foreach (var preset in presets)
        {
            Assert.Equal(5, preset.Bands.Count);
            Assert.Equal("Bass", preset.Bands[0].Name);
            Assert.Equal("Low Mid", preset.Bands[1].Name);
            Assert.Equal("Mid", preset.Bands[2].Name);
            Assert.Equal("High Mid", preset.Bands[3].Name);
            Assert.Equal("Treble", preset.Bands[4].Name);

            foreach (var band in preset.Bands)
            {
                Assert.InRange(band.GainDb, -6.0, 6.0);
            }
        }
    }

    [Fact]
    public void Preset_Clone_CreatesDeepCopy()
    {
        var original = EqualizerPreset.CreateDefaultPresets()[0];
        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.Name, clone.Name);
        Assert.NotSame(original.Bands[0], clone.Bands[0]);

        clone.Bands[0].GainDb = 5.0;
        Assert.NotEqual(original.Bands[0].GainDb, clone.Bands[0].GainDb);
    }
}
