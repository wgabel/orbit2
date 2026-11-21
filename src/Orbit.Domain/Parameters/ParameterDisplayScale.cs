namespace Orbit.Domain.Parameters;

/// <summary>A physical value at a normalized position on a logarithmic display rail.</summary>
public readonly record struct ParameterDisplayAnchor(double Position, double Value);

/// <summary>Immutable display metadata. Consecutive anchors define logarithmic
/// segments; neither modulation nor stored parameter values use these coordinates.</summary>
public sealed class ParameterDisplayScale
{
    public ParameterDisplayScale(params ParameterDisplayAnchor[] anchors)
    {
        ArgumentNullException.ThrowIfNull(anchors);
        var copy = anchors.ToArray();
        if (copy.Length < 2 || copy[0].Position != 0 || copy[^1].Position != 1 ||
            copy.Any(anchor => !double.IsFinite(anchor.Position) || !double.IsFinite(anchor.Value) || anchor.Value <= 0))
            throw new ArgumentException("Logarithmic display anchors must be finite, positive and span the normalized rail.", nameof(anchors));

        for (var index = 1; index < copy.Length; index++)
            if (copy[index].Position <= copy[index - 1].Position || copy[index].Value <= copy[index - 1].Value)
                throw new ArgumentException("Display anchor positions and values must increase strictly.", nameof(anchors));

        Anchors = Array.AsReadOnly(copy);
    }

    public IReadOnlyList<ParameterDisplayAnchor> Anchors { get; }
}
