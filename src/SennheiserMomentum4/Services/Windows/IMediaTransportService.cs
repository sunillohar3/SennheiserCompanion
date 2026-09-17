using System;
using System.Threading.Tasks;

namespace SennheiserMomentum4.Services.Windows;

public class MediaPlaybackInfo
{
    public string Title { get; set; } = "No media playing";
    public string Artist { get; set; } = string.Empty;
    public bool IsPlaying { get; set; } = false;
    public bool HasMedia { get; set; } = false;
}

public interface IMediaTransportService
{
    MediaPlaybackInfo CurrentMedia { get; }
    event EventHandler<MediaPlaybackInfo>? MediaChanged;
    Task InitializeAsync();
    Task TogglePlayPauseAsync();
    Task NextAsync();
    Task PreviousAsync();
}
