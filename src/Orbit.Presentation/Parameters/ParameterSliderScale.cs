using Orbit.Domain.Parameters;

namespace Orbit.Presentation.Parameters;

/// <summary>The common physical-value/rail-position mapping for the thumb,
/// inverse range edits, predicted modulation bounds and live marker.</summary>
public static class ParameterSliderScale
{
    public static double ToPosition(ParameterDefinition definition, double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (definition.Maximum <= definition.Minimum) return 0;
        if (value <= definition.Minimum) return 0;
        if (value >= definition.Maximum) return 1;
        if (definition.DisplayScale is not { } scale)
            return (value - definition.Minimum) / (definition.Maximum - definition.Minimum);

        var anchors = scale.Anchors;
        for (var index = 1; index < anchors.Count; index++)
        {
            var upper = anchors[index];
            if (value > upper.Value) continue;
            var lower = anchors[index - 1];
            if (value == upper.Value) return upper.Position;
            var fraction = (Math.Log(value) - Math.Log(lower.Value)) /
                (Math.Log(upper.Value) - Math.Log(lower.Value));
            return lower.Position + (upper.Position - lower.Position) * fraction;
        }
        return 1;
    }

    public static double FromPosition(ParameterDefinition definition, double position)
    {
        if (!double.IsFinite(position)) throw new ArgumentOutOfRangeException(nameof(position));
        if (position <= 0) return definition.Minimum;
        if (position >= 1) return definition.Maximum;
        if (definition.DisplayScale is not { } scale)
            return definition.Minimum + position * (definition.Maximum - definition.Minimum);

        var anchors = scale.Anchors;
        for (var index = 1; index < anchors.Count; index++)
        {
            var upper = anchors[index];
            if (position > upper.Position) continue;
            var lower = anchors[index - 1];
            if (position == upper.Position) return upper.Value;
            var fraction = (position - lower.Position) / (upper.Position - lower.Position);
            return Math.Clamp(Math.Exp(Math.Log(lower.Value) +
                fraction * (Math.Log(upper.Value) - Math.Log(lower.Value))), lower.Value, upper.Value);
        }
        return definition.Maximum;
    }
}
