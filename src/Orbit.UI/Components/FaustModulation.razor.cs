using Microsoft.AspNetCore.Components;
using Orbit.Application.Models;
using Orbit.Application.Services;
using Orbit.UI.Components.Parameters;

namespace Orbit.UI.Components;

/// <summary>
/// Holds only presentation settings, status and errors. Playback continues
/// until Stop is requested, without an application-level audio timer.
/// </summary>
public partial class FaustModulation : IDisposable
{
    [Inject]
    private IModulationDemoService ModulationService { get; set; } = default!;

    private double BaseFrequency { get; set; } = new ModulationDemoSettings().BaseFrequencyHertz;
    private double LfoFrequency { get; set; } = new ModulationDemoSettings().LfoFrequencyHertz;
    private double Depth { get; set; } = new ModulationDemoSettings().DepthHertz;
    private ModulationDemoSettings CurrentSettings => new(BaseFrequency, LfoFrequency, Depth);
    private readonly HashSet<string> InvalidParameters = [];
    private CancellationTokenSource? monitorCancellation;
    private double? EffectiveFrequency { get; set; }
    private string? TelemetryError { get; set; }

    private void OnParameterValidation(ParameterValidation validation)
    {
        if (validation.Error is null)
            InvalidParameters.Remove(validation.Key);
        else
            InvalidParameters.Add(validation.Key);
    }

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
        if (!IsReady || IsPlaying || InvalidParameters.Count > 0)
            return;

        ErrorMessage = null;
        IsPlaying = true;
        TelemetryError = null;
        using var cancellation = new CancellationTokenSource();
        monitorCancellation = cancellation;
        var monitoring = MonitorEffectiveValueAsync(cancellation.Token);
        try
        {
            await ModulationService.PlayAsync(CurrentSettings);
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Playback failed: {exception.Message}";
        }
        finally
        {
            cancellation.Cancel();
            await monitoring;
            monitorCancellation = null;
            EffectiveFrequency = null;
            IsPlaying = false;
        }
    }

    // Display-rate observation only: Faust generates every audio sample itself.
    private async Task MonitorEffectiveValueAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var value = await ModulationService.GetEffectiveFrequencyAsync();
                if (cancellationToken.IsCancellationRequested)
                    break;
                EffectiveFrequency = value;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            EffectiveFrequency = null;
            TelemetryError = "Live parameter feedback unavailable.";
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested) { }
    }

    public void Dispose() => monitorCancellation?.Cancel();

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
