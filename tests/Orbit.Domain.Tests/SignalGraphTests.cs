using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

/// <summary>Exercises routing and invariants without an audio device or UI.</summary>
public sealed class SignalGraphTests
{
    [Fact]
    public void ConnectsOscillatorModulationAndAudioProcessing()
    {
        var graph = new SignalGraph();
        var lfo = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        var oscillator = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        var gain = AddNode(graph, new GainProcessor(Guid.NewGuid()));
        var output = AddNode(graph, new MasterOutput(Guid.NewGuid()));

        graph.SetParameterValue(Address(lfo, "frequency"), 0.5);
        var route = graph.Connect(Output(lfo), Input(oscillator, "frequency-modulation-in"), 100);
        graph.Connect(Output(oscillator), Input(gain, "audio-in"));
        graph.Connect(Output(gain), Input(output, "audio-in"));

        Assert.Equal(4, graph.Nodes.Count);
        Assert.Equal(3, graph.Connections.Count);
        Assert.Equal(100d, route.Amount);
        Assert.Equal(0.5, Parameter(lfo, "frequency").BaseValue);
        // Modulation describes a route, not a replacement for the base value.
        Assert.Equal(440d, Parameter(oscillator, "frequency").BaseValue);
    }

    [Fact]
    public void SupportsFanOutAndMultipleModulatorsOnOneParameter()
    {
        var graph = new SignalGraph();
        var first = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        var second = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        var target = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));

        graph.Connect(Output(first), Input(target, "frequency-modulation-in"), 20);
        graph.Connect(Output(second), Input(target, "frequency-modulation-in"), -10);
        graph.Connect(Output(first), Input(target, "amplitude-modulation-in"), 0.2);

        Assert.Equal(3, graph.Connections.Count);
        Assert.Equal(2, graph.Connections.Count(edge =>
            edge.Destination == Input(target, "frequency-modulation-in")));
        Assert.Equal(2, graph.Connections.Count(edge => edge.Source == Output(first)));
    }

    [Fact]
    public void RejectsSecondConnectionToSingleInput()
    {
        var graph = new SignalGraph();
        var first = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        var second = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        var gain = AddNode(graph, new GainProcessor(Guid.NewGuid()));

        graph.Connect(Output(first), Input(gain, "audio-in"));

        Assert.Throws<GraphValidationException>(() =>
            graph.Connect(Output(second), Input(gain, "audio-in")));
        Assert.Single(graph.Connections);
    }

    [Fact]
    public void RejectsDuplicateConnectionAddressAndIdentity()
    {
        var graph = new SignalGraph();
        var first = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        var target = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        var original = graph.Connect(Output(first), Input(target, "frequency-modulation-in"), 20);

        Assert.Throws<GraphValidationException>(() =>
            graph.Connect(original.Source, original.Destination, 30));
        Assert.Throws<GraphValidationException>(() =>
            graph.AddConnection(original with { Destination = Input(target, "amplitude-modulation-in") }));
        Assert.Single(graph.Connections);
    }

    [Fact]
    public void RejectsMissingEndpointsAndReversedPortDirection()
    {
        var graph = new SignalGraph();
        var first = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        var gain = AddNode(graph, new GainProcessor(Guid.NewGuid()));

        Assert.Throws<GraphValidationException>(() =>
            graph.Connect(new PortAddress(Guid.NewGuid(), Guid.NewGuid()), Input(gain, "audio-in")));
        Assert.Throws<GraphValidationException>(() =>
            graph.Connect(Output(first), new PortAddress(gain.Id, Guid.NewGuid())));
        Assert.Throws<GraphValidationException>(() =>
            graph.Connect(Input(first, "frequency-modulation-in"), Input(gain, "audio-in")));
        Assert.Throws<GraphValidationException>(() =>
            graph.Connect(Output(first), Output(gain)));
        Assert.Empty(graph.Connections);
    }

    [Fact]
    public void RejectsIncompatibleTypesAndChannelCounts()
    {
        var graph = new SignalGraph();
        var clock = AddNode(graph, new TestNode(
            new PortDefinition(Guid.NewGuid(), "clock", "Clock", PortDirection.Output, SignalType.Clock)));
        var stereo = AddNode(graph, new TestNode(
            new PortDefinition(Guid.NewGuid(), "stereo", "Stereo", PortDirection.Output, SignalType.Audio, 2)));
        var gain = AddNode(graph, new GainProcessor(Guid.NewGuid()));

        Assert.Throws<GraphValidationException>(() =>
            graph.Connect(Output(clock), Input(gain, "audio-in")));
        Assert.Throws<GraphValidationException>(() =>
            graph.Connect(Output(stereo), Input(gain, "audio-in")));
        Assert.Empty(graph.Connections);
    }

    [Fact]
    public void RejectsCyclesAcrossAudioAndModulationEdges()
    {
        var graph = new SignalGraph();
        var oscillator = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        var gain = AddNode(graph, new GainProcessor(Guid.NewGuid()));
        var secondGain = AddNode(graph, new GainProcessor(Guid.NewGuid()));

        graph.Connect(Output(oscillator), Input(gain, "audio-in"));
        graph.Connect(Output(gain), Input(secondGain, "audio-in"));

        Assert.Throws<GraphValidationException>(() =>
            graph.Connect(Output(secondGain), Input(oscillator, "frequency-modulation-in")));
        Assert.Throws<GraphValidationException>(() =>
            graph.Connect(Output(oscillator), Input(oscillator, "amplitude-modulation-in")));
        Assert.Equal(2, graph.Connections.Count);
    }

    [Fact]
    public void RejectsPureModulationCycles()
    {
        var graph = new SignalGraph();
        var first = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        var second = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        graph.Connect(Output(first), Input(second, "frequency-modulation-in"), 20);

        Assert.Throws<GraphValidationException>(() =>
            graph.Connect(Output(second), Input(first, "frequency-modulation-in"), 20));
        Assert.Single(graph.Connections);
    }

    [Fact]
    public void RemovingNodeRemovesOnlyIncidentEdges()
    {
        var graph = new SignalGraph();
        var oscillator = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        var gain = AddNode(graph, new GainProcessor(Guid.NewGuid()));
        var output = AddNode(graph, new MasterOutput(Guid.NewGuid()));
        var independent = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        var secondOutput = AddNode(graph, new MasterOutput(Guid.NewGuid()));

        graph.Connect(Output(oscillator), Input(gain, "audio-in"));
        graph.Connect(Output(gain), Input(output, "audio-in"));
        var unaffected = graph.Connect(Output(independent), Input(secondOutput, "audio-in"));

        graph.RemoveNode(gain.Id);

        Assert.Equal(4, graph.Nodes.Count);
        Assert.DoesNotContain(graph.Nodes, node => node.Id == gain.Id);
        Assert.Equal(unaffected, Assert.Single(graph.Connections));
    }

    [Fact]
    public void RemovingConnectionMakesSingleInputAvailableAgain()
    {
        var graph = new SignalGraph();
        var first = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        var second = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        var output = AddNode(graph, new MasterOutput(Guid.NewGuid()));
        var connection = graph.Connect(Output(first), Input(output, "audio-in"));

        graph.RemoveConnection(connection.Id);
        var replacement = graph.Connect(Output(second), Input(output, "audio-in"));

        Assert.Equal(replacement, Assert.Single(graph.Connections));
        Assert.Throws<GraphValidationException>(() => graph.RemoveConnection(connection.Id));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void RejectsNonFiniteConnectionAmount(double amount)
    {
        var graph = new SignalGraph();
        var oscillator = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));
        var gain = AddNode(graph, new GainProcessor(Guid.NewGuid()));

        Assert.Throws<GraphValidationException>(() =>
            graph.Connect(Output(oscillator), Input(gain, "audio-in"), amount));
        Assert.Empty(graph.Connections);
    }

    [Fact]
    public void RejectsDuplicateNodeIds()
    {
        var graph = new SignalGraph();
        var node = new OscillatorGenerator(Guid.NewGuid());
        graph.AddNode(node);

        Assert.Throws<GraphValidationException>(() => graph.AddNode(new OscillatorGenerator(node.Id)));
        Assert.Same(node, Assert.Single(graph.Nodes));
    }

    [Fact]
    public void RejectsInvalidNodePortDefinitions()
    {
        var graph = new SignalGraph();
        var invalid = new TestNode(new PortDefinition(
            Guid.Empty, "invalid", "Invalid", PortDirection.Output, SignalType.Audio));

        Assert.Throws<GraphValidationException>(() => graph.AddNode(invalid));
        Assert.Empty(graph.Nodes);
    }

    [Fact]
    public void GraphCollectionsDoNotExposeWritableLists()
    {
        var graph = new SignalGraph();
        var node = AddNode(graph, new OscillatorGenerator(Guid.NewGuid()));

        Assert.True(Assert.IsAssignableFrom<IList<ISignalNode>>(graph.Nodes).IsReadOnly);
        Assert.True(Assert.IsAssignableFrom<IList<SignalConnection>>(graph.Connections).IsReadOnly);
        Assert.True(Assert.IsAssignableFrom<IList<PortDefinition>>(node.Inputs).IsReadOnly);
    }

    private static T AddNode<T>(SignalGraph graph, T node) where T : ISignalNode
    {
        graph.AddNode(node);
        return node;
    }

    private static PortAddress Output(ISignalNode node) =>
        new(node.Id, node.Outputs.Single().Id);

    private static PortAddress Input(ISignalNode node, string key) =>
        new(node.Id, node.Inputs.Single(port => port.Key == key).Id);

    private static Parameter Parameter(ISignalNode node, string key) =>
        node.Parameters.Single(parameter => parameter.Definition.Key == key);

    private static ParameterAddress Address(ISignalNode node, string key) =>
        new(node.Id, Parameter(node, key).Id);

    // Uses the same extension contract as future node types, with no DSP runtime.
    private sealed class TestNode(PortDefinition output)
        : SignalNode(Guid.NewGuid(), "test.node", "Test node", [], [], [output])
    {
    }
}
