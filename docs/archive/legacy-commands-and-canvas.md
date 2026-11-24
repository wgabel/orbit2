# Historical implementation record — commands and canvas

> Superseded 2026-11-23 README implementation notes, regrouped without changing their technical claims. These are **historical**, not current feature status. Go to the [archive index](legacy-readme.md), [roadmap](../roadmap/overview.md) and [feature contracts](../README.md) before implementing.

## Graph commands and committed events (Phase 3 foundation)

The application layer owns graph edits through `IPatchSession`. Each session owns one root graph, its descendants, and one notification dispatcher. Neither views nor processing components mutate the graph directly. Domain validation still occurs inside `SignalGraph`; no second set of graph rules is introduced.

~~~mermaid
sequenceDiagram
    participant Caller as Editor / patch builder
    participant Session as PatchSession
    participant Domain as SignalGraph
    participant Listeners as Subscribers
    Caller->>Session: Execute graph command
    Session->>Domain: Validate and apply edit
    Domain-->>Session: Success
    Session->>Session: Capture snapshot and revision
    Session->>Listeners: Publish one committed change set
    Session-->>Caller: Result and listener errors
~~~

| Command | Successful event |
| --- | --- |
| `AddNodeCommand` | `NodeAdded` |
| `ConnectNodesCommand` | `ConnectionCreated` |
| `RemoveConnectionCommand` | `ConnectionRemoved` |
| `SetConnectionAmountCommand` | `ConnectionAmountChanged` (same edge identity, before and after coefficients) |
| `RemoveNodeCommand` | All incident `ConnectionRemoved` facts, then `NodeRemoved`, in one change set |
| `SetParameterValueCommand` | `ParameterChanged` (not topology) |

Example: two independent listeners receive the same successful edit.

~~~csharp
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Events;
using Orbit.Application.Graphs.Sessions;
using Orbit.Domain.Nodes.Generators;

using IPatchSession session = new PatchSession(BuiltInNodes.CreateRegistry());
using var graphListener = session.Subscribe(change =>
    Console.WriteLine($"Revision {change.Revision}, {change.Snapshot.Nodes.Count} nodes"));
using var nodeListener = session.Subscribe<NodeAdded>(change =>
    Console.WriteLine($"Added {change.Node.Name}"));

var result = session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType));
~~~

- The result includes the immutable committed root snapshot and ordered events. `GraphChangeSet.GraphId` identifies the command target; `ChangedGraph` returns its local snapshot. One revision identifies the entire patch, even after future edits.
- Rejected commands and unchanged base-value edits do not increment the revision or publish anything. Validation exceptions are returned to the caller through the existing exception contract.
- Subscriptions are disposable, session-scoped and optionally filtered by the command or affected fact's graph ID. A boundary edit publishes one changeset to each affected graph, while typed subscriptions receive only that graph's facts. Unfiltered subscribers see every commit exactly once. Delivery is synchronous and serialized; listeners should schedule asynchronous work and return quickly. A failed listener is reported in `GraphCommandResult.SubscriberErrors`, without rolling back the change or blocking other subscribers.
- Layout events and DSP telemetry are separate from graph commands. Audio and canvas projections will subscribe to committed changes when the dynamic editor/compiler are introduced.
- The existing fixed demo now constructs its graph with these commands and derives its playback request from the final snapshot. Its precompiled regression DSP remains independent of the live graph.

See [the architecture document](../core/commands-and-events.md#9-commands-events-and-transaction-boundaries) for the transaction boundary, event flow, and future nested-graph requirements.

## Canvas presentation state (Phase 3 foundation)

`Orbit.Presentation` is a plain .NET project for UI state and geometry. It references Application, not Blazor or infrastructure. Each future root or nested canvas owns a `CanvasStateService` instance observing the graph's committed revisions.

~~~mermaid
flowchart TD
    Session["PatchSession"] --> Snapshot["Committed graph snapshot"]
    Snapshot --> State["CanvasStateService"]
    Gesture["Layout gesture"] --> State
    State --> View["Immutable canvas snapshot"]
    View --> Renderer["Future graph canvas"]
    State --> Event["Canvas state notification"]
~~~

| Type | Responsibility |
| --- | --- |
| `CanvasPoint` | Finite world/screen coordinate at the service boundary |
| `CanvasViewport` | Pan, zoom, pointer-anchored coordinate conversion |
| `INodePlacementPolicy` / `GridNodePlacementPolicy` | Initial grid layout and visible-area placement for interactive node additions |
| `ICanvasStateService` / `CanvasStateService` | Reconcile committed graph edits; manage layout and selection |
| `CanvasStateSnapshot` | Immutable graph snapshot plus layout revision, positions, viewport and selection |
| `CanvasStateChange` | Local notification with the consistent final presentation snapshot |

A node movement, selection, pan or zoom increments only the layout revision. A graph commit updates the graph revision and reconciles deleted nodes/edges and newly added positions. Existing positions and viewport remain stable; a parameter change does not alter the layout revision. Graph and layout notification channels reuse the same generic `NotificationChannel<T>` implementation, but are separate instances with independent subscribers.

~~~csharp
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Sessions;
using Orbit.Domain.Nodes.Generators;
using Orbit.Presentation.Canvas;

using IPatchSession session = new PatchSession(BuiltInNodes.CreateRegistry());
using ICanvasStateService canvas = new CanvasStateService(session);
using var listener = canvas.Subscribe(change =>
    Console.WriteLine($"{change.Kind}, layout revision {change.Snapshot.LayoutRevision}"));

session.Execute(new AddNodeCommand(session.GraphId, OscillatorGenerator.NodeType));
var nodeId = canvas.Snapshot.Graph.Nodes.Single().Id;
canvas.MoveNode(nodeId, new CanvasPoint(200, 100));
canvas.PanBy(new CanvasPoint(-40, 80));
canvas.ZoomAt(2, new CanvasPoint(200, 100)); // 200%, anchored to pointer
~~~

The coordinate transform is `screen = world * zoom + pan`, so moving a node by a screen delta at 200% zoom changes its world position by half that delta. Zoom is clamped to 25–400%. Canvas instances have independent state. Use the presentation dispatcher thread for layout and graph edits observed by the canvas; previously captured immutable snapshots can be read by asynchronous work. Dispose each canvas to unsubscribe from the session.

This foundation is tested independently of Blazor. The first visible graph editor uses these snapshots and transforms, and fit-to-content uses measured node bounds.

## First visual graph editor (Phase 4)

The application opens a persistent editable patch session with the initial modulation topology (LFO → sine generator → gain → output). The editor uses the existing domain registry; add nodes, delete selected nodes or wires, and edit the base values using the shared ParameterSlider. These changes commit graph revisions, not audio events.

- Drag a node header to move it. Drag empty background or middle-drag a node header to pan.
- **Add** places the new node inside the current visible canvas, preferring an unoccupied area. It does not move other nodes or change the viewport; undo/redo restores the original placement. This also works in nested drone canvases and when zoomed in.
- Wheel zoom is anchored at the pointer; the toolbar shows the current zoom, zoom buttons, reset and fit-to-content.
- Begin from an output and drag to an input, or click the output then click an input. Escape cancels. The existing domain validation rejects incompatible, duplicate or cyclic edges.
- Nodes use role-colored frames and typed port shapes. A generator feeding only modulation inputs is visually an LFO; no different DSP type is required.
- SVG paths use cubic Bézier geometry shared by all connections, with endpoints measured from actual port markers. Click a wire to select it.
- The compact workspace shares one toolbar for zoom, node creation/deletion, playback and transport. Breadcrumbs sit beside the title; node/connection count and interaction hints are non-interactive canvas overlays. The canvas fills remaining viewport height, with a token-defined minimum. Playback remains owned by the root workspace while the keyed canvas changes on navigation.
- All static dimensions and colors are sourced from design tokens. Layout state is separate from graph state.

~~~mermaid
flowchart TD
    Workspace["GraphWorkspace"] --> Session["PatchSession"]
    Workspace --> State["CanvasStateService"]
    Session --> State
    State --> Renderer["GraphCanvas"]
    Renderer --> Components["NodeFrame / PortConnector / ConnectionLayer"]
    Renderer --> DOM["canvasDom.js · DOM only"]
    Renderer --> Workspace
~~~

The small DOM adapter measures port/node geometry, captures pointers and resolves a dropped input. It does not own graph state or compute audio. The C# presentation library provides viewport and Bézier geometry. The workspace translates edits into session commands.

The editor uses GraphPlaybackControls to play any supported committed patch. Edits update the playing revision automatically, either through live base controls or by crossfading a replacement worklet. The obsolete fixed proof UI has been removed.

### Editing connection amounts

Click a Bézier connection to select it and open the shared `ConnectionAmountEditor`. Enter a signed number and choose **Apply amount**. For direct parameter modulation the editor displays the target unit (Hz, normalized, ratio, etc.); for audio and intermediate boundary routes it displays a multiplier. Each route has its own coefficient. A zero amount silences its contribution without removing the wire; a negative amount inverts its signal. The source amplitude still determines the actual excursion.

~~~csharp
session.Execute(new SetConnectionAmountCommand(graphId, connectionId, 100));
~~~

For a unit-amplitude bipolar sine, depth 100 into a 440 Hz frequency gives 340–540 Hz; depth 0.2 into amplitude gives a ±0.2 contribution before clamping. At a composite boundary, amounts multiply along the route, and the final parameter's unit applies only at its input.

`SignalGraph.SetConnectionAmount` replaces the immutable edge in place, retaining ID, endpoints and order. The session commits one revision and publishes `ConnectionAmountChanged` with the before/after records. Invalid or unchanged values do not commit. The canvas retains the selection; range estimation and playback receive the new snapshot. As amounts are presently source literals, a reachable changed amount recompiles and crossfades while playing. Explicit Apply avoids recompiling each intermediate keystroke.

**Implemented core, Phase 6.19.5:** compact cable badges display committed amounts in destination units (e.g. `+100 Hz`, `−0.3`, `×0.5`), distinguish zero and dormant connections, and support per-canvas All/Modified/Selected/Off. Click a wire or badge to open its anchored amount editor; Apply commits the edit. Display changes are layout-only. Optional further zoom/overlap refinements and individual contribution insight remain design work. [PR #1](https://github.com/wgabel/orbit2/pull/1), [PR #2](https://github.com/wgabel/orbit2/pull/2), [PR #3](https://github.com/wgabel/orbit2/pull/3); [connection visualization](../editor/annotations-and-connections.md#connection-amount-badges-and-inline-editing-planned-phase-6195).
