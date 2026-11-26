using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Commands;

namespace Orbit.Application.Graphs.Sessions;

/// <summary>Bounded stacks of immutable committed snapshots. No mutable graph or runtime is retained.</summary>
internal sealed class PatchHistory
{
    private const int Capacity = 100;
    private readonly List<Entry> undo = [];
    private readonly List<Entry> redo = [];
    private PendingGesture? pending;

    internal sealed record Entry(GraphSnapshot Before, GraphSnapshot After, Guid[] AffectedGraphs);
    private sealed record GestureTarget(Guid GraphId, Guid NodeId, Guid ControlId, bool IsMacro);
    private sealed record PendingGesture(Guid Id, GestureTarget Target, IGraphCommand RestoreCommand,
        double InitialValue, double FinalValue, Entry Entry)
    {
        public bool HasNetChange => InitialValue != FinalValue;
    }

    public bool CanUndo => pending?.HasNetChange == true || undo.Count != 0;
    public bool CanRedo => pending?.HasNetChange != true && redo.Count != 0;

    public void RecordParameterGesture(Guid id, GraphSnapshot before, GraphSnapshot after, ParameterChanged change) =>
        RecordGesture(id, before, after, new(change.GraphId, change.Address.NodeId, change.Address.ParameterId, false),
            new SetParameterValueCommand(change.GraphId, change.Address, change.PreviousValue),
            change.PreviousValue, change.CurrentValue);

    public void RecordMacroGesture(Guid id, GraphSnapshot before, GraphSnapshot after, CompositeMacroValueChanged change) =>
        RecordGesture(id, before, after, new(change.GraphId, change.NodeId, change.MacroId, true),
            new SetCompositeMacroValueCommand(change.GraphId, change.NodeId, change.MacroId, change.PreviousValue),
            change.PreviousValue, change.CurrentValue);

    private void RecordGesture(Guid id, GraphSnapshot before, GraphSnapshot after, GestureTarget target,
        IGraphCommand restoreCommand, double previousValue, double currentValue)
    {
        if (pending is { } previous && previous.Id == id && previous.Target == target)
        {
            pending = previous with { FinalValue = currentValue, Entry = previous.Entry with { After = after } };
            return;
        }
        CompleteGesture();
        pending = new PendingGesture(id, target, restoreCommand, previousValue, currentValue,
            new Entry(before, after, [target.GraphId]));
    }

    public void CompleteGesture(Guid? id = null)
    {
        if (pending is not { } gesture || (id.HasValue && gesture.Id != id.Value)) return;
        pending = null;
        if (gesture.HasNetChange) Append(gesture.Entry);
    }

    public IGraphCommand? RestoreGesture(Guid id) => pending is { } gesture && gesture.Id == id
        ? gesture.RestoreCommand : null;

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
