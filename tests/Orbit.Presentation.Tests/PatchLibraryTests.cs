using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Persistence;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class PatchLibraryTests
{
    [Fact]
    public async Task OverviewSearchUsesSavedMetadataAndDoesNotRestoreGraph()
    {
        var storage = new MemoryStorage();
        var serializer = new PatchDocumentSerializer();
        var library = new PatchLibraryService(storage, serializer);
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var graph = session.Snapshot;
        var overview = SavedPatchOverview.Create(" Distant horizon ", "Dark evolving space", "Introduce melody later",
            [" Ambient ", "ambient", "Space", "", "A minor"]);
        var document = serializer.Capture(session, 72, graph.Id, [], overview);
        await library.SaveAsync("Drone study", document);
        await library.SaveAsync("Legacy", serializer.Capture(session, 120, graph.Id, []));
        // A document with no reconstructable graph still exposes valid metadata;
        // browsing must never create a PatchSession or load a graph.
        await storage.WriteAsync("Metadata only", """{"schemaVersion":1,"overview":{"title":"Electric forest","description":"Atmospheric","instructions":"","tags":["Space"]}}""", false);
        await storage.WriteAsync("Malformed", """{"schemaVersion":1,"overview":null}""", false);
        var entries = await library.ListEntriesAsync();
        Assert.Equal(4, entries.Length);
        var entry = Assert.Single(entries, e => e.Name == "Drone study");
        Assert.Equal("Distant horizon", entry.Overview.Title);
        Assert.Equal(new[] { "Ambient", "Space", "A minor" }, entry.Overview.Tags);
        Assert.Equal(entry, Assert.Single(entries, e => e.Matches("DRONE evolving ambient", "space")));
        Assert.Equal(2, entries.Count(e => e.Matches("", "SPACE")));
        Assert.Single(entries, e => e.Matches("forest", ""));
        Assert.DoesNotContain(entries, e => e.Matches("missing", ""));
        Assert.True(Assert.Single(entries, e => e.Name == "Malformed").MetadataUnavailable);
        Assert.Empty(Assert.Single(entries, e => e.Name == "Legacy").Overview.Tags);
        await library.SaveAsync(entry.Name, document with { Overview = SavedPatchOverview.Empty }, overwrite: true);
        Assert.DoesNotContain(await library.ListEntriesAsync(), e => e.Matches("horizon", ""));
        await library.ExportAsync(document);
        var imported = library.Import(storage.Downloaded!);
        Assert.Equal(overview.Instructions, imported.Overview.Instructions);
        Assert.Equal(72, imported.BeatsPerMinute);
        Assert.Same(graph, session.Snapshot);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void MetadataRoundTripsAndRejectsInvalidValues()
    {
        var registry = BuiltInNodes.CreateEditorRegistry();
        using var session = new PatchSession(registry);
        var serializer = new PatchDocumentSerializer();
        var tags = new[] { "Space" };
        var overview = new SavedPatchOverview("Title", "Line 1\nLine 2", "Start playback", tags);
        var document = serializer.Capture(session, 120, session.GraphId, [], overview);
        tags[0] = "Changed";
        var json = serializer.Serialize(document);
        var restored = serializer.Deserialize(json);
        Assert.Equal("Space", Assert.Single(restored.Overview.Tags));
        Assert.Equal(overview.Description, restored.Overview.Description);
        Assert.Equal("Start playback", serializer.ReadOverview(json).Instructions);
        var old = """{"schemaVersion":1,"patchId":"00000000-0000-0000-0000-000000000001","rootGraph":{"id":"00000000-0000-0000-0000-000000000001","nodes":[],"connections":[]},"beatsPerMinute":120,"activeGraphId":"00000000-0000-0000-0000-000000000001","canvasLayouts":[]}""";
        Assert.Equal("", serializer.Deserialize(old).Overview.Title);
        Assert.Empty(serializer.ReadOverview(old).Tags);
        foreach (var invalid in new SavedPatchOverview[]
        {
            null!,
            overview with { Title = new string('x', 121) },
            overview with { Instructions = null! },
            overview with { Description = new string('x', 16001) },
            overview with { Tags = ["Space", "space"] },
            overview with { Tags = [" bad "] },
            overview with { Tags = ["bad,tag"] },
            overview with { Tags = [new string('x', 41)] },
            overview with { Tags = Enumerable.Range(1, 25).Select(i => $"Tag{i}").ToArray() }
        })
        {
            Assert.Throws<ArgumentException>(() => serializer.Serialize(document with { Overview = invalid }));
            Assert.Throws<ArgumentException>(() => serializer.Restore(document with { Overview = invalid }, registry));
        }
        Assert.Throws<ArgumentException>(() => serializer.Deserialize(old.Replace("\"canvasLayouts\":[]", "\"canvasLayouts\":[],\"overview\":null")));
        Assert.Throws<ArgumentException>(() => serializer.ReadOverview("""{"schemaVersion":1,"overview":{"title":"","description":"","instructions":"","tags":["A","a"]}}"""));
    }

    [Theory]
    [InlineData(PatchExampleCatalog.ModulationKey, 1)]
    [InlineData(PatchExampleCatalog.ImpactKey, 2)]
    public async Task ExportableExamplesHaveValidGraphAndAnnotations(string key, int canvases)
    {
        var storage = new MemoryStorage();
        var serializer = new PatchDocumentSerializer();
        var library = new PatchLibraryService(storage, serializer);
        await library.ExportExampleAsync(key);
        var document = library.Import(storage.Downloaded!);
        Assert.NotEmpty(document.Overview.Title);
        Assert.NotEmpty(document.Overview.Instructions);
        Assert.Contains("Tutorial", document.Overview.Tags);
        Assert.Equal(canvases, document.CanvasLayouts.Length);
        Assert.All(document.CanvasLayouts, canvas =>
        {
            Assert.NotEmpty(canvas.NodeDescriptions);
            Assert.Single(canvas.Notes);
        });
        using var session = serializer.Restore(document, BuiltInNodes.CreateEditorRegistry());
        Assert.NotEmpty(session.Snapshot.Connections);
        using var navigation = new Orbit.Presentation.Canvas.CanvasNavigationService(session, layouts: document.CanvasLayouts);
        Assert.Single(navigation.ActiveCanvas.Snapshot.Notes);
        Assert.Empty(await library.ListAsync());
    }

    [Fact]
    public async Task NamedSavesAreIndependentAndExplicitlyOverwritten()
    {
        var storage = new MemoryStorage();
        var serializer = new PatchDocumentSerializer();
        var library = new PatchLibraryService(storage, serializer);
        using var session = new PatchSession(BuiltInNodes.CreateEditorRegistry());
        var first = serializer.Capture(session, 90, session.GraphId, []);
        var second = first with { BeatsPerMinute = 120 };
        await library.SaveAsync(" First ", first);
        await library.SaveAsync("Second", second);
        Assert.Equal(new[] { "First", "Second" }, await library.ListAsync());
        Assert.Equal(90, (await library.LoadAsync("First")).BeatsPerMinute);
        await Assert.ThrowsAsync<ArgumentException>(() => library.SaveAsync("First", second));
        await library.SaveAsync("First", second, overwrite: true);
        Assert.Equal(120, (await library.LoadAsync("First")).BeatsPerMinute);
        await library.ExportAsync(first);
        Assert.Equal(90, library.Import(storage.Downloaded!).BeatsPerMinute);
        await library.DeleteAsync("First");
        Assert.Equal(new[] { "Second" }, await library.ListAsync());
    }

    private sealed class MemoryStorage : IPatchLibraryStorage
    {
        private readonly Dictionary<string, string> documents = [];
        public string? Downloaded { get; private set; }
        public Task<string[]> ListNamesAsync() => Task.FromResult(documents.Keys.ToArray());
        public Task<string?> ReadAsync(string name) => Task.FromResult(documents.GetValueOrDefault(name));
        public Task WriteAsync(string name, string json, bool overwrite)
        {
            if (overwrite) documents[name] = json;
            else documents.Add(name, json);
            return Task.CompletedTask;
        }
        public Task DeleteAsync(string name) { documents.Remove(name); return Task.CompletedTask; }
        public Task DownloadAsync(string name, string json) { Downloaded = json; return Task.CompletedTask; }
    }
}
