using Orbit.Application.Graphs;
using Orbit.Application.Persistence;
using Orbit.Domain.Nodes.Generators;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class FoundationReferenceTests
{
    [Fact]
    public void OriginalBreathingHarmonicDroneRestoresItsSavedGraph()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Foundations", "breathing-harmonic-drone", "reference.orbit2.json");
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Deserialize(File.ReadAllText(path));
        using var restored = serializer.Restore(document, BuiltInNodes.CreateEditorRegistry());

        Assert.Equal(1, document.SchemaVersion);
        Assert.Equal(120, document.BeatsPerMinute);
        Assert.Equal(restored.GraphId, document.ActiveGraphId);
        Assert.Equal(14, restored.Snapshot.Nodes.Count);
        Assert.Equal(16, restored.Snapshot.Connections.Count);
        var muted = Assert.Single(restored.Snapshot.Nodes, node => node.IsMuted);
        Assert.Equal(OscillatorGenerator.NodeType, muted.TypeKey);
        Assert.Equal(1, muted.Parameters.Single(parameter => parameter.Definition.Key == "frequency").BaseValue);
    }
}
