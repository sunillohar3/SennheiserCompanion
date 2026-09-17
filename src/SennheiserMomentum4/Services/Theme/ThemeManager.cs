using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using SennheiserMomentum4.Models;
using SennheiserMomentum4.Services.Logging;
using SennheiserMomentum4.Services.Settings;

namespace SennheiserMomentum4.Services.Theme;

public interface IThemeManager
{
    AppTheme CurrentTheme { get; }
    bool IsDark { get; }
    event EventHandler<bool>? ThemeChanged;
    void ApplyTheme(AppTheme theme);
    void ToggleTheme();
}

public class ThemeManager : IThemeManager
{
    private readonly ISettingsService _settingsService;
    private readonly IAppLogger _logger;
    private AppTheme _currentTheme;
    private bool _isDark;

    public AppTheme CurrentTheme => _currentTheme;
    public bool IsDark => _isDark;
    public event EventHandler<bool>? ThemeChanged;

    public ThemeManager(ISettingsService settingsService, IAppLogger logger)
    {
        _settingsService = settingsService;
        _logger = logger;
        _currentTheme = _settingsService.Current.Theme;

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_currentTheme == AppTheme.System && e.Category == UserPreferenceCategory.General)
        {
            ApplyTheme(AppTheme.System);
        }
    }

    public void ApplyTheme(AppTheme theme)
    {
        _currentTheme = theme;
        _isDark = theme switch
        {
            AppTheme.Light => false,
            AppTheme.Dark => true,
            _ => IsWindowsInDarkMode()
        };

        var app = Application.Current;
        if (app == null) return;

        if (_isDark)
        {
            // Dark Theme Palette
            SetBrush(app, "BackgroundBrush", Color.FromRgb(0x12, 0x12, 0x12));
            SetBrush(app, "CardBrush", Color.FromRgb(0x1E, 0x1E, 0x1E));
            SetBrush(app, "CardHoverBrush", Color.FromRgb(0x28, 0x28, 0x28));
            SetBrush(app, "CardSecondaryBrush", Color.FromRgb(0x16, 0x16, 0x16));
            SetBrush(app, "BorderBrush", Color.FromRgb(0x2E, 0x2E, 0x2E));
            SetBrush(app, "BorderSubtleBrush", Color.FromRgb(0x22, 0x22, 0x22));
            SetBrush(app, "TextPrimaryBrush", Color.FromRgb(0xF5, 0xF5, 0xF5));
            SetBrush(app, "TextSecondaryBrush", Color.FromRgb(0x9E, 0x9E, 0x9E));
            SetBrush(app, "TextTertiaryBrush", Color.FromRgb(0x66, 0x66, 0x66));
            SetBrush(app, "SennheiserRedBrush", Color.FromRgb(0xE2, 0x23, 0x1A));
            SetBrush(app, "SennheiserRedHoverBrush", Color.FromRgb(0xFF, 0x3B, 0x30));
            SetBrush(app, "SennheiserRedSubtleBrush", Color.FromArgb(0x33, 0xE2, 0x23, 0x1A));
            SetBrush(app, "SuccessBrush", Color.FromRgb(0x30, 0xD1, 0x58));
        }
        else
        {
            // Light Theme Palette
            SetBrush(app, "BackgroundBrush", Color.FromRgb(0xF8, 0xF9, 0xFA));
            SetBrush(app, "CardBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
            SetBrush(app, "CardHoverBrush", Color.FromRgb(0xF1, 0xF3, 0xF5));
            SetBrush(app, "CardSecondaryBrush", Color.FromRgb(0xFA, 0xFA, 0xFA));
            SetBrush(app, "BorderBrush", Color.FromRgb(0xE5, 0xE7, 0xEB));
            SetBrush(app, "BorderSubtleBrush", Color.FromRgb(0xF0, 0xF2, 0xF4));
            SetBrush(app, "TextPrimaryBrush", Color.FromRgb(0x1A, 0x1A, 0x1A));
            SetBrush(app, "TextSecondaryBrush", Color.FromRgb(0x6C, 0x75, 0x7D));
            SetBrush(app, "TextTertiaryBrush", Color.FromRgb(0x9A, 0xA0, 0xA6));
            SetBrush(app, "SennheiserRedBrush", Color.FromRgb(0xD8, 0x22, 0x18));
            SetBrush(app, "SennheiserRedHoverBrush", Color.FromRgb(0xB7, 0x1C, 0x14));
            SetBrush(app, "SennheiserRedSubtleBrush", Color.FromArgb(0x22, 0xD8, 0x22, 0x18));
            SetBrush(app, "SuccessBrush", Color.FromRgb(0x28, 0xA7, 0x45));
        }

        _settingsService.Current.Theme = theme;
        _settingsService.Save();

        _logger.App(LogLevel.Info, $"Theme applied: {theme} (IsDark: {_isDark})");
        ThemeChanged?.Invoke(this, _isDark);
    }

    public void ToggleTheme()
    {
        var nextTheme = _isDark ? AppTheme.Light : AppTheme.Dark;
        ApplyTheme(nextTheme);
    }

    private static void SetBrush(Application app, string resourceKey, Color color)
    {
        if (app.Resources.Contains(resourceKey) && app.Resources[resourceKey] is SolidColorBrush brush)
        {
            if (brush.IsFrozen)
            {
                app.Resources[resourceKey] = new SolidColorBrush(color);
            }
            else
            {
                brush.Color = color;
            }
        }
        else
        {
            app.Resources[resourceKey] = new SolidColorBrush(color);
        }
    }

    private static bool IsWindowsInDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            if (val is int intVal)
            {
                return intVal == 0;
            }
        }
        catch { }
        return true; // default dark
    }
}
