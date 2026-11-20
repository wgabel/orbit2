using System.Text.Json;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Models;
using Orbit.Audio.Compilation;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

// Produce a graph via ordinary commands, not hand-written Faust fixture code.
// The same LFO fans out to pitch and gain; offline tests verify both effects.
if (args.Length != 1)
    throw new ArgumentException("Pass an output directory for the generated probe.");
Directory.CreateDirectory(args[0]);
using var session = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(440, 0.5, 100));
var lfo = session.Snapshot.Nodes[0];
var oscillator = session.Snapshot.Nodes[1];
var gain = session.Snapshot.Nodes[2];
session.Execute(new ConnectNodesCommand(session.GraphId,
    new PortAddress(lfo.Id, lfo.Outputs.Single().Id),
    new PortAddress(gain.Id, gain.Parameters.Single().ModulationPortId!.Value), 0.05));
var program = FaustGraphCompiler.CreateBuiltIn().Compile(session.Snapshot);
var frequency = program.Controls.Single(control => control.Address == new ParameterAddress(oscillator.Id,
    oscillator.Parameters.Single(parameter => parameter.Definition.Key == "frequency").Id));
File.WriteAllText(Path.Combine(args[0], "program.dsp"), program.Source);
File.WriteAllText(Path.Combine(args[0], "controls.json"), JsonSerializer.Serialize(new
{
    values = program.Controls.ToDictionary(control => control.Name, control => control.InitialValue),
    frequency = frequency.Name
}));
