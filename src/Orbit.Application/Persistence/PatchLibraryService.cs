namespace Orbit.Application.Persistence;

/// <summary>Named snapshot and portable file operations share the recovery document format.</summary>
public sealed class PatchLibraryService(IPatchLibraryStorage storage, PatchDocumentSerializer serializer)
{
    public async Task<string[]> ListAsync() => (await storage.ListNamesAsync()).Order(StringComparer.OrdinalIgnoreCase).ToArray();

    public Task SaveAsync(string name, PatchDocument document, bool overwrite = false) =>
        storage.WriteAsync(ValidateName(name), serializer.Serialize(document), overwrite);

    public async Task<PatchDocument> LoadAsync(string name) =>
        serializer.Deserialize(await storage.ReadAsync(ValidateName(name)) ?? throw new InvalidOperationException("Saved patch no longer exists."));

    public Task DeleteAsync(string name) => storage.DeleteAsync(ValidateName(name));

    public Task ExportAsync(PatchDocument document) => storage.DownloadAsync("orbit2-patch.json", serializer.Serialize(document));

    public PatchDocument Import(string json) => serializer.Deserialize(json);

    private static string ValidateName(string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 80 || name.Any(char.IsControl))
            throw new ArgumentException("Enter a patch name of 1–80 characters.");
        return name;
    }
}
