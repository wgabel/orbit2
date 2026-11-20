using Microsoft.AspNetCore.Components;
using Orbit.Application.Models;
using Orbit.Application.Services;

namespace Orbit.UI.Components;

/// <summary>
/// Holds only presentation settings, status and errors. Playback continues
/// until Stop is requested, without an application-level audio timer.
/// </summary>
public partial class FaustModulation
{
    [Inject]
    private IModulationDemoService ModulationService { get; set; } = default!;

    private double BaseFrequency { get; set; } = new ModulationDemoSettings().BaseFrequencyHertz;
    private double LfoFrequency { get; set; } = new ModulationDemoSettings().LfoFrequencyHertz;
    private double Depth { get; set; } = new ModulationDemoSettings().DepthHertz;
    private bool IsReady { get; set; }
    private bool IsPlaying { get; set; }
    private bool IsStopping { get; set; }
    private string? ErrorMessage { get; set; }
    private string ClockDescription { get; set; } = "No audio clock read yet.";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        try
        {
            await ModulationService.InitializeAsync();
            IsReady = true;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Audio initialization failed: {exception.Message}";
        }

        StateHasChanged();
    }

    private async Task PlayAsync()
    {
        if (!IsReady || IsPlaying)
            return;

        ErrorMessage = null;
        IsPlaying = true;
        try
        {
            await ModulationService.PlayAsync(new ModulationDemoSettings(BaseFrequency, LfoFrequency, Depth));
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

    private async Task StopAsync()
    {
        if (!IsPlaying || IsStopping)
            return;

        IsStopping = true;
        try
        {
            await ModulationService.StopAsync();
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Stopping failed: {exception.Message}";
        }
        finally
        {
            IsStopping = false;
        }
    }

    private async Task ReadClockAsync()
    {
        try
        {
            var clock = await ModulationService.GetClockSnapshotAsync();
            ClockDescription = clock is null
                ? "Audio context has not started."
                : $"{clock.SampleRate:N0} Hz · frame {clock.CurrentSample:N0} · {clock.CurrentTime:F3} s";
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Could not read the audio clock: {exception.Message}";
        }
    }
}
