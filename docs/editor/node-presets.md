# Individual node presets

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [parameters and modulation](../core/parameters-and-modulation.md) · [commands and events](../core/commands-and-events.md) · [persistence and history](persistence-and-history.md) · [design system](design-system.md).

### Individual node presets (Phase 6.17 — implemented)

#### Model and catalog (6.17.1)

A node preset is a reusable immutable configuration of an existing registered node type, not a new node type, a factory, a DSP implementation, a patch or a live link to another node. Initially support leaf generators and processors with editable parameters; racks, master output without editable parameters, and composite graph definitions are not individual preset targets. Custom composite definitions remain a separate graph-template feature.

Use a versioned data-only `NodePreset` containing a stable preset ID, node type key, name, group, optional usage description and a map of stable parameter definition keys to validated base values. Include discrete structural selections and inactive conditional parameters so selecting a preset restores the complete configuration, including dormant settings. Do not store instance GUIDs, connections, connection amounts, positions, effective modulation samples or DSP state. The current patch stores its actual base values, not a dependency on the preset library. Editing or deleting a preset cannot change nodes that previously used it.

~~~json
{
  "schemaVersion": 1,
  "id": "reverb-dark-ambient",
  "nodeType": "processor.reverb",
  "name": "Dark Ambient",
  "group": "Atmospheric",
  "description": "Long, dark reverb for evolving drones.",
  "parameters": { "decay": 8.0, "damping": 0.8, "mix": 0.65 }
}
~~~

**Catalog and responsibilities.** Put the immutable model, catalog, validator and `NodePresetService` in `Orbit.Application/Presets`. Keep factory registration in `NodeRegistry`; use its stable type keys and parameter definitions to validate preset compatibility. Ship built-in presets as versioned JSON resources per node type, loaded by infrastructure through an application-facing catalog contract. Adding another built-in configuration must not require changes to C#, Faust or Razor. The service merges read-only built-ins with the custom library, groups by usage label and sorts consistently. Reserve the `Custom` group for user-created presets; do not allow imported content to impersonate built-ins. Use stable IDs rather than display names as selection keys; validate names, group labels, schema, duplicate IDs and finite in-range values. Explicitly reject incompatible or incomplete configurations unless a versioned migration supplies missing values.

**Implemented 6.17.1.** `NodePreset` defensively copies its complete stable-key parameter dictionary and validates version and metadata. `NodePresetValidator` derives eligible leaf types and parameter definitions from the composed `NodeRegistry` (generators/processors with editable parameters, excluding composites and catalog aliases). The same `ParameterDefinition.IsValidBaseValue` contract validates continuous values and discrete choices, including inactive settings. `NodePresetJsonReader` rejects unknown fields, duplicate JSON keys and malformed configurations without reflection-based deserialization. `EmbeddedNodePresetCatalog` discovers the per-type JSON arrays in `Orbit.Infrastructure/Presets/BuiltIn/` via `INodePresetCatalog`; the resources ship inside the assembly, avoiding independent downloads or stale catalog caches. `NodePresetService` validates the complete set at initialization, rejects globally duplicate stable IDs and the reserved Custom group, and returns read-only groups ordered by label and preset name with ordinal ID tie-breaking. Every eligible type has an empty Custom group ready for the future library. The composition root shares one registry between the editor and validator and eagerly verifies the catalog at startup. Ten initial examples cover all eight eligible types; application, selector and broader content follow in separate steps. No graph or DSP mutation is introduced here.

~~~mermaid
flowchart TD
    J[Embedded JSON resources] --> C[INodePresetCatalog]
    C --> S[NodePresetService]
    R[NodeRegistry] --> V[NodePresetValidator]
    V --> S
    S --> G[Immutable usage groups]
~~~

#### Atomic application (6.17.2)

**Atomic application.** Introduce `ApplyNodePresetCommand` targeting a graph ID and node ID with a validated type-key/value configuration. A domain batch parameter operation resolves and validates every key, value and target before mutating anything, including structural choices and stored inactive values. A failed or unchanged configuration produces no revision. A successful application preserves node, parameter and port IDs, connections and layout; publishes one `GraphChangeSet` with the individual `ParameterChanged` facts; records one undo entry; and triggers existing audio, range projection and autosave subscribers once. Do not loop through separate session commands. Topology comparison already distinguishes base-only changes from structural changes, so ordinary presets update active controls and structural choices use the existing crossfade policy. No preset-specific logic belongs in DSP or audio infrastructure. Existing pending slider history is finalized before a distinct preset application.

**Implemented 6.17.2.** `ApplyNodePresetCommand` carries an immutable `NodePreset` and the target graph/node IDs. `GraphCommandHandler` uses the registry-derived `NodePresetValidator` before calling `SignalGraph.SetNodeParameterValues`. The domain operation independently checks the actual node type, resolves the complete ordinal key set, validates every value through the same parameter validation as individual edits, and prepares all changed `ParameterValueChange` records before mutation. It returns only changed values in parameter definition order. The handler maps these to the existing `ParameterChanged` facts; `PatchSession` commits one root snapshot, one revision and one history entry. Only `SetParameterValueCommand` can participate in slider gesture grouping, so even a single-value preset remains a distinct operation. Rejected and identical configurations have no notification or history entry. Nested parameter and port IDs, dormant wires, audit flags and layouts stay intact; history restoration and patch serialization already carry all base settings. Playback consumes the final committed snapshot through its unchanged topology comparison and control manifest, so one live batch or one structural replacement is sufficient. Tests exercise invalid late values, no-ops, nested and root targets, undo/redo, audition state, and running playback updates.

~~~mermaid
flowchart TD
    C[ApplyNodePresetCommand] --> V[Registry compatibility validation]
    V --> G[SignalGraph: preflight all keys and values]
    G --> P[Apply changed base values]
    P --> E[One committed change set]
    E --> H[History and autosave]
    E --> D[Existing playback update]
    E --> U[Canvas projection]
~~~

~~~mermaid
flowchart TD
    B[Built-in JSON catalog] --> S[Node preset service]
    C[(Custom preset library)] --> S
    S --> U[Grouped preset selector]
    U --> A[Apply preset command]
    A --> P[Patch session and validation]
    P --> H[One undo entry]
    P --> D[Existing audio update]
    P --> V[Canvas and autosave]
~~~

#### Shared selector (6.17.3)

**Presentation.** Add a shared `NodePresetSelector` inside `NodeFrame`, below the header, using design tokens and the same component at every canvas level. Render groups via a compact accessible selector, provide a usage description and allow loading a preset without extra permanent toolbars. Show an unselected/modified state when the current base configuration does not match a preset; modulation alone does not mark it modified. Saving captures the current base configuration from an immutable snapshot. Built-ins cannot be overwritten, renamed or deleted; users can apply them, adjust values and save a new custom preset. Management actions (save as, deliberate overwrite, rename, delete) belong to a compact shared menu/dialog, not per-node implementations. Editing the preset library is separate from graph undo/redo; applying a preset is a graph edit.

**Implemented 6.17.3.** `NodePresetSelector` is composed once in `NodeFrame` immediately below the header. It consumes `NodePresetService` and an immutable `GraphNodeSnapshot`, resolves eligibility and ordered usage groups through the existing catalog, and exposes a native accessible grouped select with a description and an explicit modified/unmatched option. The empty Custom group stays visible but disabled. `NodePresetService.FindMatchingPreset` compares the complete stable-key base configuration, including inactive parameters, and chooses the first match in deterministic catalog order; effective telemetry and audition flags are not involved. The component skips rematching the same snapshot on layout/telemetry-only refresh. Selecting a preset emits `NodePresetApplyRequest` through the shared frame and canvas to the workspace, which addresses the active graph and executes the atomic command. The control is bound to the committed matching value, not a separately persisted selection; undo/redo, reload and direct edits therefore stay consistent. Pointer and keyboard events do not start canvas gestures, and committed edits trigger the existing measured connection geometry refresh. CSS reuses parameter-choice styling and existing JSON-derived semantic/node tokens, with a single-line description and full tooltip. Published browser tests cover grouped options, applied values, modified/undo state and nested playback without interruption. Custom management remains a later phase.

**Preset selector sizing.** The shared preset row reserves intrinsic-width columns for the label and the management button, with a shrinkable middle select column and a constrained parent grid track. Long built-in or custom names must not widen the node or push the management button past its border. Browser regressions check actual element bounds in root/nested canvases, including long custom names.

~~~mermaid
flowchart TD
    S[Node snapshot] --> M[Catalog match]
    C[Grouped preset catalog] --> M
    M --> U[NodePresetSelector]
    U --> R[Preset apply request]
    R --> W[GraphWorkspace]
    W --> P[ApplyNodePresetCommand]
    P --> S
~~~

#### Built-in content (6.17.4 — implemented at this milestone)

| Node | Presets | Built-in usage groups | Example presets |
|---|---:|---|---|
| Oscillator | 14 | Bass, Drones, Harmonic layers, Modulation, Rhythmic modulation | Sub Bass, Slow LFO, Deep Sine, Bright Saw |
| Noise | 7 | Atmosphere, Modulation, Texture | Wind, Deep Rumble, Bright Hiss |
| Gain | 6 | Gain staging, Layering, Modulation | Half Level, Quiet Bed, Modulation Center |
| Filter | 8 | Corrective, Resonance, Texture, Tone shaping | Warm Low-pass, Gentle High-pass, Vocal Resonance |
| Parametric EQ | 7 | Corrective, Texture, Tone shaping | Warmth, Presence, Remove Muddiness |
| Delay | 8 | Atmospheric, Rhythmic, Short reflections | Quarter Note, Triplet, Long Echo |
| Reverb | 8 | Atmospheric, Spaces, Texture | Small Room, Large Hall, Dark Ambient |
| Chorus | 7 | Modulated pitch, Texture, Thickening | Gentle Chorus, Deep Ensemble |

Examples describe intended usage, not a guarantee of a particular sound without suitable input/routing. Each supported node also exposes `Custom` even when empty. Presets should demonstrate meaningful settings, sensible output headroom and compatible parameter modes; factory defaults remain available independently.

**Implemented 6.17.4.** The embedded catalog now contains 65 presets across all eight eligible types. The 55 additions preserve the original ten IDs and configurations and introduce no new runtime mechanism or storage format. All entries include descriptions and complete stable-key values (including dormant parameters). CI validates every entry against the registry, applies it through the atomic graph command, checks exact selector matching and unchanged connections, and generates a reachable Faust graph for each configuration. Duplicate complete configurations within a node type would fail the matching test. Custom persistence is the next step.

The 65 presets across eight types describe the original completed 6.17.4 catalog, not a permanent inventory: 7.1.1 and 7.2.2 subsequently added five AD and five Pitch Control presets respectively.

#### Custom library (6.17.5)

**Custom persistence.** Define `INodePresetStorage` separately from `IPatchLibraryStorage`, with a browser adapter using the existing versioned IndexedDB interop and a dedicated logical store/namespace. Reuse validated serialization, transaction and error patterns where appropriate; never mix node presets with recovery records or named complete patches. Persist versioned data by stable ID, enforce unique names per node type (case-insensitive) and explicit overwrite semantics, and report storage failure without changing the current node. Custom presets survive reload and patch switching; eventually support portable individual JSON import/export with validation and conflict handling. Future audio-asset-valued configurations need explicit references and portability rules from Phase 8; scalar parameter presets do not embed recordings.

**Implemented 6.17.5.** `NodePresetLibraryService` owns serialized library operations. The application-facing `INodePresetStorage` is implemented by `BrowserPatchStorage` through its existing versioned persistence module. A disjoint `node-presets:custom` key contains the whole array of versioned configurations, using the strict reader and reflection-free `NodePresetJsonWriter`. Save as generates a GUID; rename and overwrite preserve identity; only custom IDs of the current node type may be managed. The catalog validates complete parameters, group, IDs and case-insensitive names (including built-ins) before the IndexedDB transaction. A compare-and-replace inside that same read/write transaction prevents lost updates from another tab; a rejected write or invalid loaded document leaves the published catalog unchanged. Initial loading is independent of patch recovery, and the manager can explicitly reload the library. `NodePresetService` merges sorted custom entries after built-in groups and assigns a catalog revision only after a committed change or successful load. Matching keeps deterministic built-in-first precedence when two configurations are identical. No library ID is stored on a graph node.

The shared `NodePresetSelector` opens one `NodePresetManager` panel at workspace level, scoped to the active node's immutable snapshot. It reuses the patch-manager panel styling and offers save as, rename, overwrite and delete without modifying node geometry. A catalog-revision parameter invalidates all visible selector matches even if their graph snapshots are unchanged. Workspace navigation or replacement closes the manager; the singleton library remains available across every canvas and patch. Library writes do not dispatch graph commands, change playback, or enter graph history; applying a preset still follows the existing atomic command.

~~~mermaid
flowchart TD
    N[Current node snapshot] --> M[Shared preset manager]
    M --> L[NodePresetLibraryService]
    L --> V[Validate candidate catalog]
    V --> S[IndexedDB compare-and-replace]
    S --> C[Publish custom catalog revision]
    C --> U[Refresh shared selectors]
    U --> A[Apply preset command]
~~~

#### Portable node presets (6.17.6)

**Implemented 6.17.6.** Portable files contain one schema-versioned `NodePreset` object, not the internal array or a patch document. The shared reflection-free JSON writer and strict reader use the same field names and stable parameter keys for both formats. Existing schema-1 library arrays remain unchanged; unknown schema versions are rejected until an explicit migration exists. Export is restricted to a saved custom preset and delegates to the existing browser download adapter through `INodePresetStorage`. The `NodePresetManager` limits file reads to 256 KiB, previews a completely validated custom configuration for the active node type, and offers three explicit actions: preserve identity (reject ID/name conflicts), save a copy with a new GUID and unique name, or replace a matching custom ID of the same type. Built-in IDs/groups cannot be imported or replaced. The candidate is validated again inside the serialized library edit, including name uniqueness and full parameter compatibility. Only a successful compare-and-replace publishes a catalog revision; erroneous files, cancellations, stale writes and failed storage leave existing entries unchanged. Import/export do not mutate the patch or history; applying a configuration is still a separate graph command.

~~~mermaid
flowchart TD
    F[Single preset JSON] --> V[Strict schema and node type validation]
    V --> P[Preview and explicit conflict choice]
    P --> C[Validate candidate catalog]
    C --> S[Existing atomic library transaction]
    S --> R[Refresh custom group]
~~~

#### Completed milestone integration record

- Implemented (6.17.1): immutable versioned individual node preset model, registry-derived validation and grouped embedded JSON catalog.
- Implemented (6.17.2): atomic preset application through validated batch parameter changes, one revision/undo entry, and existing live playback.
- Implemented (6.17.3): shared grouped node preset selector, description and exact base-configuration matching in every canvas.
- Implemented (6.17.4): expanded data-only catalog of 65 built-in configurations across all eight eligible node types.
- Implemented (6.17.5): custom preset library with stable IDs, explicit management, independent IndexedDB persistence and stale-write protection.
- Implemented (6.17.6): portable individual JSON presets with preview, compatibility validation and explicit conflict choices; signal observation next.
