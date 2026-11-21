using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Audio.Analysis;
using Orbit.Audio.Compilation;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class EnvelopeTests
{
    private static ParameterAddress Address(GraphNodeSnapshot node, string key) => new(node.Id, node.Parameters.Single(p => p.Definition.Key == key).Id);

    [Fact]
    public void TriggerAcceptsAudioAndModulationButOnlyOneSource()
    {
        var graph = new SignalGraph();
        var envelope = new EnvelopeGenerator(Guid.NewGuid());
        var osc = new OscillatorGenerator(Guid.NewGuid());
        var pitch = new PitchControlGenerator(Guid.NewGuid());
        graph.AddNode(envelope); graph.AddNode(osc); graph.AddNode(pitch);
        var trigger = envelope.Inputs.Single(p => p.Key == EnvelopeGenerator.TriggerPortKey);
        Assert.Equal(FanInPolicy.Single, trigger.FanIn);
        Assert.Equal(SignalType.Modulation, trigger.SignalType);
        Assert.DoesNotContain(envelope.Parameters, p => p.ModulationPortId == trigger.Id);
        Assert.All(envelope.Parameters.Where(p => p.Definition.Key != EnvelopeGenerator.TriggerSourceKey), p => { Assert.NotNull(p.ModulationPortId); Assert.False(p.Definition.RequiresRecompile); });
        var input = new PortAddress(envelope.Id, trigger.Id);
        var edge = graph.Connect(new PortAddress(osc.Id, osc.Outputs.Single().Id), input);
        Assert.Equal(1, edge.Amount);
        Assert.Throws<GraphValidationException>(() => graph.Connect(new PortAddress(pitch.Id, pitch.Outputs.Single().Id), input));
        graph.RemoveConnection(edge.Id);
        graph.Connect(new PortAddress(pitch.Id, pitch.Outputs.Single().Id), input, 0.5);
        Assert.Equal(SignalType.Modulation, envelope.Outputs.Single().SignalType);
    }

    [Fact]
    public void NestedImpactPersistsIndependentEnvelopesAndPredictsDestinationRanges()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var root = session.GraphId;
        var impact = NestedFaustPatch.Add(session, root, ImpactTemplate.PresetKey);
        var separateImpact = NestedFaustPatch.Add(session, root, ImpactTemplate.PresetKey);
        var master = NestedFaustPatch.Add(session, root, MasterOutput.NodeType);
        session.Execute(new ConnectNodesCommand(root, new PortAddress(impact.Id, impact.Outputs.Single().Id), new PortAddress(master.Id, master.Inputs.Single().Id)));
        var child = impact.Composite!.Graph;
        var envelope = child.Nodes.Where(n => n.TypeKey == EnvelopeGenerator.NodeType).ToArray();
        Assert.Equal(2, envelope.Length);
        Assert.Empty(child.Nodes.Select(n => n.Id).Intersect(separateImpact.Composite!.Graph.Nodes.Select(n => n.Id)));
        var voice = child.Nodes.Single(n => n.TypeKey == OscillatorGenerator.NodeType && n.Parameters.Single(p => p.Definition.Key == "frequency").BaseValue == 55);
        var estimator = new GraphModulationRangeEstimator(BuiltInFaustEmitters.Create());
        var ranges = estimator.Estimate(session.Snapshot, child.Id);
        Assert.Equal(new ParameterRange(55, 355), ranges[Address(voice, "frequency")]);
        Assert.Equal(new ParameterRange(0, 0.8), ranges[Address(voice, "amplitude")]);
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var before = compiler.Compile(session.Snapshot);
        var attack = Address(envelope[0], EnvelopeGenerator.AttackKey);
        session.Execute(new SetParameterValueCommand(child.Id, attack, 77));
        var edited = compiler.Compile(session.Snapshot);
        Assert.Equal(before.Source, edited.Source);
        Assert.Equal(77, edited.Controls.Single(c => c.Address == attack).InitialValue);
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, root, []);
        using var restored = serializer.Restore(serializer.Deserialize(serializer.Serialize(document)), registry);
        Assert.Equal(edited.Source, compiler.Compile(restored.Snapshot).Source);
        Assert.Equal(session.Snapshot.GetGraph(child.Id).Connections, restored.Snapshot.GetGraph(child.Id).Connections);
        Assert.Equal(77, compiler.Compile(restored.Snapshot).Controls.Single(c => c.Address == attack).InitialValue);
    }
}
