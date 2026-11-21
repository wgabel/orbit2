using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Models;
using Orbit.Audio.Analysis;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class ProcessorResponseTests
{
    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    public void FilterModesHaveExactCenterAndEndpointResponses(int rate)
    {
        foreach (var kind in Enum.GetValues<ProcessorResponseKind>().Where(kind => kind != ProcessorResponseKind.Peaking))
        {
            var settings = new ProcessorResponseParameters(kind, 1000, 4);
            var center = StateVariableResponse.Magnitude(settings, rate, 1000);
            Assert.Equal(kind == ProcessorResponseKind.Notch ? 0 : 4, center, 10);
            Assert.Equal(kind is ProcessorResponseKind.LowPass or ProcessorResponseKind.Notch ? 1 : 0,
                StateVariableResponse.Magnitude(settings, rate, 0), 10);
            Assert.Equal(kind is ProcessorResponseKind.HighPass or ProcessorResponseKind.Notch ? 1 : 0,
                StateVariableResponse.Magnitude(settings, rate, rate / 2.0), 10);
        }
    }

    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    public void EqBoostAndCutAreReciprocalWithExactCenterGain(int rate)
    {
        var boost = new ProcessorResponseParameters(ProcessorResponseKind.Peaking, 1000, 8, 12);
        var cut = boost with { GainDb = -12 };
        Assert.Equal(12, StateVariableResponse.Decibels(boost, rate, 1000), 10);
        Assert.Equal(-12, StateVariableResponse.Decibels(cut, rate, 1000), 10);
        foreach (var frequency in new[] { 0.0, 20, 800, 1000, 1300, 12000, rate / 2.0 })
        {
            Assert.Equal(0, StateVariableResponse.Decibels(boost, rate, frequency) + StateVariableResponse.Decibels(cut, rate, frequency), 10);
            Assert.Equal(0, StateVariableResponse.Decibels(boost with { GainDb = 0 }, rate, frequency), 10);
        }
    }

    [Fact]
    public void LogarithmicCurveIncludesTheClampedCenterAndFiniteNotchFloor()
    {
        var settings = new ProcessorResponseParameters(ProcessorResponseKind.Notch, 20000, 8);
        var analyzer = new ProcessorResponseAnalyzer();
        var curve = analyzer.Calculate(settings, 44100);
        Assert.InRange(curve.Count, 161, 162);
        Assert.Equal(20, curve[0].Frequency);
        Assert.Equal(22050, curve[^1].Frequency);
        var center = StateVariableResponse.EffectiveCenter(settings, 44100);
        Assert.Equal(19845, center, 8);
        Assert.Equal(-120, curve.Single(point => point.Frequency == center).Decibels);
        Assert.All(curve, point => Assert.True(double.IsFinite(point.Decibels)));
    }

    [Fact]
    public void ProjectionRequiresCompleteLiveModulationAndPreservesConfiguredValues()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = session.Execute(new AddNodeCommand(session.GraphId, ParametricEqProcessor.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        ParameterAddress Address(string key) => new(node.Id, node.Parameters.Single(parameter => parameter.Definition.Key == key).Id);
        var ranges = new Dictionary<ParameterAddress, ParameterRange?> { [Address("frequency")] = null, [Address("gain")] = null };
        var effective = new Dictionary<ParameterAddress, double> { [Address("frequency")] = 1500 };
        var analyzer = new ProcessorResponseAnalyzer();
        var before = session.Snapshot;
        var missing = analyzer.Resolve(node, ranges, effective)!;
        Assert.Null(missing.Effective);
        effective[Address("gain")] = 12;
        var complete = analyzer.Resolve(node, ranges, effective)!;
        Assert.Equal(new ProcessorResponseParameters(ProcessorResponseKind.Peaking, 1000, 1, 0), complete.Configured);
        Assert.Equal(new ProcessorResponseParameters(ProcessorResponseKind.Peaking, 1500, 1, 12), complete.Effective!.Value);
        Assert.Same(before, session.Snapshot);
        effective[Address("gain")] = double.NaN;
        Assert.Null(analyzer.Resolve(node, ranges, effective)!.Effective);
    }
}
