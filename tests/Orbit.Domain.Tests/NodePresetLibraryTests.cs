using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Presets;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Nodes.Processors;
using Orbit.Domain.Parameters;
using Orbit.Infrastructure.Presets;
using Xunit;

namespace Orbit.Domain.Tests;

public sealed class NodePresetLibraryTests
{
    [Fact]
    public async Task SaveRenameOverwriteReloadApplyAndDeleteKeepGraphInstancesIndependent()
    {
        var storage = new MemoryStorage();
        var (catalog, library) = CreateLibrary(storage);
        await library.LoadAsync();
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = Add(session, OscillatorGenerator.NodeType);
        Set(session, node, "frequency", 333);
        Set(session, node, "timing", (int)OscillatorTimingMode.Synchronized);
        var snapshot = session.Snapshot;
        var saved = await library.SaveAsync(snapshot.GetNode(node.Id), "  My pulse  ", "Personal texture");
        Assert.Equal("My pulse", saved.Name);
        Assert.Equal(333, saved.Parameters["frequency"]); // Inactive value is captured.
        Assert.Same(saved, catalog.FindMatchingPreset(snapshot.GetNode(node.Id)));
        Assert.Same(snapshot, session.Snapshot);

        var renamed = await library.RenameAsync(saved.Id, saved.NodeType, "My rhythm");
        Assert.Equal(saved.Id, renamed.Id);
        Assert.Equal(saved.Description, renamed.Description);
        Set(session, node, "frequency", 444);
        var overwritten = await library.OverwriteAsync(saved.Id, session.Snapshot.GetNode(node.Id));
        Assert.Equal(444, overwritten.Parameters["frequency"]);
        Assert.Equal(333, saved.Parameters["frequency"]);

        var (restoredCatalog, restoredLibrary) = CreateLibrary(storage);
        await restoredLibrary.LoadAsync();
        var restored = Assert.Single(restoredCatalog.GetCustomPresets(node.TypeKey));
        Assert.Equal(saved.Id, restored.Id);
        Assert.Equal("My rhythm", restored.Name);
        Assert.Equal(overwritten.Parameters.OrderBy(x => x.Key), restored.Parameters.OrderBy(x => x.Key));
        Assert.Equal(saved.Description, restored.Description);

        using var otherSession = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var other = Add(otherSession, node.TypeKey);
        otherSession.Execute(new ApplyNodePresetCommand(otherSession.GraphId, other.Id, restored));
        Assert.Equal(444, otherSession.Snapshot.GetNode(other.Id).Parameters.Single(p => p.Definition.Key == "frequency").BaseValue);
        var applied = otherSession.Snapshot;
        await restoredLibrary.DeleteAsync(restored.Id, restored.NodeType);
        Assert.Empty(restoredCatalog.GetCustomPresets(node.TypeKey));
        Assert.Same(applied, otherSession.Snapshot);
        Assert.NotEqual(node.Id, other.Id);
        Assert.Throws<KeyNotFoundException>(() => restoredCatalog.GetPreset(saved.Id));
    }

    [Fact]
    public async Task NamesAreUniquePerTypeAndBuiltInsCannotBeEdited()
    {
        var (catalog, library) = CreateLibrary(new MemoryStorage());
        await library.LoadAsync();
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var oscillator = Add(session, OscillatorGenerator.NodeType);
        var gain = Add(session, GainProcessor.NodeType);
        var first = await library.SaveAsync(oscillator, "My preset");
        await library.SaveAsync(gain, "My preset");
        var second = await library.SaveAsync(oscillator, "Other");
        var revision = catalog.Revision;
        await Assert.ThrowsAsync<ArgumentException>(() => library.SaveAsync(oscillator, "MY PRESET"));
        await Assert.ThrowsAsync<ArgumentException>(() => library.RenameAsync(second.Id, oscillator.TypeKey, "my preset"));
        await Assert.ThrowsAsync<ArgumentException>(() => library.SaveAsync(oscillator, "Deep Sine"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => library.DeleteAsync("oscillator-deep-sine", oscillator.TypeKey));
        await Assert.ThrowsAsync<InvalidOperationException>(() => library.OverwriteAsync(first.Id, gain));
        Assert.Equal(revision, catalog.Revision);
        Assert.Equal(2, catalog.GetCustomPresets(oscillator.TypeKey).Count);
        Assert.Single(catalog.GetCustomPresets(gain.TypeKey));
    }

    [Fact]
    public async Task FailedWritesAndInvalidStoredDataDoNotReplaceCatalog()
    {
        var storage = new MemoryStorage();
        var (catalog, library) = CreateLibrary(storage);
        await library.LoadAsync();
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = Add(session, OscillatorGenerator.NodeType);
        var saved = await library.SaveAsync(node, "Before failure");
        var revision = catalog.Revision;
        storage.FailWrites = true;
        await Assert.ThrowsAsync<IOException>(() => library.DeleteAsync(saved.Id, node.TypeKey));
        Assert.Same(saved, catalog.GetPreset(saved.Id));
        storage.Json = storage.Json!.Replace("\"group\":\"Custom\"", "\"group\":\"Built-in\"");
        await Assert.ThrowsAsync<ArgumentException>(library.LoadAsync);
        Assert.Same(saved, catalog.GetPreset(saved.Id));
        Assert.Equal(revision, catalog.Revision);
    }

    [Fact]
    public async Task StaleLibraryCannotOverwriteConcurrentTabAndCanReload()
    {
        var storage = new MemoryStorage();
        var (firstCatalog, first) = CreateLibrary(storage);
        var (secondCatalog, second) = CreateLibrary(storage);
        await first.LoadAsync();
        await second.LoadAsync();
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = Add(session, OscillatorGenerator.NodeType);
        await first.SaveAsync(node, "First");
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.SaveAsync(node, "Second"));
        Assert.Empty(secondCatalog.GetCustomPresets(node.TypeKey));
        await second.LoadAsync();
        await second.SaveAsync(node, "Second");
        await first.LoadAsync();
        Assert.Equal(new[] { "First", "Second" }, firstCatalog.GetCustomPresets(node.TypeKey).Select(preset => preset.Name));
    }

    private static (NodePresetService Catalog, NodePresetLibraryService Library) CreateLibrary(MemoryStorage storage)
    {
        var catalog = new NodePresetService(new EmbeddedNodePresetCatalog(), new NodePresetValidator(BuiltInNodes.CreateEditorRegistry()));
        return (catalog, new NodePresetLibraryService(storage, catalog));
    }

    private static GraphNodeSnapshot Add(PatchSession session, string type) => session.Execute(new AddNodeCommand(session.GraphId, type))
        .ChangeSet!.Events.OfType<NodeAdded>().Single().Node;

    private static void Set(PatchSession session, GraphNodeSnapshot node, string key, double value) =>
        session.Execute(new SetParameterValueCommand(session.GraphId, new ParameterAddress(node.Id,
            node.Parameters.Single(parameter => parameter.Definition.Key == key).Id), value));

    private sealed class MemoryStorage : INodePresetStorage
    {
        public string? Json { get; set; }
        public bool FailWrites { get; set; }
        public Task<string?> LoadCustomAsync() => Task.FromResult(Json);
        public Task<bool> ReplaceCustomAsync(string? expectedJson, string json)
        {
            if (FailWrites) throw new IOException("Storage unavailable.");
            if (Json != expectedJson) return Task.FromResult(false);
            Json = json;
            return Task.FromResult(true);
        }
    }
}
