using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs;

/// <summary>Starter composite factory. Each instance gets its own graph, sockets,
/// parameter identities and editable internal signal routing.</summary>
public static class DroneTemplate
{
    public const string LayeredPresetKey = "preset.drone.layered-fifth";

    public static CompositeNode Create(Guid id) => CreateDrone(id, "Drone", 0.1,
        [(440, OscillatorWaveform.Sine, 1.0, 1.0)], false);

    public static CompositeNode CreateLayered(Guid id) => CreateDrone(id, "Layered fifth", 0.5,
        [(110, OscillatorWaveform.Sine, 0.35, 1.0),
         (165, OscillatorWaveform.Triangle, 0.25, 1.5),
         (220.7, OscillatorWaveform.Sine, 0.2, 220.7 / 110)], true);

    private static CompositeNode CreateDrone(Guid id, string name, double gainValue,
        IReadOnlyList<(double Frequency, OscillatorWaveform Waveform, double Amplitude, double PitchRatio)> voices,
        bool includeNoise)
    {
        var pitch = new ExposedPort(Guid.NewGuid(), "Pitch", SignalType.Modulation, FanIn: FanInPolicy.Sum);
        var level = new ExposedPort(Guid.NewGuid(), "Gain", SignalType.Modulation, FanIn: FanInPolicy.Sum);
        var audio = new ExposedPort(Guid.NewGuid(), "Audio", SignalType.Audio);
        var drone = new CompositeNode(id, name, [pitch, level], [audio]);
        var graph = drone.InternalGraph;
        var gain = new GainProcessor(Guid.NewGuid());
        MixerProcessor? mixer = voices.Count > 1 || includeNoise ? new MixerProcessor(Guid.NewGuid()) : null;
        foreach (var voice in voices)
        {
            var oscillator = new OscillatorGenerator(Guid.NewGuid());
            graph.AddNode(oscillator);
            Set(oscillator, "frequency", voice.Frequency);
            Set(oscillator, "amplitude", voice.Amplitude);
            Set(oscillator, "waveform", (int)voice.Waveform);
            var frequency = oscillator.Parameters.Single(p => p.Definition.Key == "frequency");
            graph.Connect(new PortAddress(drone.InputRack.Id, pitch.Id),
                new PortAddress(oscillator.Id, frequency.ModulationPortId!.Value), voice.PitchRatio);
        }
        if (includeNoise)
        {
            var noise = new NoiseGenerator(Guid.NewGuid());
            graph.AddNode(noise);
            Set(noise, "color", (int)NoiseColor.Pink);
            Set(noise, "amplitude", 0.04);
        }
        var sources = graph.Nodes.OfType<IGenerator>().ToArray();
        if (mixer is not null) graph.AddNode(mixer);
        graph.AddNode(gain);
        var volume = gain.Parameters.Single(parameter => parameter.Definition.Key == "gain");
        Set(gain, "gain", gainValue);
        graph.Connect(new PortAddress(drone.InputRack.Id, level.Id),
            new PortAddress(gain.Id, volume.ModulationPortId!.Value));
        foreach (var source in sources) ConnectAudio(source, (ISignalNode?)mixer ?? gain);
        if (mixer is not null) ConnectAudio(mixer, gain);
        graph.Connect(new PortAddress(gain.Id, gain.Outputs.Single().Id),
            new PortAddress(drone.OutputRack.Id, audio.Id));
        return drone;

        void Set(ISignalNode node, string key, double value) => graph.SetParameterValue(
            new ParameterAddress(node.Id, node.Parameters.Single(p => p.Definition.Key == key).Id), value);

        void ConnectAudio(ISignalNode source, ISignalNode destination) => graph.Connect(
            new PortAddress(source.Id, source.Outputs.Single().Id),
            new PortAddress(destination.Id, destination.Inputs.Single(port => port.Key == "audio-in").Id));
    }
}
