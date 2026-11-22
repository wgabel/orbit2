namespace Orbit.Application.Persistence;

/// <summary>Patch-wide descriptive metadata, independent of transport and graph state.</summary>
public sealed record SavedPatchOverview(string Title, string Description, string Instructions, IReadOnlyList<string> Tags)
{
    public const int MaxTitleLength = 120;
    public const int MaxTextLength = 16000;
    public const int MaxTags = 24;
    public const int MaxTagLength = 40;
    public static SavedPatchOverview Empty { get; } = new("", "", "", Array.Empty<string>());

    public static SavedPatchOverview Create(string title, string description, string instructions, IEnumerable<string> tags) =>
        Validate(new SavedPatchOverview(title.Trim(), description, instructions,
            tags.Select(tag => tag.Trim()).Where(tag => tag.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));

    public static SavedPatchOverview Validate(SavedPatchOverview? overview)
    {
        if (overview is null || overview.Title is null || overview.Title.Length > MaxTitleLength ||
            overview.Description is null || overview.Description.Length > MaxTextLength ||
            overview.Instructions is null || overview.Instructions.Length > MaxTextLength ||
            overview.Tags is null || overview.Tags.Count > MaxTags)
            throw new ArgumentException("Invalid patch overview or metadata limit exceeded.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in overview.Tags)
            if (tag is null || tag.Length is < 1 or > MaxTagLength || tag != tag.Trim() ||
                tag.Any(char.IsControl) || tag.Contains(',') || !seen.Add(tag))
                throw new ArgumentException("Tags must be unique, trimmed, 1–40 characters and contain no commas or control characters.");
        return overview with { Tags = Array.AsReadOnly(overview.Tags.ToArray()) };
    }
}
