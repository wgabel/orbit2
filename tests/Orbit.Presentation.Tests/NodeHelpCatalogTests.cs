using Orbit.Application.Graphs;
using Orbit.Domain.Ports;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class NodeHelpCatalogTests
{
    [Fact]
    public void EveryBuiltInEditorNodeHasUseCasesAndEveryPortHasAReadableHint()
    {
        var catalog = BuiltInNodes.CreateEditorRegistry();
        foreach (var key in catalog.RegisteredTypes)
        {
            Assert.True(NodeHelpCatalog.HasCuratedHelp(key), $"Missing curated help for {key}");
            var node = catalog.Create(key);
            var help = NodeHelpCatalog.Get(key);
            Assert.False(string.IsNullOrWhiteSpace(help.Summary));
            Assert.InRange(help.UseCases.Count, 2, 3);
            foreach (var example in help.UseCases) Assert.False(string.IsNullOrWhiteSpace(example));

            foreach (var port in node.Inputs.Concat(node.Outputs))
            {
                var hint = NodeHelpCatalog.PortHint(node.TypeKey, port);
                Assert.False(string.IsNullOrWhiteSpace(hint), $"Missing {port.Key} hint on {key}");
            }
        }
    }

    [Fact]
    public void MusicalPitchDiffersFromLinearFrequencyAndTriggerFromGate()
    {
        var catalog = BuiltInNodes.CreateEditorRegistry();
        var oscillator = catalog.Create("generator.oscillator");
        var hz = oscillator.Inputs.Single(port => port.Key == "frequency-modulation-in");
        var semitones = oscillator.Inputs.Single(port => port.Key == "pitch-modulation-in");
        Assert.Contains("Hz", NodeHelpCatalog.PortHint(oscillator.TypeKey, hz));
        Assert.Contains("semitone", NodeHelpCatalog.PortHint(oscillator.TypeKey, semitones));
        var sequencer = catalog.Create("generator.note-sequencer");
        var trigger = sequencer.Outputs.Single(port => port.SignalType == SignalType.Trigger);
        var gate = sequencer.Outputs.Single(port => port.SignalType == SignalType.Gate);
        Assert.Contains("pulse", NodeHelpCatalog.PortHint(sequencer.TypeKey, trigger));
        Assert.Contains("held", NodeHelpCatalog.PortHint(sequencer.TypeKey, gate));
    }

    [Fact]
    public void UnknownNodeAndCustomBoundaryPortUseSafeFallbacks()
    {
        Assert.False(NodeHelpCatalog.HasCuratedHelp("custom.unknown"));
        var entry = NodeHelpCatalog.Get("custom.unknown");
        Assert.NotEmpty(entry.Summary);
        Assert.Equal(2, entry.UseCases.Count);
        var custom = new PortDefinition(Guid.NewGuid(), "custom-boundary", "My signal",
            PortDirection.Input, SignalType.Modulation);
        Assert.Contains("control", NodeHelpCatalog.PortHint("composite.graph", custom));
    }
}
