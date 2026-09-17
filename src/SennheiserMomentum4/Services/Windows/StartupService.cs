using System;
using System.Diagnostics;
using Microsoft.Win32;
using SennheiserMomentum4.Services.Logging;

namespace SennheiserMomentum4.Services.Windows;

public interface IStartupService
{
    bool IsStartWithWindowsEnabled();
    void SetStartWithWindows(bool enable);
}

public class StartupService : IStartupService
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "SennheiserMomentum4";
    private readonly IAppLogger _logger;

    public StartupService(IAppLogger logger)
    {
        _logger = logger;
    }

    public bool IsStartWithWindowsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
            return key?.GetValue(AppName) != null;
        }
        catch (Exception ex)
        {
            _logger.App(LogLevel.Warn, "Error reading startup registry key", ex.Message);
            return false;
        }
    }

    public void SetStartWithWindows(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\" --minimized");
                    _logger.App(LogLevel.Info, "Startup with Windows enabled.");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
                _logger.App(LogLevel.Info, "Startup with Windows disabled.");
            }
        }
        catch (Exception ex)
        {
            _logger.App(LogLevel.Warn, "Error modifying startup registry key", ex.Message);
        }
    }
}
