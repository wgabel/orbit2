using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Application.Graphs.Sessions;

/// <summary>The application boundary for editing a patch and observing committed changes.
/// All owned child graphs share the same command contract, root snapshot and patch revision.</summary>
public interface IPatchSession : IDisposable
{
    Guid PatchId { get; }
    /// <summary>Identity of the root graph; commands may also target descendants.</summary>
    Guid GraphId { get; }
    /// <summary>The entire committed patch, including all nested graphs.</summary>
    GraphSnapshot Snapshot { get; }

    /// <summary>An optional gesture identity groups consecutive edits to the same parameter in history only.</summary>
    GraphCommandResult Execute(IGraphCommand command, Guid? historyGestureId = null);
    void CompleteHistoryGesture(Guid? gestureId = null);
    bool CanUndo { get; }
    bool CanRedo { get; }
    GraphCommandResult Undo();
    GraphCommandResult Redo();

    /// <summary>Observe the entire patch by default, or only commits affecting the supplied graph.</summary>
    IDisposable Subscribe(Action<GraphChangeSet> handler, Guid? graphId = null);
    IDisposable Subscribe<TEvent>(Action<TEvent> handler, Guid? graphId = null)
        where TEvent : IGraphEvent;
}
