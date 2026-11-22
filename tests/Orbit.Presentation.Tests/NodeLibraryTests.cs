using Orbit.Application.Graphs;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class NodeLibraryTests
{
    [Fact]
    public void BuiltInEditorCatalogHasRecognizablePresentationMetadata()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        foreach (var key in registry.RegisteredTypes)
        {
            var appearance = NodeAppearanceCatalog.Get(key);
            Assert.NotEmpty(appearance.IconPath);
            Assert.NotEqual("Registered node", appearance.Description);
            Assert.True(new NodeLibraryEntry(key, registry.Describe(key), appearance).Matches(registry.Describe(key)));
        }
        Assert.Equal(NodeCategory.Modulation, NodeAppearanceCatalog.Get("generator.oscillator", NodeVisualRole.Modulator).Category);
        Assert.Equal(NodeAppearanceCatalog.Get("generator.oscillator").IconPath, NodeAppearanceCatalog.Get("generator.oscillator", NodeVisualRole.Modulator).IconPath);
        Assert.NotEqual(NodeAppearanceCatalog.Get("processor.delay").IconPath, NodeAppearanceCatalog.Get("processor.reverb").IconPath);
    }

    [Fact]
    public void SearchCombinesWordsAndCategory()
    {
        var entry = new NodeLibraryEntry("processor.delay", "Delay", NodeAppearanceCatalog.Get("processor.delay"));
        Assert.True(entry.Matches("echo DELAY", NodeCategory.Effects));
        Assert.False(entry.Matches("echo", NodeCategory.Audio));
        Assert.False(entry.Matches("echo missing"));
    }

    [Fact]
    public void PreferencesNormalizeUnavailableTypesAndBoundRecentUse()
    {
        var known = Enumerable.Range(0, 10).Select(i => $"node{i}").ToHashSet();
        var preferences = new NodeLibraryPreferences(["node0", "node0", "missing"], ["missing", "node1", "node1"]).Normalize(known);
        Assert.Equal(new[] { "node0" }, preferences.Favorites);
        Assert.Equal(new[] { "node1" }, preferences.Recent);
        foreach (var key in known) preferences = preferences.RecordUse(key);
        preferences = preferences.RecordUse("node1").ToggleFavorite("node0").ToggleFavorite("node2");
        Assert.Equal(6, preferences.Recent.Length);
        Assert.Equal("node1", preferences.Recent[0]);
        Assert.Equal(6, preferences.Recent.Distinct().Count());
        Assert.Equal(new[] { "node2" }, preferences.Favorites);
    }
}
