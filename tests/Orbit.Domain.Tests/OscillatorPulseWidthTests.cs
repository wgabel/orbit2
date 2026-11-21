using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Persistence;
using Orbit.Application.Presets;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class OscillatorPulseWidthTests
{
    [Fact]
    public void LegacyPatchAndPresetKeepOriginalIdentitiesAndHalfDuty()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var node = session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType))
            .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Capture(session, 120, session.GraphId, []);
        var saved = document.RootGraph.Nodes.Single();
        var legacy = saved with {
            Parameters = saved.Parameters.Where(p => p.Key is not (OscillatorGenerator.PulseWidthKey or OscillatorGenerator.PitchKey or OscillatorGenerator.PitchTrackingKey)).ToArray(),
            Ports = saved.Ports.Where(p => p.Key is not (OscillatorGenerator.PulseWidthPortKey or OscillatorGenerator.PitchPortKey)).ToArray()
        };
        var oldDocument = document with { RootGraph = document.RootGraph with { Nodes = [legacy] } };
        using var restored = serializer.Restore(serializer.Deserialize(serializer.Serialize(oldDocument)), registry);
        var restoredNode = restored.Snapshot.GetNode(node.Id);
        var pulse = restoredNode.Parameters.Single(p => p.Definition.Key == OscillatorGenerator.PulseWidthKey);
        Assert.Equal(0.5, pulse.BaseValue);
        Assert.False(restoredNode.IsParameterActive(pulse));
        Assert.All(legacy.Parameters, p => Assert.Contains(restoredNode.Parameters, current => current.Id == p.Id));
        Assert.All(legacy.Ports, p => Assert.Contains(restoredNode.Inputs.Concat(restoredNode.Outputs), current => current.Id == p.Id));
        var waveform = restoredNode.Parameters.Single(p => p.Definition.Key == "waveform");
        restored.Execute(new SetParameterValueCommand(restored.GraphId, new ParameterAddress(node.Id, waveform.Id), (int)OscillatorWaveform.Square));
        Assert.True(restored.Snapshot.GetNode(node.Id).IsParameterActive(pulse));
        restored.Execute(new SetParameterValueCommand(restored.GraphId, new ParameterAddress(node.Id, pulse.Id), 0.2));
        var preset = new NodePreset(1, "legacy", OscillatorGenerator.NodeType, "Legacy", "Custom", null,
            legacy.Parameters.ToDictionary(p => p.Key, p => p.BaseValue));
        Assert.Equal(0.5, preset.Parameters[OscillatorGenerator.PulseWidthKey]);
    }
}
