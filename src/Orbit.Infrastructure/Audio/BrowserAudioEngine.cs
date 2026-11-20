using Microsoft.JSInterop;
using Orbit.Application.Interfaces;
using Orbit.Application.Models;

namespace Orbit.Infrastructure.Audio;

/// <summary>
/// Infrastructure adapter implementing the framework-neutral engine port.
/// Owns C# to JavaScript interop, but no audio graph or playback policy.
/// </summary>
public sealed class BrowserAudioEngine(IJSRuntime javaScriptRuntime) : IAudioEngine, IAsyncDisposable
{
    private IJSObjectReference? audioModule;

    public async Task InitializeAsync()
    {
        // Import only the entry module before the user gesture. AudioContext
        // creation and resume are deferred until play is invoked.
        audioModule ??= await javaScriptRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Orbit.Infrastructure/audio/audio.js");
    }

    public async Task PlayAsync(PlaybackRequest request)
    {
        await GetAudioModule().InvokeVoidAsync("play", request);
    }

    public async Task StopAsync(string soundId)
    {
        await GetAudioModule().InvokeVoidAsync("stop", soundId);
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
        }
    }

    private IJSObjectReference GetAudioModule() =>
        audioModule ?? throw new InvalidOperationException("The audio engine is not initialized.");
}
