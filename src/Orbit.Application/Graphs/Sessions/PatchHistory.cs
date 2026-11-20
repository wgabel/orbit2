using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Application.Graphs.Sessions;

/// <summary>Bounded stacks of immutable committed snapshots. No mutable graph or runtime is retained.</summary>
internal sealed class PatchHistory
{
    private const int Capacity = 100;
    private readonly List<Entry> undo = [];
    private readonly List<Entry> redo = [];

    internal sealed record Entry(GraphSnapshot Before, GraphSnapshot After, Guid[] AffectedGraphs);
    public bool CanUndo => undo.Count != 0;
    public bool CanRedo => redo.Count != 0;

    public void Record(GraphSnapshot before, GraphSnapshot after, Guid[] affectedGraphs)
    {
        undo.Add(new Entry(before, after, affectedGraphs));
        if (undo.Count > Capacity) undo.RemoveAt(0);
        redo.Clear();
    }

    public Entry? Peek(bool isUndo)
    {
        var stack = isUndo ? undo : redo;
        return stack.Count == 0 ? null : stack[^1];
    }

    public void Move(bool isUndo)
    {
        var source = isUndo ? undo : redo;
        var target = isUndo ? redo : undo;
        target.Add(source[^1]);
        source.RemoveAt(source.Count - 1);
    }

    public void Clear() { undo.Clear(); redo.Clear(); }
}
