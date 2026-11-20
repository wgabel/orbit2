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
        registry.Register(NoiseGenerator.NodeType, id => new NoiseGenerator(id), "Noise");
        registry.Register(GainProcessor.NodeType, id => new GainProcessor(id), "Gain");
        registry.Register(MixerProcessor.NodeType, id => new MixerProcessor(id), "Mixer");
        registry.Register(FilterProcessor.NodeType, id => new FilterProcessor(id), "Filter");
        registry.Register(ParametricEqProcessor.NodeType, id => new ParametricEqProcessor(id), "Parametric EQ");
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
        return registry;
    }
}
