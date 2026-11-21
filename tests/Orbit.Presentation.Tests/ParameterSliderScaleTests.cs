using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;
using Orbit.Presentation.Parameters;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class ParameterSliderScaleTests
{
    private static readonly ParameterDefinition Frequency = new OscillatorGenerator(Guid.NewGuid())
        .Parameters.Single(parameter => parameter.Definition.Key == "frequency").Definition;

    [Theory]
    [InlineData(0.01, 0)]
    [InlineData(40, 0.20)]
    [InlineData(1000, 0.85)]
    [InlineData(20000, 1)]
    public void OscillatorAnchorsMapExactlyInBothDirections(double value, double position)
    {
        Assert.Equal(position, ParameterSliderScale.ToPosition(Frequency, value));
        Assert.Equal(value, ParameterSliderScale.FromPosition(Frequency, position));
    }

    [Theory]
    [InlineData(0.02)]
    [InlineData(0.5)]
    [InlineData(39.9)]
    [InlineData(40.1)]
    [InlineData(100)]
    [InlineData(440)]
    [InlineData(999.9)]
    [InlineData(1000.1)]
    [InlineData(18000)]
    public void RoundTripPreservesPhysicalValues(double value)
    {
        var position = ParameterSliderScale.ToPosition(Frequency, value);
        Assert.InRange(Math.Abs(ParameterSliderScale.FromPosition(Frequency, position) / value - 1), 0, 1e-12);
    }

    [Fact]
    public void SegmentsAreContinuousAndStrictlyIncreasing()
    {
        var previous = Frequency.Minimum;
        for (var index = 1; index <= 10000; index++)
        {
            var position = index / 10000d;
            var value = ParameterSliderScale.FromPosition(Frequency, position);
            Assert.True(value > previous);
            Assert.Equal(position, ParameterSliderScale.ToPosition(Frequency, value), 12);
            previous = value;
        }
    }

    [Fact]
    public void ModulationBandAndLiveMarkerUseSameScaleAndClampVisualOverflows()
    {
        var start = ParameterSliderScale.ToPosition(Frequency, 340);
        var end = ParameterSliderScale.ToPosition(Frequency, 540);
        var thumb = ParameterSliderScale.ToPosition(Frequency, 440);
        Assert.InRange(start, 0.63, 0.64);
        Assert.InRange(thumb, 0.68, 0.69);
        Assert.InRange(end, 0.72, 0.73);
        Assert.True(thumb - start > end - thumb);
        Assert.Equal(0, ParameterSliderScale.ToPosition(Frequency, -100));
        Assert.Equal(1, ParameterSliderScale.ToPosition(Frequency, 25000));
        Assert.Equal(Frequency.Minimum, ParameterSliderScale.FromPosition(Frequency, -1));
        Assert.Equal(Frequency.Maximum, ParameterSliderScale.FromPosition(Frequency, 2));
    }

    [Fact]
    public void OtherParametersStayLinearAndTwoAnchorScalesAreReusable()
    {
        var linear = new ParameterDefinition("test", "Test", 0, -10, 10,
            ParameterUnit.Ratio, ParameterScale.Linear, false);
        Assert.Equal(0.75, ParameterSliderScale.ToPosition(linear, 5));
        Assert.Equal(5, ParameterSliderScale.FromPosition(linear, 0.75));
        var logarithmic = linear with { Minimum = 1, Maximum = 100, DefaultValue = 10,
            DisplayScale = new(new(0, 1), new(1, 100)) };
        Assert.Equal(0.5, ParameterSliderScale.ToPosition(logarithmic, 10), 12);
        Assert.Equal(10, ParameterSliderScale.FromPosition(logarithmic, 0.5), 12);
    }
}
