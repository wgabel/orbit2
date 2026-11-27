using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Audio.Compilation;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class MacroDependencyProjectionTests
{
    [Fact]
    public void ConnectedNestedMacroProjectsOneWeightedRouteAndActiveTargetsOnly()
    {
        var registry = BuiltInNodes.CreateRegistry();
        registry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Macro module",
            [new ExposedPort(Guid.NewGuid(), "Modulation", SignalType.Modulation)],
            [new ExposedPort(Guid.NewGuid(), "Audio", SignalType.Audio)]));
        using var session = new PatchSession(registry);
        var root = session.GraphId;
        var outer = Add(root, CompositeNode.NodeType);
        var middle = outer.Composite!.Graph.Id;
        var inner = Add(middle, CompositeNode.NodeType);
        var child = inner.Composite!.Graph.Id;
        var lfo = Add(middle, OscillatorGenerator.NodeType);
        var osc = Add(child, OscillatorGenerator.NodeType);
        var master = Add(root, MasterOutput.NodeType);
        Wire(child, osc.Id, osc.Outputs[0].Id, inner.Composite.OutputRackId, inner.Outputs[0].Id);
        Wire(middle, inner.Id, inner.Outputs[0].Id, outer.Composite.OutputRackId, outer.Outputs[0].Id);
        Wire(root, outer.Id, outer.Outputs[0].Id, master.Id, master.Inputs[0].Id);
        var frequency = Address("frequency");
        var amplitude = Address("amplitude");
        var width = Address(OscillatorGenerator.PulseWidthKey);
        var id = Guid.NewGuid();
        var mapping = new CompositeMacroMapping([new(frequency, 220, 440, 2),
            new(amplitude, 0.6, 0.2), new(width, 0.2, 0.4)]);
        void SetMacro(Guid? input) => session.Execute(new ReplaceCompositeMacrosCommand(middle, inner.Id,
            [new(id, "Size", 0.5, 0.5, mapping, input)]));
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        SetMacro(null);
        var staticSource = compiler.Compile(session.Snapshot).Source;
        SetMacro(inner.Inputs[0].Id);
        Assert.Equal(staticSource, compiler.Compile(session.Snapshot).Source);
        Assert.Empty(GraphFlattener.Flatten(session.Snapshot, true).MacroTargets);
        var unconnected = compiler.Compile(session.Snapshot, enableMacroModulation: true);
        Assert.Equal(staticSource, unconnected.Source);
        Assert.Empty(unconnected.MacroControls);

        Wire(middle, lfo.Id, lfo.Outputs[0].Id, inner.Id, inner.Inputs[0].Id, -0.4);
        var authored = session.Snapshot;
        var ordinary = GraphFlattener.Flatten(authored);
        var lowered = GraphFlattener.Flatten(authored, true);
        Assert.Empty(ordinary.MacroTargets);
        Assert.Equal(ordinary.Graph.Connections.ToArray(), lowered.Graph.Connections.ToArray());
        Assert.Equal(2, lowered.MacroTargets.Count);
        var flattenedOsc = lowered.Origins.Single(p => p.Value.GraphId == child && p.Value.NodeId == osc.Id).Key;
        var flattenedLfo = lowered.Origins.Single(p => p.Value.GraphId == middle && p.Value.NodeId == lfo.Id).Key;
        var mappedFrequency = lowered.MacroTargets[new(flattenedOsc, frequency.ParameterId)];
        var mappedAmplitude = lowered.MacroTargets[new(flattenedOsc, amplitude.ParameterId)];
        Assert.Same(mappedFrequency.Source, mappedAmplitude.Source);
        Assert.Equal(middle, mappedFrequency.Source.GraphId);
        Assert.Equal(inner.Id, mappedFrequency.Source.CompositeId);
        Assert.Equal(id, mappedFrequency.Source.MacroId);
        Assert.Equal(mapping.Bindings[0], mappedFrequency.Binding);
        Assert.DoesNotContain(new ParameterAddress(flattenedOsc, width.ParameterId), lowered.MacroTargets.Keys);
        var route = mappedFrequency.Source.Signal;
        Assert.Equal(lowered.Endpoints[(child, new(inner.Composite.InputRackId, inner.Inputs[0].Id))], route);
        var weighted = Assert.Single(lowered.Graph.Connections.Where(edge => edge.Destination.NodeId == route.NodeId));
        Assert.Equal(-0.4, weighted.Amount);
        Assert.Equal(flattenedLfo, weighted.Source.NodeId);
        Assert.Equal((route.NodeId, flattenedOsc), Assert.Single(lowered.MacroDependencies));

        var program = compiler.Compile(authored, enableMacroModulation: true);
        var control = Assert.Single(program.MacroControls);
        Assert.Equal(new FaustMacroAddress(middle, inner.Id, id), control.Address);
        Assert.Equal(0.5, control.InitialValue);
        Assert.Equal(0.5, program.InitialControlValues()[control.Name]);
        Assert.Contains($"hslider(\"{control.Name}\"", program.Source);
        Assert.Contains($"pow({control.Name}_u, ", program.Source);
        Assert.Contains($"hbargraph(\"{control.MeterName}\"", program.Source);
        Assert.Contains($"mapped_{flattenedOsc:N}_{frequency.ParameterId:N}", program.Source);
        Assert.Contains($"mapped_{flattenedOsc:N}_{amplitude.ParameterId:N}", program.Source);
        Assert.DoesNotContain($"mapped_{flattenedOsc:N}_{width.ParameterId:N}", program.Source);
        Assert.DoesNotContain(program.Controls, c => c.GraphId == child && (c.Address == frequency || c.Address == amplitude));
        Assert.Empty(compiler.Compile(authored).MacroControls);

        var oldOrder = GraphDependencyOrder.Get(ordinary.Graph, master.Id, out _);
        Assert.DoesNotContain(oldOrder, node => node.Id == flattenedLfo);
        var ordered = GraphDependencyOrder.Get(lowered.Graph, master.Id, out _, lowered.MacroDependencies).Select(n => n.Id).ToList();
        Assert.True(ordered.IndexOf(flattenedLfo) >= 0);
        Assert.True(ordered.IndexOf(flattenedLfo) < ordered.IndexOf(route.NodeId));
        Assert.True(ordered.IndexOf(route.NodeId) < ordered.IndexOf(flattenedOsc));
        Assert.Throws<FaustCompilationException>(() => GraphDependencyOrder.Get(lowered.Graph, master.Id, out _,
            lowered.MacroDependencies.Append((flattenedOsc, route.NodeId))));

        session.Execute(new SetCompositeMacroValueCommand(middle, inner.Id, id, 0.75));
        var revised = GraphFlattener.Flatten(session.Snapshot, true);
        Assert.Equal(mappedFrequency.Source with { BaseValue = 0.75 }, revised.MacroTargets[new(flattenedOsc, frequency.ParameterId)].Source);
        Assert.Equal(0.5, mappedFrequency.Source.BaseValue);
        var updated = compiler.Compile(session.Snapshot, enableMacroModulation: true);
        Assert.Equal(program.Source, updated.Source);
        Assert.Equal(0.75, Assert.Single(updated.MacroControls).InitialValue);
        session.Execute(new SetParameterValueCommand(child, Address("waveform"), (int)OscillatorWaveform.Square));
        var active = GraphFlattener.Flatten(session.Snapshot, true);
        Assert.Equal(3, active.MacroTargets.Count);
        Assert.Single(active.MacroDependencies);
        var square = compiler.Compile(session.Snapshot, enableMacroModulation: true);
        Assert.Contains($"mapped_{flattenedOsc:N}_{width.ParameterId:N}", square.Source);
        Assert.Single(square.MacroControls);
        var edgeId = session.Snapshot.GetGraph(middle).Connections.Single(e => e.Destination.NodeId == inner.Id).Id;
        session.Execute(new SetConnectionAmountCommand(middle, edgeId, 0));
        Assert.Equal(3, GraphFlattener.Flatten(session.Snapshot, true).MacroTargets.Count);
        SetMacro(null);
        Assert.Empty(GraphFlattener.Flatten(session.Snapshot, true).MacroTargets);

        GraphNodeSnapshot Add(Guid graph, string type) => session.Execute(new AddNodeCommand(graph, type))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        void Wire(Guid graph, Guid from, Guid output, Guid to, Guid input, double amount = 1) =>
            session.Execute(new ConnectNodesCommand(graph, new(from, output), new(to, input), amount));
        ParameterAddress Address(string key) => new(osc.Id, osc.Parameters.Single(p => p.Definition.Key == key).Id);
    }
}
