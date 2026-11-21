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

        // Activation reads stored local structural choices without recursion or
        // per-sample structural changes. A conditional selector requires an
        // explicit guard in every clause referring to it.
        foreach (var activation in Parameters.Select(parameter => parameter.Definition.ActiveWhen)
            .Concat(Inputs.Select(port => port.ActiveWhen)).OfType<ParameterActivation>())
        {
            foreach (var clause in activation.Clauses())
            foreach (var condition in clause)
            {
                var selector = Parameters.SingleOrDefault(candidate => candidate.Definition.Key == condition.SelectorKey);
                if (selector is null || !selector.Definition.RequiresRecompile || selector.Definition.Choices is null ||
                    !selector.Definition.IsValidBaseValue(condition.EqualsValue) ||
                    (selector.Definition.ActiveWhen is { } guard && !guard.Matches(key =>
                        clause.Where(c => c.SelectorKey == key && c.SelectorKey != condition.SelectorKey).Select(c => (double?)c.EqualsValue).FirstOrDefault())))
                    throw new ArgumentException("Activation requires a valid structural choice with an explicit guard if conditional.", nameof(parameters));
            }
        }
    }

    public Guid Id { get; }
    public string TypeKey { get; }
    public string Name { get; }
    public virtual bool SupportsMute => this is IGenerator or IProcessor or Composites.ICompositeNode;
    public bool IsMuted { get; internal set; }
    public virtual string? BypassInputKey => null;
    public bool IsBypassed { get; internal set; }
    public IReadOnlyList<Parameter> Parameters { get; private set; }
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

    /// <summary>Restore a factory-created, detached leaf using stable keys and saved identities.
    /// Definitions remain owned by the current node type, never by the document.</summary>
    public void RestoreIdentity(IReadOnlyDictionary<string, Guid> portIds,
        IReadOnlyDictionary<string, (Guid Id, double Value)> parameterValues)
    {
        if (OwningGraph is not null || this is Composites.ICompositeNode or Composites.InputRack or Composites.OutputRack)
            throw new InvalidOperationException("Only detached leaf nodes can restore identities.");
        if (portIds.Count != Inputs.Count + Outputs.Count || parameterValues.Count != Parameters.Count ||
            portIds.Values.Any(id => id == Guid.Empty) || portIds.Values.Distinct().Count() != portIds.Count ||
            parameterValues.Values.Select(value => value.Id).Distinct().Count() != parameterValues.Count)
            throw new ArgumentException("Saved node identities do not match its definition.");
        var mapping = Inputs.Concat(Outputs).ToDictionary(port => port.Id, port => portIds[port.Key]);
        var parameters = Parameters.Select(parameter =>
        {
            var saved = parameterValues[parameter.Definition.Key];
            return new Parameter(saved.Id, parameter.Definition,
                parameter.ModulationPortId is Guid portId ? mapping[portId] : null, saved.Value);
        }).ToArray();
        ReplaceSockets(Inputs.Select(port => port with { Id = mapping[port.Id] }),
            Outputs.Select(port => port with { Id = mapping[port.Id] }));
        Parameters = Array.AsReadOnly(parameters);
    }
}
