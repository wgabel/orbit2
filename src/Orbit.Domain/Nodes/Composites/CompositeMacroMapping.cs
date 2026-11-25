using Orbit.Domain.Graphs;
using Orbit.Domain.Parameters;

namespace Orbit.Domain.Nodes.Composites;

/// <summary>One normalized control mapped to an internal parameter's physical units.
/// The endpoint order may be reversed; an exponent of one is linear.</summary>
public sealed record CompositeMacroBinding(ParameterAddress Target, double AtZero, double AtOne, double Exponent = 1);

/// <summary>Immutable mapping data for a single explicitly applied macro value.
/// Persistent front-panel controls and audio-rate mappings are separate concerns.</summary>
public sealed class CompositeMacroMapping
{
    public CompositeMacroMapping(IEnumerable<CompositeMacroBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        var copy = bindings.ToArray();
        if (copy.Length == 0 || copy.Any(binding => binding is null ||
            binding.Target.NodeId == Guid.Empty || binding.Target.ParameterId == Guid.Empty ||
            !double.IsFinite(binding.AtZero) || !double.IsFinite(binding.AtOne) ||
            !double.IsFinite(binding.Exponent) || binding.Exponent < 0.125 || binding.Exponent > 8))
            throw new GraphValidationException("Macro bindings require finite endpoints, a valid target and an exponent between 0.125 and 8.");
        if (copy.Select(binding => binding.Target).Distinct().Count() != copy.Length)
            throw new GraphValidationException("A macro cannot bind the same parameter more than once.");
        Bindings = Array.AsReadOnly(copy);
    }

    public IReadOnlyList<CompositeMacroBinding> Bindings { get; }

    internal static void ValidateValue(double value)
    {
        if (!double.IsFinite(value) || value < 0 || value > 1)
            throw new GraphValidationException("Macro value must be finite and between 0 and 1.");
    }

    internal static double Map(CompositeMacroBinding binding, double value)
    {
        if (value == 0) return binding.AtZero;
        if (value == 1) return binding.AtOne;
        var position = Math.Pow(value, binding.Exponent);
        return (1 - position) * binding.AtZero + position * binding.AtOne;
    }
}
