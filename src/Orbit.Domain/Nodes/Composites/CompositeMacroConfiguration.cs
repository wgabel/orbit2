using Orbit.Domain.Graphs;
using Orbit.Domain.Parameters;

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
        Func<ParameterAddress, ParameterDefinition> resolve)
    {
        ArgumentNullException.ThrowIfNull(macros);
        ArgumentNullException.ThrowIfNull(resolve);
        if (macros.Count > 64 || macros.Any(macro => macro is null) ||
            macros.Select(macro => macro.Id).Distinct().Count() != macros.Count ||
            macros.Select(macro => macro.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != macros.Count)
            throw new GraphValidationException("A composite supports up to 64 uniquely named and identified macros.");

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
}
