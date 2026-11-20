using System.Globalization;
using Microsoft.AspNetCore.Components;
using Orbit.Domain.Timing;

namespace Orbit.UI.Components.Canvas;

/// <summary>Reusable display and tempo editor. Clock and beat calculations never live in the view.</summary>
public partial class TransportControls
{
    [Parameter, EditorRequired] public TransportSnapshot Snapshot { get; set; } = TransportSnapshot.Initial;
    [Parameter] public EventCallback<double> OnSetTempo { get; set; }
    [Parameter] public EventCallback OnReset { get; set; }
    [Parameter] public string? ClockError { get; set; }

    private readonly string inputId = $"transport-tempo-{Guid.NewGuid():N}";
    private string InputId => inputId;
    private string ErrorId => $"{inputId}-error";
    private double? lastTempo;
    private string Draft { get; set; } = "";
    private string? Error { get; set; }

    protected override void OnParametersSet()
    {
        if (lastTempo == Snapshot.BeatsPerMinute) return;
        lastTempo = Snapshot.BeatsPerMinute;
        Draft = Snapshot.BeatsPerMinute.ToString("G", CultureInfo.InvariantCulture);
        Error = null;
    }

    private void EditDraft(ChangeEventArgs args)
    {
        Draft = args.Value?.ToString() ?? "";
        Error = null;
    }

    private async Task ApplyTempoAsync()
    {
        if (!double.TryParse(Draft, NumberStyles.Float, CultureInfo.InvariantCulture, out var tempo) ||
            !double.IsFinite(tempo) || tempo < MusicalTransport.MinimumTempo || tempo > MusicalTransport.MaximumTempo)
        {
            Error = $"Enter a tempo from {MusicalTransport.MinimumTempo} to {MusicalTransport.MaximumTempo} BPM.";
            return;
        }
        Error = null;
        await OnSetTempo.InvokeAsync(tempo);
    }

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
