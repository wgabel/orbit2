using Orbit.Domain.Graphs;
using Orbit.Domain.Parameters;

namespace Orbit.Domain.Nodes.Composites;

/// <summary>Immutable per-instance macro configuration. Values are normalized;
/// manual child parameter values remain independent of the mapped runtime base.</summary>
public sealed class CompositeMacroDefinition
{
    public CompositeMacroDefinition(Guid id, string name, double defaultValue, double baseValue,
        CompositeMacroMapping mapping, Guid? modulationInputId = null)
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
        if (modulationInputId == Guid.Empty)
            throw new GraphValidationException("A macro modulation input needs a valid identity.");
        ModulationInputId = modulationInputId;
    }

    public Guid Id { get; }
    public string Name { get; }
    public double DefaultValue { get; }
    public double BaseValue { get; }
    public CompositeMacroMapping Mapping { get; }
    public Guid? ModulationInputId { get; }

    /// <summary>Resolve the stored base through the same curve used by one-shot mappings.</summary>
    public IEnumerable<(ParameterAddress Target, double Value)> ResolveBaseValues() =>
        Mapping.Bindings.Select(binding => (binding.Target, CompositeMacroMapping.Map(binding, BaseValue)));

    internal bool HasSameConfiguration(CompositeMacroDefinition other) =>
        Id == other.Id && Name == other.Name && DefaultValue == other.DefaultValue &&
        BaseValue == other.BaseValue && ModulationInputId == other.ModulationInputId && Mapping.Bindings.SequenceEqual(other.Mapping.Bindings);
}
