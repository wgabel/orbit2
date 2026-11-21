namespace Orbit.Domain.Parameters;

/// <summary>A conditional parameter is active only when another local structural
/// choice has this value. Its stored base value and modulation socket remain intact.</summary>
public sealed record ParameterActivation(string SelectorKey, double EqualsValue, ParameterActivation? And = null)
{
    public IEnumerable<ParameterActivation> Conditions()
    {
        for (ParameterActivation? condition = this; condition is not null; condition = condition.And)
            yield return condition;
    }
}
