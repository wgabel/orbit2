using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Timing;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class ParameterReadoutTests
{
    [Fact]
    public void CycleTimingReflectsRateAndSquarePulseWidthWithoutEditingGraph()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        void Set(string key, double value) => session.Execute(new SetParameterValueCommand(session.GraphId,
            new ParameterAddress(node.Id, node.Parameters.Single(p => p.Definition.Key == key).Id), value));
        Set("frequency", 0.2);
        Set("waveform", (int)OscillatorWaveform.Square);
        Set(OscillatorGenerator.PulseWidthKey, 0.2);
        var current = session.Snapshot.GetNode(node.Id);
        var timing = CycleTiming.ForNode(current, 120)!;
        Assert.Equal(5, timing.Seconds);
        Assert.Equal(1, timing.PositiveSeconds);
        Assert.Equal(4, timing.NegativeSeconds);
        Assert.Equal("5 s", CycleTiming.FormatDuration(timing.Seconds));
        Assert.Equal("2.5 ms", CycleTiming.FormatDuration(0.0025));
        Assert.Equal("50 µs", CycleTiming.FormatDuration(0.00005));
        Assert.True(NodeVisualizationCapabilities.SupportsCycle(current, current.Outputs.Single().Id));
        Assert.False(NodeVisualizationCapabilities.SupportsCycle(current, current.Inputs.First().Id));
        Set("timing", (int)OscillatorTimingMode.Synchronized);
        Set("division", (int)MusicalDivision.QuarterNote);
        Assert.Equal(0.5, CycleTiming.ForNode(session.Snapshot.GetNode(node.Id), 120)!.Seconds);
        Set("waveform", (int)OscillatorWaveform.Sine);
        Assert.Equal(0.25, CycleTiming.ForNode(session.Snapshot.GetNode(node.Id), 120)!.PositiveSeconds);
    }
    [Theory]
    [InlineData(120, MusicalDivision.QuarterNote, 2)]
    [InlineData(90, MusicalDivision.WholeNote, 0.375)]
    [InlineData(120, MusicalDivision.EighthNote, 4)]
    [InlineData(120, MusicalDivision.QuarterNoteTriplet, 3)]
    [InlineData(20, MusicalDivision.WholeNote, 1d / 12)]
    public void SynchronizedOscillatorDisplaysDerivedFrequency(double tempo, MusicalDivision division, double expected)
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var timing = node.Parameters.Single(p => p.Definition.Key == "timing");
        var rate = node.Parameters.Single(p => p.Definition.Key == "division");
        Assert.Null(ParameterReadout.ForChoice(node, rate, tempo));
        session.Execute(new SetParameterValueCommand(session.GraphId, new ParameterAddress(node.Id, timing.Id), (int)OscillatorTimingMode.Synchronized));
        session.Execute(new SetParameterValueCommand(session.GraphId, new ParameterAddress(node.Id, rate.Id), (int)division));
        var synchronized = session.Snapshot.GetNode(node.Id);
        var committedRate = synchronized.Parameters.Single(p => p.Id == rate.Id);
        var revision = session.Snapshot.Revision;
        var readout = Assert.IsType<ParameterReadout>(ParameterReadout.ForChoice(synchronized, committedRate, tempo));
        Assert.Equal(ParameterUnit.Hertz, readout.Unit);
        Assert.Equal(expected, readout.Value, 10);
        Assert.Equal(expected * 2, ParameterReadout.ForChoice(synchronized, committedRate, tempo * 2)!.Value, 10);
        Assert.Equal(revision, session.Snapshot.Revision);
        Assert.Null(ParameterReadout.ForChoice(synchronized, timing, tempo));
        session.Execute(new SetParameterValueCommand(session.GraphId, new ParameterAddress(node.Id, timing.Id), (int)OscillatorTimingMode.Free));
        Assert.Null(ParameterReadout.ForChoice(session.Snapshot.GetNode(node.Id), committedRate, tempo));
    }

    [Fact]
    public void DelayDivisionDoesNotDisplayOscillatorFrequency()
    {
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = session.Execute(new AddNodeCommand(session.GraphId, DelayProcessor.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var timing = node.Parameters.Single(p => p.Definition.Key == "timing");
        session.Execute(new SetParameterValueCommand(session.GraphId, new ParameterAddress(node.Id, timing.Id), (int)DelayTimingMode.Synchronized));
        var synchronized = session.Snapshot.GetNode(node.Id);
        var rate = synchronized.Parameters.Single(p => p.Definition.Key == "division");
        Assert.True(synchronized.IsParameterActive(rate));
        Assert.Null(ParameterReadout.ForChoice(synchronized, rate, 120));
    }
}
