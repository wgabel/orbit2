using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class RoutingPreviewTests
{
    [Fact]
    public void PreviewUsesTheSameRoutingRulesAsCommittedConnection()
    {
        var graph = new SignalGraph();
        var first = new OscillatorGenerator(Guid.NewGuid());
        var second = new OscillatorGenerator(Guid.NewGuid());
        var gain = new GainProcessor(Guid.NewGuid());
        graph.AddNode(first);
        graph.AddNode(second);
        graph.AddNode(gain);

        var output = first.Outputs.Single();
        var source = new PortAddress(first.Id, output.Id);
        var frequency = second.Inputs.Single(input => input.Key == "frequency-modulation-in");
        var destination = new PortAddress(second.Id, frequency.Id);
        Assert.Null(SignalGraphValidator.RoutingRejection(source, output, destination, frequency, graph.Connections));
        graph.Connect(source, destination);
        Assert.NotNull(SignalGraphValidator.RoutingRejection(source, output, destination, frequency, graph.Connections));
        Assert.NotNull(SignalGraphValidator.RoutingRejection(source, output,
            new PortAddress(first.Id, first.Inputs[0].Id), first.Inputs[0], graph.Connections));
        var secondOutput = second.Outputs.Single();
        Assert.NotNull(SignalGraphValidator.RoutingRejection(new PortAddress(second.Id, secondOutput.Id), secondOutput,
            new PortAddress(first.Id, first.Inputs[0].Id), first.Inputs[0], graph.Connections));

        var audio = gain.Inputs.Single(input => input.Key == "audio-in");
        var audioDestination = new PortAddress(gain.Id, audio.Id);
        graph.Connect(source, audioDestination);
        Assert.NotNull(SignalGraphValidator.RoutingRejection(new PortAddress(second.Id, secondOutput.Id), secondOutput,
            audioDestination, audio, graph.Connections));
        Assert.NotNull(SignalGraphValidator.RoutingRejection(source, output,
            destination, frequency with { ChannelCount = 2 }, []));
        Assert.NotNull(SignalGraphValidator.RoutingRejection(source, output,
            destination, frequency with { SignalType = SignalType.Trigger }, []));
        Assert.Equal(2, graph.Connections.Count);
    }
}
