using Orbit.Application.Interfaces;
using Orbit.Application.Models;

namespace Orbit.Application.Services;

/// <summary>
/// Application use cases for audio. The test-tone settings belong here, not
/// in the browser bridge or the Razor component.
/// </summary>
public sealed class AudioService(IAudioEngine audioEngine) : IAudioService
{
    public Task InitializeAsync() => audioEngine.InitializeAsync();

    public Task PlayTestToneAsync()
    {
        var request = new PlaybackRequest(
            SoundId: "test-tone",
            Processor: "Sine",
            DurationMilliseconds: 400,
            Volume: 0.1,
            Parameters: new Dictionary<string, double>
            {
                ["frequency"] = 440
            });

        return audioEngine.PlayAsync(request);
    }
}
