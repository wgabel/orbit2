using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;

namespace Orbit.Application.Graphs;

/// <summary>
/// Initial catalog composition. Adding a node type requires registration here,
/// not changes to graph ownership or the eventual canvas editor.
/// </summary>
public static class BuiltInNodes
{
    public static NodeRegistry CreateRegistry()
    {
        var registry = new NodeRegistry();
        registry.Register(OscillatorGenerator.NodeType, id => new OscillatorGenerator(id), "Oscillator");
        registry.Register(PitchControlGenerator.NodeType, id => new PitchControlGenerator(id), "Pitch Control");
        registry.Register(NoteSequencerGenerator.NodeType, id => new NoteSequencerGenerator(id), "Note Sequencer");
        registry.Register(EnvelopeGenerator.NodeType, id => new EnvelopeGenerator(id), "Envelope");
        registry.Register(SignalToTriggerGenerator.NodeType, id => new SignalToTriggerGenerator(id), "Signal to Trigger");
        registry.Register(SignalToGateGenerator.NodeType, id => new SignalToGateGenerator(id), "Signal to Gate");
        registry.Register(ManualTriggerGenerator.NodeType, id => new ManualTriggerGenerator(id), "Manual Trigger");
        registry.Register(GateGenerator.NodeType, id => new GateGenerator(id), "Gate");
        registry.Register(ClockGenerator.NodeType, id => new ClockGenerator(id), "Clock");
        registry.Register(NoiseGenerator.NodeType, id => new NoiseGenerator(id), "Noise");
        registry.Register(GainProcessor.NodeType, id => new GainProcessor(id), "Gain");
        registry.Register(MixerProcessor.NodeType, id => new MixerProcessor(id), "Mixer");
        registry.Register(FilterProcessor.NodeType, id => new FilterProcessor(id), "Filter");
        registry.Register(ParametricEqProcessor.NodeType, id => new ParametricEqProcessor(id), "Parametric EQ");
        registry.Register(DelayProcessor.NodeType, id => new DelayProcessor(id), "Delay");
        registry.Register(ReverbProcessor.NodeType, id => new ReverbProcessor(id), "Reverb");
        registry.Register(ResonatorProcessor.NodeType, id => new ResonatorProcessor(id), "Resonator");
        registry.Register(ChorusProcessor.NodeType, id => new ChorusProcessor(id), "Chorus");
        registry.Register(MasterOutput.NodeType, id => new MasterOutput(id), "Master output");
        return registry;
    }

    /// <summary>Editable patch catalog, including the starter composite template.
    /// The leaf registry remains available for callers providing their own composite factories.</summary>
    public static NodeRegistry CreateEditorRegistry()
    {
        var registry = CreateRegistry();
        registry.Register(CompositeNode.NodeType, DroneTemplate.Create, "Drone (composite)");
        registry.RegisterCompositePreset(DroneTemplate.LayeredPresetKey, "Drone — Layered fifth", DroneTemplate.CreateLayered);
        registry.RegisterCompositePreset(ImpactTemplate.PresetKey, "Impact — AD percussion", ImpactTemplate.Create);
        InstrumentTemplates.Register(registry);
        return registry;
    }
}
