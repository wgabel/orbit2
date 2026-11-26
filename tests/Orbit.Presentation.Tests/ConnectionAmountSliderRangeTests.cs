using Orbit.Application.Graphs;
using Orbit.Application.Models;
using Orbit.Domain.Ports;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class ConnectionAmountSliderRangeTests
{
    [Fact]
    public void DirectFrequencyDepthUsesPhysicalUnitsAndPreservesSignedValues()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var graph = session.Snapshot;
        var semantics = ConnectionAmountSemantics.Resolve(graph, graph.Connections[0]);
        var range = ConnectionAmountSliderRange.Resolve(semantics, -100)!;
        Assert.Equal(-200d, range.Minimum);
        Assert.Equal(200d, range.Maximum);
        Assert.Equal(-0.5, range.ToPosition(-100));
        Assert.Equal(-100d, range.FromPosition(-0.5));
        Assert.Equal(0d, range.FromPosition(0));
        Assert.Equal(200d, range.FromPosition(1));
    }

    [Fact]
    public void AudioRailIsDimensionlessAndPreservesUnity()
    {
        var semantics = new ConnectionAmountSemantics(SignalType.Audio, null);
        var range = ConnectionAmountSliderRange.Resolve(semantics, 1)!;
        Assert.Equal(-2d, range.Minimum);
        Assert.Equal(2d, range.Maximum);
        Assert.Equal(0.5, range.ToPosition(1));
        Assert.Equal(1d, range.FromPosition(0.5));
        Assert.False(range.Contains(4));
    }

    [Theory]
    [InlineData(-30000d)]
    [InlineData(30000d)]
    [InlineData(double.MaxValue)]
    [InlineData(-double.MaxValue)]
    public void ExistingLargeCoefficientsExpandRailWithoutOverflow(double amount)
    {
        var semantics = new ConnectionAmountSemantics(SignalType.Modulation, null);
        var range = ConnectionAmountSliderRange.Resolve(semantics, amount)!;
        Assert.True(range.Contains(amount));
        Assert.True(double.IsFinite(range.Extent));
        Assert.Equal(amount, range.FromPosition(range.ToPosition(amount)));
        Assert.True(double.IsFinite(range.FromPosition(-1)));
        Assert.True(double.IsFinite(range.FromPosition(1)));
    }

    [Theory]
    [InlineData(SignalType.Trigger)]
    [InlineData(SignalType.Gate)]
    [InlineData(SignalType.Clock)]
    public void EventDestinationsHaveNoRail(SignalType type) =>
        Assert.Null(ConnectionAmountSliderRange.Resolve(new(type, null), 1));

    [Fact]
    public void NonFiniteValuesAreRejectedAndPositionOverflowIsClampedOnlyForDisplay()
    {
        var semantics = new ConnectionAmountSemantics(SignalType.Audio, null);
        Assert.Throws<ArgumentOutOfRangeException>(() => ConnectionAmountSliderRange.Resolve(semantics, double.NaN));
        var range = ConnectionAmountSliderRange.Resolve(semantics, 0)!;
        Assert.Throws<ArgumentOutOfRangeException>(() => range.ToPosition(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => range.FromPosition(double.NaN));
        Assert.Equal(1d, range.ToPosition(10));
        Assert.Equal(-2d, range.FromPosition(-10));
        Assert.Equal(0.01, ConnectionAmountSliderRange.PositionStep);
    }
}
