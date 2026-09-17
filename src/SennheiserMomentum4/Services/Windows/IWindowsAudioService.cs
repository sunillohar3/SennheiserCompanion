using System;

namespace SennheiserMomentum4.Services.Windows;

public interface IWindowsAudioService
{
    int Volume { get; set; }
    bool IsMuted { get; set; }
    string DefaultDeviceName { get; }
    bool IsSennheiserDevice { get; }
    bool IsAudioActive { get; }
    event EventHandler<int>? VolumeChanged;
    event EventHandler<bool>? MuteChanged;
    void OpenSoundSettings();
}
