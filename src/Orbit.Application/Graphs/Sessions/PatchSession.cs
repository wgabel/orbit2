using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;

namespace Orbit.Application.Graphs.Sessions;

/// <summary>
/// Owns the editable root and its child graphs, with a single event channel. Consumers see only
/// immutable committed snapshots, never the mutable SignalGraph instance.
/// Commands and notifications are serialized so revision order is deterministic.
/// </summary>
public sealed class PatchSession : IPatchSession
{
    private readonly object gate = new();
    private readonly SignalGraph graph;
    private readonly GraphCommandHandler commands;
    private readonly GraphChangeDispatcher dispatcher = new();

    private GraphSnapshot snapshot;
    private bool executing;
    private bool disposed;

    public PatchSession(NodeRegistry registry) : this(registry, new SignalGraph(), Guid.NewGuid()) { }

    internal PatchSession(NodeRegistry registry, SignalGraph graph, Guid patchId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (patchId == Guid.Empty) throw new ArgumentException("Patch ID cannot be empty.", nameof(patchId));
        this.graph = graph;
        PatchId = patchId;
        commands = new GraphCommandHandler(registry);
        snapshot = GraphSnapshot.Capture(graph, 0);
    }

    public Guid PatchId { get; }
    public Guid GraphId => graph.Id;

    public GraphSnapshot Snapshot
    {
        get
        {
            lock (gate)
                return snapshot;
        }
    }

    public GraphCommandResult Execute(IGraphCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (executing)
                throw new InvalidOperationException("A graph edit cannot run from inside a change callback.");
            // Resolve from the owned graph tree under the same session lock.
            // Foreign or removed child graph IDs cannot receive commands.
            var targetGraph = graph.GetGraph(command.GraphId);

            executing = true;
            try
            {
                var nextRevision = checked(snapshot.Revision + 1);
                // Validation happens in the domain before each mutation. A
                // rejected command leaves the revision and snapshots untouched.
                var events = commands.Execute(targetGraph, command, nextRevision);
                if (events.Count == 0)
                    return new GraphCommandResult(null, Array.Empty<Exception>());

                snapshot = GraphSnapshot.Capture(graph, nextRevision);
                var changeSet = new GraphChangeSet(PatchId, targetGraph.Id, snapshot, Array.AsReadOnly(events.ToArray()));
                // Commit precedes delivery. Subscribers should return quickly and
                // schedule asynchronous projection or compilation independently.
                var errors = dispatcher.Publish(changeSet);
                return new GraphCommandResult(changeSet, errors);
            }
            finally
            {
                executing = false;
            }
        }
    }

    public IDisposable Subscribe(Action<GraphChangeSet> handler, Guid? graphId = null)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return dispatcher.Subscribe(handler, graphId);
        }
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler, Guid? graphId = null)
        where TEvent : IGraphEvent
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return dispatcher.Subscribe(handler, graphId);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            if (executing)
                throw new InvalidOperationException("A session cannot be disposed from inside a change callback.");

            disposed = true;
            dispatcher.Clear();
        }
    }
}
