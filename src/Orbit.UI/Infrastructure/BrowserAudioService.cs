using Microsoft.JSInterop;
using Orbit.Application.Services;

namespace Orbit.UI.Infrastructure;

/// <summary>
/// Browser implementation of IAudioService. This infrastructure adapter is
/// the only C# code that knows about JavaScript interop or the Faust module.
/// </summary>
public sealed class BrowserAudioService(IJSRuntime javaScriptRuntime) : IAudioService
{
    private IJSObjectReference? audioModule;

    public async Task InitializeAsync()
    {
        // Preload the module before a click so playback can begin from the
        // user's gesture without waiting for a JavaScript module download.
        audioModule ??= await javaScriptRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./faust/audio.js");
    }

    public async Task PlayTestToneAsync()
    {
        if (audioModule is null)
        {
            throw new InvalidOperationException("The Faust audio adapter is not initialized.");
        }

        await audioModule.InvokeVoidAsync("playTestTone");
    }
}
