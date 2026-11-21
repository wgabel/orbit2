# Commands, events and transaction boundaries

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [graph model](graph-model.md) · [composite nodes](../editor/composite-nodes.md) · [persistence and history](../editor/persistence-and-history.md).

## 9. Commands, events and transaction boundaries

### Why an event system?

The user suggested broadcasting node connection, disconnection and rack port changes to listeners. This is a good fit for application/domain events, but structural changes should be owned by the graph, not independent node listeners.

A connection belongs to the graph and involves two endpoints. Removing a boundary port may affect both a child graph and its parent. If nodes mutate each other by reacting to events, event ordering can cause inconsistent state, circular reactions, duplicate recompilations and hard-to-debug behavior.

### Event scope

| Scope | Example events |
|---|---|
| Node | ParameterChanged, NodeRenamed |
| Graph | NodeAdded, NodeRemoved, ConnectionCreated, ConnectionRemoved, ConnectionAmountChanged, PortAdded, PortRemoved, PortRenamed |
| Patch/session | GraphChanged, PatchLoaded, TransportStarted, TransportStopped |
| Layout/UI | NodeMoved, SelectionChanged, CanvasOpened, CanvasClosed |

Use one application event dispatcher per active patch session. Events identify the originating GraphId and NodeId where applicable. A subscriber may filter by graph or subscribe across the patch.

A domain object can record events while being changed, but it should not directly call audio or UI listeners. The application layer publishes notifications only after validating and committing the complete change.

### Commands and events

| Command (request) | Event (successful fact) |
|---|---|
| AddNode | NodeAdded |
| RemoveNode | NodeRemoved |
| ConnectNodes | ConnectionCreated |
| RemoveConnection | ConnectionRemoved |
| SetConnectionAmount | ConnectionAmountChanged |
| SetParameterValue | ParameterChanged |
| AddExposedPort | PortAdded |
| RemoveExposedPort | PortRemoved |
| RenameExposedPort | PortRenamed |
| MoveNode | NodeMoved (layout-only) |

Commands may fail validation. Events communicate committed state, not an instruction to complete that state. Do not use events as a replacement for clear application service methods or as a mechanism for hidden mutation cascades.

### Implemented contracts (Phase 3 foundation)

The application layer provides `IPatchSession` for one root graph and its owned descendants. It owns the mutable graph tree, a command handler and a private event dispatcher. The domain remains independent of observers and the UI. All edits use typed commands; the five initial commands in the preceding table are implemented with the `Command` suffix. Exposed-port and layout commands are planned.

~~~csharp
using IPatchSession session = new PatchSession(BuiltInNodes.CreateRegistry());

using var canvasSubscription = session.Subscribe(change =>
    Console.WriteLine($"Render revision {change.Revision}"), session.GraphId);
using var nodeSubscription = session.Subscribe<NodeAdded>(change =>
    Console.WriteLine($"Added {change.Node.Id}"));

GraphCommandResult result = session.Execute(
    new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType));
~~~

The read contract is an immutable `GraphSnapshot` with graph ID, revision, node/parameter snapshots and immutable connections. The published change set contains the patch ID, command target graph ID, final root snapshot, all ordered facts from the operation, and a derived `TopologyChanged` flag. `ChangedGraph` resolves the edited graph within that root snapshot. It never exposes the mutable graph or mutable domain parameter instances. A snapshot taken at revision 3 does not change at revision 4.

| Class / interface | Responsibility |
| --- | --- |
| `IGraphCommand` | Target graph ID and requested edit |
| `GraphCommandHandler` | Apply one validated domain operation, construct facts |
| `IPatchSession` / `PatchSession` | Own graph, serialize edits, commit each successful revision |
| `GraphSnapshot` | Stable consistent read model for views and compiler |
| `IGraphEvent` | Typed committed fact, includes graph ID |
| `GraphChangeSet` | One revision's snapshot and all ordered facts |
| `GraphChangeDispatcher` | Session-local notification channel (internal) |
| `GraphCommandResult` | Committed change set, or no change, and subscriber errors |

A rejected command throws the existing domain validation exception (or registry/parameter validation error) before mutation. It produces no revision and no event. Setting a parameter to its current base value is a no-op. Node removal is one atomic domain operation: it removes all incident connections, then publishes their original immutable edge records and the removed node snapshot in one committed revision. This preserves data useful to future undo/redo.

Subscribe to a whole `GraphChangeSet` for graph projection and audio graph compilation; subscribe to typed events for a specific fact (for example, `ConnectionRemoved`). Subscription filters can target one graph. Registration order and fact order are deterministic within each changeset; registration changes during notification apply to the next revision. Subscribers are independently disposable, and closing the session clears them. Listener callbacks are synchronous and must return quickly; they should schedule independent asynchronous work rather than perform reentrant graph mutations. A listener exception is recorded in the command result, not treated as a failed edit or rollback. Other listeners still receive the facts.

The transaction boundary is **one validated command on an owned graph** (root or descendant). A boundary command targets the child and atomically changes its immediate parent too. The patch session resolves the target under its lock, applies domain validation, captures the entire root graph and publishes a single shared patch revision. Unfiltered listeners see every commit once; changeset filters match the command target or any affected fact's graph ID. Typed event filters match only the local fact. Foreign and removed child graphs are rejected. General multi-command transactions remain future work.

The first application consumer is `ModulationDemoPatch`: it uses the session to construct the existing fixed graph and maps the final snapshot to a playback request. The canvas and dynamic playback consume immutable snapshots; live playback subscriptions will follow when runtime replacement is implemented.

### Example: connecting two nodes

~~~mermaid
sequenceDiagram
    participant UI as Canvas
    participant App as Command service
    participant Graph as SignalGraph
    participant Bus as Event dispatcher
    participant DSP as Audio graph service
    UI->>App: ConnectNodes command
    App->>Graph: Validate and add connection
    Graph-->>App: Validated mutation
    App->>App: Commit snapshot and revision
    App->>Bus: Publish committed change set
    Bus-->>UI: Refresh graph view
    Bus-->>DSP: Request topology update
~~~

### Example: removing an exposed input port

1. The user requests `RemoveBoundaryPortCommand` with child graph ID, outer direction and port ID.
2. Resolve the actual composite instance and shared boundary identity.
3. Find all parent edges connected to the composite's input and all internal edges connected to the rack's matching output.
4. Validate the candidate boundary before mutation. Removal policy is to disconnect all affected edges.
5. Atomically update parent/child graph and remove the boundary definition.
6. Validate the resulting patch; commit exactly one revision.
7. Publish `ConnectionRemoved` facts and two `BoundaryPortChanged` facts (local node ID and opposite socket directions, before/after definitions and indices) in one committed change set.
8. Update both canvas projections and schedule one DSP graph update.
9. Future undo can restore the captured port definitions and removed edge records as one edit.

~~~mermaid
flowchart TD
    A[Remove port command] --> V[Validate parent and child]
    V --> P[Remove parent edges]
    V --> I[Remove child edges]
    P --> R[Remove shared port]
    I --> R
    R --> C[Commit revision]
    C --> E[Publish change set]
    E --> U[Update views]
    E --> D[Schedule DSP update]
~~~

### Subscribers

| Service | Responsibility |
|---|---|
| CanvasStateService | Updates rendered nodes, ports and edges; filters by current graph. |
| AudioGraphService | Applies base parameter changes or requests a topology compile. |
| UndoRedoService | Records reversible commands and change sets, not audio samples. |
| PatchPersistenceService | Saves compatible graph and layout state; may debounce autosave. |
| GraphDiagnosticsService | Logs or reports relevant application changes and compile status. |

Notice that the graph has no direct dependencies on these subscribers. Undo/redo must use validated commands or equivalent transactions, not reverse DSP samples. Subscriber failure should not silently roll back an already committed graph; report runtime sync errors separately.

### Real-time signal versus event

| Application events | DSP signals |
|---|---|
| User actions, commands and state changes | Continuous real-time audio/modulation |
| C# application layer | Faust runtime / AudioWorklet |
| Occur when editing | Up to one value per frame per signal |
| May trigger a graph compile | Never trigger a compile simply because a waveform varies |

An LFO does not publish ParameterChanged 48,000 times a second. Its effective target values are calculated inside DSP. UI monitoring can receive throttled snapshots for metering/visualization; it is not the source of audio time.
