using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Application.Graphs;

/// <summary>Editable sustained instrument approximations assembled from ordinary nodes.
/// Every pitch-tracked partial receives the same semitone offset from the public rack.</summary>
public static class InstrumentTemplates
{
    public const string Organ = "preset.instrument.organ";
    public const string Flute = "preset.instrument.flute";
    public const string Brass = "preset.instrument.brass";
    public const string Strings = "preset.instrument.bowed-strings";
    public const string Didgeridoo = "preset.instrument.didgeridoo";
    public const string PlayableOrgan = "preset.instrument.playable-organ";
    public const string PlayableFlute = "preset.instrument.playable-flute";
    public const string PlayableBrass = "preset.instrument.playable-brass";
    public const string PlayableStrings = "preset.instrument.playable-bowed-strings";
    public const string PlayableDidgeridoo = "preset.instrument.playable-didgeridoo";

    internal static void Register(NodeRegistry registry)
    {
        registry.RegisterCompositePreset(Organ, "Instrument — Organ", id => CreateOrgan(id, false));
        registry.RegisterCompositePreset(Flute, "Instrument — Flute", id => CreateFlute(id, false));
        registry.RegisterCompositePreset(Brass, "Instrument — Brass", id => CreateBrass(id, false));
        registry.RegisterCompositePreset(Strings, "Instrument — Bowed strings", id => CreateStrings(id, false));
        registry.RegisterCompositePreset(Didgeridoo, "Instrument — Didgeridoo", id => CreateDidgeridoo(id, false));
        registry.RegisterCompositePreset(PlayableOrgan, "Playable — Organ", id => CreateOrgan(id, true));
        registry.RegisterCompositePreset(PlayableFlute, "Playable — Flute", id => CreateFlute(id, true));
        registry.RegisterCompositePreset(PlayableBrass, "Playable — Brass", id => CreateBrass(id, true));
        registry.RegisterCompositePreset(PlayableStrings, "Playable — Bowed strings", id => CreateStrings(id, true));
        registry.RegisterCompositePreset(PlayableDidgeridoo, "Playable — Didgeridoo", id => CreateDidgeridoo(id, true));
    }

    private static CompositeNode CreateOrgan(Guid id, bool playable)
    {
        var b = new Builder(id, "Organ", 110, playable ? new(8, 80, .92, 120) : null);
        foreach (var (ratio, level) in new (double, double)[] { (1, .30), (2, .15), (3, .10), (4, .05), (5, .025) })
            b.Mix(b.Voice(ratio, OscillatorWaveform.Sine, level));
        return b.Finish(b.Chorus(b.Bus, .45, 1.5, .18), .16, 1.8);
    }

    private static CompositeNode CreateFlute(Guid id, bool playable)
    {
        var b = new Builder(id, "Flute", 220, playable ? new(65, 180, .78, 190) : null);
        b.Mix(b.Voice(1, OscillatorWaveform.Sine, .30));
        b.Mix(b.Voice(2, OscillatorWaveform.Sine, .055));
        b.Mix(b.Voice(3, OscillatorWaveform.Triangle, .018));
        b.Mix(b.Filter(b.Noise(NoiseColor.White, .025), FilterMode.BandPass, 1800, 2));
        return b.Finish(b.Bus, .17, 2.2);
    }

    private static CompositeNode CreateBrass(Guid id, bool playable)
    {
        var b = new Builder(id, "Brass", 110, playable ? new(45, 160, .70, 160) : null);
        b.Mix(b.Voice(1, OscillatorWaveform.Sawtooth, .24));
        b.Mix(b.Voice(1, OscillatorWaveform.Triangle, .18));
        b.Mix(b.Voice(2, OscillatorWaveform.Sawtooth, .07));
        return b.Finish(b.Filter(b.Bus, FilterMode.LowPass, 1450, 1.1), .19, 1.7);
    }

    private static CompositeNode CreateStrings(Guid id, bool playable)
    {
        var b = new Builder(id, "Bowed strings", 110, playable ? new(180, 300, .85, 420) : null);
        b.Mix(b.Voice(1, OscillatorWaveform.Sawtooth, .23));
        b.Mix(b.Voice(Math.Pow(2, 7.0 / 1200), OscillatorWaveform.Sawtooth, .15));
        b.Mix(b.Voice(2, OscillatorWaveform.Triangle, .08));
        b.Mix(b.Filter(b.Noise(NoiseColor.Pink, .018), FilterMode.BandPass, 950, 1.4));
        var body = b.Filter(b.Bus, FilterMode.LowPass, 2200, .9);
        return b.Finish(b.Chorus(body, .32, 2.4, .22), .24, 2.6);
    }

    private static CompositeNode CreateDidgeridoo(Guid id, bool playable)
    {
        var b = new Builder(id, "Didgeridoo", 55, playable ? new(100, 220, .88, 300) : null);
        var reed = b.Voice(1, OscillatorWaveform.Sawtooth, .30);
        b.Mix(b.Voice(1, OscillatorWaveform.Triangle, .12));
        b.Mix(b.Filter(reed, FilterMode.LowPass, 290, .8), .8);
        b.Mix(b.Filter(reed, FilterMode.BandPass, 460, 3), .35);
        b.Mix(b.Filter(reed, FilterMode.BandPass, 1150, 4), .18);
        b.Mix(b.Filter(b.Noise(NoiseColor.Brown, .025), FilterMode.BandPass, 450, 1.5));
        return b.Finish(b.Bus, .14, 1.5);
    }

    private sealed record Articulation(double Attack, double Decay, double Sustain, double Release);

    private sealed class Builder
    {
        private readonly CompositeNode instrument;
        private readonly double fundamental;
        private readonly Articulation? articulation;
        private readonly ExposedPort pitch = new(Guid.NewGuid(), "Pitch (st)", SignalType.Modulation, FanIn: FanInPolicy.Sum);
        private readonly ExposedPort level = new(Guid.NewGuid(), "Gain offset", SignalType.Modulation, FanIn: FanInPolicy.Sum);
        private readonly ExposedPort gate = new(Guid.NewGuid(), "Gate", SignalType.Gate);
        private readonly ExposedPort velocity = new(Guid.NewGuid(), "Velocity", SignalType.Modulation, FanIn: FanInPolicy.Sum);
        private readonly ExposedPort audio = new(Guid.NewGuid(), "Audio", SignalType.Audio);

        public Builder(Guid id, string name, double fundamental, Articulation? articulation)
        {
            this.fundamental = fundamental;
            this.articulation = articulation;
            instrument = articulation is null
                ? new CompositeNode(id, name, [pitch, level], [audio])
                : new CompositeNode(id, $"Playable {name}", [pitch, gate, velocity], [audio]);
            Bus = Add(new MixerProcessor(Guid.NewGuid()));
        }

        public MixerProcessor Bus { get; }

        public OscillatorGenerator Voice(double ratio, OscillatorWaveform waveform, double amplitude)
        {
            var node = Add(new OscillatorGenerator(Guid.NewGuid()));
            Set(node, "frequency", fundamental * ratio);
            Set(node, "waveform", (int)waveform);
            Set(node, "amplitude", amplitude);
            Set(node, OscillatorGenerator.PitchTrackingKey, 1);
            Connect(instrument.InputRack, pitch.Id, node, OscillatorGenerator.PitchPortKey);
            return node;
        }

        public NoiseGenerator Noise(NoiseColor color, double amplitude)
        {
            var node = Add(new NoiseGenerator(Guid.NewGuid()));
            Set(node, "color", (int)color);
            Set(node, "amplitude", amplitude);
            return node;
        }

        public FilterProcessor Filter(ISignalNode source, FilterMode mode, double cutoff, double q)
        {
            var node = Add(new FilterProcessor(Guid.NewGuid()));
            Set(node, "mode", (int)mode);
            Set(node, "cutoff", cutoff);
            Set(node, "resonance", q);
            ConnectAudio(source, node);
            return node;
        }

        public ChorusProcessor Chorus(ISignalNode source, double rate, double depth, double mix)
        {
            var node = Add(new ChorusProcessor(Guid.NewGuid()));
            Set(node, "rate", rate);
            Set(node, "depth", depth);
            Set(node, "mix", mix);
            ConnectAudio(source, node);
            return node;
        }

        public void Mix(ISignalNode source, double amount = 1) => ConnectAudio(source, Bus, amount);

        public CompositeNode Finish(ISignalNode source, double wetMix, double decay)
        {
            if (articulation is not null)
                source = Articulate(source, articulation);
            var room = Add(new ReverbProcessor(Guid.NewGuid()));
            Set(room, "mix", wetMix);
            Set(room, "decay", decay);
            Set(room, "damping", .65);
            ConnectAudio(source, room);
            var gain = Add(new GainProcessor(Guid.NewGuid()));
            Set(gain, "gain", .5);
            ConnectAudio(room, gain);
            if (articulation is null)
                Connect(instrument.InputRack, level.Id, gain, "gain-modulation-in");
            instrument.InternalGraph.Connect(new PortAddress(gain.Id, gain.Outputs.Single().Id),
                new PortAddress(instrument.OutputRack.Id, audio.Id));
            return instrument;
        }

        private ISignalNode Articulate(ISignalNode source, Articulation shape)
        {
            var envelope = Add(new EnvelopeGenerator(Guid.NewGuid()));
            Set(envelope, EnvelopeGenerator.ModeKey, (int)EnvelopeMode.Adsr);
            Set(envelope, EnvelopeGenerator.AttackKey, shape.Attack);
            Set(envelope, EnvelopeGenerator.DecayKey, shape.Decay);
            Set(envelope, EnvelopeGenerator.SustainKey, shape.Sustain);
            Set(envelope, EnvelopeGenerator.ReleaseKey, shape.Release);
            Connect(instrument.InputRack, gate.Id, envelope, EnvelopeGenerator.GatePortKey);

            var velocityGain = Add(new GainProcessor(Guid.NewGuid()));
            Set(velocityGain, "gain", 0);
            ConnectAudio(source, velocityGain);
            Connect(instrument.InputRack, velocity.Id, velocityGain, "gain-modulation-in");

            var envelopeGain = Add(new GainProcessor(Guid.NewGuid()));
            Set(envelopeGain, "gain", 0);
            ConnectAudio(velocityGain, envelopeGain);
            Connect(envelope, envelope.Outputs.Single().Id, envelopeGain, "gain-modulation-in");
            return envelopeGain; // Reverb is downstream so its tail can decay naturally.
        }

        private T Add<T>(T node) where T : ISignalNode
        {
            instrument.InternalGraph.AddNode(node);
            return node;
        }

        private void Set(ISignalNode node, string key, double value) => instrument.InternalGraph.SetParameterValue(
            new ParameterAddress(node.Id, node.Parameters.Single(p => p.Definition.Key == key).Id), value);

        private void ConnectAudio(ISignalNode source, ISignalNode target, double amount = 1) =>
            Connect(source, source.Outputs.Single().Id, target, "audio-in", amount);

        private void Connect(ISignalNode source, Guid output, ISignalNode target, string input, double amount = 1) =>
            instrument.InternalGraph.Connect(new PortAddress(source.Id, output),
                new PortAddress(target.Id, target.Inputs.Single(p => p.Key == input).Id), amount);
    }
}
