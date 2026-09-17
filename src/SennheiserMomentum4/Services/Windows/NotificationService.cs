using System;
using SennheiserMomentum4.Services.Logging;

namespace SennheiserMomentum4.Services.Windows;

public interface INotificationService
{
    void ShowNotification(string title, string message);
}

public class NotificationService : INotificationService
{
    private readonly IAppLogger _logger;
    public event Action<string, string>? NotificationRequested;

    public NotificationService(IAppLogger logger)
    {
        _logger = logger;
    }

    public void ShowNotification(string title, string message)
    {
        _logger.App(LogLevel.Info, $"Notification: {title} - {message}");
        NotificationRequested?.Invoke(title, message);
    }
}
