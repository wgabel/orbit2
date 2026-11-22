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
    public const string PlayableHandpan = "preset.modal.playable-handpan";
    public const string PlayableBell = "preset.modal.playable-bell";
    public const string PlayableGong = "preset.modal.playable-gong";
    public const string PlayableWood = "preset.modal.playable-wood";

    public static void Register(NodeRegistry registry)
    {
        RegisterPair(Handpan, PlayableHandpan, "Handpan", 220, 1, 38,
            [new(1, 1.8, 1), new(2, 1.2, .65), new(3, .65, .38), new(4.12, .32, .16)]);
        RegisterPair(Bell, PlayableBell, "Bell", 330, 1, 30,
            [new(1, 2.8, .85), new(2.01, 2.2, .65), new(2.4, 1.6, .5), new(3.76, .82, .34), new(5.21, .38, .22)]);
        RegisterPair(Gong, PlayableGong, "Gong", 83, 5, 100,
            [new(1, 3.5, .9), new(1.51, 2.6, .75), new(2.17, 2.1, .65), new(3.42, 1.5, .55), new(5.29, .8, .35), new(8.13, .4, .2)]);
        RegisterPair(Wood, PlayableWood, "Wood percussion", 294, 1, 12,
            [new(1, .32, .9), new(2.76, .16, .48), new(5.4, .075, .2)]);

        void RegisterPair(string original, string playable, string name, double fundamental, double attackMs, double strikeMs, Mode[] modes)
        {
            registry.RegisterCompositePreset(original, $"Modal — {name}", id => Create(id, name, fundamental, attackMs, strikeMs, modes, false));
            registry.RegisterCompositePreset(playable, $"Playable — {name}", id => Create(id, name, fundamental, attackMs, strikeMs, modes, true));
        }
    }

    private sealed record Mode(double Ratio, double Decay, double Weight);

    private static CompositeNode Create(Guid id, string name, double fundamental, double attackMs, double strikeMs, Mode[] modes, bool playable)
    {
        var trigger = new ExposedPort(Guid.NewGuid(), "Trigger", SignalType.Trigger);
        var pitch = new ExposedPort(Guid.NewGuid(), "Pitch (st)", SignalType.Modulation, FanIn: FanInPolicy.Sum);
        var strength = new ExposedPort(Guid.NewGuid(), playable ? "Velocity" : "Strength offset", SignalType.Modulation, FanIn: FanInPolicy.Sum);
        var excitation = new ExposedPort(Guid.NewGuid(), "Excitation", SignalType.Audio);
        var output = new ExposedPort(Guid.NewGuid(), "Audio", SignalType.Audio);
        var instrument = new CompositeNode(id, playable ? $"Playable {name}" : name, [trigger, pitch, strength, excitation], [output]);
        var graph = instrument.InternalGraph;
        var envelope = Add(new EnvelopeGenerator(Guid.NewGuid()));
        var noise = Add(new NoiseGenerator(Guid.NewGuid()));
        var drive = Add(new MixerProcessor(Guid.NewGuid()));
        var gain = Add(new GainProcessor(Guid.NewGuid()));
        var sum = Add(new MixerProcessor(Guid.NewGuid()));
        var trim = Add(new GainProcessor(Guid.NewGuid()));

        Set(envelope, EnvelopeGenerator.ModeKey, (int)EnvelopeMode.Ad);
        Set(envelope, EnvelopeGenerator.TriggerSourceKey, 1);
        Set(envelope, EnvelopeGenerator.AttackKey, attackMs);
        Set(envelope, EnvelopeGenerator.DecayKey, strikeMs);
        Set(envelope, EnvelopeGenerator.DecayCurveKey, 1.5);
        Set(noise, "amplitude", 0);
        Set(gain, "gain", playable ? 0 : .75);
        Set(trim, "gain", .8);
        FromRack(trigger, envelope, EnvelopeGenerator.EventTriggerPortKey);
        Wire(envelope, noise, "amplitude-modulation-in", 1);
        FromRack(strength, gain, "gain-modulation-in");
        // The original strength offset affects both exciters. In the playable variant,
        // keyboard velocity scales only the internal strike, leaving external audio independent.
        Wire(noise, playable ? gain : drive, "audio-in");
        FromRack(excitation, drive, "audio-in", .03);
        if (playable)
            Wire(gain, drive, "audio-in");
        else
            Wire(drive, gain, "audio-in");
        ISignalNode bankInput = playable ? drive : gain;

        foreach (var mode in modes)
        {
            var resonator = Add(new ResonatorProcessor(Guid.NewGuid()));
            Set(resonator, "frequency", fundamental * mode.Ratio);
            Set(resonator, "decay", mode.Decay);
            Set(resonator, "excitation", 32);
            Set(resonator, "output-gain", mode.Weight);
            Set(resonator, "mix", 1);
            Wire(bankInput, resonator, "audio-in");
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
