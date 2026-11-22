namespace Orbit.Application.Persistence;

/// <summary>Named snapshot and portable file operations share the recovery document format.</summary>
public sealed class PatchLibraryService(IPatchLibraryStorage storage, PatchDocumentSerializer serializer)
{
    public async Task<string[]> ListAsync() => (await storage.ListNamesAsync()).Order(StringComparer.OrdinalIgnoreCase).ToArray();

    public async Task<PatchLibraryEntry[]> ListEntriesAsync()
    {
        var entries = new List<PatchLibraryEntry>();
        foreach (var name in await ListAsync())
        {
            var json = await storage.ReadAsync(name);
            if (json is null) continue;
            try { entries.Add(new(name, serializer.ReadOverview(json))); }
            catch (Exception exception) when (exception is ArgumentException or System.Text.Json.JsonException
                or NotSupportedException or InvalidOperationException or FormatException)
            {
                entries.Add(new(name, SavedPatchOverview.Empty, MetadataUnavailable: true));
            }
        }
        return entries.ToArray();
    }

    public Task SaveAsync(string name, PatchDocument document, bool overwrite = false) =>
        storage.WriteAsync(ValidateName(name), serializer.Serialize(document), overwrite);

    public async Task<PatchDocument> LoadAsync(string name) =>
        serializer.Deserialize(await storage.ReadAsync(ValidateName(name)) ?? throw new InvalidOperationException("Saved patch no longer exists."));

    public Task DeleteAsync(string name) => storage.DeleteAsync(ValidateName(name));

    public Task ExportAsync(PatchDocument document) => storage.DownloadAsync("orbit2-patch.json", serializer.Serialize(document));

    public Task ExportExampleAsync(string key) => storage.DownloadAsync($"orbit2-example-{key}.json",
        serializer.Serialize(PatchExampleCatalog.Create(key, serializer)));

    public PatchDocument Import(string json) => serializer.Deserialize(json);

    private static string ValidateName(string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 80 || name.Any(char.IsControl))
            throw new ArgumentException("Enter a patch name of 1–80 characters.");
        return name;
    }
}
