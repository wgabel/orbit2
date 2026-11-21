using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;
using Orbit.Application.Persistence;

namespace Orbit.Application.Graphs.Sessions;

/// <summary>
/// Owns the editable root and its child graphs, with a single event channel. Consumers see only
/// immutable committed snapshots, never the mutable SignalGraph instance.
/// Commands and notifications are serialized so revision order is deterministic.
/// </summary>
public sealed class PatchSession : IPatchSession
{
    private readonly object gate = new();
    private SignalGraph graph;
    private readonly NodeRegistry registry;
    private readonly PatchHistory history = new();
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
        this.registry = registry;
        PatchId = patchId;
        commands = new GraphCommandHandler(registry);
        snapshot = GraphSnapshot.Capture(graph, 0);
    }

    public Guid PatchId { get; }
    public Guid GraphId => graph.Id;
    public bool CanUndo { get { lock (gate) return !disposed && history.CanUndo; } }
    public bool CanRedo { get { lock (gate) return !disposed && history.CanRedo; } }

    public GraphSnapshot Snapshot
    {
        get
        {
            lock (gate)
                return snapshot;
        }
    }

    public GraphCommandResult Execute(IGraphCommand command, Guid? historyGestureId = null)
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

                var before = snapshot;
                snapshot = GraphSnapshot.Capture(graph, nextRevision);
                if (command is SetParameterValueCommand && historyGestureId is Guid gestureId &&
                    events.Count == 1 && events[0] is ParameterChanged parameter)
                    history.RecordParameterGesture(gestureId, before, snapshot, parameter);
                else
                    history.Record(before, snapshot, events.Select(fact => fact.GraphId).Append(targetGraph.Id).Distinct().ToArray());
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

    public GraphCommandResult Undo() => RestoreHistory(isUndo: true);
    public GraphCommandResult Redo() => RestoreHistory(isUndo: false);

    public void CompleteHistoryGesture(Guid? gestureId = null)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (executing) throw new InvalidOperationException("History cannot change inside a graph notification.");
            history.CompleteGesture(gestureId);
        }
    }

    internal void ClearHistory() { lock (gate) history.Clear(); }

    private GraphCommandResult RestoreHistory(bool isUndo)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (executing) throw new InvalidOperationException("History cannot change inside a graph notification.");
            history.CompleteGesture();
            if (history.Peek(isUndo) is not { } entry) return new GraphCommandResult(null, Array.Empty<Exception>());
            executing = true;
            try
            {
                var before = snapshot;
                var target = isUndo ? entry.Before : entry.After;
                var restored = PatchDocumentSerializer.RestoreSnapshot(target, registry);
                var next = GraphSnapshot.Capture(restored, checked(snapshot.Revision + 1));
                var affected = entry.AffectedGraphs.Concat(GraphIds(before).Except(GraphIds(next)))
                    .Concat(GraphIds(next).Except(GraphIds(before))).Distinct();
                var facts = affected.Select(id => (IGraphEvent)new GraphHistoryRestored(id, before.FindGraph(id), next.FindGraph(id), isUndo)).ToArray();
                graph = restored;
                snapshot = next;
                history.Move(isUndo);
                var change = new GraphChangeSet(PatchId, graph.Id, next, Array.AsReadOnly(facts));
                return new GraphCommandResult(change, dispatcher.Publish(change));
            }
            finally { executing = false; }
        }
    }

    private static IEnumerable<Guid> GraphIds(GraphSnapshot graph)
    {
        yield return graph.Id;
        foreach (var node in graph.Nodes)
            if (node.Composite is { } composite)
                foreach (var id in GraphIds(composite.Graph)) yield return id;
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
            history.Clear();
            dispatcher.Clear();
        }
    }
}
