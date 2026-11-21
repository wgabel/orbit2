namespace Orbit.Audio.Compilation;

/// <summary>One sample pulse on a high crossing after a low sample; initially unarmed.</summary>
public static class FaustSchmittEdge
{
    /// <summary>Hysteretic binary level, initially closed. High opens; low closes.</summary>
    public static string EmitGate(string input) => $$"""
        (step ~ _) with {
            step(previous) = select2(({{input}}) >= 0.1, select2(({{input}}) <= 0.0, previous, 0), 1);
        }
        """;

    public static string Emit(string input) => $$"""
        ((armed : mem) * (x >= 0.1)) with {
            x = {{input}};
            armed = update ~ _;
            update(previous) = select2(x <= 0.0, select2(x >= 0.1, previous, 0), 1);
        }
        """;
}
