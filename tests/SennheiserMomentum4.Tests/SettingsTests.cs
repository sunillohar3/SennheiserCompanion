using Xunit;
using SennheiserMomentum4.Models;

namespace SennheiserMomentum4.Tests;

public class SettingsTests
{
    [Fact]
    public void AppSettings_Defaults_HaveRecommendedHotkeys()
    {
        var settings = new AppSettings();

        Assert.Equal("Ctrl+Alt+A", settings.HotkeyToggleAnc);
        Assert.Equal("Ctrl+Alt+T", settings.HotkeyToggleTransparency);
        Assert.Equal("Ctrl+Alt+P", settings.HotkeyPlayPause);
        Assert.True(settings.MinimizeToTray);
        Assert.True(settings.CloseToTray);
        Assert.True(settings.ShowNotifications);
    }

    [Fact]
    public void AppSettings_DemoMode_DefaultsToFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.DemoMode);
    }
}
