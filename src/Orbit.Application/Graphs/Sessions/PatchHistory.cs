using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Graphs.Events;
using Orbit.Domain.Parameters;

namespace Orbit.Application.Graphs.Sessions;

/// <summary>Bounded stacks of immutable committed snapshots. No mutable graph or runtime is retained.</summary>
internal sealed class PatchHistory
{
    private const int Capacity = 100;
    private readonly List<Entry> undo = [];
    private readonly List<Entry> redo = [];
    private PendingGesture? pending;

    internal sealed record Entry(GraphSnapshot Before, GraphSnapshot After, Guid[] AffectedGraphs);
    private sealed record PendingGesture(Guid Id, Guid GraphId, ParameterAddress Address,
        double InitialValue, double FinalValue, Entry Entry)
    {
        public bool HasNetChange => InitialValue != FinalValue;
    }

    public bool CanUndo => pending?.HasNetChange == true || undo.Count != 0;
    public bool CanRedo => pending?.HasNetChange != true && redo.Count != 0;

    public void RecordParameterGesture(Guid id, GraphSnapshot before, GraphSnapshot after, ParameterChanged change)
    {
        if (pending is { } previous && previous.Id == id && previous.GraphId == change.GraphId && previous.Address == change.Address)
        {
            pending = previous with { FinalValue = change.CurrentValue, Entry = previous.Entry with { After = after } };
            return;
        }
        CompleteGesture();
        pending = new PendingGesture(id, change.GraphId, change.Address, change.PreviousValue, change.CurrentValue,
            new Entry(before, after, [change.GraphId]));
    }

    public void CompleteGesture(Guid? id = null)
    {
        if (pending is not { } gesture || (id.HasValue && gesture.Id != id.Value)) return;
        pending = null;
        if (gesture.HasNetChange) Append(gesture.Entry);
    }

    public void Record(GraphSnapshot before, GraphSnapshot after, Guid[] affectedGraphs)
    {
        CompleteGesture();
        Append(new Entry(before, after, affectedGraphs));
    }

    private void Append(Entry entry)
    {
        undo.Add(entry);
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

    public void Clear() { pending = null; undo.Clear(); redo.Clear(); }
}
