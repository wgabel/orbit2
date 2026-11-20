using Orbit.Domain.Graphs;
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

        // Activation depends only on a local, unconditional structural choice.
        // This avoids recursive conditions or per-sample structural changes.
        foreach (var parameter in Parameters)
        {
            if (parameter.Definition.ActiveWhen is not { } activation) continue;
            var selector = Parameters.SingleOrDefault(candidate => candidate.Definition.Key == activation.SelectorKey);
            if (selector is null || selector.Definition.ActiveWhen is not null ||
                !selector.Definition.RequiresRecompile || selector.Definition.Choices is null ||
                !selector.Definition.IsValidBaseValue(activation.EqualsValue))
                throw new ArgumentException("Parameter activation requires a valid unconditional structural choice.", nameof(parameters));
        }
    }

    public Guid Id { get; }
    public string TypeKey { get; }
    public string Name { get; }
    public IReadOnlyList<Parameter> Parameters { get; }
    public IReadOnlyList<PortDefinition> Inputs { get; private set; }
    public IReadOnlyList<PortDefinition> Outputs { get; private set; }

    // Only derived node implementations may replace their read-only socket views.
    // Composite boundary transactions validate the complete pair before doing so.
    protected void ReplaceSockets(IEnumerable<PortDefinition> inputs, IEnumerable<PortDefinition> outputs)
    {
        Inputs = Array.AsReadOnly(inputs.ToArray());
        Outputs = Array.AsReadOnly(outputs.ToArray());
    }

    // A concrete node instance belongs to one graph. This prevents sharing
    // mutable parameter state between otherwise independent composite instances.
    internal SignalGraph? OwningGraph { get; set; }
}
