using System.Text;
using System.Text.Json;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Presets;
using Orbit.Audio.Compilation;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;
using Orbit.Infrastructure.Presets;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class NodePresetTests
{
    private readonly NodePresetValidator validator = new(BuiltInNodes.CreateEditorRegistry());

    [Fact]
    public void MatchingUsesAllBaseValuesAndIgnoresAuditionState()
    {
        var service = new NodePresetService(new EmbeddedNodePresetCatalog(), validator);
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var graphId = session.GraphId;
        var oscillator = session.Execute(new AddNodeCommand(graphId, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        Assert.Null(service.FindMatchingPreset(oscillator));
        var preset = service.GetPreset("oscillator-quarter-pulse");
        session.Execute(new ApplyNodePresetCommand(graphId, oscillator.Id, preset));
        Assert.Same(preset, service.FindMatchingPreset(session.Snapshot.GetNode(oscillator.Id)));
        session.Execute(new SetNodeMutedCommand(graphId, oscillator.Id, true));
        Assert.Same(preset, service.FindMatchingPreset(session.Snapshot.GetNode(oscillator.Id)));
        var frequency = oscillator.Parameters.Single(parameter => parameter.Definition.Key == "frequency");
        session.Execute(new SetParameterValueCommand(graphId, new ParameterAddress(oscillator.Id, frequency.Id), 2));
        Assert.Null(service.FindMatchingPreset(session.Snapshot.GetNode(oscillator.Id)));
        session.Undo();
        Assert.Same(preset, service.FindMatchingPreset(session.Snapshot.GetNode(oscillator.Id)));
        var output = session.Execute(new AddNodeCommand(graphId, MasterOutput.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        Assert.Null(service.FindMatchingPreset(output));
    }

    [Fact]
    public void EveryShippedPresetIsValidAndEveryEligibleTypeHasExampleData()
    {
        var catalog = new EmbeddedNodePresetCatalog();
        var service = new NodePresetService(catalog, validator);
        var registry = BuiltInNodes.CreateEditorRegistry();
        foreach (var key in registry.RegisteredTypes.Where(validator.Supports))
        {
            var groups = service.GetGroups(key);
            Assert.True(groups.Count >= 3, key);
            Assert.Equal(NodePreset.CustomGroup, groups[^1].Name);
            Assert.Empty(groups[^1].Presets);
            var presets = groups.SelectMany(group => group.Presets).ToArray();
            Assert.True(presets.Length >= 5, key);
            Assert.Equal(presets.Length, presets.Select(preset => preset.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            foreach (var preset in presets)
            {
                Assert.Equal(key, preset.NodeType);
                Assert.False(string.IsNullOrWhiteSpace(preset.Description), preset.Id);
                Assert.Same(preset, service.GetPreset(preset.Id));
                Assert.Equal(registry.Create(key).Parameters.Count, preset.Parameters.Count);
                validator.Validate(preset);
            }
        }
        Assert.NotEmpty(catalog.LoadBuiltInPresets());
    }

    [Fact]
    public void EveryShippedPresetAppliesMatchesAndGeneratesReachableDsp()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        var service = new NodePresetService(new EmbeddedNodePresetCatalog(), validator);
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        foreach (var type in registry.RegisteredTypes.Where(validator.Supports))
        {
            using var session = new PatchSession(registry);
            var graphId = session.GraphId;
            var target = session.Execute(new AddNodeCommand(graphId, type)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
            var master = session.Execute(new AddNodeCommand(graphId, MasterOutput.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
            if (target.Inputs.SingleOrDefault(port => port.SignalType == SignalType.Audio || port.Key == EnvelopeGenerator.TriggerPortKey) is { } input)
            {
                var source = session.Execute(new AddNodeCommand(graphId, OscillatorGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
                session.Execute(new ConnectNodesCommand(graphId, new PortAddress(source.Id, source.Outputs.Single().Id),
                    new PortAddress(target.Id, input.Id), input.Key == EnvelopeGenerator.TriggerPortKey ? 1 : 0.2));
            }
            if (target.Outputs.Single().SignalType.IsEvent())
            {
                var envelope = session.Execute(new AddNodeCommand(graphId, EnvelopeGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
                var gate = target.Outputs.Single().SignalType == SignalType.Gate;
                session.Execute(new SetParameterValueCommand(graphId, new ParameterAddress(envelope.Id, envelope.Parameters.Single(p => p.Definition.Key == EnvelopeGenerator.TriggerSourceKey).Id), gate ? 2 : 1));
                session.Execute(new ConnectNodesCommand(graphId, new(target.Id, target.Outputs[0].Id), new(envelope.Id, envelope.Inputs.Single(p => p.Key == (gate ? EnvelopeGenerator.GatePortKey : EnvelopeGenerator.EventTriggerPortKey)).Id)));
                var voice = session.Execute(new AddNodeCommand(graphId, OscillatorGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
                session.Execute(new ConnectNodesCommand(graphId, new(envelope.Id, envelope.Outputs[0].Id), new(voice.Id, voice.Inputs.Single(p => p.Key == "amplitude-modulation-in").Id)));
                session.Execute(new ConnectNodesCommand(graphId, new(voice.Id, voice.Outputs[0].Id), new(master.Id, master.Inputs[0].Id)));
            }
            else if (target.Outputs.Single().SignalType == SignalType.Modulation)
            {
                var carrier = session.Execute(new AddNodeCommand(graphId, OscillatorGenerator.NodeType)).ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
                session.Execute(new SetParameterValueCommand(graphId, new ParameterAddress(carrier.Id, carrier.Parameters.Single(p => p.Definition.Key == OscillatorGenerator.PitchTrackingKey).Id), 1));
                session.Execute(new ConnectNodesCommand(graphId, new PortAddress(target.Id, target.Outputs.Single().Id), new PortAddress(carrier.Id, carrier.Inputs.Single(p => p.Key == OscillatorGenerator.PitchPortKey).Id)));
                session.Execute(new ConnectNodesCommand(graphId, new PortAddress(carrier.Id, carrier.Outputs.Single().Id), new PortAddress(master.Id, master.Inputs.Single().Id)));
            }
            else session.Execute(new ConnectNodesCommand(graphId, new PortAddress(target.Id, target.Outputs.Single().Id),
                new PortAddress(master.Id, master.Inputs.Single().Id)));
            var connections = session.Snapshot.Connections.ToArray();
            foreach (var preset in service.GetGroups(type).SelectMany(group => group.Presets))
            {
                var before = session.Snapshot.Revision;
                var result = session.Execute(new ApplyNodePresetCommand(graphId, target.Id, preset));
                var applied = session.Snapshot.GetNode(target.Id);
                Assert.Equal(before + (result.Changed ? 1 : 0), session.Snapshot.Revision);
                foreach (var parameter in applied.Parameters)
                    Assert.Equal(preset.Parameters[parameter.Definition.Key], parameter.BaseValue);
                // Duplicate complete configurations would select the wrong catalog entry.
                Assert.Same(preset, service.FindMatchingPreset(applied));
                Assert.Equal(connections, session.Snapshot.Connections.ToArray());
                Assert.NotEmpty(compiler.Compile(session.Snapshot).Source);
            }
        }
    }

    [Fact]
    public void GroupsAndEntriesHaveStableOrderingAndRetainInactiveValues()
    {
        var service = new NodePresetService(new EmbeddedNodePresetCatalog(), validator);
        var groups = service.GetGroups(OscillatorGenerator.NodeType);
        Assert.Equal(groups.Take(groups.Count - 1).Select(group => group.Name).Order(StringComparer.OrdinalIgnoreCase),
            groups.Take(groups.Count - 1).Select(group => group.Name));
        Assert.Equal(NodePreset.CustomGroup, groups[^1].Name);
        foreach (var group in groups)
            Assert.Equal(group.Presets.OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(preset => preset.Id, StringComparer.Ordinal), group.Presets);
        var pulse = service.GetPreset("oscillator-quarter-pulse");
        Assert.Equal(1d, pulse.Parameters["timing"]);
        Assert.Equal(1d, pulse.Parameters["frequency"]); // Dormant in synchronized mode.
        Assert.Equal(2d, pulse.Parameters["division"]);
    }

    [Theory]
    [InlineData(MixerProcessor.NodeType)]
    [InlineData(MasterOutput.NodeType)]
    [InlineData(InputRack.NodeType)]
    [InlineData(OutputRack.NodeType)]
    [InlineData(CompositeNode.NodeType)]
    [InlineData(DroneTemplate.LayeredPresetKey)]
    [InlineData("unknown")]
    public void IneligibleTypesCannotHavePresets(string nodeType)
    {
        Assert.False(validator.Supports(nodeType));
        var service = new NodePresetService(new TestCatalog([]), validator);
        Assert.Empty(service.GetGroups(nodeType));
        Assert.Throws<ArgumentException>(() => validator.Validate(CreatePreset(nodeType, new Dictionary<string, double>())));
    }

    [Fact]
    public void EligibleTypeWithoutBuiltInsStillHasCustomGroup()
    {
        var service = new NodePresetService(new TestCatalog([]), validator);
        var custom = Assert.Single(service.GetGroups(GainProcessor.NodeType));
        Assert.Equal(NodePreset.CustomGroup, custom.Name);
        Assert.Empty(custom.Presets);
    }

    [Fact]
    public void ModelDefensivelyCopiesParameterValues()
    {
        var input = new Dictionary<string, double> { ["gain"] = 0.5 };
        var preset = CreatePreset(GainProcessor.NodeType, input);
        input["gain"] = 0.8;
        Assert.Equal(0.5, preset.Parameters["gain"]);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, double>)preset.Parameters)["gain"] = 1);
    }

    [Fact]
    public void InvalidOrIncompleteConfigurationsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => validator.Validate(CreatePreset(GainProcessor.NodeType, new Dictionary<string, double>())));
        Assert.Throws<ArgumentException>(() => validator.Validate(CreatePreset(GainProcessor.NodeType, new Dictionary<string, double> { ["wrong"] = 0.5 })));
        Assert.Throws<ArgumentException>(() => validator.Validate(CreatePreset(GainProcessor.NodeType, new Dictionary<string, double> { ["gain"] = 1.1 })));
        Assert.Throws<ArgumentException>(() => CreatePreset(GainProcessor.NodeType, new Dictionary<string, double> { ["gain"] = double.NaN }));
        Assert.Throws<ArgumentException>(() => validator.Validate(CreatePreset(NoiseGenerator.NodeType, new Dictionary<string, double> { ["color"] = 0.5, ["amplitude"] = 0.1 })));
        var oscillator = new EmbeddedNodePresetCatalog().LoadBuiltInPresets().First(preset => preset.Id == "oscillator-quarter-pulse");
        var values = oscillator.Parameters.ToDictionary(pair => pair.Key, pair => pair.Value);
        values.Remove("frequency");
        Assert.Throws<ArgumentException>(() => validator.Validate(CreatePreset(oscillator.NodeType, values)));
    }

    [Fact]
    public void BuiltInCatalogRejectsDuplicateIdsAndReservedGroup()
    {
        var preset = CreatePreset(GainProcessor.NodeType, new Dictionary<string, double> { ["gain"] = 0.5 });
        Assert.Throws<ArgumentException>(() => new NodePresetService(new TestCatalog([preset, preset]), validator));
        var custom = new NodePreset(1, "custom-id", GainProcessor.NodeType, "Custom", "custom", null, preset.Parameters);
        Assert.Throws<ArgumentException>(() => new NodePresetService(new TestCatalog([custom]), validator));
    }

    [Fact]
    public void ReaderRejectsUnsupportedSchemaDuplicateKeysAndUnknownProperties()
    {
        const string valid = """
            [{"schemaVersion":1,"id":"gain-test","nodeType":"processor.gain","name":"Test","group":"Level","parameters":{"gain":0.5}}]
            """;
        Assert.Single(Read(valid));
        Assert.Throws<NotSupportedException>(() => Read(valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":2")));
        Assert.Throws<ArgumentException>(() => Read(valid.Replace("\"gain\":0.5", "\"gain\":0.5,\"gain\":0.2")));
        Assert.Throws<ArgumentException>(() => Read(valid.Replace("\"name\":\"Test\"", "\"name\":\"Test\",\"name\":\"Other\"")));
        Assert.Throws<JsonException>(() => Read(valid.Replace("\"name\":\"Test\"", "\"name\":\"Test\",\"isMuted\":true")));
        Assert.Throws<ArgumentException>(() => Read(valid.Replace("\"group\":\"Level\"", "\"group\":\" \"")));
    }

    private static NodePreset CreatePreset(string nodeType, IReadOnlyDictionary<string, double> values) =>
        new(1, "test", nodeType, "Test", "Usage", null, values);

    private static IReadOnlyList<NodePreset> Read(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return NodePresetJsonReader.ReadCatalog(stream);
    }

    private sealed class TestCatalog(IReadOnlyList<NodePreset> presets) : INodePresetCatalog
    {
        public IReadOnlyList<NodePreset> LoadBuiltInPresets() => presets;
    }
}
