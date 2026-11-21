using Microsoft.JSInterop;
using Orbit.Application.Interfaces;
using Orbit.Application.Models;
using Orbit.Domain.Timing;

namespace Orbit.Infrastructure.Audio;

/// <summary>
/// Infrastructure adapter implementing the framework-neutral engine port.
/// Owns C# to JavaScript interop, but no audio graph or playback policy.
/// </summary>
public sealed class BrowserAudioEngine(IJSRuntime javaScriptRuntime) : IAudioEngine, ISignalCapture, IAudioClock, ITransportSignalSink, IRhythmicActionSink, IAsyncDisposable
{
    // Deployments publish the entire relative-import tree under a unique URL.
    // An entry-only cache buster would still allow stale transitive imports.
    public static string AudioModulePath { get; } = BrowserModulePaths.GetPath("audio", "audio.js");
    private IJSObjectReference? audioModule;
    private Task<IJSObjectReference>? audioModuleTask;

    public async Task InitializeAsync()
    {
        // Import only the entry module before the user gesture. AudioContext
        // creation and resume are deferred until play is invoked.
        // Concurrent initializers share the one import.
        var task = audioModuleTask ??= javaScriptRuntime.InvokeAsync<IJSObjectReference>(
            "import", AudioModulePath).AsTask();

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

    public async Task<PlaybackState> GetPlaybackStateAsync(string soundId)
    {
        var state = await GetAudioModule().InvokeAsync<string>("getPlaybackState", soundId);
        return Enum.Parse<PlaybackState>(state, ignoreCase: true);
    }

    public async Task<bool> SetParametersAsync(string soundId, IReadOnlyDictionary<string, double> parameters) =>
        await GetAudioModule().InvokeAsync<bool>("setParameters", soundId, parameters);

    public async Task MarkRevisionAsync(string soundId, long revision) =>
        await GetAudioModule().InvokeVoidAsync("markRevision", soundId, revision);

    public async Task<bool> ReplaceAsync(PlaybackRequest request, long revision) =>
        await GetAudioModule().InvokeAsync<bool>("replace", request, revision);

    public async Task<bool> SendRhythmicActionAsync(string soundId, Guid generation, int index, RhythmicAction action) =>
        await GetAudioModule().InvokeAsync<bool>("sendRhythmicAction", soundId, generation, index, (int)action);

    public async Task<AudioClockSnapshot?> GetSnapshotAsync() =>
        await GetAudioModule().InvokeAsync<AudioClockSnapshot?>("getClockSnapshot");

    public async Task SetTransportAsync(TransportSnapshot snapshot)
    {
        // Tempo can be edited before playback. Importing the module must not start audio.
        await InitializeAsync();
        await GetAudioModule().InvokeVoidAsync("setTransport", snapshot);
    }

    public async Task<Dictionary<string, double>?> GetOutputValuesAsync(string soundId) =>
        await GetAudioModule().InvokeAsync<Dictionary<string, double>?>("getOutputValues", soundId);

    public async Task<SignalObservationFrame?> ReadSignalAsync(string soundId, Guid generation, Guid subscriptionId, int channel) =>
        await GetAudioModule().InvokeAsync<SignalObservationFrame?>("readSignal", soundId, generation, subscriptionId, channel);

    public async Task ReleaseSignalAsync(string soundId, Guid subscriptionId)
    {
        if (audioModule is not null)
            await audioModule.InvokeVoidAsync("releaseSignal", soundId, subscriptionId);
    }

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
