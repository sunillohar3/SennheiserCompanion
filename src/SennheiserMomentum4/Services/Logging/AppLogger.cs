using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SennheiserMomentum4.Services.Logging;

public class AppLogger : IAppLogger
{
    private readonly string _logDirectory;
    private readonly object _lock = new();

    public AppLogger()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _logDirectory = Path.Combine(localAppData, "SennheiserMomentum4", "logs");

        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }
        catch
        {
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(_logDirectory);
        }
    }

    public string GetLogDirectory() => _logDirectory;

    public void Log(LogCategory category, LogLevel level, string message, string? details = null)
    {
        var fileName = category switch
        {
            LogCategory.App => "app.log",
            LogCategory.Device => "device.log",
            LogCategory.Bluetooth => "bluetooth.log",
            _ => "app.log"
        };

        var filePath = Path.Combine(_logDirectory, fileName);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var levelStr = level.ToString().ToUpperInvariant().PadRight(5);
        var logLine = $"{timestamp} [{levelStr}] {message}";

        if (!string.IsNullOrEmpty(details))
        {
            logLine += $" | Details: {details}";
        }

        lock (_lock)
        {
            try
            {
                File.AppendAllText(filePath, logLine + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Never crash app from logging failure
            }
        }
    }

    public void App(LogLevel level, string message, string? details = null)
        => Log(LogCategory.App, level, message, details);

    public void Device(LogLevel level, string message, string? details = null)
        => Log(LogCategory.Device, level, message, details);

    public void Bluetooth(LogLevel level, string message, string? details = null)
        => Log(LogCategory.Bluetooth, level, message, details);

    public string GetLogContent(LogCategory category, int maxLines = 100)
    {
        var fileName = category switch
        {
            LogCategory.App => "app.log",
            LogCategory.Device => "device.log",
            LogCategory.Bluetooth => "bluetooth.log",
            _ => "app.log"
        };

        var filePath = Path.Combine(_logDirectory, fileName);
        if (!File.Exists(filePath))
        {
            return $"No log entries recorded in {fileName} yet.";
        }

        lock (_lock)
        {
            try
            {
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                var takeLines = lines.Skip(Math.Max(0, lines.Length - maxLines)).ToArray();
                return string.Join(Environment.NewLine, takeLines);
            }
            catch (Exception ex)
            {
                return $"Error reading {fileName}: {ex.Message}";
            }
        }
    }
}
