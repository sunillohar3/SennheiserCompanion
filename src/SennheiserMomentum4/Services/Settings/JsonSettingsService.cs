using System;
using System.IO;
using System.Text.Json;
using SennheiserMomentum4.Models;
using SennheiserMomentum4.Services.Logging;

namespace SennheiserMomentum4.Services.Settings;

public class JsonSettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly IAppLogger _logger;
    private AppSettings _current;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public AppSettings Current => _current;

    public event EventHandler<AppSettings>? SettingsChanged;

    public JsonSettingsService(IAppLogger logger)
    {
        _logger = logger;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "SennheiserMomentum4");
        Directory.CreateDirectory(dir);
        _settingsFilePath = Path.Combine(dir, "settings.json");

        _current = LoadFromFile();
    }

    private AppSettings LoadFromFile()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    _logger.App(LogLevel.Info, "Settings loaded successfully from disk.");
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.App(LogLevel.Warn, "Failed to load settings file. Using defaults.", ex.Message);
        }

        var defaultSettings = new AppSettings();
        SaveInternal(defaultSettings);
        return defaultSettings;
    }

    public void Save()
    {
        SaveInternal(_current);
        SettingsChanged?.Invoke(this, _current);
    }

    private void SaveInternal(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(_settingsFilePath, json);
            _logger.App(LogLevel.Info, "Settings saved to disk.");
        }
        catch (Exception ex)
        {
            _logger.App(LogLevel.Error, "Failed to write settings to disk.", ex.Message);
        }
    }

    public void Reload()
    {
        _current = LoadFromFile();
        SettingsChanged?.Invoke(this, _current);
    }

    public void Reset()
    {
        _current = new AppSettings();
        Save();
        _logger.App(LogLevel.Info, "Settings reset to defaults.");
    }
}
