using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes;

/// <summary>
/// Reusable model implementation for node types. Collections are copied and
/// exposed read-only; graph edits and parameter updates are owned by SignalGraph.
/// </summary>
public abstract class SignalNode : ISignalNode
{
    protected SignalNode(Guid id, string typeKey, string name,
        IEnumerable<Parameter> parameters,
        IEnumerable<PortDefinition> inputs,
        IEnumerable<PortDefinition> outputs)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Node ID cannot be empty.", nameof(id));

        ArgumentException.ThrowIfNullOrWhiteSpace(typeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        TypeKey = typeKey;
        Name = name;
        Parameters = Array.AsReadOnly(parameters.ToArray());
        Inputs = Array.AsReadOnly(inputs.ToArray());
        Outputs = Array.AsReadOnly(outputs.ToArray());
    }

    public Guid Id { get; }
    public string TypeKey { get; }
    public string Name { get; }
    public IReadOnlyList<Parameter> Parameters { get; }
    public IReadOnlyList<PortDefinition> Inputs { get; }
    public IReadOnlyList<PortDefinition> Outputs { get; }
}
