# Workspace persistence and history

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [graph model](../core/graph-model.md) · [commands and events](../core/commands-and-events.md) · [canvas](canvas.md) · [node presets](node-presets.md) · [reusable modules](reusable-modules.md) (planned separate graph-template storage and cloning).

<a id="workspace-persistence-and-history-phases-68611"></a>

### Workspace persistence and history (Phases 6.8–6.11)

**Status: Implemented.** These storage, session-history and gesture rules are architecture, not roadmap milestones. Layout and navigation remain separate from the signal graph.

For optional note-card fields (6.19.1, implemented), node/connection metadata and shared presentation edit history (6.19.2, implemented), visual group membership and atomic layout history (6.19.3, implemented), patch-wide overview and searchable named-save metadata (6.19.4, in review), and future annotation remapping contracts, see [patch annotations](patch-annotations.md).

Workspace persistence uses a data-only schema version 1. `PatchDocumentSerializer` captures immutable session snapshots with stable node, parameter, socket, connection and graph IDs. Parameter and port definitions are reconstructed by the registry using their stable keys; composites reconstruct their boundary definitions, preserving both rack identities and receiving-side fan-in. Invalid definitions, unknown schemas and invalid connections are rejected before a session is exposed. `CanvasNavigationService` captures and restores layout and navigation independently. `PatchPersistenceService` coalesces changes for 350 ms and serializes writes; `IPatchStorage` is implemented by `BrowserPatchStorage` through IndexedDB transactions. No live runtime state or selection is stored. Startup loads before rendering the workspace; a missing document seeds the demo, while a failed restore preserves the saved record and disables autosave for that session. Very fast reloads within the debounce window can lose the latest edit; storage is browser-local and can be cleared by the browser.

Phase 6.10 adds `PatchLibraryService` and `IPatchLibraryStorage`, using distinct named keys alongside the recovery key without changing the schema. Named snapshots are not live links to the editor; Save as rejects duplicate names and overwriting is explicit. JSON import/export uses the same serializer. Workspace replacement validates a candidate session and navigation, requires stopped playback, drains pending recovery writes and commits the replacement immediately; then the workspace recreates its keyed presentation and playback components. Invalid imports retain the existing graph and named snapshots.

Phase 6.11 puts `PatchHistory` inside `PatchSession`, retaining up to 100 successful graph command before/after snapshots. Undo/redo reconstructs the target graph using the persistence reconstruction path (without JSON or storage), validates before replacing, advances the current revision and publishes one `GraphChangeSet` containing `GraphHistoryRestored` facts for affected graphs, including added/removed descendants. This preserves the original patch identity, subscriptions, and current audio lifetime. Failed/no-op edits do not enter history; divergent commands clear redo. History is not persisted and begins empty after startup or patch replacement. Presentation retains bounded last-known layouts for recently removed nodes/graphs; standalone layout gestures and tempo are outside graph history.

Continuous parameter editing groups history separately from graph transactions. The shared parameter slider gives pointer drags and range keyboard gestures unique identities; `PatchSession.Execute` accepts an optional history gesture identity. `PatchHistory` retains the initial and latest immutable snapshots for consecutive parameter changes with the same identity, graph and parameter address, while every accepted value still commits and notifies the normal DSP, canvas and autosave subscribers. Release, cancellation, blur, navigation, undo/redo or an intervening successful edit finalizes the pending gesture. A stale completion cannot finalize a newer gesture. If the final value equals the initial value, no entry is added and redo is preserved. Numeric edits and discrete choices retain their ordinary command behavior. No UI component manages history snapshots directly.

~~~mermaid
sequenceDiagram
    participant U as Shared slider
    participant S as Patch session
    participant H as History
    participant D as DSP and autosave
    loop Drag updates
        U->>S: Value and gesture ID
        S->>H: Keep first and latest snapshots
        S->>D: Committed revision
    end
    U->>S: Gesture ended
    S->>H: Finalize one entry
~~~

~~~mermaid
flowchart TD
    C[Graph command] --> H[Patch session history]
    U[Undo or redo] --> H
    H --> V[Validated graph reconstruction]
    V --> R[New committed revision]
    R --> A[Audio update]
    R --> P[Canvas and autosave]
~~~

~~~mermaid
flowchart TD
    M[Patch manager] --> L[Patch library service]
    L --> S[Document serializer]
    L --> I[(Named snapshots)]
    S --> V[Validated candidate]
    V --> W[Workspace replacement]
    W --> R[(Recovery record)]
~~~

~~~mermaid
flowchart TD
    S[Patch session snapshot] --> D[Patch document serializer]
    L[Canvas navigation layouts] --> D
    T[Tempo] --> D
    D --> P[Patch persistence service]
    P --> B[IPatchStorage]
    B --> I[(IndexedDB)]
~~~
