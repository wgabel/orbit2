using System.Text;
using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Application.Presets;

/// <summary>Serializes library edits, validates candidates and publishes only durable commits.</summary>
public sealed class NodePresetLibraryService(INodePresetStorage storage, NodePresetService catalog)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? expectedJson;
    public bool IsLoaded { get; private set; }

    public async Task LoadAsync()
    {
        await gate.WaitAsync();
        try
        {
            var json = await storage.LoadCustomAsync();
            if (IsLoaded && json == expectedJson) return;
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json ?? "[]"));
            catalog.ReplaceCustomPresets(NodePresetJsonReader.ReadCatalog(stream));
            expectedJson = json;
            IsLoaded = true;
        }
        finally { gate.Release(); }
    }

    public Task<NodePreset> SaveAsync(GraphNodeSnapshot node, string name, string? description = null) =>
        EditAsync(presets =>
        {
            var preset = Capture(node, Guid.NewGuid().ToString("D"), name, description);
            presets.Add(preset);
            return preset;
        });

    public Task<NodePreset> OverwriteAsync(string id, GraphNodeSnapshot node) => EditAsync(presets =>
    {
        var existing = Find(presets, id, node.TypeKey);
        var preset = Capture(node, id, existing.Name, existing.Description);
        presets[presets.IndexOf(existing)] = preset;
        return preset;
    });

    public Task<NodePreset> RenameAsync(string id, string nodeType, string name) => EditAsync(presets =>
    {
        var existing = Find(presets, id, nodeType);
        var preset = new NodePreset(existing.SchemaVersion, id, nodeType, ValidateName(name), NodePreset.CustomGroup,
            existing.Description, existing.Parameters);
        presets[presets.IndexOf(existing)] = preset;
        return preset;
    });

    public Task<NodePreset> DeleteAsync(string id, string nodeType) => EditAsync(presets =>
    {
        var existing = Find(presets, id, nodeType);
        presets.Remove(existing);
        return existing;
    });

    private async Task<NodePreset> EditAsync(Func<List<NodePreset>, NodePreset> edit)
    {
        await gate.WaitAsync();
        try
        {
            if (!IsLoaded) throw new InvalidOperationException("Load the custom preset library before editing it.");
            var presets = catalog.CustomPresets.ToList();
            var result = edit(presets);
            catalog.ValidateCustomPresets(presets);
            var json = NodePresetJsonWriter.WriteCatalog(presets);
            if (!await storage.ReplaceCustomAsync(expectedJson, json))
                throw new InvalidOperationException("The custom preset library changed in another tab. Reload the library and retry.");
            catalog.ReplaceCustomPresets(presets);
            expectedJson = json;
            return result;
        }
        finally { gate.Release(); }
    }

    private static NodePreset Find(List<NodePreset> presets, string id, string nodeType) =>
        presets.SingleOrDefault(preset => preset.Id == id && preset.NodeType == nodeType)
        ?? throw new InvalidOperationException("Select an existing custom preset for this node type.");

    private static NodePreset Capture(GraphNodeSnapshot node, string id, string name, string? description) =>
        new(NodePreset.CurrentVersion, id, node.TypeKey, ValidateName(name), NodePreset.CustomGroup,
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            node.Parameters.ToDictionary(parameter => parameter.Definition.Key, parameter => parameter.BaseValue, StringComparer.Ordinal));

    private static string ValidateName(string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 80 || name.Any(char.IsControl))
            throw new ArgumentException("Enter a preset name of 1–80 characters.");
        return name;
    }
}
