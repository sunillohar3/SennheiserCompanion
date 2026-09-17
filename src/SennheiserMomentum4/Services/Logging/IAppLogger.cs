namespace SennheiserMomentum4.Services.Logging;

public enum LogCategory
{
    App,
    Device,
    Bluetooth
}

public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error
}

public interface IAppLogger
{
    void Log(LogCategory category, LogLevel level, string message, string? details = null);
    void App(LogLevel level, string message, string? details = null);
    void Device(LogLevel level, string message, string? details = null);
    void Bluetooth(LogLevel level, string message, string? details = null);
    string GetLogContent(LogCategory category, int maxLines = 100);
    string GetLogDirectory();
}
