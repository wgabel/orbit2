using System.Buffers.Binary;
using Orbit.Domain.Nodes.Generators;

namespace Orbit.Audio.Compilation;

/// <summary>Bounded sample-rate noise with node-local pseudorandom state.</summary>
internal static class FaustNoiseSpectra
{
    public static string Emit(NoiseColor color, Guid nodeId)
    {
        // Distinct odd increments prevent identical generators from sharing one
        // compiler-merged random stream. Flattened node IDs include graph identity.
        var increment = (BinaryPrimitives.ReadInt32LittleEndian(nodeId.ToByteArray()) & int.MaxValue) | 1;
        var spectrum = color switch
        {
            NoiseColor.White => "white",
            NoiseColor.Pink => "0.12 * white + sum(i, 10, band(40.0 * pow(2.0, i)) / sqrt(pow(2.0, i))) * 2.0",
            NoiseColor.Brown => "band(15.0) * sqrt(ma.SR / (ma.PI * 15.0)) * 0.5",
            _ => throw new FaustCompilationException($"Unsupported noise color: {color}.")
        };
        // A bank of octave-spaced low-pass responses approximates 1/f power.
        // Brown uses a leaky integrator rather than an unbounded random walk.
        return $$"""
            min(1.0, max(-1.0, {{spectrum}})) with {
                random = +( {{increment}} ) ~ *(1103515245);
                white = float(random) / 2147483648.0;
                band(hz) = white : si.smooth(exp(-2.0 * ma.PI * hz / ma.SR));
            }
            """;
    }
}
