using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SennheiserMomentum4.Models;
using SennheiserMomentum4.Services.Device;
using SennheiserMomentum4.Services.Logging;

namespace SennheiserMomentum4.ViewModels;

public partial class NoiseControlViewModel : ViewModelBase
{
    private readonly IMomentum4DeviceService _deviceService;
    private readonly IAppLogger _logger;

    [ObservableProperty]
    private NoiseControlState _state;

    [ObservableProperty]
    private bool _isAdaptiveAnc;

    [ObservableProperty]
    private bool _isTransparency;

    [ObservableProperty]
    private bool _isOff;

    [ObservableProperty]
    private double _ancIntensity;

    [ObservableProperty]
    private double _transparencyIntensity;

    [ObservableProperty]
    private bool _windNoiseReduction;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusError = false;

    public bool IsFeatureAvailable => State.IsFeatureAvailableOnInterface;
    public string? InterfaceMessage => State.InterfaceMessage;
    public bool IsDemoMode => _deviceService.IsDemoMode;

    public NoiseControlViewModel(IMomentum4DeviceService deviceService, IAppLogger logger)
    {
        _deviceService = deviceService;
        _logger = logger;

        _state = _deviceService.NoiseControl;
        SyncFromState(_state);

        _deviceService.NoiseControlChanged += (s, e) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                State = e;
                SyncFromState(e);
                OnPropertyChanged(nameof(IsFeatureAvailable));
                OnPropertyChanged(nameof(InterfaceMessage));
            });
        };
    }

    private void SyncFromState(NoiseControlState state)
    {
        IsAdaptiveAnc = state.Mode == NoiseControlMode.AdaptiveAnc;
        IsTransparency = state.Mode == NoiseControlMode.Transparency;
        IsOff = state.Mode == NoiseControlMode.Off;

        AncIntensity = state.AncIntensity;
        TransparencyIntensity = state.TransparencyIntensity;
        WindNoiseReduction = state.WindNoiseReduction;
    }

    [RelayCommand]
    private async Task SelectModeAsync(string modeName)
    {
        if (Enum.TryParse<NoiseControlMode>(modeName, true, out var mode))
        {
            var newState = new NoiseControlState
            {
                Mode = mode,
                AncIntensity = AncIntensity,
                TransparencyIntensity = TransparencyIntensity,
                WindNoiseReduction = WindNoiseReduction
            };

            var res = await _deviceService.SetNoiseControlAsync(newState);
            StatusMessage = res.Message;
            IsStatusError = !res.IsSuccess;

            if (res.IsSuccess)
            {
                SyncFromState(_deviceService.NoiseControl);
            }
        }
    }

    [RelayCommand]
    private async Task UpdateSettingsAsync()
    {
        var mode = IsAdaptiveAnc ? NoiseControlMode.AdaptiveAnc
            : IsTransparency ? NoiseControlMode.Transparency
            : NoiseControlMode.Off;

        var newState = new NoiseControlState
        {
            Mode = mode,
            AncIntensity = AncIntensity,
            TransparencyIntensity = TransparencyIntensity,
            WindNoiseReduction = WindNoiseReduction
        };

        var res = await _deviceService.SetNoiseControlAsync(newState);
        StatusMessage = res.Message;
        IsStatusError = !res.IsSuccess;
    }
}
