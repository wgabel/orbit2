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
    Task<PlaybackState> GetPlaybackStateAsync(string soundId);
    /// <summary>Set existing controls on a starting or playing voice; false when inactive.</summary>
    Task<bool> SetParametersAsync(string soundId, IReadOnlyDictionary<string, double> parameters);

    /// <summary>Latest read-only processor meter values, or null when inactive.</summary>
    Task<Dictionary<string, double>?> GetOutputValuesAsync(string soundId);
}
