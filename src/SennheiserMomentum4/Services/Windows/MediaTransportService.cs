using System;
using System.Threading.Tasks;
using Windows.Media.Control;
using SennheiserMomentum4.Services.Logging;

namespace SennheiserMomentum4.Services.Windows;

public class MediaTransportService : IMediaTransportService
{
    private readonly IAppLogger _logger;
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private MediaPlaybackInfo _currentMedia = new();

    public MediaPlaybackInfo CurrentMedia => _currentMedia;
    public event EventHandler<MediaPlaybackInfo>? MediaChanged;

    public MediaTransportService(IAppLogger logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var task = Task.Run(async () => await GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
            var completed = await Task.WhenAny(task, Task.Delay(1500));
            if (completed == task)
            {
                _sessionManager = await task;
                if (_sessionManager != null)
                {
                    _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
                    UpdateCurrentSession();
                    _logger.App(LogLevel.Info, "Windows Media Transport Session Manager connected.");
                }
            }
            else
            {
                _logger.App(LogLevel.Info, "Windows Media Transport Session Manager deferred or timed out.");
            }
        }
        catch (Exception ex)
        {
            _logger.App(LogLevel.Warn, "Failed to initialize Windows Media Transport Session Manager", ex.Message);
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        UpdateCurrentSession();
    }

    private void UpdateCurrentSession()
    {
        if (_sessionManager == null) return;

        try
        {
            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }

            _currentSession = _sessionManager.GetCurrentSession();

            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                RefreshMediaInfo();
            }
            else
            {
                _currentMedia = new MediaPlaybackInfo { Title = "No active media", Artist = "Windows Media Controls", HasMedia = false };
                SafeInvokeMediaChanged();
            }
        }
        catch (Exception ex)
        {
            _logger.App(LogLevel.Debug, "Error updating current media session", ex.Message);
        }
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        RefreshMediaInfo();
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        RefreshMediaInfo();
    }

    private async void RefreshMediaInfo()
    {
        if (_currentSession == null) return;

        try
        {
            var props = await _currentSession.TryGetMediaPropertiesAsync();
            var playback = _currentSession.GetPlaybackInfo();

            var isPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var title = props?.Title;
            var artist = props?.Artist;

            _currentMedia = new MediaPlaybackInfo
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Audio Playing" : title,
                Artist = string.IsNullOrWhiteSpace(artist) ? "Windows Media" : artist,
                IsPlaying = isPlaying,
                HasMedia = true
            };

            SafeInvokeMediaChanged();
        }
        catch (Exception ex)
        {
            _logger.App(LogLevel.Debug, "Error refreshing media info", ex.Message);
        }
    }

    private void SafeInvokeMediaChanged()
    {
        var app = System.Windows.Application.Current;
        if (app != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.InvokeAsync(() => MediaChanged?.Invoke(this, _currentMedia));
        }
        else
        {
            MediaChanged?.Invoke(this, _currentMedia);
        }
    }

    public async Task TogglePlayPauseAsync()
    {
        try
        {
            if (_currentSession != null)
            {
                await _currentSession.TryTogglePlayPauseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.App(LogLevel.Warn, "Failed to toggle play/pause", ex.Message);
        }
    }

    public async Task NextAsync()
    {
        try
        {
            if (_currentSession != null)
            {
                await _currentSession.TrySkipNextAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.App(LogLevel.Warn, "Failed to skip next track", ex.Message);
        }
    }

    public async Task PreviousAsync()
    {
        try
        {
            if (_currentSession != null)
            {
                await _currentSession.TrySkipPreviousAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.App(LogLevel.Warn, "Failed to skip previous track", ex.Message);
        }
    }
}
