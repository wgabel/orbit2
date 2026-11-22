namespace Orbit.Application.Persistence;

/// <summary>A lightweight, searchable view of one named save; unreadable entries remain deletable.</summary>
public sealed record PatchLibraryEntry(string Name, SavedPatchOverview Overview, bool MetadataUnavailable = false)
{
    public bool Matches(string query, string tag)
    {
        if (tag.Length > 0 && !Overview.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) return false;
        var fields = new[] { Name, Overview.Title, Overview.Description }.Concat(Overview.Tags).ToArray();
        return query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .All(term => fields.Any(field => field.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }
}
