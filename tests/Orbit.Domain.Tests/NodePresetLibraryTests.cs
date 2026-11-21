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

    [Fact]
    public async Task PortableRoundTripPreservesAllValuesAndDoesNotModifyGraph()
    {
        var storage = new MemoryStorage();
        var (catalog, library) = CreateLibrary(storage);
        await library.LoadAsync();
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = Add(session, OscillatorGenerator.NodeType);
        Set(session, node, "frequency", 321);
        Set(session, node, "timing", (int)OscillatorTimingMode.Synchronized);
        var snapshot = session.Snapshot;
        var saved = await library.SaveAsync(snapshot.GetNode(node.Id), "Portable pulse", "A description");
        var revision = catalog.Revision;
        await library.ExportAsync(saved.Id, node.TypeKey);
        Assert.Equal("orbit2-node-preset.json", storage.DownloadName);
        var (otherCatalog, otherLibrary) = CreateLibrary(new MemoryStorage());
        await otherLibrary.LoadAsync();
        var preview = otherLibrary.PreviewImport(storage.Downloaded!, node.TypeKey);
        Assert.Equal(saved.Id, preview.Id);
        Assert.Equal(saved.Name, preview.Name);
        Assert.Equal(saved.Description, preview.Description);
        Assert.Equal(saved.Parameters.OrderBy(x => x.Key), preview.Parameters.OrderBy(x => x.Key));
        Assert.Empty(otherCatalog.GetCustomPresets(node.TypeKey));
        var imported = await otherLibrary.ImportAsync(preview, node.TypeKey, NodePresetImportMode.PreserveIdentity);
        Assert.Same(imported, otherCatalog.GetPreset(saved.Id));
        Assert.Equal(321, imported.Parameters["frequency"]);
        Assert.Same(snapshot, session.Snapshot);
        Assert.Equal(revision, catalog.Revision);
    }

    [Fact]
    public async Task PortableConflictsRequireExplicitCopyOrMatchingIdentityReplacement()
    {
        var storage = new MemoryStorage();
        var (catalog, library) = CreateLibrary(storage);
        await library.LoadAsync();
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = Add(session, OscillatorGenerator.NodeType);
        var saved = await library.SaveAsync(node, "Original");
        var incoming = new NodePreset(saved.SchemaVersion, saved.Id, saved.NodeType, "Replacement", NodePreset.CustomGroup,
            "File description", saved.Parameters.ToDictionary(pair => pair.Key, pair => pair.Key == "frequency" ? 333 : pair.Value));
        var revision = catalog.Revision;
        var json = storage.Json;
        await Assert.ThrowsAsync<InvalidOperationException>(() => library.ImportAsync(incoming, node.TypeKey, NodePresetImportMode.PreserveIdentity));
        await Assert.ThrowsAsync<ArgumentException>(() => library.ImportAsync(
            new NodePreset(1, Guid.NewGuid().ToString("D"), node.TypeKey, "ORIGINAL", NodePreset.CustomGroup, null, saved.Parameters),
            node.TypeKey, NodePresetImportMode.PreserveIdentity));
        await Assert.ThrowsAsync<ArgumentException>(() => library.ImportAsync(incoming, node.TypeKey, NodePresetImportMode.Copy, "ORIGINAL"));
        Assert.Equal(revision, catalog.Revision);
        Assert.Equal(json, storage.Json);
        var copy = await library.ImportAsync(incoming, node.TypeKey, NodePresetImportMode.Copy, "Copied file");
        Assert.NotEqual(saved.Id, copy.Id);
        Assert.Same(saved, catalog.GetPreset(saved.Id));
        var replaced = await library.ImportAsync(incoming, node.TypeKey, NodePresetImportMode.ReplaceMatchingIdentity);
        Assert.Equal(saved.Id, replaced.Id);
        Assert.Equal("Replacement", replaced.Name);
        Assert.Equal(333, replaced.Parameters["frequency"]);
        Assert.Equal(2, catalog.GetCustomPresets(node.TypeKey).Count);
        Assert.Equal(440, session.Snapshot.GetNode(node.Id).Parameters.Single(p => p.Definition.Key == "frequency").BaseValue);
    }

    [Fact]
    public async Task InvalidPortableFilesAndFailedImportsKeepBothCatalogAndStorageUnchanged()
    {
        var storage = new MemoryStorage();
        var (catalog, library) = CreateLibrary(storage);
        await library.LoadAsync();
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var node = Add(session, OscillatorGenerator.NodeType);
        var saved = await library.SaveAsync(node, "Original");
        var portable = NodePresetJsonWriter.WritePortable(saved);
        var revision = catalog.Revision;
        var json = storage.Json;
        Assert.Throws<ArgumentException>(() => library.PreviewImport(portable, GainProcessor.NodeType));
        Assert.Throws<NotSupportedException>(() => library.PreviewImport(portable.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"), node.TypeKey));
        Assert.Throws<ArgumentException>(() => library.PreviewImport(portable.Replace("\"frequency\":440", "\"frequency\":99999"), node.TypeKey));
        Assert.Throws<ArgumentException>(() => library.PreviewImport(portable.Replace("\"frequency\":440,", ""), node.TypeKey));
        Assert.Throws<ArgumentException>(() => library.PreviewImport(portable.Replace("\"frequency\":440", "\"frequency\":440,\"frequency\":333"), node.TypeKey));
        Assert.Throws<ArgumentException>(() => library.PreviewImport(NodePresetJsonWriter.WritePortable(catalog.GetPreset("oscillator-deep-sine")), node.TypeKey));
        await Assert.ThrowsAsync<InvalidOperationException>(() => library.ImportAsync(
            new NodePreset(1, Guid.NewGuid().ToString("D"), node.TypeKey, "Missing target", NodePreset.CustomGroup, null, saved.Parameters),
            node.TypeKey, NodePresetImportMode.ReplaceMatchingIdentity));
        storage.FailWrites = true;
        await Assert.ThrowsAsync<IOException>(() => library.ImportAsync(saved, node.TypeKey, NodePresetImportMode.Copy, "Failed copy"));
        Assert.Equal(revision, catalog.Revision);
        Assert.Equal(json, storage.Json);
        Assert.Same(saved, catalog.GetPreset(saved.Id));
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
        public string? DownloadName { get; private set; }
        public string? Downloaded { get; private set; }
        public Task DownloadAsync(string name, string json)
        {
            DownloadName = name;
            Downloaded = json;
            return Task.CompletedTask;
        }
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
