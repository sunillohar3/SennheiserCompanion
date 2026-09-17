using System;
using SennheiserMomentum4.Models;

namespace SennheiserMomentum4.Services.Settings;

public interface ISettingsService
{
    AppSettings Current { get; }
    event EventHandler<AppSettings>? SettingsChanged;
    void Save();
    void Reload();
    void Reset();
}
