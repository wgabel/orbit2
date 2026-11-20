using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs;

/// <summary>Starter composite factory. Each instance gets its own graph, sockets,
/// parameter identities and editable internal signal routing.</summary>
public static class DroneTemplate
{
    public static CompositeNode Create(Guid id)
    {
        var pitch = new ExposedPort(Guid.NewGuid(), "Pitch", SignalType.Modulation, FanIn: FanInPolicy.Sum);
        var level = new ExposedPort(Guid.NewGuid(), "Gain", SignalType.Modulation, FanIn: FanInPolicy.Sum);
        var audio = new ExposedPort(Guid.NewGuid(), "Audio", SignalType.Audio);
        var drone = new CompositeNode(id, "Drone", [pitch, level], [audio]);
        var graph = drone.InternalGraph;
        var oscillator = new SineGenerator(Guid.NewGuid());
        var gain = new GainProcessor(Guid.NewGuid());
        graph.AddNode(oscillator);
        graph.AddNode(gain);
        var frequency = oscillator.Parameters.Single(parameter => parameter.Definition.Key == "frequency");
        var volume = gain.Parameters.Single(parameter => parameter.Definition.Key == "gain");
        graph.SetParameterValue(new ParameterAddress(gain.Id, volume.Id), 0.1);
        graph.Connect(new PortAddress(drone.InputRack.Id, pitch.Id),
            new PortAddress(oscillator.Id, frequency.ModulationPortId!.Value));
        graph.Connect(new PortAddress(drone.InputRack.Id, level.Id),
            new PortAddress(gain.Id, volume.ModulationPortId!.Value));
        graph.Connect(new PortAddress(oscillator.Id, oscillator.Outputs.Single().Id),
            new PortAddress(gain.Id, gain.Inputs.Single(port => port.Key == "audio-in").Id));
        graph.Connect(new PortAddress(gain.Id, gain.Outputs.Single().Id),
            new PortAddress(drone.OutputRack.Id, audio.Id));
        return drone;
    }
}
