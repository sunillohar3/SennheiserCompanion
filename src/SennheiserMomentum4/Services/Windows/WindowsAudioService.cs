using System;
using System.Diagnostics;
using NAudio.CoreAudioApi;
using SennheiserMomentum4.Services.Logging;

namespace SennheiserMomentum4.Services.Windows;

public class WindowsAudioService : IWindowsAudioService, IDisposable
{
    private readonly IAppLogger _logger;
    private MMDeviceEnumerator? _deviceEnumerator;
    private MMDevice? _defaultPlaybackDevice;
    private AudioEndpointVolume? _endpointVolume;

    public event EventHandler<int>? VolumeChanged;
    public event EventHandler<bool>? MuteChanged;

    public string DefaultDeviceName => _defaultPlaybackDevice?.FriendlyName ?? "Default Windows Audio";

    public bool IsAudioActive
    {
        get
        {
            try
            {
                if (_defaultPlaybackDevice != null)
                {
                    return _defaultPlaybackDevice.AudioMeterInformation.MasterPeakValue > 0.001f;
                }
            }
            catch { }
            return false;
        }
    }

    private int _lastSetVolume = -1;
    private long _lastSetTimestamp = 0;
    private int _cachedVolume = 50;

    public int Volume
    {
        get
        {
            if (_endpointVolume != null)
            {
                try
                {
                    _cachedVolume = (int)Math.Round(_endpointVolume.MasterVolumeLevelScalar * 100);
                    return _cachedVolume;
                }
                catch
                {
                    EnsureEndpoint();
                }
            }
            else
            {
                EnsureEndpoint();
            }
            return _cachedVolume;
        }
        set
        {
            _cachedVolume = Math.Clamp(value, 0, 100);
            if (_endpointVolume != null)
            {
                try
                {
                    float targetScalar = _cachedVolume / 100f;
                    if (Math.Abs(_endpointVolume.MasterVolumeLevelScalar - targetScalar) >= 0.005f)
                    {
                        _lastSetVolume = _cachedVolume;
                        _lastSetTimestamp = Environment.TickCount64;
                        _endpointVolume.MasterVolumeLevelScalar = targetScalar;
                    }
                }
                catch (Exception ex)
                {
                    _logger.App(LogLevel.Warn, "Failed setting volume, recovering endpoint", ex.Message);
                    EnsureEndpoint();
                    try
                    {
                        if (_endpointVolume != null)
                        {
                            _endpointVolume.MasterVolumeLevelScalar = _cachedVolume / 100f;
                        }
                    }
                    catch { }
                }
            }
            else
            {
                EnsureEndpoint();
            }
        }
    }

    public bool IsMuted
    {
        get
        {
            if (_endpointVolume != null)
            {
                try
                {
                    return _endpointVolume.Mute;
                }
                catch { }
            }
            return false;
        }
        set
        {
            if (_endpointVolume != null)
            {
                try
                {
                    _endpointVolume.Mute = value;
                }
                catch (Exception ex)
                {
                    _logger.App(LogLevel.Warn, "Failed setting mute", ex.Message);
                }
            }
        }
    }

    public bool IsSennheiserDevice => _defaultPlaybackDevice?.FriendlyName.Contains("MOMENTUM", StringComparison.OrdinalIgnoreCase) == true ||
                                      _defaultPlaybackDevice?.FriendlyName.Contains("Sennheiser", StringComparison.OrdinalIgnoreCase) == true;

    public WindowsAudioService(IAppLogger logger)
    {
        _logger = logger;
        Initialize();
    }

    private void Initialize()
    {
        EnsureEndpoint();
    }

    private void EnsureEndpoint()
    {
        try
        {
            if (_deviceEnumerator == null)
            {
                _deviceEnumerator = new MMDeviceEnumerator();
            }

            MMDevice? targetDevice = null;
            try
            {
                // Specifically search for Sennheiser MOMENTUM 4 active audio device
                var activeDevices = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (var dev in activeDevices)
                {
                    if (dev.FriendlyName.Contains("MOMENTUM", StringComparison.OrdinalIgnoreCase) ||
                        dev.FriendlyName.Contains("Sennheiser", StringComparison.OrdinalIgnoreCase))
                    {
                        targetDevice = dev;
                        break;
                    }
                }
            }
            catch { }

            // Fallback to system default playback device if Sennheiser device not found separately
            targetDevice ??= _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            if (targetDevice != null)
            {
                if (_endpointVolume != null)
                {
                    try { _endpointVolume.OnVolumeNotification -= OnVolumeNotification; } catch { }
                    try { _endpointVolume.Dispose(); } catch { }
                }

                _defaultPlaybackDevice = targetDevice;
                _endpointVolume = _defaultPlaybackDevice.AudioEndpointVolume;
                _endpointVolume.OnVolumeNotification += OnVolumeNotification;
                _cachedVolume = (int)Math.Round(_endpointVolume.MasterVolumeLevelScalar * 100);
            }
        }
        catch (Exception ex)
        {
            _logger.App(LogLevel.Warn, "Failed initializing Windows Audio Endpoint Volume", ex.Message);
        }
    }

    private void OnVolumeNotification(AudioVolumeNotificationData data)
    {
        int vol = (int)Math.Round(data.MasterVolume * 100);
        // Suppress echo callbacks triggered by our own volume set operations within 600ms
        if (Environment.TickCount64 - _lastSetTimestamp < 600 && Math.Abs(vol - _lastSetVolume) <= 2)
        {
            return;
        }

        VolumeChanged?.Invoke(this, vol);
        MuteChanged?.Invoke(this, data.Muted);
    }

    public void OpenSoundSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.App(LogLevel.Warn, "Failed opening Windows sound settings", ex.Message);
        }
    }

    public void Dispose()
    {
        if (_endpointVolume != null)
        {
            _endpointVolume.OnVolumeNotification -= OnVolumeNotification;
            _endpointVolume.Dispose();
        }
        _defaultPlaybackDevice?.Dispose();
        _deviceEnumerator?.Dispose();
    }
}
