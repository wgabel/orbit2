using System.Globalization;

namespace Orbit.Audio.Compilation;

/// <summary>Produces invariant Faust float literals, never unchecked user labels.</summary>
internal static class FaustNumber
{
    public static string Format(double value)
    {
        if (!double.IsFinite(value) || Math.Abs(value) > float.MaxValue)
            throw new FaustCompilationException("A value cannot be represented as a Faust float.");
        var literal = value.ToString("R", CultureInfo.InvariantCulture).ToLowerInvariant();
        return literal.Contains('.') || literal.Contains('e') ? literal : literal + ".0";
    }
}
