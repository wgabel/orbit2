namespace Orbit.Domain.Parameters;

/// <summary>A condition over stored local structural choices. Alternatives allow
/// shared parameters/sockets in several modes without duplicating their identities.</summary>
public sealed record ParameterActivation(string SelectorKey, double EqualsValue, ParameterActivation? And = null, ParameterActivation? Or = null)
{
    public bool Matches(Func<string, double?> value) =>
        (value(SelectorKey) == EqualsValue && (And is null || And.Matches(value))) || (Or is not null && Or.Matches(value));

    public IEnumerable<IReadOnlyList<ParameterActivation>> Clauses()
    {
        if (And is null) yield return new[] { this };
        else foreach (var clause in And.Clauses()) yield return new[] { this }.Concat(clause).ToArray();
        if (Or is not null) foreach (var clause in Or.Clauses()) yield return clause;
    }
}
