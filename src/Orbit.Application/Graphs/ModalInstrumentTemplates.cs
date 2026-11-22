using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs;

/// <summary>Ordinary editable resonators, with one strike exciter and shared pitch and strength.</summary>
public static class ModalInstrumentTemplates
{
    public const string Handpan = "preset.modal.handpan";
    public const string Bell = "preset.modal.bell";
    public const string Gong = "preset.modal.gong";
    public const string Wood = "preset.modal.wood";

    public static void Register(NodeRegistry registry)
    {
        registry.RegisterCompositePreset(Handpan, "Modal — Handpan", id => Create(id, "Handpan", 220, 38,
            [new(1, 1.8, 1), new(2, 1.2, .65), new(3, .65, .38), new(4.12, .32, .16)]));
        registry.RegisterCompositePreset(Bell, "Modal — Bell", id => Create(id, "Bell", 330, 30,
            [new(1, 2.8, .85), new(2.01, 2.2, .65), new(2.4, 1.6, .5), new(3.76, .82, .34), new(5.21, .38, .22)]));
        registry.RegisterCompositePreset(Gong, "Modal — Gong", id => Create(id, "Gong", 83, 100,
            [new(1, 3.5, .9), new(1.51, 2.6, .75), new(2.17, 2.1, .65), new(3.42, 1.5, .55), new(5.29, .8, .35), new(8.13, .4, .2)]));
        registry.RegisterCompositePreset(Wood, "Modal — Wood percussion", id => Create(id, "Wood percussion", 294, 12,
            [new(1, .32, .9), new(2.76, .16, .48), new(5.4, .075, .2)]));
    }

    private sealed record Mode(double Ratio, double Decay, double Weight);

    private static CompositeNode Create(Guid id, string name, double fundamental, double strikeMs, Mode[] modes)
    {
        var trigger = new ExposedPort(Guid.NewGuid(), "Trigger", SignalType.Trigger);
        var pitch = new ExposedPort(Guid.NewGuid(), "Pitch (st)", SignalType.Modulation, FanIn: FanInPolicy.Sum);
        var strength = new ExposedPort(Guid.NewGuid(), "Strength offset", SignalType.Modulation, FanIn: FanInPolicy.Sum);
        var excitation = new ExposedPort(Guid.NewGuid(), "Excitation", SignalType.Audio);
        var output = new ExposedPort(Guid.NewGuid(), "Audio", SignalType.Audio);
        var instrument = new CompositeNode(id, name, [trigger, pitch, strength, excitation], [output]);
        var graph = instrument.InternalGraph;
        var envelope = Add(new EnvelopeGenerator(Guid.NewGuid()));
        var noise = Add(new NoiseGenerator(Guid.NewGuid()));
        var drive = Add(new MixerProcessor(Guid.NewGuid()));
        var gain = Add(new GainProcessor(Guid.NewGuid()));
        var sum = Add(new MixerProcessor(Guid.NewGuid()));
        var trim = Add(new GainProcessor(Guid.NewGuid()));

        Set(envelope, EnvelopeGenerator.ModeKey, (int)EnvelopeMode.Ad);
        Set(envelope, EnvelopeGenerator.TriggerSourceKey, 1);
        Set(envelope, EnvelopeGenerator.AttackKey, name == "Gong" ? 5 : 1);
        Set(envelope, EnvelopeGenerator.DecayKey, strikeMs);
        Set(envelope, EnvelopeGenerator.DecayCurveKey, 1.5);
        Set(noise, "amplitude", 0);
        Set(gain, "gain", .75);
        Set(trim, "gain", .8);
        FromRack(trigger, envelope, EnvelopeGenerator.EventTriggerPortKey);
        Wire(envelope, noise, "amplitude-modulation-in", 1);
        Wire(noise, drive, "audio-in");
        // A separate, attenuated audio path permits custom exciters without a strike.
        FromRack(excitation, drive, "audio-in", .03);
        Wire(drive, gain, "audio-in");
        FromRack(strength, gain, "gain-modulation-in");

        foreach (var mode in modes)
        {
            var resonator = Add(new ResonatorProcessor(Guid.NewGuid()));
            Set(resonator, "frequency", fundamental * mode.Ratio);
            Set(resonator, "decay", mode.Decay);
            Set(resonator, "excitation", 32);
            Set(resonator, "output-gain", mode.Weight);
            Set(resonator, "mix", 1);
            Wire(gain, resonator, "audio-in");
            FromRack(pitch, resonator, ResonatorProcessor.PitchPortKey);
            Wire(resonator, sum, "audio-in");
        }
        Wire(sum, trim, "audio-in");
        graph.Connect(new(trim.Id, trim.Outputs.Single().Id), new(instrument.OutputRack.Id, output.Id));
        return instrument;

        T Add<T>(T node) where T : ISignalNode { graph.AddNode(node); return node; }
        void Set(ISignalNode node, string key, double value) => graph.SetParameterValue(
            new ParameterAddress(node.Id, node.Parameters.Single(p => p.Definition.Key == key).Id), value);
        void Wire(ISignalNode from, ISignalNode to, string key, double amount = 1) => graph.Connect(
            new(from.Id, from.Outputs.Single().Id), new(to.Id, to.Inputs.Single(p => p.Key == key).Id), amount);
        void FromRack(ExposedPort port, ISignalNode to, string key, double amount = 1) => graph.Connect(
            new(instrument.InputRack.Id, port.Id), new(to.Id, to.Inputs.Single(p => p.Key == key).Id), amount);
    }
}
