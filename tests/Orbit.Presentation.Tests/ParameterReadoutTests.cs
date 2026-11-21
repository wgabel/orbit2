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
