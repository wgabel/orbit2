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
        registry.Register(SineGenerator.NodeType, id => new SineGenerator(id));
        registry.Register(TempoSineGenerator.NodeType, id => new TempoSineGenerator(id));
        registry.Register(GainProcessor.NodeType, id => new GainProcessor(id));
        registry.Register(MasterOutput.NodeType, id => new MasterOutput(id));
        return registry;
    }

    /// <summary>Editable patch catalog, including the starter composite template.
    /// The leaf registry remains available for callers providing their own composite factories.</summary>
    public static NodeRegistry CreateEditorRegistry()
    {
        var registry = CreateRegistry();
        registry.Register(CompositeNode.NodeType, DroneTemplate.Create);
        return registry;
    }
}
