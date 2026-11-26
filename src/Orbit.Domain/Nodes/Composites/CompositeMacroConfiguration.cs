using Orbit.Domain.Graphs;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Composites;

/// <summary>Shared persistent mapping validation for the domain and immutable editor previews.
/// The resolver must address only parameters in the composite's direct child graph.</summary>
public static class CompositeMacroConfiguration
{
    public static bool IsSupportedTarget(ParameterDefinition definition) =>
        !definition.RequiresRecompile && definition.Choices is null;

    public static bool IsValidBinding(CompositeMacroBinding binding, ParameterDefinition definition) =>
        IsSupportedTarget(definition) &&
        definition.IsValidBaseValue(binding.AtZero) && definition.IsValidBaseValue(binding.AtOne);

    public static void Validate(IReadOnlyList<CompositeMacroDefinition> macros,
        Func<ParameterAddress, ParameterDefinition> resolve, IEnumerable<PortDefinition> inputs)
    {
        ArgumentNullException.ThrowIfNull(macros);
        ArgumentNullException.ThrowIfNull(resolve);
        if (macros.Count > 64 || macros.Any(macro => macro is null) ||
            macros.Select(macro => macro.Id).Distinct().Count() != macros.Count ||
            macros.Select(macro => macro.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != macros.Count)
            throw new GraphValidationException("A composite supports up to 64 uniquely named and identified macros.");

        ValidateModulationInputs(macros, inputs);
        var targets = new HashSet<ParameterAddress>();
        foreach (var macro in macros)
        foreach (var binding in macro.Mapping.Bindings)
        {
            if (!targets.Add(binding.Target))
                throw new GraphValidationException("An internal parameter cannot be owned by several macros.");
            if (!IsValidBinding(binding, resolve(binding.Target)))
                throw new GraphValidationException("Macro targets must be continuous parameters with valid range endpoints.");
        }
    }

    public static void ValidateModulationInputs(IReadOnlyList<CompositeMacroDefinition> macros,
        IEnumerable<PortDefinition> inputs)
    {
        var eligible = inputs.Where(port => port.Direction == PortDirection.Input &&
            port.SignalType == SignalType.Modulation && port.ChannelCount == 1).Select(port => port.Id).ToHashSet();
        var reserved = new HashSet<Guid>();
        foreach (var macro in macros)
            if (macro.ModulationInputId is Guid id && (!eligible.Contains(id) || !reserved.Add(id)))
                throw new GraphValidationException("Macro modulation requires a unique existing mono Modulation input. Unlink the macro before removing its input.");
    }
}
