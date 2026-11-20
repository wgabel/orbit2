using System.Globalization;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Models;
using Orbit.Audio.Compilation;
using Orbit.Audio.Analysis;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Domain.Timing;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>Tests the graph-to-source boundary without requiring a browser or audio device.</summary>
public sealed class FaustGraphCompilerTests
{
    private readonly FaustGraphCompiler compiler = FaustGraphCompiler.CreateBuiltIn();

    [Fact]
    public void FilterModesPreserveRoutingAndContinuousControls()
    {
        using var session = new PatchSession(BuiltInNodes.CreateRegistry());
        var oscillator = Add(session, OscillatorGenerator.NodeType);
        var filter = Add(session, FilterProcessor.NodeType);
        var master = Add(session, MasterOutput.NodeType);
        Connect(session, oscillator, filter, filter.Inputs.Single(p => p.Key == "audio-in").Id, 0.1);
        var cutoff = filter.Parameters.Single(p => p.Definition.Key == "cutoff");
        var resonance = filter.Parameters.Single(p => p.Definition.Key == "resonance");
        var mode = filter.Parameters.Single(p => p.Definition.Key == "mode");
        Connect(session, oscillator, filter, cutoff.ModulationPortId!.Value, 100);
        Connect(session, oscillator, filter, resonance.ModulationPortId!.Value, 0.2);
        Connect(session, filter, master, master.Inputs.Single().Id, 1);
        var initial = compiler.Compile(session.Snapshot);
        Assert.NotNull(Control(initial, filter, "cutoff").MeterName);
        Assert.NotNull(Control(initial, filter, "resonance").MeterName);
        Assert.Null(mode.ModulationPortId);
        session.Execute(new SetParameterValueCommand(session.GraphId, new ParameterAddress(filter.Id, cutoff.Id), 800));
        Assert.Equal(initial.Source, compiler.Compile(session.Snapshot).Source);
        var edges = session.Snapshot.Connections.ToArray();
        var sources = new HashSet<string>();
        foreach (var choice in Enum.GetValues<FilterMode>())
        {
            session.Execute(new SetParameterValueCommand(session.GraphId, new ParameterAddress(filter.Id, mode.Id), (int)choice));
            var program = compiler.Compile(session.Snapshot);
            Assert.True(sources.Add(program.Source));
            Assert.Equal(Control(initial, filter, "cutoff").Name, Control(program, filter, "cutoff").Name);
            Assert.Equal(edges, session.Snapshot.Connections.ToArray());
        }
    }

    [Fact]
    public void NoiseColorRecompilesButAmplitudeAndConnectionsStayStable()
    {
        using var session = new PatchSession(BuiltInNodes.CreateRegistry());
        var noise = Add(session, NoiseGenerator.NodeType);
        var master = Add(session, MasterOutput.NodeType);
        Connect(session, noise, master, master.Inputs.Single().Id, 1);
        var color = noise.Parameters.Single(p => p.Definition.Key == "color");
        var amplitude = noise.Parameters.Single(p => p.Definition.Key == "amplitude");
        var first = compiler.Compile(session.Snapshot);
        Assert.Single(first.Controls);
        Assert.False(first.RequiresTransport);
        Assert.Null(color.ModulationPortId);
        Assert.NotNull(amplitude.ModulationPortId);
        session.Execute(new SetParameterValueCommand(session.GraphId, new ParameterAddress(noise.Id, amplitude.Id), 0.5));
        Assert.Equal(first.Source, compiler.Compile(session.Snapshot).Source);
        var connections = session.Snapshot.Connections.ToArray();
        session.Execute(new SetParameterValueCommand(session.GraphId, new ParameterAddress(noise.Id, color.Id), (int)NoiseColor.Pink));
        var pink = compiler.Compile(session.Snapshot);
        Assert.NotEqual(first.Source, pink.Source);
        Assert.Equal(first.Controls.Single().Name, pink.Controls.Single().Name);
        Assert.Equal(connections, session.Snapshot.Connections.ToArray());
    }

    [Fact]
    public void CompilesSeedWithStableControlsAndAudioRateModulation()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(440, 0.5, 100));
        var program = compiler.Compile(session.Snapshot);
        var lfo = session.Snapshot.Nodes[0];
        var oscillator = session.Snapshot.Nodes[1];
        Assert.Equal(session.GraphId, program.GraphId);
        Assert.Equal(session.Snapshot.Revision, program.Revision);
        Assert.Equal(7, program.Controls.Count);
        Assert.Contains($"n_{lfo.Id:N} * 100.0", program.Source);
        Assert.Contains($"n_{oscillator.Id:N} * 1.0", program.Source);
        Assert.Contains("os.lf_sawpos(min(0.49 * ma.SR", program.Source);
        Assert.False(program.RequiresTransport);
        Assert.Contains(" : si.smoo;", program.Source);
        Assert.Contains("process = ", program.Source);
        Assert.Equal(0.5, Control(program, lfo, "frequency").InitialValue);
        Assert.Equal(440, Control(program, oscillator, "frequency").InitialValue);
        var meter = Control(program, oscillator, "frequency").MeterName;
        Assert.NotNull(meter);
        Assert.Contains($"hbargraph(\"{meter}\"", program.Source);
        Assert.Null(Control(program, lfo, "frequency").MeterName);
    }

    [Fact]
    public void SynchronizationSuspendsFrequencyRoutesAndRestoresThemWithoutRemovingConnections()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(440, 0.5, 100));
        var free = session.Snapshot;
        var carrier = free.Nodes[1];
        var frequency = carrier.Parameters.Single(p => p.Definition.Key == "frequency");
        var division = carrier.Parameters.Single(p => p.Definition.Key == "division");
        var timing = carrier.Parameters.Single(p => p.Definition.Key == "timing");
        var frequencyAddress = new ParameterAddress(carrier.Id, frequency.Id);
        var timingAddress = new ParameterAddress(carrier.Id, timing.Id);
        var divisionAddress = new ParameterAddress(carrier.Id, division.Id);
        var estimator = new GraphModulationRangeEstimator(BuiltInFaustEmitters.Create());
        var first = compiler.Compile(free);
        Assert.True(carrier.IsInputActive(frequency.ModulationPortId!.Value));
        Assert.False(carrier.IsParameterActive(division));
        Assert.NotNull(estimator.Estimate(free)[frequencyAddress]);
        session.Execute(new SetParameterValueCommand(session.GraphId, timingAddress, (int)OscillatorTimingMode.Synchronized));
        var synchronized = session.Snapshot;
        var node = synchronized.GetNode(carrier.Id);
        Assert.False(node.IsInputActive(frequency.ModulationPortId.Value));
        Assert.True(node.IsParameterActive(division));
        Assert.Equal(free.Connections.ToArray(), synchronized.Connections.ToArray());
        var locked = compiler.Compile(synchronized);
        Assert.True(locked.RequiresTransport);
        Assert.Contains("process(orbit_beat)", locked.Source);
        Assert.DoesNotContain(locked.Controls, c => c.Address == frequencyAddress || c.Address.NodeId == free.Nodes[0].Id);
        Assert.Contains(locked.Controls, c => c.Address == divisionAddress);
        Assert.False(estimator.Estimate(synchronized).ContainsKey(frequencyAddress));
        session.Execute(new SetParameterValueCommand(session.GraphId, divisionAddress, (int)MusicalDivision.HalfNote));
        Assert.Equal(locked.Source, compiler.Compile(session.Snapshot).Source);
        session.Execute(new SetParameterValueCommand(session.GraphId, timingAddress, (int)OscillatorTimingMode.Free));
        var restored = compiler.Compile(session.Snapshot);
        Assert.Equal(first.Source, restored.Source);
        Assert.False(restored.RequiresTransport);
        Assert.Equal(new ParameterRange(340, 540), estimator.Estimate(session.Snapshot)[frequencyAddress]!.Value);
    }

    [Fact]
    public void BaseEditsKeepSourceAndControlNamesStable()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var oscillator = session.Snapshot.Nodes[1];
        var first = compiler.Compile(session.Snapshot);
        var control = Control(first, oscillator, "frequency");
        session.Execute(new SetParameterValueCommand(session.GraphId, control.Address, 220));
        var next = compiler.Compile(session.Snapshot);
        Assert.Equal(first.Source, next.Source);
        Assert.Equal(control.Name, Control(next, oscillator, "frequency").Name);
        Assert.Equal(control.MeterName, Control(next, oscillator, "frequency").MeterName);
        Assert.Equal(220, Control(next, oscillator, "frequency").InitialValue);
        Assert.Equal(440, control.InitialValue);
        Assert.True(next.Revision > first.Revision);
    }

    [Fact]
    public void FanOutAndMultipleModulatorsResolveWithoutDuplicatingNodeDefinitions()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var lfo = session.Snapshot.Nodes[0];
        var oscillator = session.Snapshot.Nodes[1];
        var gain = session.Snapshot.Nodes[2];
        var secondLfo = Add(session, OscillatorGenerator.NodeType);
        var gainPort = gain.Parameters.Single().ModulationPortId!.Value;
        var frequencyPort = oscillator.Parameters.Single(p => p.Definition.Key == "frequency").ModulationPortId!.Value;
        Connect(session, lfo, gain, gainPort, 0.05);
        Connect(session, secondLfo, oscillator, frequencyPort, -12.5);
        var program = compiler.Compile(session.Snapshot);
        Assert.Single(program.Source.Split('\n'), line => line.StartsWith($"n_{lfo.Id:N} = "));
        Assert.Contains($"n_{lfo.Id:N} * 0.05", program.Source);
        Assert.Contains($"n_{secondLfo.Id:N} * -12.5", program.Source);
        Assert.Equal(10, program.Controls.Count);
    }

    [Fact]
    public void UnreachableNodesDoNotChangeCompiledSource()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var before = compiler.Compile(session.Snapshot);
        Add(session, OscillatorGenerator.NodeType);
        var after = compiler.Compile(session.Snapshot);
        Assert.Equal(before.Source, after.Source);
        Assert.Equal(before.Controls.ToArray(), after.Controls.ToArray());
    }

    [Fact]
    public void DisconnectedOutputCompilesToSilence()
    {
        using var session = new PatchSession(BuiltInNodes.CreateRegistry());
        Add(session, MasterOutput.NodeType);
        Add(session, OscillatorGenerator.NodeType);
        var program = compiler.Compile(session.Snapshot);
        Assert.Contains("process = 0.0;", program.Source);
        Assert.Empty(program.Controls);
    }

    [Fact]
    public void RequiresExactlyOneMasterOutput()
    {
        using var session = new PatchSession(BuiltInNodes.CreateRegistry());
        Assert.Throws<FaustCompilationException>(() => compiler.Compile(session.Snapshot));
        Add(session, MasterOutput.NodeType);
        Add(session, MasterOutput.NodeType);
        Assert.Throws<FaustCompilationException>(() => compiler.Compile(session.Snapshot));
    }

    [Fact]
    public void UsesInvariantLiteralsAndDoesNotEmitDisplayNames()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(440, 0.5, 25.5));
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            var graph = session.Snapshot;
            var renamed = graph with { Nodes = graph.Nodes.Select(node => node with { Name = "user \"label\";" }).ToArray() };
            var program = compiler.Compile(renamed);
            Assert.Contains("25.5", program.Source);
            Assert.DoesNotContain("25,5", program.Source);
            Assert.DoesNotContain("user", program.Source);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void RejectsCyclesInUntrustedSnapshots()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var graph = session.Snapshot;
        var lfo = graph.Nodes[0];
        var oscillator = graph.Nodes[1];
        var reverse = new SignalConnection(Guid.NewGuid(),
            new PortAddress(oscillator.Id, oscillator.Outputs.Single().Id),
            new PortAddress(lfo.Id, lfo.Inputs[0].Id));
        Assert.Throws<FaustCompilationException>(() => compiler.Compile(
            graph with { Connections = graph.Connections.Append(reverse).ToArray() }));
    }

    [Fact]
    public void RejectsUnsupportedConnectedNodeTypes()
    {
        using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings());
        var graph = session.Snapshot;
        var changed = graph with { Nodes = graph.Nodes.Select(node => node.TypeKey == GainProcessor.NodeType
            ? node with { TypeKey = "processor.unknown" } : node).ToArray() };
        Assert.Throws<FaustCompilationException>(() => compiler.Compile(changed));
    }

    private static FaustParameterControl Control(FaustGraphProgram program, GraphNodeSnapshot node, string key) =>
        program.Controls.Single(control => control.Address == new ParameterAddress(node.Id,
            node.Parameters.Single(parameter => parameter.Definition.Key == key).Id));

    private static GraphNodeSnapshot Add(IPatchSession session, string type) =>
        session.Execute(new AddNodeCommand(session.GraphId, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;

    private static void Connect(IPatchSession session, GraphNodeSnapshot source, GraphNodeSnapshot destination,
        Guid port, double amount) => session.Execute(new ConnectNodesCommand(session.GraphId,
            new PortAddress(source.Id, source.Outputs.Single().Id), new PortAddress(destination.Id, port), amount));
}
