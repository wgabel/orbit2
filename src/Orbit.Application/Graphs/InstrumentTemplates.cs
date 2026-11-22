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

    internal static void Register(NodeRegistry registry)
    {
        registry.RegisterCompositePreset(Organ, "Instrument — Organ", CreateOrgan);
        registry.RegisterCompositePreset(Flute, "Instrument — Flute", CreateFlute);
        registry.RegisterCompositePreset(Brass, "Instrument — Brass", CreateBrass);
        registry.RegisterCompositePreset(Strings, "Instrument — Bowed strings", CreateStrings);
        registry.RegisterCompositePreset(Didgeridoo, "Instrument — Didgeridoo", CreateDidgeridoo);
    }

    private static CompositeNode CreateOrgan(Guid id)
    {
        var b = new Builder(id, "Organ", 110);
        foreach (var (ratio, level) in new (double, double)[] { (1, .30), (2, .15), (3, .10), (4, .05), (5, .025) })
            b.Mix(b.Voice(ratio, OscillatorWaveform.Sine, level));
        return b.Finish(b.Chorus(b.Bus, .45, 1.5, .18), .16, 1.8);
    }

    private static CompositeNode CreateFlute(Guid id)
    {
        var b = new Builder(id, "Flute", 220);
        b.Mix(b.Voice(1, OscillatorWaveform.Sine, .30));
        b.Mix(b.Voice(2, OscillatorWaveform.Sine, .055));
        b.Mix(b.Voice(3, OscillatorWaveform.Triangle, .018));
        b.Mix(b.Filter(b.Noise(NoiseColor.White, .025), FilterMode.BandPass, 1800, 2));
        return b.Finish(b.Bus, .17, 2.2);
    }

    private static CompositeNode CreateBrass(Guid id)
    {
        var b = new Builder(id, "Brass", 110);
        b.Mix(b.Voice(1, OscillatorWaveform.Sawtooth, .24));
        b.Mix(b.Voice(1, OscillatorWaveform.Triangle, .18));
        b.Mix(b.Voice(2, OscillatorWaveform.Sawtooth, .07));
        return b.Finish(b.Filter(b.Bus, FilterMode.LowPass, 1450, 1.1), .19, 1.7);
    }

    private static CompositeNode CreateStrings(Guid id)
    {
        var b = new Builder(id, "Bowed strings", 110);
        b.Mix(b.Voice(1, OscillatorWaveform.Sawtooth, .23));
        b.Mix(b.Voice(Math.Pow(2, 7.0 / 1200), OscillatorWaveform.Sawtooth, .15));
        b.Mix(b.Voice(2, OscillatorWaveform.Triangle, .08));
        b.Mix(b.Filter(b.Noise(NoiseColor.Pink, .018), FilterMode.BandPass, 950, 1.4));
        var body = b.Filter(b.Bus, FilterMode.LowPass, 2200, .9);
        return b.Finish(b.Chorus(body, .32, 2.4, .22), .24, 2.6);
    }

    private static CompositeNode CreateDidgeridoo(Guid id)
    {
        var b = new Builder(id, "Didgeridoo", 55);
        var reed = b.Voice(1, OscillatorWaveform.Sawtooth, .30);
        b.Mix(b.Voice(1, OscillatorWaveform.Triangle, .12));
        b.Mix(b.Filter(reed, FilterMode.LowPass, 290, .8), .8);
        b.Mix(b.Filter(reed, FilterMode.BandPass, 460, 3), .35);
        b.Mix(b.Filter(reed, FilterMode.BandPass, 1150, 4), .18);
        b.Mix(b.Filter(b.Noise(NoiseColor.Brown, .025), FilterMode.BandPass, 450, 1.5));
        return b.Finish(b.Bus, .14, 1.5);
    }

    private sealed class Builder
    {
        private readonly CompositeNode instrument;
        private readonly double fundamental;
        private readonly ExposedPort pitch = new(Guid.NewGuid(), "Pitch (st)", SignalType.Modulation, FanIn: FanInPolicy.Sum);
        private readonly ExposedPort level = new(Guid.NewGuid(), "Gain offset", SignalType.Modulation, FanIn: FanInPolicy.Sum);
        private readonly ExposedPort audio = new(Guid.NewGuid(), "Audio", SignalType.Audio);

        public Builder(Guid id, string name, double fundamental)
        {
            this.fundamental = fundamental;
            instrument = new CompositeNode(id, name, [pitch, level], [audio]);
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
            var room = Add(new ReverbProcessor(Guid.NewGuid()));
            Set(room, "mix", wetMix);
            Set(room, "decay", decay);
            Set(room, "damping", .65);
            ConnectAudio(source, room);
            var gain = Add(new GainProcessor(Guid.NewGuid()));
            Set(gain, "gain", .5);
            ConnectAudio(room, gain);
            Connect(instrument.InputRack, level.Id, gain, "gain-modulation-in");
            instrument.InternalGraph.Connect(new PortAddress(gain.Id, gain.Outputs.Single().Id),
                new PortAddress(instrument.OutputRack.Id, audio.Id));
            return instrument;
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
