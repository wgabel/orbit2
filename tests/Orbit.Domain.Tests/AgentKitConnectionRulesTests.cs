using System.Text.Json;
using Orbit.AgentKit.Exporter;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class AgentKitConnectionRulesTests
{
    [Fact]
    public void RulesAreDeterministicAndMatchActualGraphConnections()
    {
        var json = ConnectionRulesExporter.Export();
        Assert.Equal(json, ConnectionRulesExporter.Export());
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(ConnectionRulesExporter.FormatVersion, root.GetProperty("connectionRulesFormatVersion").GetInt32());
        var pairs = root.GetProperty("compatibleSignalPairs").EnumerateArray()
            .Select(pair => (pair.GetProperty("source").GetString(), pair.GetProperty("destination").GetString())).ToHashSet();
        var types = Enum.GetValues<SignalType>();
        Assert.Equal(types.Select(type => type.ToString()).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            root.GetProperty("signalTypes").EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.Equal("equal", root.GetProperty("channelRule").GetString());
        foreach (var source in types)
        foreach (var destination in types)
        foreach (var sourceChannels in source.IsEvent() ? new[] { 1 } : new[] { 1, 2, 3 })
        foreach (var destinationChannels in destination.IsEvent() ? new[] { 1 } : new[] { 1, 2, 3 })
        {
            var graph = new SignalGraph();
            var output = AddPort(graph, PortDirection.Output, source, sourceChannels);
            var input = AddPort(graph, PortDirection.Input, destination, destinationChannels);
            var expected = pairs.Contains((source.ToString(), destination.ToString())) && sourceChannels == destinationChannels;
            if (expected)
                graph.Connect(output, input);
            else
                Assert.Throws<GraphValidationException>(() => graph.Connect(output, input));
            Assert.Equal(expected ? 1 : 0, graph.Connections.Count);
        }
        Assert.Contains(("Audio", "Modulation"), pairs);
        Assert.Contains(("Clock", "Trigger"), pairs);
        Assert.DoesNotContain(("Modulation", "Audio"), pairs);
    }

    [Fact]
    public void AmountAndFanInRulesMatchNativeValidation()
    {
        using var document = JsonDocument.Parse(ConnectionRulesExporter.Export());
        var root = document.RootElement;
        var amounts = root.GetProperty("amounts").EnumerateArray().ToDictionary(item => item.GetProperty("destination").GetString()!);
        foreach (var type in Enum.GetValues<SignalType>())
        {
            var entry = amounts[type.ToString()];
            Assert.Equal(type.IsEvent(), entry.GetProperty("eventPort").GetBoolean());
            var rule = entry.GetProperty("rule").GetString();
            Assert.Equal(type.IsEvent() ? "unity-only" : "finite-signed", rule);
            foreach (var amount in new[] { -2d, 0, 0.25, 1, 3, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                var graph = new SignalGraph();
                var source = AddPort(graph, PortDirection.Output, type);
                var target = AddPort(graph, PortDirection.Input, type);
                var expected = double.IsFinite(amount) && (rule != "unity-only" || amount == 1);
                if (expected)
                    graph.Connect(source, target, amount);
                else
                    Assert.Throws<GraphValidationException>(() => graph.Connect(source, target, amount));
                Assert.Equal(expected ? 1 : 0, graph.Connections.Count);
            }
        }

        foreach (var entry in root.GetProperty("fanIn").EnumerateArray())
        {
            var policy = Enum.Parse<FanInPolicy>(entry.GetProperty("policy").GetString()!);
            var graph = new SignalGraph();
            var first = AddPort(graph, PortDirection.Output, SignalType.Audio);
            var second = AddPort(graph, PortDirection.Output, SignalType.Audio);
            var target = AddPort(graph, PortDirection.Input, SignalType.Audio, fanIn: policy);
            graph.Connect(first, target);
            if (entry.GetProperty("allowsMultiple").GetBoolean())
                graph.Connect(second, target);
            else
                Assert.Throws<GraphValidationException>(() => graph.Connect(second, target));
            Assert.Throws<GraphValidationException>(() => graph.Connect(first, target));
        }
    }

    private static PortAddress AddPort(SignalGraph graph, PortDirection direction, SignalType type,
        int channels = 1, FanInPolicy fanIn = FanInPolicy.Single)
    {
        var port = new PortDefinition(Guid.NewGuid(), "port", "Port", direction, type, channels, fanIn);
        var node = new ProbeNode(port);
        graph.AddNode(node);
        return new PortAddress(node.Id, port.Id);
    }

    private sealed class ProbeNode(PortDefinition port) : SignalNode(Guid.NewGuid(), "test.probe", "Probe", [],
        port.Direction == PortDirection.Input ? [port] : [],
        port.Direction == PortDirection.Output ? [port] : [])
    {
    }
}
