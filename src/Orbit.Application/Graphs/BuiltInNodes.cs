using Orbit.Domain.Nodes.Generators;
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
        registry.Register(GainProcessor.NodeType, id => new GainProcessor(id));
        registry.Register(MasterOutput.NodeType, id => new MasterOutput(id));
        return registry;
    }
}
