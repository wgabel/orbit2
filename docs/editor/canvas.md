# Canvas and editor state

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [composite nodes](composite-nodes.md) · [design system](design-system.md) · [patch annotations](patch-annotations.md) · [connection visualization](annotations-and-connections.md) · [persistence and history](persistence-and-history.md).

For hands-on navigation, connections, annotation controls and local/recursive counts, see the [patch editor guide](../usage/patch-editor.md).

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

**Canvas and entire-patch counts (implemented, [PR #36](https://github.com/wgabel/orbit2/pull/36)).** The upper-right overlay preserves the active canvas's node/connection counts (and local notes, if present), and adds a separately labelled entire-patch total. The total recursively sums each committed graph's own nodes and connections, starting at the root, including composite wrappers, internal input/output rack nodes and dormant connections. Visual groups, notes and mirrored sockets are not additional graph elements. For example, a root with one drone wrapper and a four-node, four-connection child shows one local node and five total nodes. `CanvasNavigationService` calculates this projection once at initialization and per committed patch change, so it stays consistent across navigation, nested edits, deletion, undo/redo and patch replacement without traversing the hierarchy on pan, zoom or telemetry updates. The counts do not change audio, commands, or persistence.

### Canvas-first responsive workspace (6.21.1–6.21.3 implemented)

The [shared piano dock](../instruments/pitch-control.md#phase-723--note-source-in-review) ([PR #70](https://github.com/wgabel/orbit2/pull/70), in review) is a reserved responsive row between canvas and mobile navigation, not a modal overlay. The selected keyboard target is independent of canvas selection and remains available when entering a composite; the dock collapses to a compact header without moving or obscuring the navigation controls. Canvas gestures use the remaining canvas row. F fits the graph when the canvas itself has focus; when the piano has focus, F plays the mapped note.

[PR #60](https://github.com/wgabel/orbit2/pull/60) introduces a CSS-responsive shell for the same keyed `GraphCanvas` and root-owned `GraphPlaybackControls` instances. Two 44 CSS-pixel rows contain compact path, BPM, playback and bottom navigation; zoom floats at an edge. The existing tool groups are shown on demand and remain mounted so drafts and playback state do not depend on viewport changes. The mobile View panel contains cable labels, annotation visibility, counts and scope; Nodes opens the shared visual library. [PR #61](https://github.com/wgabel/orbit2/pull/61) moves selection, marquee, grouping, descriptions and delete into Edit; contextual actions appear only when applicable. Named mobile sheets provide close controls, Escape/Back and focus restoration through the DOM-only workspacePanels adapter; pending patch operations and overview drafts prevent dismissal. No graph or playback ownership is moved into the adapter. Desktop retains its toolbars. [PR #62](https://github.com/wgabel/orbit2/pull/62) replaces the node dropdown on both layouts with the shared NodeLibrary; it projects registered factories into a searchable presentation catalog using stable keys. Favorites/recent are local editor preferences, not graph/session data. Library search/filter and add/favorite actions remain disabled until these preferences finish loading; web-event bindings are provided by the Blazor Web namespace. The existing add command and visible placement reservation remain the only way to add a DSP node; canvas notes retain their annotation command path. On mobile, a tap directly on the cable hit path or its amount badge selects the existing connection; the Edit sheet's Delete selected command removes the selected graph connection and Undo restores it. The amount editor's Cancel/Close clears selection, so open Edit without canceling. [PR #63](https://github.com/wgabel/orbit2/pull/63) adds published-browser touch coverage for deletion with labels off and for undo, checking that other wires and nodes remain intact. At coarse-pointer sizes the invisible cable hit stroke uses a screen-space 24px target; actual line width and node geometry are unchanged. Cable paths are also focusable and support Enter/Space. See [usage](../usage/patch-editor.md#delete-a-connection-on-mobile). The responsive layout uses dynamic viewport height and safe-area insets, with no new graph commands or persistence schema. Follow the [6.21 roadmap](../roadmap/editor-and-effects.md#phase-621--canvas-first-workspace-and-node-library-planned) for the remaining touch, compact-node and fullscreen work.

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

Phase 6.19.8 extends navigation with `JumpToGraph` for any existing graph and a cached searchable outline. The workspace resolves stable targets and the keyed canvas performs measured selection focus after rendering. See [patch outline](patch-annotations.md#3-patch-outline-and-navigation-6198--implemented).

Phase 7.4.1 (implemented) extends the graph-local selection with measured marquee hit testing and accessible bulk actions. See [module selection](reusable-modules.md#implementation-multi-node-selection-741-in-review). Rectangle preview is transient; only the final eligible node IDs commit once to presentation state. A canceled/stale gesture does not modify the graph, positions or viewport.

Phase 6.19.1 (implemented) extends the same canvas projection with graph-scoped note cards, a separate bounded history, measured placement/fit and persistent authored bounds. Phase 6.19.2 (implemented) extends that history and projection to node aliases/descriptions and connection labels, remeasuring node geometry when aliases change. Phase 6.19.3 (implemented) adds graph-local visual groups behind cables, nodes and notes. Drag previews translate the frame, explicit member positions and cable endpoints together, committing one annotation operation; resize affects only the frame. Phase 6.19.4 (implemented) adds patch-wide overview outside the canvas projection. Phase 6.19.7 (implemented) derives compact annotation detail from zoom and per-canvas visibility, with temporary reveal and signal-only fit; authored geometry remains unchanged. See [patch annotations](patch-annotations.md) for the user interaction and data contract.

### First visible editor implementation

`GraphWorkspace` constructs the root patch session from `ModulationDemoPatch.OpenSession`, owns one presentation state service, and translates completed user actions into typed commands. `GraphCanvas` consumes that state and is reusable for future child graphs. It composes `NodeFrame`, `PortConnector`, `ConnectionLayer`, `ConnectionPath` and the existing `ParameterSlider` inside node frames. There is no per-node renderer duplication.

The canvas world is one transformed coordinate space shared by HTML node frames and SVG Bézier paths. The browser adapter (`wwwroot/js/canvasDom.js`) only measures actual DOM bounds/port centers, captures a pointer, and identifies the input under a dropped wire. The renderer converts measured offsets back into world coordinates, so zoom and panning do not change the topology or require new measurements. Nodes move using the state service and connections follow their positions. Fit uses measured frame bounds; `BezierConnection.Between` supplies horizontal tangents even for backwards edges.

The editor supports node addition/removal, base parameter edits, drag-to-connect or keyboard/click port activation, selection, background panning, middle-button panning from node headers, pointer zoom and fit, and nested canvas navigation. Space-drag remains a future enhancement. The fixed modulation proof is preserved separately from the dynamic Faust compiler.

### Gesture-only canvas navigation

**Phase 6.21.4, implemented ([PR #69](https://github.com/wgabel/orbit2/pull/69)).** Remove the floating/mobile and desktop zoom toolbar, including the percentage, reset and Fit buttons. Keep wheel zoom anchored at its pointer. Two captured touch pointers promote an active canvas gesture into a pinch; cancel wire/annotation previews and restore a dragged node's original position before takeover. Noninteractive node surfaces support touch panning too; native controls keep their interactions. Each change computes the two-finger separation ratio from the gesture baseline and maps the initial midpoint's world point to the current midpoint (preventing zoom ratcheting at either limit), clamping zoom at 25–400% and committing a single viewport transform. Releasing one finger sets the remaining finger's last position as the pan baseline; cancellation resets the gesture. This is presentation-only and uses the same state on every graph level.

Fit uses three completed primary taps/clicks on empty canvas within 600 ms and 24 CSS px, with no drag over 8 px or hold over 250 ms. A single tap continues to deselect; a third eligible tap fits the active graph's measured bounds. Other canvas gestures reset the sequence. Fit interpolates pan and zoom together over approximately 220 ms with ease-out; reduced-motion preference skips the animation, and new navigation interrupts it. Canvas-focused keyboard shortcuts F, +/− and 0 remain as nonvisual alternatives. Explicit annotation Fit signals, outline locate and initial programmatic fitting are preserved. Fullscreen is deferred.

Browser coverage uses real Chromium CDP two-finger movement, wheel and triple-tap/click events; since CDP releases touch contacts together, the independent one-finger handoff regression dispatches a partial pointer-up using the actual second pointer ID before continuing the first contact. Adding a second finger directly over an input port also takes over an in-progress wire gesture, avoiding accidental activation. Physical-device verification remains separate.

### Assisted touch cable dragging

**Phase 6.21.4, implemented ([PR #65](https://github.com/wgabel/orbit2/pull/65)).** A fingertip hides the tiny port it is aiming at, especially at 25% zoom. For touch pointers only, a drag from an output begins after 6 CSS pixels of movement. The visible cable cursor is offset upward by 60 CSS pixels, tapering to zero near the lower edge so the bottommost visible port remains reachable. Snapping acquires within 24 CSS pixels of a compatible input marker and retains that target until 36 CSS pixels away; a challenger more than 4 px closer or within 2 px of the cursor may take over, preventing flicker without trapping adjacent ports. Distances are CSS pixels, never scaled canvas units. A fixed-size ring highlights each eligible visible input; invalid inputs dim, the selected ring enlarges (120 ms ease-out, no transition under reduced motion), and an unscaled floating node → input label plus optional 12 ms vibration confirms the candidate. No picker, automatic expansion, extra audio state or persistence fields.

`canvasDom.js` measures unobscured visible marker centers in client space and uses the actual scrolled origin of the absolute overlay and transformed world. A gesture-scoped, frame-coalesced DOM observer refreshes the measurement on viewport/visual-viewport scrolling and resizing and node/header/port-group resize; the release takes a final fresh measurement before committing. `GraphCanvas` caches compatible input addresses for the current graph revision using the domain's shared read-only routing rejection helper (matching types/channels, duplicate, fan-in and cycles; inactive destinations are not drag candidates). `TouchWireAssist` resolves screen-space geometry; the renderer transforms the chosen endpoint with the existing viewport into world coordinates for the draft cable. Only release of a moved gesture on an eligible target issues the existing graph-qualified connection request; it is still validated by the command. Unsnapped release, pointer cancellation, lost capture, Escape, navigation or a graph/layout revision change clear the preview, with no graph mutation. Desktop direct drag, keyboard and existing tap-output/tap-input activation remain available. All root and nested graphs use the same renderer; compact nodes should retain their visible ports when implemented. If aiming near overlapping or off-screen nodes, pan/zoom first. Real touch testing on physical hardware remains desirable; automated coverage uses Chromium touch events and independent presentation geometry tests. The merged build passed the published-editor and deployment checks; a subsequent older deployment overwrote it despite the successful newer deployment. The current main artifact was redeployed and live JS/CSS verified. See the [deployment ordering contract](../../README.md#build-tests-and-deployment) (guard in review, [PR #67](https://github.com/wgabel/orbit2/pull/67)).

Snap-highlight alignment follow-up ([PR #74](https://github.com/wgabel/orbit2/pull/74), in review): native scrolling of the overflow-hidden viewport previously shifted rings by its scroll offset, and browser-only reflow could leave measured targets at obsolete coordinates. The corrected origin keeps ring, cursor and draft endpoint aligned; refreshed geometry preserves hysteresis by port identity and excludes newly covered/clipped targets. No graph mutation, repeated compatibility validation or per-pointer-move port scan is needed. The observer is disposed on gesture end/cancel and late callbacks are ignored by gesture generation. Browser regression checks every ring against its actual marker under scroll and a mid-drag header reflow.
