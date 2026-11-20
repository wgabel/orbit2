using Microsoft.JSInterop;
using Orbit.Application.Interfaces;
using Orbit.Application.Models;
using Orbit.Domain.Timing;

namespace Orbit.Infrastructure.Audio;

/// <summary>
/// Infrastructure adapter implementing the framework-neutral engine port.
/// Owns C# to JavaScript interop, but no audio graph or playback policy.
/// </summary>
public sealed class BrowserAudioEngine(IJSRuntime javaScriptRuntime) : IAudioEngine, IAudioClock, IAsyncDisposable
{
    private IJSObjectReference? audioModule;
    private Task<IJSObjectReference>? audioModuleTask;

    public async Task InitializeAsync()
    {
        // Import only the entry module before the user gesture. AudioContext
        // creation and resume are deferred until play is invoked.
        // Concurrent initializers share the one import.
        var task = audioModuleTask ??= javaScriptRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Orbit.Infrastructure/audio/audio.js").AsTask();

        try
        {
            audioModule = await task;
        }
        catch
        {
            if (ReferenceEquals(audioModuleTask, task))
                audioModuleTask = null;

            throw;
        }
    }

    public async Task PlayAsync(PlaybackRequest request)
    {
        await GetAudioModule().InvokeVoidAsync("play", request);
    }

    public async Task StopAsync(string soundId)
    {
        await GetAudioModule().InvokeVoidAsync("stop", soundId);
    }

    public async Task<AudioClockSnapshot?> GetSnapshotAsync() =>
        await GetAudioModule().InvokeAsync<AudioClockSnapshot?>("getClockSnapshot");

    public async Task<Dictionary<string, double>?> GetOutputValuesAsync(string soundId) =>
        await GetAudioModule().InvokeAsync<Dictionary<string, double>?>("getOutputValues", soundId);

    public async ValueTask DisposeAsync()
    {
        if (audioModule is null)
        {
            return;
        }

        try
        {
            await audioModule.InvokeVoidAsync("dispose");
        }
        finally
        {
            await audioModule.DisposeAsync();
            audioModule = null;
            audioModuleTask = null;
        }
    }

    private IJSObjectReference GetAudioModule() =>
        audioModule ?? throw new InvalidOperationException("The audio engine is not initialized.");
}
