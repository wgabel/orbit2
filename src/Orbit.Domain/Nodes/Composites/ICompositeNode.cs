using Orbit.Domain.Graphs;

namespace Orbit.Domain.Nodes.Composites;

/// <summary>A normal graph node with a separate, explicitly bounded child graph.</summary>
public interface ICompositeNode : ISignalNode
{
    SignalGraph InternalGraph { get; }
    InputRack InputRack { get; }
    OutputRack OutputRack { get; }
}
