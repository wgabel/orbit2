using Orbit.Domain.Parameters;

namespace Orbit.Presentation.Canvas;

/// <summary>A symmetric, linear display rail, not a graph validation limit.
/// Capture the range when editing begins; retain it throughout the gesture.
/// The normalized rail avoids overflowing a physical max-minus-min span.</summary>
public sealed class ConnectionAmountSliderRange
{
    private ConnectionAmountSliderRange(double extent) => Extent = extent;

    public double Extent { get; }
    public double Minimum => -Extent;
    public double Maximum => Extent;
    public const double PositionMinimum = -1;
    public const double PositionMaximum = 1;
    public const double PositionStep = 0.01;

    public static ConnectionAmountSliderRange? Resolve(ConnectionAmountSemantics semantics, double amount)
    {
        if (!semantics.CanEdit) return null;
        if (!double.IsFinite(amount)) throw new ArgumentOutOfRangeException(nameof(amount));

        var preferredExtent = semantics.TargetParameter?.Definition.Unit switch
        {
            ParameterUnit.Hertz => 200d,
            ParameterUnit.Cents => 1200d,
            ParameterUnit.Decibels => 24d,
            ParameterUnit.Seconds => 2d,
            ParameterUnit.Degrees => 360d,
            ParameterUnit.BeatsPerMinute => 120d,
            ParameterUnit.Milliseconds => 1000d,
            ParameterUnit.Semitones => 12d,
            _ => 2d
        };
        var magnitude = Math.Abs(amount);
        // Expand only presentation bounds, retaining headroom when possible.
        // Never narrow the set of valid authored coefficients.
        var extent = magnitude <= preferredExtent ? preferredExtent :
            magnitude > double.MaxValue / 2 ? double.MaxValue : magnitude * 2;
        return new(extent);
    }

    public double ToPosition(double amount)
    {
        if (!double.IsFinite(amount)) throw new ArgumentOutOfRangeException(nameof(amount));
        return Math.Clamp(amount / Extent, PositionMinimum, PositionMaximum);
    }

    public double FromPosition(double position)
    {
        if (!double.IsFinite(position)) throw new ArgumentOutOfRangeException(nameof(position));
        return Math.Clamp(position, PositionMinimum, PositionMaximum) * Extent;
    }

    public bool Contains(double amount) => double.IsFinite(amount) && amount >= Minimum && amount <= Maximum;
}
