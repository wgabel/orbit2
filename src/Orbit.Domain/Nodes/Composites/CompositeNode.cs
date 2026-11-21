using Orbit.Domain.Graphs;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Composites;

/// <summary>Owns exactly one child graph and its two boundary racks. The child
/// graph boundary transaction replaces both mirrored views together.</summary>
public sealed class CompositeNode : SignalNode, ICompositeNode
{
    public const string NodeType = "composite.graph";

    public CompositeNode(Guid id, string name, IEnumerable<ExposedPort> inputs, IEnumerable<ExposedPort> outputs)
        : this(id, name, inputs, outputs, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
    {
    }

    public CompositeNode(Guid id, string name, IEnumerable<ExposedPort> inputs, IEnumerable<ExposedPort> outputs,
        Guid graphId, Guid inputRackId, Guid outputRackId)
        : this(id, name, Boundary.Create(inputs, outputs), graphId, inputRackId, outputRackId) { }

    private CompositeNode(Guid id, string name, Boundary boundary, Guid graphId, Guid inputRackId, Guid outputRackId)
        : base(id, NodeType, name, [],
            boundary.Inputs.Select(port => port.Project(PortDirection.Input)),
            boundary.Outputs.Select(port => port.Project(PortDirection.Output)))
    {
        InputRack = new InputRack(inputRackId, boundary.Inputs);
        OutputRack = new OutputRack(outputRackId, boundary.Outputs);
        InternalGraph = new SignalGraph(this, graphId);
        InternalGraph.AddNode(InputRack);
        InternalGraph.AddNode(OutputRack);
    }

    public SignalGraph InternalGraph { get; }
    public InputRack InputRack { get; }
    public OutputRack OutputRack { get; }

    internal void ReplaceBoundary(IEnumerable<ExposedPort> inputs, IEnumerable<ExposedPort> outputs)
    {
        // Materialize and validate the whole boundary before touching any view.
        var boundary = Boundary.Create(inputs, outputs);
        ReplaceSockets(boundary.Inputs.Select(port => port.Project(PortDirection.Input)),
            boundary.Outputs.Select(port => port.Project(PortDirection.Output)));
        InputRack.ReplaceBoundary(boundary.Inputs);
        OutputRack.ReplaceBoundary(boundary.Outputs);
    }

    /// <summary>Materializes and validates both boundary lists before any node is created.</summary>
    private sealed record Boundary(IReadOnlyList<ExposedPort> Inputs, IReadOnlyList<ExposedPort> Outputs)
    {
        public static Boundary Create(IEnumerable<ExposedPort> inputs, IEnumerable<ExposedPort> outputs)
        {
            ArgumentNullException.ThrowIfNull(inputs);
            ArgumentNullException.ThrowIfNull(outputs);
            var inputPorts = inputs.ToArray();
            var outputPorts = outputs.ToArray();
            var all = inputPorts.Concat(outputPorts).ToArray();
            if (all.Any(port => port is null || port.Id == Guid.Empty ||
                string.IsNullOrWhiteSpace(port.Name) || port.ChannelCount < 1 ||
                !Enum.IsDefined(port.SignalType) || !Enum.IsDefined(port.FanIn) ||
                port.SignalType.IsEvent() && (port.ChannelCount != 1 || port.FanIn != FanInPolicy.Single)))
                throw new GraphValidationException("Every exposed port needs a valid definition.");
            if (all.Select(port => port.Id).Distinct().Count() != all.Length)
                throw new GraphValidationException("Boundary port IDs must be unique within a composite.");
            if (inputPorts.Select(port => port.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != inputPorts.Length ||
                outputPorts.Select(port => port.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != outputPorts.Length)
                throw new GraphValidationException("Boundary port names must be unique per direction.");
            return new Boundary(Array.AsReadOnly(inputPorts), Array.AsReadOnly(outputPorts));
        }
    }
}
