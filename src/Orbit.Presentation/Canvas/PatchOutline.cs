using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Presentation.Canvas;

public enum OutlineKind { Canvas, Composite, Node, Group, Note }

/// <summary>Graph-qualified identity; annotations and graph nodes have independent namespaces.</summary>
public readonly record struct OutlineTarget(Guid GraphId, OutlineKind Kind, Guid Id);

public sealed record OutlineEntry(OutlineTarget Target, string Title, string Detail, string Breadcrumb,
    string SearchText, Guid? ChildGraphId, IReadOnlyList<OutlineEntry> Children);

public sealed record OutlineRow(OutlineEntry Entry, int Depth);

/// <summary>Read-only index over committed graph and annotation snapshots, never a second topology.</summary>
public sealed class PatchOutline
{
    private readonly Dictionary<OutlineTarget, OutlineEntry> entries = [];
    public OutlineEntry Root { get; }
    public int Count => entries.Count;

    public PatchOutline(GraphSnapshot root, IEnumerable<CanvasStateSnapshot> layouts, string title)
    {
        var byGraph = layouts.Where(layout => layout.IsAvailable).ToDictionary(layout => layout.GraphId);
        title = string.IsNullOrWhiteSpace(title) ? "Patch" : title;
        Root = new OutlineEntry(new OutlineTarget(root.Id, OutlineKind.Canvas, root.Id), title,
            "Root canvas", title, title, null, BuildGraph(root, title));
        Index(Root);

        IReadOnlyList<OutlineEntry> BuildGraph(GraphSnapshot graph, string path)
        {
            byGraph.TryGetValue(graph.Id, out var layout);
            var nodes = graph.Nodes.ToDictionary(node => node.Id, node =>
            {
                var description = layout?.NodeDescriptions.GetValueOrDefault(node.Id);
                var name = string.IsNullOrWhiteSpace(description?.Alias) ? node.Name : description.Alias;
                var child = node.Composite?.Graph;
                return new OutlineEntry(new OutlineTarget(graph.Id, child is null ? OutlineKind.Node : OutlineKind.Composite, node.Id),
                    name, node.Name, path, $"{name} {node.Name} {node.TypeKey} {description?.Description}", child?.Id,
                    child is null ? [] : BuildGraph(child, $"{path} / {name}"));
            });
            var notes = (layout?.Notes ?? []).ToDictionary(note => note.Id, note =>
                new OutlineEntry(new OutlineTarget(graph.Id, OutlineKind.Note, note.Id), note.Title, "Note", path,
                    $"{note.Title} {note.Body}", null, []));
            var result = new List<OutlineEntry>();
            foreach (var group in layout?.VisualGroups ?? [])
            {
                var members = new List<OutlineEntry>();
                foreach (var id in group.NodeIds) if (nodes.Remove(id, out var node)) members.Add(node);
                foreach (var id in group.NoteIds) if (notes.Remove(id, out var note)) members.Add(note);
                result.Add(new OutlineEntry(new OutlineTarget(graph.Id, OutlineKind.Group, group.Id), group.Title,
                    $"Visual group · {members.Count} members", path, $"{group.Title} {group.Description}", null, members.AsReadOnly()));
            }
            result.AddRange(nodes.Values);
            result.AddRange(notes.Values);
            return result.AsReadOnly();
        }
    }

    private void Index(OutlineEntry entry)
    {
        entries.Add(entry.Target, entry);
        foreach (var child in entry.Children) Index(child);
    }

    public OutlineEntry? Find(OutlineTarget target) => entries.GetValueOrDefault(target);

    /// <summary>Browse only expanded branches; search returns graph-qualified matches independently of expansion.</summary>
    public IEnumerable<OutlineRow> Rows(IReadOnlySet<OutlineTarget> expanded, string query, OutlineKind? filter)
    {
        if (!string.IsNullOrWhiteSpace(query) || filter is not null)
        {
            var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var entry in entries.Values)
                if ((filter is null || entry.Target.Kind == filter) && words.All(word =>
                    entry.SearchText.Contains(word, StringComparison.OrdinalIgnoreCase) ||
                    entry.Breadcrumb.Contains(word, StringComparison.OrdinalIgnoreCase)))
                    yield return new OutlineRow(entry, 0);
            yield break;
        }
        foreach (var row in Browse(Root, 0)) yield return row;

        IEnumerable<OutlineRow> Browse(OutlineEntry entry, int depth)
        {
            yield return new OutlineRow(entry, depth);
            if (!expanded.Contains(entry.Target)) yield break;
            foreach (var child in entry.Children)
                foreach (var row in Browse(child, depth + 1)) yield return row;
        }
    }
}
