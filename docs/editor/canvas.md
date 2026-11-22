# Canvas and editor state

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [composite nodes](composite-nodes.md) · [design system](design-system.md) · [patch annotations](patch-annotations.md) · [connection visualization](annotations-and-connections.md) · [persistence and history](persistence-and-history.md).

### The canvases

A main patch might contain:

~~~mermaid
flowchart LR
    L[LFO] --> A[Drone A]
    L --> B[Drone B]
    A --> M[Master mixer]
    B --> M
    M --> O[Output]
~~~

Opening Drone A reveals its own graph:

~~~mermaid
flowchart TD
    S[Sine] --> M[Mixer]
    T[Triangle] --> M
    N[Noise] --> M
    L[Internal LFO] --> F[Filter cutoff]
    M --> LP[Low-pass]
    F --> LP
    LP --> R[Reverb]
    R --> G[Gain]
    G --> O[Output rack]
~~~

The same canvas UI, interaction rules, connection rendering and node registry should be used for both root and nested graphs.

**Canvas and entire-patch counts (in review, [PR #36](https://github.com/wgabel/orbit2/pull/36)).** The upper-right overlay preserves the active canvas's node/connection counts (and local notes, if present), and adds a separately labelled entire-patch total. The total recursively sums each committed graph's own nodes and connections, starting at the root, including composite wrappers, internal input/output rack nodes and dormant connections. Visual groups, notes and mirrored sockets are not additional graph elements. For example, a root with one drone wrapper and a four-node, four-connection child shows one local node and five total nodes. `CanvasNavigationService` calculates this projection once at initialization and per committed patch change, so it stays consistent across navigation, nested edits, deletion, undo/redo and patch replacement without traversing the hierarchy on pan, zoom or telemetry updates. The counts do not change audio, commands, or persistence.

### Separation from visual layout

| Object | Responsibility |
|---|---|
| SignalGraph | DSP-relevant topology, node configuration and connections. |
| CanvasLayout | Positions, size, collapsed state, visual grouping and appearance. |
| CanvasNavigation | Current graph path, parent navigation, breadcrumb and selection. |
| PatchDocument | Root graph, nested graphs, layout data, metadata and schema version. |

Moving a node or changing zoom/selection must not recompile DSP. Opening or closing a subcanvas must not reset any oscillator or interrupt audio. A graph edit can affect audio; a purely visual edit must not.

The UI shows a breadcrumb such as Patch / Drone / Drone. `CanvasNavigationService` uses stable graph IDs to resolve the currently edited graph, caches each visited canvas projection and exposes an immutable breadcrumb entry per level. It releases removed subtree projections and returns to the closest surviving ancestor. `GraphCanvas` is keyed by graph ID to reset transient gestures on navigation; it fits only on first visit, preserving the stored viewport on return.

The editor catalog includes `DroneTemplate`, a composite factory with independent Pitch and Gain modulation inputs and Audio output, an internal sine and gain stage, and all rack connections. It is an ordinary editable graph, not an opaque DSP processor. Racks cannot be deleted independently; the master output is available only in the root catalog.

~~~mermaid
flowchart TD
    Workspace["GraphWorkspace"] --> Nav["CanvasNavigationService"]
    Nav --> Canvas["Active CanvasStateService"]
    Workspace --> Root["Root PatchSession snapshot"]
    Root --> Playback["GraphPlaybackControls"]
    Canvas --> Editor["Reusable GraphCanvas"]
    Root --> Ranges["Graph modulation ranges"]
    Ranges --> Editor
~~~

UI gestures dispatch commands using the active graph ID. `GraphPlaybackControls` stays attached to the root snapshot across navigation, so the same audio voice and revision remain active. The range and telemetry projections select the active graph using its ID while retaining the root revision for correctness.

### Implemented canvas state foundation

The presentation-state library is `Orbit.Presentation` (a plain .NET class library). It references Application and has no dependency on Blazor, browser APIs or processing. Each canvas/subcanvas can own one `ICanvasStateService` for its graph. Phase 5 adds graph-ID-targeted projections in a shared session with independent layouts and viewports.

| State | Owner | Change notification |
| --- | --- | --- |
| Nodes, ports, base parameters and routing | PatchSession / SignalGraph | Committed graph revision |
| World-coordinate node positions, selection and viewport | CanvasStateService | Local canvas state change |
| Effective audio parameter values | DSP runtime | Throttled meter snapshot |

`CanvasStateService` subscribes to unfiltered patch `GraphChangeSet` notifications and resolves its own graph from the final immutable root snapshot. A configurable `INodePlacementPolicy` supplies initial positions (default: a three-column grid). Existing positions are preserved, removed nodes and their incident connections are cleared from selection, and each active canvas emits one update per patch commit. New placements and selection cleanup increment the layout revision; a parameter-only graph change does not. Removing an ancestor composite invalidates descendant canvases, which emit `GraphRemoved` and reject local edits while retaining their last read-only snapshot for navigation.

**Interactive node placement.** The shared canvas measures its current visible screen rectangle (including page clipping) and existing frame sizes at Add time. `CanvasViewport.ScreenToWorld` converts that rectangle to world bounds; `GridNodePlacementPolicy.FindVisiblePosition` searches a bounded set of positions, including nearby measured frame edges, minimizing overlap for the measured shared frame width and estimated grid-slot height. It prefers a fully visible placement and then the center when overlap is equal. If a whole frame cannot fit without overlap, it prefers an unobstructed visible header even if the lower frame extends below the viewport; overlap is allowed when no free spot exists. Initial/unmeasured layouts still use the deterministic origin grid. The active canvas reserves the proposed placement only for the synchronous `AddNodeCommand`; reconciliation consumes it for the matching `NodeAdded` fact, publishing the committed graph and position together. A rejected command or stale canvas measurement leaves no pending placement. Undo/redo uses existing removed-position restoration, and ordinary persistence stores the resulting world coordinates, not measurements or reservations. No browser geometry enters Domain, graph commands or audio; existing positions and viewport stay unchanged.

~~~mermaid
sequenceDiagram
    participant W as Workspace
    participant C as Canvas
    participant L as Layout state
    participant S as Patch session
    W->>C: Measure visible area
    C->>L: Reserve world position
    W->>S: AddNodeCommand
    S->>L: Committed NodeAdded
    L-->>C: Snapshot with new position
~~~

~~~mermaid
flowchart TD
    Command["Graph command"] --> Commit["PatchSession commit"]
    Commit --> Graph["Graph snapshot"]
    Graph --> Canvas["CanvasStateService"]
    Layout["Pan / zoom / move / select"] --> Canvas
    Canvas --> Read["CanvasStateSnapshot"]
    Read --> View["GraphCanvas renderer"]
    Commit --> Audio["Future audio graph subscriber"]
~~~

`CanvasViewport` is an immutable transform with a finite pan and zoom between 0.25 and 4. It is independent of browser input and CSS. It provides world-to-screen, screen-to-world, screen-delta-to-world and pointer-anchored zoom operations. Pointer positions are viewport-relative. A drag delta is divided by zoom before changing a node's world position, while panning is measured in screen pixels. Reset returns to zero pan and 100% zoom. Fit-to-content is a renderer concern once measured node bounds are available.

Each view consumes an immutable `CanvasStateSnapshot` with separate graph and layout revision numbers, world positions, selected node/connection IDs and viewport. Layout changes never call `IPatchSession.Execute` or publish graph events. Graph and canvas dispatchers share the generic disposable `NotificationChannel<T>` implementation but remain separate notification instances. Listeners are notified after state commits; their failures do not undo state. Use the presentation dispatcher thread for interactive canvas operations and graph commands observed by the canvas; immutable snapshots may be handed to asynchronous processing. Consumers must dispose canvas subscriptions and the service when closing the corresponding canvas.

The geometry and projection are implemented and independently tested. The renderer consumes this state, including measured node bounds, pointer gestures and Bézier paths. Phase 5 implements nested navigation using the same renderer and a cached state per graph.

Phase 6.19.1 (implemented) extends the same canvas projection with graph-scoped note cards, a separate bounded history, measured placement/fit and persistent authored bounds. Phase 6.19.2 (implemented) extends that history and projection to node aliases/descriptions and connection labels, remeasuring node geometry when aliases change. Phase 6.19.3 (implemented) adds graph-local visual groups behind cables, nodes and notes. Drag previews translate the frame, explicit member positions and cable endpoints together, committing one annotation operation; resize affects only the frame. Phase 6.19.4 (implemented) adds patch-wide overview outside the canvas projection. Phase 6.19.7 (in review) derives compact annotation detail from zoom and per-canvas visibility, with temporary reveal and signal-only fit; authored geometry remains unchanged. See [patch annotations](patch-annotations.md) for the user interaction and data contract.

### First visible editor implementation

`GraphWorkspace` constructs the root patch session from `ModulationDemoPatch.OpenSession`, owns one presentation state service, and translates completed user actions into typed commands. `GraphCanvas` consumes that state and is reusable for future child graphs. It composes `CanvasToolbar`, `NodeFrame`, `PortConnector`, `ConnectionLayer`, `ConnectionPath` and the existing `ParameterSlider` inside node frames. There is no per-node renderer duplication.

The canvas world is one transformed coordinate space shared by HTML node frames and SVG Bézier paths. The browser adapter (`wwwroot/js/canvasDom.js`) only measures actual DOM bounds/port centers, captures a pointer, and identifies the input under a dropped wire. The renderer converts measured offsets back into world coordinates, so zoom and panning do not change the topology or require new measurements. Nodes move using the state service and connections follow their positions. Fit uses measured frame bounds; `BezierConnection.Between` supplies horizontal tangents even for backwards edges.

The editor supports node addition/removal, base parameter edits, drag-to-connect or keyboard/click port activation, selection, background panning, middle-button panning from node headers, pointer zoom and fit, and nested canvas navigation. Space-drag remains a future enhancement. The fixed modulation proof is preserved separately from the dynamic Faust compiler.
