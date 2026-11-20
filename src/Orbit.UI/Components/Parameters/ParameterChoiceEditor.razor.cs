using System.Globalization;
using Microsoft.AspNetCore.Components;
using Orbit.Domain.Parameters;

namespace Orbit.UI.Components.Parameters;

/// <summary>Maps a selection back to a validated base parameter value.</summary>
public partial class ParameterChoiceEditor
{
    [Parameter, EditorRequired] public ParameterDefinition Definition { get; set; } = default!;
    [Parameter] public double Value { get; set; }
    [Parameter] public EventCallback<double> ValueChanged { get; set; }
    private string InputId { get; } = $"choice-{Guid.NewGuid():N}";
    private string ErrorId => $"{InputId}-error";
    private string? Error { get; set; }

    private static string Format(double value) => value.ToString("G", CultureInfo.InvariantCulture);
    private async Task EditAsync(ChangeEventArgs args)
    {
        if (!double.TryParse(args.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            !Definition.IsValidBaseValue(value))
        {
            Error = "Choose an available option.";
            return;
        }
        Error = null;
        await ValueChanged.InvokeAsync(value);
    }
}
