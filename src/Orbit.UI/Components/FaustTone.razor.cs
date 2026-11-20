using Microsoft.AspNetCore.Components;
using Orbit.Application.Services;

namespace Orbit.UI.Components;

/// <summary>
/// Presentation boundary for FaustTone.razor. Holds only UI readiness and
/// error state; delegates all audio operations to the injected contract.
/// </summary>
public partial class FaustTone
{
    [Inject]
    private IAudioService AudioService { get; set; } = default!;

    private bool IsReady { get; set; }
    private bool IsPlaying { get; set; }
    private string? ErrorMessage { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            await AudioService.InitializeAsync();
            IsReady = true;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Audio initialization failed: {exception.Message}";
        }

        // OnAfterRenderAsync does not automatically schedule another render.
        StateHasChanged();
    }

    private async Task PlayToneAsync()
    {
        if (!IsReady || IsPlaying)
        {
            return;
        }

        IsPlaying = true;
        ErrorMessage = null;

        try
        {
            await AudioService.PlayTestToneAsync();
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Playback failed: {exception.Message}";
        }
        finally
        {
            IsPlaying = false;
        }
    }
}
