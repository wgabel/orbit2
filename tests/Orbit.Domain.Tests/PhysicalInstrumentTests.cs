using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Audio.Compilation;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Ports;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class PhysicalInstrumentTests
{
    [Theory]
    [InlineData(PhysicalPluckGenerator.NodeType)]
    [InlineData(PhysicalMarimbaGenerator.NodeType)]
    [InlineData(PhysicalSustainedGenerator.ClarinetType)]
    [InlineData(PhysicalSustainedGenerator.FluteType)]
    [InlineData(DrawbarOrganGenerator.NodeType)]
    [InlineData(PhysicalViolinGenerator.NodeType)]
    [InlineData(PhysicalBrassGenerator.NodeType)]
    [InlineData(PhysicalElectricGuitarGenerator.NodeType)]
    [InlineData(RotarySpeakerProcessor.NodeType)]
    public void RegisteredNodesOwnIndependentPortsAndRoundTrip(string type)
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var root = session.GraphId;
        var node = NestedFaustPatch.Add(session, root, type);
        var other = NestedFaustPatch.Add(session, root, type);
        Assert.Empty(node.Parameters.Select(p => p.Id).Intersect(other.Parameters.Select(p => p.Id)));
        Assert.Empty(node.Inputs.Select(p => p.Id).Intersect(other.Inputs.Select(p => p.Id)));
        Assert.Equal(SignalType.Audio, node.Outputs.Single().SignalType);
        var output = NestedFaustPatch.Add(session, root, MasterOutput.NodeType);
        session.Execute(new ConnectNodesCommand(root, new(node.Id, node.Outputs.Single().Id), new(output.Id, output.Inputs.Single().Id)));
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var before = compiler.Compile(session.Snapshot);
        var parameter = node.Parameters[0];
        session.Execute(new SetParameterValueCommand(root, new(node.Id, parameter.Id), parameter.Definition.Maximum));
        Assert.Equal(before.Source, compiler.Compile(session.Snapshot).Source);
        var serializer = new PatchDocumentSerializer();
        var json = serializer.Serialize(serializer.Capture(session, 120, root, []));
        using var restored = serializer.Restore(serializer.Deserialize(json), registry);
        Assert.Equal(json, serializer.Serialize(serializer.Capture(restored, 120, root, [])));
        Assert.Equal(before.Source, compiler.Compile(restored.Snapshot).Source);
    }
}
