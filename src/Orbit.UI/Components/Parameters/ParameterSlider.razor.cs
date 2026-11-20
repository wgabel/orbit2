using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Orbit.Domain.Parameters;

namespace Orbit.UI.Components.Parameters;

/// <summary>Presentation-only editor shared by every parameter and node.</summary>
public partial class ParameterSlider
{
    [Parameter, EditorRequired] public ParameterDefinition Definition { get; set; } = default!;
    [Parameter] public double Value { get; set; }
    [Parameter] public EventCallback<ParameterValueEdit> ValueChanged { get; set; }
    [Parameter] public EventCallback<Guid> GestureEnded { get; set; }
    [Parameter] public double Step { get; set; } = 1;
    [Parameter] public bool IsModulated { get; set; }
    [Parameter] public ParameterRange? ModulationRange { get; set; }
    [Parameter] public double? EffectiveValue { get; set; }
    [Parameter] public EventCallback<ParameterValidation> ValidationChanged { get; set; }

    private readonly string inputId = $"parameter-{Guid.NewGuid():N}";
    private double? lastValue;
    private Guid? gestureId;
    private bool keyboardGesture;
    private string InputId => inputId;
    private string DescriptionId => $"{inputId}-description";
    private string ErrorId => $"{inputId}-error";
    private string Draft { get; set; } = "";
    private string? Error { get; set; }

    private string UnitLabel => ParameterUnitLabel.Format(Definition.Unit);

    protected override void OnParametersSet()
    {
        // Telemetry re-renders must not erase a user's unfinished invalid entry.
        if (lastValue != Value)
        {
            lastValue = Value;
            Draft = Format(Value);
            Error = null;
        }
    }

    private void BeginPointer(PointerEventArgs args)
    {
        if (!args.IsPrimary || args.Button != 0) return;
        gestureId = Guid.NewGuid();
        keyboardGesture = false;
    }

    private void BeginKeyboard(KeyboardEventArgs args)
    {
        if (!IsRangeKey(args.Key) || gestureId.HasValue) return;
        gestureId = Guid.NewGuid();
        keyboardGesture = true;
    }

    private Task EndKeyboard(KeyboardEventArgs args) => IsRangeKey(args.Key) && keyboardGesture ? EndGestureAsync() : Task.CompletedTask;
    private Task EndPointerAsync() => keyboardGesture ? Task.CompletedTask : EndGestureAsync();
    private static bool IsRangeKey(string key) => key is "ArrowLeft" or "ArrowRight" or "ArrowUp" or "ArrowDown" or "PageUp" or "PageDown" or "Home" or "End";

    private async Task EndGestureAsync()
    {
        if (gestureId is not Guid id) return;
        gestureId = null;
        keyboardGesture = false;
        await GestureEnded.InvokeAsync(id);
    }

    private Task EditRangeAsync(ChangeEventArgs args) => EditAsync(args, gestureId);
    private Task EditNumberAsync(ChangeEventArgs args) => EditAsync(args, null);

    private async Task EditAsync(ChangeEventArgs args, Guid? editGestureId)
    {
        Draft = args.Value?.ToString() ?? "";
        if (!double.TryParse(Draft, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value))
        {
            Error = "Enter a valid number.";
        }
        else if (!Definition.IsValidBaseValue(value))
        {
            Error = $"Enter a value from {Format(Definition.Minimum)} to {Format(Definition.Maximum)} {UnitLabel}.";
        }
        else
        {
            Error = null;
            lastValue = value;
            await ValueChanged.InvokeAsync(new ParameterValueEdit(value, editGestureId));
        }

        await ValidationChanged.InvokeAsync(new ParameterValidation(Definition.Key, Error));
    }

    private static string Format(double value) => value.ToString("G", CultureInfo.InvariantCulture);
    private static string FormatLive(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    // The native range edits base limits; modulation beyond them is clipped
    // visually, while the labels still show the full range and actual value.
    private double Position(double value) => Definition.Maximum > Definition.Minimum
        ? Math.Clamp((value - Definition.Minimum) / (Definition.Maximum - Definition.Minimum), 0, 1) * 100
        : 0;

    private string BandStyle(ParameterRange range)
    {
        var start = Position(range.Minimum);
        var end = Position(range.Maximum);
        return $"left:{FormatLive(start)}%;width:{FormatLive(Math.Max(0, end - start))}%";
    }

    private string MarkerStyle(double value) => $"left:{FormatLive(Position(value))}%";
}
