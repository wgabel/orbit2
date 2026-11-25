using Orbit.Domain.Graphs;

namespace Orbit.Domain.Nodes.Composites;

/// <summary>Immutable per-instance macro configuration. Values are normalized;
/// manual child parameter values remain independent until runtime lowering is added.</summary>
public sealed class CompositeMacroDefinition
{
    public CompositeMacroDefinition(Guid id, string name, double defaultValue, double baseValue,
        CompositeMacroMapping mapping)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new GraphValidationException("A macro needs an identity and a name of 1–100 characters.");
        CompositeMacroMapping.ValidateValue(defaultValue);
        CompositeMacroMapping.ValidateValue(baseValue);
        ArgumentNullException.ThrowIfNull(mapping);
        Id = id;
        Name = name.Trim();
        DefaultValue = defaultValue;
        BaseValue = baseValue;
        Mapping = mapping;
    }

    public Guid Id { get; }
    public string Name { get; }
    public double DefaultValue { get; }
    public double BaseValue { get; }
    public CompositeMacroMapping Mapping { get; }

    internal bool HasSameConfiguration(CompositeMacroDefinition other) =>
        Id == other.Id && Name == other.Name && DefaultValue == other.DefaultValue &&
        BaseValue == other.BaseValue && Mapping.Bindings.SequenceEqual(other.Mapping.Bindings);
}
