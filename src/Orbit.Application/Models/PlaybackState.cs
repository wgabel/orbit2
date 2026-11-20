namespace Orbit.Application.Models;

/// <summary>Observable lifecycle of an audio voice, independent of the UI.</summary>
public enum PlaybackState
{
    Idle,
    Starting,
    Playing,
    Stopping
}
