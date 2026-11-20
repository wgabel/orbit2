using Orbit.Application.Models;

namespace Orbit.Application.Interfaces;

/// <summary>
/// Port to the audio infrastructure. Application services depend only on
/// this contract, never on browser APIs, Faust or JavaScript interop.
/// </summary>
public interface IAudioEngine
{
    Task InitializeAsync();
    Task PlayAsync(PlaybackRequest request);
    Task StopAsync(string soundId);
}
