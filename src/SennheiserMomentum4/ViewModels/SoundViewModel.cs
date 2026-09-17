using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SennheiserMomentum4.Models;
using SennheiserMomentum4.Services.Device;
using SennheiserMomentum4.Services.Logging;
using SennheiserMomentum4.Services.Settings;

namespace SennheiserMomentum4.ViewModels;

public partial class SoundViewModel : ViewModelBase
{
    private readonly IMomentum4DeviceService _deviceService;
    private readonly ISettingsService _settingsService;
    private readonly IAppLogger _logger;
    private System.Threading.Timer? _debounceTimer;
    private bool _suppressBandEvents = false;

    [ObservableProperty]
    private ObservableCollection<EqualizerPreset> _presets = new();

    [ObservableProperty]
    private EqualizerPreset? _selectedPreset;

    [ObservableProperty]
    private ObservableCollection<EqualizerBand> _activeBands = new();

    [ObservableProperty]
    private string _customPresetNameInput = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusError = false;

    [ObservableProperty]
    private bool _isBassBoostEnabled = false;

    public bool IsDemoMode => _deviceService.IsDemoMode;

    public SoundViewModel(
        IMomentum4DeviceService deviceService,
        ISettingsService settingsService,
        IAppLogger logger)
    {
        _deviceService = deviceService;
        _settingsService = settingsService;
        _logger = logger;

        _isBassBoostEnabled = _deviceService.IsBassBoostEnabled;

        LoadPresets();

        _deviceService.EqualizerChanged += (s, preset) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var match = Presets.FirstOrDefault(p => p.Id == preset.Id || p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase));
                if (match != null && SelectedPreset != match)
                {
                    _selectedPreset = match;
                    OnPropertyChanged(nameof(SelectedPreset));
                    RebindActiveBands(match);
                }
            });
        };

        _deviceService.BassBoostChanged += (s, enabled) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsBassBoostEnabled = enabled;
                OnPropertyChanged(nameof(IsBassBoostEnabled));
            });
        };
    }

    [RelayCommand]
    private async Task ToggleBassBoostAsync()
    {
        bool targetState = !IsBassBoostEnabled;
        var res = await _deviceService.SetBassBoostAsync(targetState);
        if (res.IsSuccess)
        {
            IsBassBoostEnabled = targetState;
            StatusMessage = targetState ? "💥 Bass Boost activated on MOMENTUM 4 DSP." : "Bass Boost deactivated.";
            IsStatusError = false;
        }
        else
        {
            StatusMessage = res.Message;
            IsStatusError = true;
        }
    }

    private void LoadPresets()
    {
        Presets.Clear();
        var savedPresets = _settingsService.Current.EqualizerPresets;
        if (savedPresets == null || savedPresets.Count == 0)
        {
            savedPresets = EqualizerPreset.CreateDefaultPresets();
            _settingsService.Current.EqualizerPresets = savedPresets;
            _settingsService.Save();
        }

        foreach (var preset in savedPresets)
        {
            Presets.Add(preset);
        }

        var activeId = _settingsService.Current.SelectedPresetId;
        var initial = Presets.FirstOrDefault(p => p.Id == activeId) ?? Presets.FirstOrDefault();
        if (initial != null)
        {
            SelectedPreset = initial;
        }
    }

    partial void OnSelectedPresetChanged(EqualizerPreset? value)
    {
        if (value == null) return;

        RebindActiveBands(value);

        _settingsService.Current.SelectedPresetId = value.Id;
        _settingsService.Save();

        _ = ApplyEqualizerAsync(value);
    }

    private void RebindActiveBands(EqualizerPreset preset)
    {
        _suppressBandEvents = true;
        try
        {
            foreach (var b in ActiveBands)
            {
                b.PropertyChanged -= OnBandPropertyChanged;
            }
            ActiveBands.Clear();

            foreach (var band in preset.Bands)
            {
                var cloned = band.Clone();
                cloned.PropertyChanged += OnBandPropertyChanged;
                ActiveBands.Add(cloned);
            }
        }
        finally
        {
            _suppressBandEvents = false;
        }
    }

    private void OnBandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressBandEvents) return;

        if (e.PropertyName == nameof(EqualizerBand.GainDb))
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new System.Threading.Timer(_ =>
            {
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
                {
                    await OnBandGainChangedAsync();
                });
            }, null, 120, Timeout.Infinite);
        }
    }

    [RelayCommand]
    private async Task OnBandGainChangedAsync()
    {
        if (SelectedPreset == null) return;

        _suppressBandEvents = true;
        try
        {
            // If built-in preset was modified, switch to/create Custom preset
            if (SelectedPreset.IsBuiltIn)
            {
                var customPreset = Presets.FirstOrDefault(p => !p.IsBuiltIn && p.Name.StartsWith("Custom"));
                if (customPreset == null)
                {
                    customPreset = new EqualizerPreset
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = "Custom",
                        IsBuiltIn = false,
                        Bands = ActiveBands.Select(b => b.Clone()).ToList()
                    };
                    Presets.Add(customPreset);
                }
                else
                {
                    customPreset.Bands = ActiveBands.Select(b => b.Clone()).ToList();
                }

#pragma warning disable MVVMTK0034
                _selectedPreset = customPreset;
                OnPropertyChanged(nameof(SelectedPreset));
#pragma warning restore MVVMTK0034
            }
            else
            {
                SelectedPreset.Bands = ActiveBands.Select(b => b.Clone()).ToList();
            }
        }
        finally
        {
            _suppressBandEvents = false;
        }

        SavePresetsToSettings();
        await ApplyEqualizerAsync(SelectedPreset);
    }

    private async Task ApplyEqualizerAsync(EqualizerPreset preset)
    {
        var result = await _deviceService.SetEqualizerAsync(preset);
        StatusMessage = result.Message;
        IsStatusError = !result.IsSuccess;
    }

    [RelayCommand]
    private void SaveNewPreset()
    {
        if (string.IsNullOrWhiteSpace(CustomPresetNameInput))
        {
            StatusMessage = "Please enter a valid preset name.";
            IsStatusError = true;
            return;
        }

        var newPreset = new EqualizerPreset
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = CustomPresetNameInput.Trim(),
            IsBuiltIn = false,
            Bands = ActiveBands.Select(b => b.Clone()).ToList()
        };

        Presets.Add(newPreset);
        SavePresetsToSettings();
        SelectedPreset = newPreset;
        CustomPresetNameInput = string.Empty;
        StatusMessage = $"Saved new preset '{newPreset.Name}'.";
        IsStatusError = false;
    }

    [RelayCommand]
    private void DeleteCurrentPreset()
    {
        if (SelectedPreset == null || SelectedPreset.IsBuiltIn)
        {
            StatusMessage = "Built-in presets cannot be deleted.";
            IsStatusError = true;
            return;
        }

        var name = SelectedPreset.Name;
        Presets.Remove(SelectedPreset);
        SavePresetsToSettings();
        SelectedPreset = Presets.FirstOrDefault();
        StatusMessage = $"Deleted preset '{name}'.";
        IsStatusError = false;
    }

    [RelayCommand]
    private void ResetToNeutral()
    {
        var neutral = Presets.FirstOrDefault(p => p.Id == "neutral");
        if (neutral != null)
        {
            SelectedPreset = neutral;
            StatusMessage = "Reset equalizer to Neutral.";
            IsStatusError = false;
        }
    }

    private void SavePresetsToSettings()
    {
        _settingsService.Current.EqualizerPresets = Presets.ToList();
        _settingsService.Save();
    }
}
