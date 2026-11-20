using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Snapshots;

namespace Orbit.Application.Graphs.Sessions;

/// <summary>The application boundary for editing a patch and observing committed changes.
/// This initial version owns one root graph; nested graphs will use the same contracts.</summary>
public interface IPatchSession : IDisposable
{
    Guid PatchId { get; }
    Guid GraphId { get; }
    GraphSnapshot Snapshot { get; }

    GraphCommandResult Execute(IGraphCommand command);

    IDisposable Subscribe(Action<GraphChangeSet> handler, Guid? graphId = null);
    IDisposable Subscribe<TEvent>(Action<TEvent> handler, Guid? graphId = null)
        where TEvent : IGraphEvent;
}
