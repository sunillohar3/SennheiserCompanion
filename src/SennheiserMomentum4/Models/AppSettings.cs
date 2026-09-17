using System.Collections.Generic;

namespace SennheiserMomentum4.Models;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;

    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;

    public bool AutoConnect { get; set; } = true;
    public bool RememberLastDevice { get; set; } = true;
    public string LastDeviceAddress { get; set; } = string.Empty;
    public string DeviceColorVariant { get; set; } = "Denim"; // "Black", "White", "Denim"

    public bool DemoMode { get; set; } = false;

    // Hotkey definitions
    public string HotkeyToggleAnc { get; set; } = "Ctrl+Alt+A";
    public string HotkeyToggleTransparency { get; set; } = "Ctrl+Alt+T";
    public string HotkeyPlayPause { get; set; } = "Ctrl+Alt+P";

    public string SelectedPresetId { get; set; } = "neutral";
    public List<EqualizerPreset> EqualizerPresets { get; set; } = EqualizerPreset.CreateDefaultPresets();
}
