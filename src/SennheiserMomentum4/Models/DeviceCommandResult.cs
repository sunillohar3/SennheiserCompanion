namespace SennheiserMomentum4.Models;

public class DeviceCommandResult
{
    public bool IsSuccess { get; set; }
    public bool IsSupported { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public string? ErrorDetail { get; set; }

    public static DeviceCommandResult Success(string message = "Operation completed successfully.")
    {
        return new DeviceCommandResult
        {
            IsSuccess = true,
            IsSupported = true,
            Message = message
        };
    }

    public static DeviceCommandResult Unavailable(string reason)
    {
        return new DeviceCommandResult
        {
            IsSuccess = false,
            IsSupported = false,
            Message = reason
        };
    }

    public static DeviceCommandResult Failed(string message, string? errorDetail = null)
    {
        return new DeviceCommandResult
        {
            IsSuccess = false,
            IsSupported = true,
            Message = message,
            ErrorDetail = errorDetail
        };
    }
}
