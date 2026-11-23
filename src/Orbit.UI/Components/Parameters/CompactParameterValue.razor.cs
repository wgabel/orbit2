using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Orbit.Domain.Parameters;
using Orbit.Presentation.Parameters;

namespace Orbit.UI.Components.Parameters;

/// <summary>Direct compact editing of a parameter's committed base value, never its live DSP value.</summary>
public partial class CompactParameterValue
{
    [Parameter, EditorRequired] public Guid ParameterId { get; set; }
    [Parameter, EditorRequired] public ParameterDefinition Definition { get; set; } = default!;
    [Parameter] public double Value { get; set; }
    [Parameter] public bool IsModulated { get; set; }
    [Parameter] public double? EffectiveValue { get; set; }
    [Parameter] public EventCallback<double> ValueChanged { get; set; }

    private readonly string inputId = $"compact-parameter-{Guid.NewGuid():N}";
    private double? previousValue;
    private bool editing;
    private string draft = "";
    private string InputId => inputId;
    private string ErrorId => $"{inputId}-error";
    private string Draft => draft;
    private string UnitLabel => ParameterUnitLabel.Format(Definition.Unit);
    private string EffectiveText => EffectiveValue is double value && double.IsFinite(value)
        ? $"{value.ToString("0.####", CultureInfo.InvariantCulture)} {UnitLabel}".TrimEnd()
        : "—";

    private string? Error => TryGetDraftValue(out _) switch
    {
        DraftValidity.InvalidNumber => "Enter a valid number.",
        DraftValidity.OutOfRange => $"Enter a value from {Format(Definition.Minimum)} to {Format(Definition.Maximum)} {UnitLabel}.",
        _ => null
    };

    protected override void OnParametersSet()
    {
        // Telemetry rerenders must not replace a focused, unfinished numeric draft.
        if (previousValue == Value) return;
        previousValue = Value;
        if (!editing) draft = Format(Value);
    }

    private void BeginEdit(FocusEventArgs _) => editing = true;
    private void EditDraft(ChangeEventArgs args) => draft = args.Value?.ToString() ?? "";

    private async Task HandleKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
        {
            draft = Format(Value);
            editing = false;
        }
        else if (args.Key == "Enter") await CommitAsync();
    }

    private Task CommitOnBlurAsync(FocusEventArgs _)
    {
        editing = false;
        return CommitAsync();
    }

    private async Task CommitAsync()
    {
        if (TryGetDraftValue(out var value) != DraftValidity.Valid) return;
        if (value != Value) await ValueChanged.InvokeAsync(value);
    }

    private DraftValidity TryGetDraftValue(out double value)
    {
        if (!double.TryParse(draft, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
            !double.IsFinite(value)) return DraftValidity.InvalidNumber;
        return Definition.IsValidBaseValue(value) ? DraftValidity.Valid : DraftValidity.OutOfRange;
    }

    private static string Format(double value) => value.ToString("G", CultureInfo.InvariantCulture);
    private enum DraftValidity { Valid, InvalidNumber, OutOfRange }
}
