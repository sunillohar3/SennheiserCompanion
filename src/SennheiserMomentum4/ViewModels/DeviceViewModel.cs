using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SennheiserMomentum4.Models;
using SennheiserMomentum4.Services.Device;
using SennheiserMomentum4.Services.Logging;

namespace SennheiserMomentum4.ViewModels;

public partial class DeviceViewModel : ViewModelBase
{
    private readonly IMomentum4DeviceService _deviceService;
    private readonly IAppLogger _logger;

    [ObservableProperty]
    private DeviceInfo _deviceInfo;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusError = false;

    [ObservableProperty]
    private bool _isBusy = false;

    public bool IsConnected => DeviceInfo.State == ConnectionState.Connected;
    public bool IsDemoMode => _deviceService.IsDemoMode;

    public string ProductImageSource => DeviceInfo?.ColorVariant switch
    {
        "White" => "pack://application:,,,/Resources/Images/m4_white.png",
        "Denim" => "pack://application:,,,/Resources/Images/m4_denim.png",
        "Copper" => "pack://application:,,,/Resources/Images/m4_copper.png",
        "Graphite" => "pack://application:,,,/Resources/Images/m4_graphite.png",
        _ => "pack://application:,,,/Resources/Images/m4_black.png"
    };

    public string ColorFinishDisplay => DeviceInfo?.ColorVariant switch
    {
        "Denim" => "Denim Edition",
        "White" => "White & Silver",
        "Copper" => "Copper Edition",
        "Graphite" => "Graphite Edition",
        _ => "Matte Black"
    };

    public DeviceViewModel(IMomentum4DeviceService deviceService, IAppLogger logger)
    {
        _deviceService = deviceService;
        _logger = logger;
        _deviceInfo = _deviceService.DeviceInfo.Clone();

        _deviceService.DeviceInfoChanged += (s, info) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                DeviceInfo = info.Clone();
                OnPropertyChanged(nameof(DeviceInfo));
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ProductImageSource));
                OnPropertyChanged(nameof(ColorFinishDisplay));
            });
        };
    }

    [RelayCommand]
    private async Task ReconnectAsync()
    {
        IsBusy = true;
        StatusMessage = "Reconnecting to MOMENTUM 4...";
        IsStatusError = false;

        await _deviceService.DisconnectAsync();
        var result = await _deviceService.ConnectAsync();

        StatusMessage = result.Message;
        IsStatusError = !result.IsSuccess;
        IsBusy = false;
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        IsBusy = true;
        var result = await _deviceService.DisconnectAsync();
        StatusMessage = result.Message;
        IsStatusError = !result.IsSuccess;
        IsBusy = false;
    }

    [RelayCommand]
    private void OpenBluetoothSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.App(LogLevel.Warn, "Failed to open bluetooth settings", ex.Message);
        }
    }

    [RelayCommand]
    private async Task CheckFirmwareUpdateAsync()
    {
        IsBusy = true;
        StatusMessage = "Checking for firmware updates...";
        IsStatusError = false;

        var result = await _deviceService.CheckFirmwareUpdateAsync();
        StatusMessage = result.Message;
        IsStatusError = !result.IsSuccess;
        IsBusy = false;
    }
}
