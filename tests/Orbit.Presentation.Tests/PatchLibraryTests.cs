using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Persistence;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class PatchLibraryTests
{
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
