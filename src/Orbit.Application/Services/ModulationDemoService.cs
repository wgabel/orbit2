using Orbit.Application.Graphs;
using Orbit.Application.Interfaces;
using Orbit.Application.Models;
using Orbit.Domain.Timing;

namespace Orbit.Application.Services;

/// <summary>
/// Builds and plays the fixed demonstration graph through the engine port.
/// No knowledge of Blazor, Web Audio or Faust's JavaScript API.
/// </summary>
public sealed class ModulationDemoService(IAudioEngine audioEngine, IAudioClock audioClock)
    : IModulationDemoService
{
    public Task InitializeAsync() => audioEngine.InitializeAsync();

    public Task PlayAsync(ModulationDemoSettings settings) =>
        audioEngine.PlayAsync(ModulationDemoPatch.Create(settings).Playback);

    public Task StopAsync() => audioEngine.StopAsync(ModulationDemoPatch.SoundId);

    // Keep the processor-specific meter name out of the presentation layer.
    public async Task<double?> GetEffectiveFrequencyAsync()
    {
        var values = await audioEngine.GetOutputValuesAsync(ModulationDemoPatch.SoundId);
        if (values is not null)
            foreach (var (path, value) in values)
                if (path.EndsWith("/effectiveFrequency", StringComparison.Ordinal))
                    return value;

        return null;
    }

    public Task<AudioClockSnapshot?> GetClockSnapshotAsync() => audioClock.GetSnapshotAsync();
}
