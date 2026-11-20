using System.Text.Json;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Audio.Compilation;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

// Produce a graph via ordinary commands, not hand-written Faust fixture code.
// A parent LFO fans out through named rack inputs to pitch and gain inside a drone.
// Offline tests verify the complete compiled path through the child output rack.
if (args.Length != 1)
    throw new ArgumentException("Pass an output directory for the generated probe.");
Directory.CreateDirectory(args[0]);
var registry = BuiltInNodes.CreateRegistry();
registry.Register(CompositeNode.NodeType, id => new CompositeNode(id, "Drone",
    [new ExposedPort(Guid.NewGuid(), "Pitch", SignalType.Modulation),
     new ExposedPort(Guid.NewGuid(), "Gain", SignalType.Modulation)],
    [new ExposedPort(Guid.NewGuid(), "Audio", SignalType.Audio)]));
using var session = new PatchSession(registry);
var root = session.GraphId;
var lfo = Add(root, SineGenerator.NodeType);
var drone = Add(root, CompositeNode.NodeType);
var master = Add(root, MasterOutput.NodeType);
var composite = drone.Composite!;
var child = composite.Graph.Id;
var oscillator = Add(child, SineGenerator.NodeType);
var gain = Add(child, GainProcessor.NodeType);
session.Execute(new SetParameterValueCommand(root, new ParameterAddress(lfo.Id, lfo.Parameters[0].Id), 0.5));
session.Execute(new SetParameterValueCommand(child, new ParameterAddress(gain.Id, gain.Parameters[0].Id), 0.1));
Connect(root, lfo.Id, lfo.Outputs[0].Id, drone.Id, drone.Inputs[0].Id, 100);
Connect(root, lfo.Id, lfo.Outputs[0].Id, drone.Id, drone.Inputs[1].Id, 0.05);
Connect(child, composite.InputRackId, drone.Inputs[0].Id, oscillator.Id, oscillator.Parameters[0].ModulationPortId!.Value);
Connect(child, composite.InputRackId, drone.Inputs[1].Id, gain.Id, gain.Parameters[0].ModulationPortId!.Value);
Connect(child, oscillator.Id, oscillator.Outputs[0].Id, gain.Id, gain.Inputs[0].Id);
Connect(child, gain.Id, gain.Outputs[0].Id, composite.OutputRackId, drone.Outputs[0].Id);
Connect(root, drone.Id, drone.Outputs[0].Id, master.Id, master.Inputs[0].Id);
var program = FaustGraphCompiler.CreateBuiltIn().Compile(session.Snapshot);
var frequency = program.Controls.Single(control => control.Address == new ParameterAddress(oscillator.Id,
    oscillator.Parameters.Single(parameter => parameter.Definition.Key == "frequency").Id));
var amplitude = program.Controls.Single(control => control.Address == new ParameterAddress(oscillator.Id,
    oscillator.Parameters.Single(parameter => parameter.Definition.Key == "amplitude").Id));
File.WriteAllText(Path.Combine(args[0], "program.dsp"), program.Source);
File.WriteAllText(Path.Combine(args[0], "controls.json"), JsonSerializer.Serialize(new
{
    values = program.Controls.ToDictionary(control => control.Name, control => control.InitialValue),
    frequency = frequency.Name,
    frequencyMeter = frequency.MeterName,
    amplitude = amplitude.Name
}));

GraphNodeSnapshot Add(Guid graph, string type) =>
    session.Execute(new AddNodeCommand(graph, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;

void Connect(Guid graph, Guid source, Guid output, Guid destination, Guid input, double amount = 1) =>
    session.Execute(new ConnectNodesCommand(graph, new PortAddress(source, output), new PortAddress(destination, input), amount));
