using System.Text.Json;
using Orbit.Domain.Graphs;
using Orbit.Domain.Ports;

namespace Orbit.AgentKit.Exporter;

/// <summary>Data-only routing contract for valid, already resolved ports.
/// Native routing probes derive the type and fan-in tables; structural rules
/// remain subject to the native validator and its regression tests.</summary>
public static class ConnectionRulesExporter
{
    public const int FormatVersion = 1;

    public static string Export()
    {
        var types = Enum.GetValues<SignalType>().OrderBy(type => type.ToString(), StringComparer.Ordinal).ToArray();
        var policies = Enum.GetValues<FanInPolicy>().OrderBy(policy => policy.ToString(), StringComparer.Ordinal).ToArray();
        var compatible = (from source in types
                          from destination in types
                          where Allows(source, destination, 1, 1)
                          select new { source = source.ToString(), destination = destination.ToString() }).ToArray();

        // The native rule is channel equality. Assert the current contract
        // before publishing it; never silently publish a stale generalization.
        foreach (var source in types)
        foreach (var destination in types)
        foreach (var sourceChannels in Channels(source))
        foreach (var destinationChannels in Channels(destination))
        {
            var expected = sourceChannels == destinationChannels && Allows(source, destination, 1, 1);
            if (Allows(source, destination, sourceChannels, destinationChannels) != expected)
                throw new InvalidOperationException("Native channel routing contract changed.");
        }

        var amounts = types.Select(type => new
        {
            destination = type.ToString(),
            rule = AmountRule(type),
            eventPort = type.IsEvent()
        }).ToArray();

        var fanIn = policies.Select(policy => new
        {
            policy = policy.ToString(),
            allowsMultiple = AllowsSecondSource(policy)
        }).ToArray();

        return JsonSerializer.Serialize(new
        {
            connectionRulesFormatVersion = FormatVersion,
            signalTypes = types.Select(type => type.ToString()).ToArray(),
            compatibleSignalPairs = compatible,
            channelRule = "equal",
            eventPortConstraints = new { channelCount = 1, fanIn = FanInPolicy.Single.ToString() },
            amounts,
            fanIn,
            topology = new
            {
                endpoints = "existing-output-to-existing-input-in-same-graph",
                uniqueConnectionIds = true,
                uniqueEndpointPairs = true,
                allowsDependencyCycles = false
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static int[] Channels(SignalType type) => type.IsEvent() ? [1] : [1, 2, 3];

    private static bool Allows(SignalType source, SignalType destination, int sourceChannels, int destinationChannels)
    {
        var output = new PortDefinition(Guid.NewGuid(), "out", "Output", PortDirection.Output, source, sourceChannels);
        var input = new PortDefinition(Guid.NewGuid(), "in", "Input", PortDirection.Input, destination, destinationChannels);
        return SignalGraphValidator.RoutingRejection(new PortAddress(Guid.NewGuid(), output.Id), output,
            new PortAddress(Guid.NewGuid(), input.Id), input, []) is null;
    }

    private static bool AllowsSecondSource(FanInPolicy policy)
    {
        var output = new PortDefinition(Guid.NewGuid(), "out", "Output", PortDirection.Output, SignalType.Audio);
        var input = new PortDefinition(Guid.NewGuid(), "in", "Input", PortDirection.Input, SignalType.Audio, FanIn: policy);
        var target = new PortAddress(Guid.NewGuid(), input.Id);
        var prior = new SignalConnection(Guid.NewGuid(), new PortAddress(Guid.NewGuid(), Guid.NewGuid()), target);
        return SignalGraphValidator.RoutingRejection(new PortAddress(Guid.NewGuid(), output.Id), output,
            target, input, [prior]) is null;
    }

    private static string AmountRule(SignalType type)
    {
        var unityOnly = type.IsEvent();
        double[] samples = [double.NegativeInfinity, double.NaN, double.PositiveInfinity,
            -double.MaxValue, -1, 0, 0.5, 1, 2, double.MaxValue];
        foreach (var sample in samples)
        {
            var expected = double.IsFinite(sample) && (!unityOnly || sample == 1);
            bool accepted;
            try
            {
                SignalGraphValidator.ValidateConnectionAmount(sample, type);
                accepted = true;
            }
            catch (GraphValidationException)
            {
                accepted = false;
            }
            if (accepted != expected)
                throw new InvalidOperationException($"Native amount contract changed for {type}.");
        }
        return unityOnly ? "unity-only" : "finite-signed";
    }
}
