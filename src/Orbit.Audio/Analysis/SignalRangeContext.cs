using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Analysis;

/// <summary>Backend range rules receive resolved inputs and effective parameters,
/// without depending on the canvas or performing sample processing.</summary>
public sealed class SignalRangeContext(GraphNodeSnapshot node,
    Func<Guid, ParameterRange?> input, Func<ParameterSnapshot, ParameterRange?> parameter)
{
    public ParameterRange? Input(string key) =>
        node.Inputs.SingleOrDefault(port => port.Key == key) is { } port ? input(port.Id) : null;

    public ParameterRange? Parameter(string key) =>
        node.Parameters.SingleOrDefault(p => p.Definition.Key == key) is { } value ? parameter(value) : null;

    public static ParameterRange? Multiply(ParameterRange? left, ParameterRange? right)
    {
        if (left is not { } a || right is not { } b) return null;
        double[] products = [a.Minimum * b.Minimum, a.Minimum * b.Maximum,
            a.Maximum * b.Minimum, a.Maximum * b.Maximum];
        return products.All(double.IsFinite) ? new ParameterRange(products.Min(), products.Max()) : null;
    }
}
