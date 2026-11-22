# Patch documentation, annotations and canvas organization

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap, Phase 6.19](../roadmap/editor-and-effects.md#phase-619--patch-documentation-and-canvas-organization-planned)

Related: [canvas](canvas.md) · [design system](design-system.md) · [persistence and history](persistence-and-history.md) · [commands and events](../core/commands-and-events.md) · [connection visualization](annotations-and-connections.md) · [composite nodes](composite-nodes.md) · [generative ambient composition](../examples/generative-ambient-composition.md).

**Status:** Note cards (6.19.1) implemented ([PR #27](https://github.com/wgabel/orbit2/pull/27)); node aliases, descriptions and connection labels (6.19.2) implemented ([PR #28](https://github.com/wgabel/orbit2/pull/28)); visual groups (6.19.3) implemented ([PR #30](https://github.com/wgabel/orbit2/pull/30)); overview and discovery (6.19.4) implemented ([PR #32](https://github.com/wgabel/orbit2/pull/32)); annotation visibility and zoom detail (6.19.7) implemented ([PR #35](https://github.com/wgabel/orbit2/pull/35)); outline (6.19.8) planned. This document owns annotation and navigation requirements. Connection amount badges and composite boundary visibility (6.19.5–6.19.6) are separate implemented features. See [hands-on organization workflows](../usage/patch-editor.md#3-document-and-organize-a-patch).

<a id="patch-documentation-and-canvas-annotations-planned-phase-619"></a>

### Patch documentation and canvas annotations (Phase 6.19.1–6.19.4)

The independent cable badge/editor (6.19.5) has shipped; it does not depend on these presentation-only annotations.

Complex patches should explain their musical intent as well as their signal topology. A **Note** appears in the Add catalog alongside other canvas items, but is a presentation-only annotation, not a `SignalNode`. It has no ports, parameter definitions, DSP emitter, presets, audition controls, observation channels or routing dependencies. Do not register an inert DSP node merely to obtain a frame or persistence. Notes and organizational metadata must be editable during playback without a graph commit, graph-history entry, audio update, compilation, phase reset or capture invalidation.

#### Note card experience

- Add Note on the current root or nested canvas using the existing visible-world placement policy, taking measured signal nodes and note cards into account. Give each note a stable ID scoped to its owning graph; allow multiple independent notes. A sensible default title and empty body are sufficient.
- Render a reusable card with a draggable header, editable title, multiline body, collapse/expand, resize handle and a small palette of named semantic accents (e.g. neutral, idea, instruction, warning). Use existing design tokens, shared selection, keyboard accessibility and world-coordinate geometry; do not duplicate the graph viewport or implement a second editor. A note has a distinguishable icon and no sockets.
- Keep editing simple: plain text with preserved line breaks, selectable/copyable text and an explicit edit/apply/cancel interaction (or equivalent accessible inline editor). Text editing owns focus; typing, selecting text, native text undo and scrolling must not drag the canvas, delete signal nodes or trigger shortcuts. Collapse retains the complete content and expanded size. Allow deletion and duplication with a fresh annotation ID; text is always stored and displayed as data, never injected as HTML, CSS, JavaScript or Faust source. A restricted Markdown renderer can be evaluated later, with raw HTML disabled.
- Use selectable notes in Fit-to-content and shared hit testing, without counting them as signal nodes or introducing fake connectors. Notes remain visible when playback is stopped and survive pan/zoom, navigation, autosave, restart, named save and JSON import/export.

Example: a note titled “Thump — pitch and noise” explains a short high-frequency onset, descending pitch, longer low-frequency decay and a filtered noise transient; the neighboring nodes remain independently editable.

<a id="note-cards--implementation-6191-in-review"></a>
#### Note cards — implementation (6.19.1, implemented)

Choose **Note (annotation)** in Add node, then **Add**. Drag the header to move the card, **Edit** to change title/body/accent in the unscaled canvas editor overlay, and drag the lower-right handle (or use its arrow keys) to resize. Collapse/expand preserves the expanded dimensions. The selected note can be duplicated or deleted; a collapsed note can also be removed using Delete selected. **Undo annotation / Redo annotation** (renamed from Undo note / Redo note in 6.19.2) are per-canvas, independent of graph Undo / Redo, with up to 100 shared annotation operations. Drag and resize previews commit only on release; Escape cancels a gesture or an open text draft. Apply commits the complete draft; Cancel discards it. Navigation, patch replacement and explicit save/export are blocked while a draft is open; supported browsers warn before closing/reloading. Recovery saves only committed text.

`SavedCanvas.Notes` defaults to an empty list for older schema-1 documents. Each record stores ID, title, body, accent, x/y, width/height and collapsed flag, with graph ownership inherited from its layout. Validation enforces 200 notes per graph, 120 title and 16,000 body characters per note, finite coordinates within ±1,000,000 world units, width 220–1,200, height 150–1,200, a valid accent and unique nonempty IDs within the graph. The existing 10 MiB JSON import limit applies. Default dimensions are 300 × 200 world units; collapsed height is 44. The optional fields use the same source-generated patch JSON and existing recovery/named-save storage. The note history and active selection are transient. Removed child layouts retain committed notes for graph undo, not annotation history. Fit includes note bounds and measured signal frames; adding notes and nodes uses measured occupied bounds. There is no registry entry or graph command for a note.

For agents generating patch documents, add a `notes` array to the existing canvas layout object for the owning graph. Accent values are numeric, as in other schema-1 enums: 0 neutral, 1 idea, 2 instruction, 3 warning. For example, a single array entry:

```json
{
  "id": "cc109484-4a18-4a52-9066-084db8878d19",
  "title": "Harmonic foundation",
  "body": "A minor.\nKeep the fifth quieter than the root.",
  "accent": 2,
  "x": 320,
  "y": 120,
  "width": 300,
  "height": 200,
  "collapsed": false
}
```

Generate a fresh UUID for each new note; choose bounds that do not obscure actual signal nodes.

#### Descriptive patch features

| Feature | Ownership and initial behavior |
|---|---|
| Note | Independent graph-scoped canvas item with title, body, bounds, accent and collapsed state. |
| Node alias and attached note | Optional per-node presentation label and description keyed by graph/node ID; keep canonical type labels and IDs visible and unchanged. Show the alias in the shared frame, breadcrumbs/selection where useful; show a consistent description affordance without replacing real parameter editors. |
| Connection label | Optional short description keyed by graph/connection ID, e.g. “slow cutoff sweep”; display in the selected-wire editor and optionally near the wire, never in its routing or amount contract. |
| Visual group | Named, colored background frame with explicit member IDs and optional group description. Unlike a composite/drone, grouping never changes ownership, creates racks or reroutes signals. Start with manual membership, resize, and explicit move-group action that translates its members once; no implicit inclusion from overlapping bounds. Render behind nodes/cables and do not intercept normal member interactions. Group deletion removes the frame only; no implicit member deletion or mute. |
| Patch overview | Optional patch-level title, description, intended sound, playing/tuning instructions and tags, stored in `PatchDocument` rather than duplicating the named-library storage key. Surface in the existing patch manager and support searching/filtering saved-patch metadata. |

Do not use grouping, labels or notes as implicit graph commands. Later, an explicit upgrade from a visual group to a real composite may be considered under Phase 7 group/ungroup with validated routing and history. Node aliases are presentation metadata, not a new registered node type or a mutation of a composite's canonical name. Existing rack port names remain the actual public interface.

#### State, persistence and history

Use immutable records for annotation data with stable IDs and graph ownership. Data-only portable records belong in `Orbit.Application.Persistence`; validation, geometry, selection, edits and immutable annotation projections belong in `Orbit.Presentation.Canvas`. Extend `CanvasStateSnapshot` and `CanvasStateService` with dedicated annotation state, selection, explicit validated add/edit/move/resize/delete/duplicate operations and a layout notification. Reuse `CanvasNavigationService` capture/restore, placement and fit geometry. The UI only translates gestures, renders a shared annotation card and handles text drafts; no browser dependency enters the presentation model. Keep annotation IDs in a separate namespace/collection from node/connection IDs and check graph ownership before edits. Use shared geometry for bounds and a generic canvas-item abstraction only as needed, without forcing signal-specific sockets/editors onto notes.

Extend `SavedCanvas` with optional annotations, aliases, connection descriptions and groups; extend `PatchDocument` with optional overview metadata. The schema-1 pattern used for visualization settings allows additive fields with empty/default values for older files; update the source-generated JSON contract and document compatibility. Validate all unique IDs, known graph/target references, finite world bounds, supported accent tokens, size/count limits and text lengths (initial proposal: title 120 characters, body 16,000, up to 200 notes per graph; bound total document size as well). Reject malformed annotation documents atomically before replacing a running workspace; do not silently discard user text. Restore missing optional fields as empty. No extra IndexedDB store is required: notes travel with patch recovery, independent named snapshots, JSON and future portable packages. The overview's patch title is distinct from the external library entry name; no automatic rename of named saves.

Keep visual changes out of `IPatchSession.Execute` and `PatchHistory`. Existing graph undo/redo remains unchanged. Add a bounded presentation-only annotation edit history (explicit undo/redo actions, one committed content change or operation rather than keystroke entries); a text draft uses native text undo before Apply. Coalesce drag/resize into one operation; do not include viewport pan/zoom in annotation history. Reuse generic immutable history mechanics if extracted, with separate history and redo boundaries. Never undo an audio setting when a user intended to undo a note edit. Selection, uncommitted drafts and history are not persisted. Mark unapplied drafts clearly and prevent navigation/closing from silently discarding them. Content edits trigger existing debounced recovery persistence on commit; users can Save explicitly.

When a signal node or connection is deleted, prune its alias/attached description and group membership, retaining graph-ID-qualified metadata in the existing bounded removal/restoration pattern for graph undo. Independent notes remain; linked annotations must not attach to an unrelated new ID. Removing a composite removes its child canvas annotations from capture and preserves them in the bounded removed-layout cache for graph undo, just like its positions and visualization settings. No orphaned layout references are emitted on export. Future copy/paste or composite template duplication must remap annotation identities and target/member IDs as part of the same copy operation; copies do not share mutable text or metadata.

~~~mermaid
flowchart TD
    N[Note or organization edit] --> L[CanvasStateService layout commit]
    L --> V[Immutable canvas snapshot]
    L --> H[Annotation history]
    V --> R[Shared GraphCanvas renderer]
    V --> P[CanvasNavigation capture]
    P --> D[PatchDocument]
    D --> S[Recovery and named saves]
    G[PatchSession graph commit] --> A[Audio engine]
    G --> L
~~~

The only direction from the signal graph to annotations is reconciliation of identity/membership. No reverse dependency into the audio path is introduced. Test root and nested note edits, independent identities, undo/redo, visible placement at pan/zoom, bounds and fit, keyboard isolation, deletion/restoration, invalid import rejection, old documents, named snapshots and reload. Verify unchanged graph revision, signal node/edge count, compiler identity and active playback generation for every annotation action, including edits while playing. Ship a small annotated example patch when the shared documentation features are ready.

## 1. Practical experience and examples

The goal is to give large musical patches a readable internal instruction manual without turning the editor into a documentation application. Notes explain intent, aliases and labels identify individual routing roles, groups explain structure, patch overview describes the entire piece, and the outline helps find things. Not every node or cable needs a description.

### Note cards (6.19.1)

Use the common canvas-item catalog for Add → Note, visually distinguish organizational items from signal nodes, and allow creation at the cursor or visible world center using existing placement conventions. Use a compact vertical header with title, color/accent indicator, collapse and overflow actions. The body can contain paragraphs, line breaks and simple lists as plain text. Example note types:

| Accent | Example title | Example body |
|---|---|---|
| Neutral | Harmonic foundation | A minor. The second oscillator adds a fifth; keep it quieter than the fundamental. |
| Idea | Try a brighter variation | Replace the triangle with a filtered saw when more presence is needed. |
| Instruction | Performance cue | At 4:30 introduce the pulse; let the lead fade before the next chord. |
| Warning | Feedback headroom | Increasing delay feedback and reverb mix together may raise peaks; inspect the master feed. |

Use tokenized contrast in light/dark themes, not hard-coded DSP role colors. Colors supplement title/icon/text, never encode meaning alone. Committed text and editing drafts are separate. The note's header drag moves the card; text selection/scroll and resize use their own gesture scope. Retain expanded geometry when collapsed, and preserve explicit collapsed state across navigation/reload. Use a bounded text editor or scrollable body instead of automatically growing the note without limit.

### Node aliases and attached descriptions (6.19.2)

For a signal node named Slow brightness drift, the shared frame might show its alias prominently and canonical type Oscillator below. An optional info icon shows a description such as "0.013 Hz sine, approximately 77 seconds per cycle, sweeps the low-pass cutoff." A compact inspector or popover can edit it without forcing every description to occupy permanent space in the frame.

An alias is display metadata; it does not rename a registered type, alter its stable IDs or change an exposed rack-port name. Duplicate aliases are allowed if they are meaningful; address targets by graph/node identity rather than unique text. Use the alias in relevant navigation and discovery surfaces but preserve the true type for understanding imported patches. Empty alias means the existing canonical label. Long aliases truncate visually with the complete value accessible, never expand node width into connected nodes.

Connection labels are equally optional and describe intent, e.g. "Brightness sweep" on a connection with a +600 Hz cutoff amount. The implemented [numeric badge and connection editor](annotations-and-connections.md) remain the authoritative presentation of the committed amount and destination unit. Show text as a distinct secondary label or tooltip/inspector detail, and preserve normal selection/editing even when descriptions are hidden. Do not store a second weight, replace the typed-event badge or conflate a label with measured modulation.

<a id="node-and-connection-descriptions-implementation"></a>
#### Node and connection descriptions — implementation (6.19.2, implemented)

Implementation: [PR #28](https://github.com/wgabel/orbit2/pull/28).

Select one node or cable and choose **Describe selected**. The same unscaled editor handles node alias and multiline description or a single connection label; the selected connection's amount editor also offers **Edit connection label**. An existing node description has an info button, while aliases appear above the canonical node name and in composite breadcrumbs. The full alias and description remain accessible in the tooltip/editor; the shared frame truncates long names rather than widening. Empty alias restores the canonical name, and empty fields remove the metadata. Connection descriptions appear in the selected cable editor and badge tooltip, not in the numeric value itself. This also works for typed event and inactive connections; switching amount badges Off does not remove descriptions.

The two metadata types use optional `SavedCanvas.nodeDescriptions` and `connectionDescriptions` arrays in schema 1. The graph is identified by the layout's existing `graphId`; each target must exist in that graph (including its real rack nodes). Duplicate or unknown targets, null text, multiline names, names longer than 120 characters, and node descriptions longer than 16,000 characters are rejected. Aliases need not be unique. Missing arrays in older documents default to empty; no domain or DSP fields are changed. Example layout members for agents (substitute the actual target UUIDs):

```json
{
  "nodeDescriptions": [
    {
      "nodeId": "ab83657b-56df-4a85-af2a-fb31052744e1",
      "alias": "Slow brightness drift",
      "description": "77-second modulation cycle. Opens the filter gradually."
    }
  ],
  "connectionDescriptions": [
    {
      "connectionId": "a0a818d5-d58b-4413-bbfd-587b9a89ea97",
      "label": "Brightness sweep"
    }
  ]
}
```

Apply is one annotation-history operation, interleaved chronologically with note edits. The shared 100-operation history stores reversible note changes and target-specific metadata changes; graph undo/redo is independent. Deleted targets leave active snapshots/export, but a bounded cache of up to 512 removed node and 512 removed connection descriptions per canvas restores metadata on graph undo. Undoing an annotation while its target is deleted changes that cached value, never creates a new target or pollutes the exported document. Removed child canvases use the same existing removed-layout restoration policy as notes. Draft protection is shared across all text editors, and editing the label of a selected cable does not alter an uncommitted numeric amount draft.

### Visual groups (6.19.3)

Suggested workflow: select related members using the existing multi-selection, choose Create visual group, set its title/accent and adjust bounds. A frame can also start empty and allow explicit Add/Remove members. Give the group a separate header/drag affordance for moving its members. Resizing changes the visual frame, not member positions or membership; overlapping a node does not add it. Group title and optional description are editable. Group bounds and header remain identifiable at low zoom.

Initially support ordinary nodes (including whole composites as members) and notes owned by the same graph; do not recursively include child graphs when moving a composite in its parent. Reject cross-graph membership. If overlapping group membership is supported, translate each member ID once per move, and define multi-group edit/undo behavior; a simpler one-group-per-member policy is acceptable initially if clearly communicated. Render group backgrounds behind wires, nodes and notes; avoid making the frame a large invisible interaction blocker. Delete group removes only the frame. Moving a group is a single layout/annotation operation restoring its bounds and member positions on undo.

A visual group can later become an actual composite via an explicit validated structural operation under Phase 7 graph grouping. That operation belongs to the composite implementation, not annotation history; preview the boundary and reject non-equivalent or invalid conversions. Never implicitly convert a group because it was renamed, moved or collapsed.

#### Visual groups — implementation (6.19.3, implemented)

Implementation: [PR #30](https://github.com/wgabel/orbit2/pull/30).

Choose **Create visual group** in the workspace toolbar after selecting nodes (or a note), or clear the selection to create an empty frame. The frame is initially fitted around measured selected items, with header space. Use **Edit** on the frame to set its title, description, semantic accent, and explicit member checkboxes. Each node or note can belong to at most one group in its graph; items already assigned to other groups are excluded from the editor. Empty groups are valid. Membership never changes automatically when objects overlap, move independently, or the frame is resized.

Drag the header to move the frame and its live members together; cables follow their endpoints during the preview. The bottom-right handle (also operable with arrow keys) changes only the frame size. Fit includes group bounds. Delete the group to remove just the frame, retaining all members, positions and connections. Group changes share the 100-operation annotation history with notes and descriptions. Moving a group commits one operation on release and restores exact member positions and frame bounds on undo/redo. Escape cancels gestures or an open edit draft.

The optional schema-1 `SavedCanvas.visualGroups` array defaults to empty for older documents. Each entry contains a stable ID, title (up to 120 characters), description (up to 16,000), semantic accent, finite world-space bounds (minimum 260 × 120, maximum 2,000,000 per dimension; origins within ±1,000,000), and graph-local node/note IDs. Documents reject duplicate IDs, unknown or cross-graph members, multiple membership and invalid bounds; limits are 200 groups per canvas and 512 members per group. Deleted targets are excluded from exported layouts; dormant membership can be restored when graph or annotation undo restores the original ID. Restoring a removed child graph also restores its group layout. A composite is moved as a single parent node, never recursively.

Example entry (IDs are illustrative and must refer to actual same-graph items):

```json
{
  "id": "a0000000-0000-0000-0000-000000000001",
  "title": "Drone voices",
  "description": "Sustained harmonic layer",
  "accent": 0,
  "x": 100, "y": 100, "width": 900, "height": 600,
  "nodeIds": ["b0000000-0000-0000-0000-000000000001"],
  "noteIds": []
}
```

The group is a layout annotation only: it creates no graph commands, DSP, signal ports, rack or playback revision changes. Structural conversion to a composite remains a separate planned feature.

### Patch overview and discovery (6.19.4)

Provide a Patch overview panel reachable from the existing workspace/patch manager. Keep the editor canvas uncluttered; expose a compact summary in the library. Suggested fields:

| Field | Example |
|---|---|
| Title | The Distant Horizon |
| Intended sound / description | A dark evolving soundscape combining an open fifth pedal and sparse upper melody. |
| Usage / performance instructions | Start playback, introduce the melodic voice at 1:15, fade textures toward the end. |
| Tags | Ambient, generative, space, A minor |
| Optional descriptive metadata | Intended duration 10 min, intended tonal center A minor, recommended tempo 72 BPM; do not treat as automatically enforced scheduling or tuning. |

Prefer the real project transport value when showing current BPM; distinguish a recommended tempo in prose from the actual setting. Do not infer exact duration or key from arbitrary notes. Metadata is optional and descriptive, not a second project tempo, transport, scale quantizer or audio configuration. Search/filter saved patches by title/name, tags and description, with case-insensitive matching and clear empty states. Do not run DSP or load a patch into the active workspace to display search metadata; use validated lightweight saved document metadata or a derived index if needed.

Example for agents: use overview text to give duration, musical concept, manual actions and assumptions; visual groups identify arrangement lanes; node aliases mark voices and control functions; a few note cards explain a tricky harmonic choice or modulation scheme. For an individual effect preset, a short note describing what it does may suffice. Use meaningful descriptions, not a repetition of every parameter value; separate unverified musical intent from actually auditioned/verified behavior. Once implemented, include these fields in generated patch JSON according to the current schema, not hypothetical keys.

#### Patch overview and discovery — implementation (6.19.4, implemented)

Implementation: [PR #32](https://github.com/wgabel/orbit2/pull/32).

Open **Patches → Edit patch overview** from any canvas. The overview describes the entire patch, not the currently open composite. Edit title, multiline description, usage instructions and comma-separated tags. Apply commits the complete draft and schedules recovery; Cancel or Escape discards it. An open draft is protected by the annotation draft warning and blocks patch replacement, graph navigation, other annotation edits and current-patch save/export. Editing while playing does not change the graph revision, transport, playback or graph history. Overview edits are patch-wide and are not entries in the per-canvas annotation undo history.

The optional top-level schema-1 `overview` property (not inside a canvas layout) defaults to empty metadata when omitted:

```json
"overview": {
  "title": "The Distant Horizon",
  "description": "An intended evolving A minor soundscape with a quiet open-fifth pedal.",
  "instructions": "Intended duration: three minutes. Fade the melodic layer in around 1:15. Recommended tempo: 72 BPM.",
  "tags": ["Ambient", "Space", "Generative", "A minor"]
}
```

Title is limited to 120 characters, description and instructions to 16,000 each, and tags to 24 unique trimmed strings of 1–40 characters (case-insensitive uniqueness, no commas or control characters). The editor trims tags, removes empty entries and deduplicates them; malformed imported metadata is rejected (including explicit null). The metadata is descriptive only: instructions about tempo/duration/key do not alter `beatsPerMinute`, automatically schedule events or quantize pitch. Save, overwrite, recovery and export capture committed metadata; opening/importing replaces it atomically with the patch, and New patch starts empty.

The library save name remains independent of the musical title. The saved-patch picker displays a selected snapshot's overview without opening it. Search matches whitespace-separated terms case-insensitively against save name, title, description and tags (all terms must match somewhere); the optional tag filter is an exact case-insensitive match. Clear filters restores the list; hidden selection is cleared to avoid unintended overwrite/delete. The library reads overview fields from stored JSON without reconstructing a graph or starting DSP. Unreadable or unsupported metadata is identified separately and the entry remains selectable by save name for deletion or an explicit load attempt; full validation remains the normal load path. Named snapshots do not track current workspace edits until explicitly overwritten.

**Annotated examples** in Patches exports a fresh ordinary portable JSON patch without changing the active workspace or named library. **Modulation study** explains the existing oscillator modulation demo; **Impact — AD percussion** includes the existing impact composite routed to a master output, root and nested notes, aliases, and overview instructions. Import the exported file to inspect or edit it. Both examples use existing graph factories and validated persistence; their annotations are learning aids, not claims of musical mastering or listening verification.

<a id="2-annotation-visibility-and-zoom-dependent-detail-6197--in-review"></a>

## 2. Annotation visibility and zoom-dependent detail (6.19.7 — implemented)

Provide a compact shared canvas toolbar control for documentation visibility, ideally a labelled toggle/menu with at least Show and Hide (optional Titles only). Keep numeric cable-amount visibility and audio visualization settings in their own controls: hiding author-written notes must not hide necessary port labels or change graph configuration. A performance-friendly clean view can hide notes, group backgrounds/titles and optional authored wire descriptions, while retaining clearly readable node aliases as primary identifiers. Allow access to descriptions through the selection inspector or outline even if canvas decorations are hidden. Do not change the owning graph, note text, manual collapse state or membership.

Zoom detail is a derived visual projection, separate from authored collapse and user visibility preference:

| Situation | Proposed presentation |
|---|---|
| Normal zoom | Show card title and body unless manually collapsed, group frame and title, optional wire description if space allows. |
| Low zoom | Show note title/icon and group title/frame only; suppress low-priority secondary labels. |
| Very low zoom | Compact note marker/name and group boundary when legible; keep selected targets accessible. |
| Hidden annotations | No decoration hit targets or invisible blockers; access notes via outline and explicit reveal. |

Use shared configurable screen-space thresholds/hysteresis and theme tokens; tune after checking actual measured readability at the existing zoom range. Do not persist auto-collapsed state or resize/reposition authored world bounds whenever zoom changes. Editing/selected items must remain accessible: finish or explicitly retain an active text draft before hiding its editor, avoid abrupt focus loss at a threshold, and allow a temporary full-size editor/inspector. Fit-to-content should include visible annotation bounds and support an explicit signal-only fit or reveal-target action; hidden documentation must not unexpectedly force distant empty extents. A hidden or auto-summarized item remains saved and discoverable.

Persist per-canvas annotation display preference in the existing optional `SavedCanvas` layout settings, with visible annotations as a backward-compatible default. If a workspace-wide toggle is offered, document whether it is a temporary override (recommended for clean performance mode) rather than rewriting every child preference. Visibility and zoom are presentation changes, not annotation content/history edits. Navigation preserves an active canvas's preference. No graph revision or audio/capture lifecycle changes beyond existing independent view visibility decisions.

<a id="implementation-6197-in-review"></a>

### Implementation (6.19.7, implemented)

Implementation: [PR #35](https://github.com/wgabel/orbit2/pull/35).

Use **Annotations** at the lower left of the canvas:

| Mode | Behavior |
| --- | --- |
| Show | Normal authored note cards and group frames; automatic compact detail when zooming out. |
| Titles only | Compact title markers even at normal zoom, with group boundaries retained. |
| Hide | Removes unselected notes and group decorations from the DOM, hit testing and fit calculations. |

Node aliases and canonical names, port labels, numeric amount badge preferences and signal plots remain independent. Secondary node descriptions and cable label tooltips are suppressed in compact/hidden views; selecting a node or cable still allows reading and changing its description through **Describe selected** or the connection editor.

The shared `AnnotationDetailPolicy` enters title detail at 65% zoom and returns to full detail at 75%; it enters the smaller marker band at 35% and leaves it at 45%. Separate enter/exit thresholds provide hysteresis, preventing repeated changes around a zoom boundary. Compact markers are screen-scaled (30px height, maximum 170px width, reduced to 100px in the smallest band and limited by the authored width); full titles remain accessible as labels/tooltips. Compact notes show only the projected marker, groups retain their authored background and boundary. Selected/focused notes and groups retain their ordinary controls and manual collapse state even when zoomed out. Activating a marker selects it; deselecting returns it to current display detail. None of these projections writes back dimensions, title, body, group membership, collapse state or annotation history.

Changing the display mode clears note/group selection, but preserves node/cable selection; the control and reveal actions are disabled while an annotation or overview draft is open. **Find annotation** lists the current canvas's notes and groups by title, including hidden items; choose one and **Reveal and fit** selects and fits it without changing the stored mode. Selection of a hidden item temporarily reveals it until selection changes. Use **Fit signals** to fit measured node bounds only. Normal Fit excludes hidden items; Show uses authored visible bounds so a note revealed by the resulting zoom can still fit, Titles only uses a compact header extent for unselected notes and the frame for groups. The full searchable nested outline is still Phase 6.19.8.

Compact note and group markers handle Enter and Space explicitly, as well as pointer activation, because their activating element is replaced by the full card/frame. Compact-marker focus does not invoke the ancestor's full-card focus handler; Tab remains available for navigation. The published editor regression exercises both keyboard paths.

Schema-1 layouts add optional `annotationVisibility`: `0` (Show, also the default when absent), `1` (Titles only), or `2` (Hide). Invalid enum values are rejected on restore. Root and child canvases save independent preferences in recovery and portable files; selection, finder state and zoom-detail hysteresis are transient. Visibility and viewport edits do not enter graph or annotation history and do not create graph/audio events.

## 3. Patch outline and navigation (6.19.8 — planned)

Large nested patches need a lightweight outline similar to a project navigator. Add one workspace-level outline panel derived from the existing immutable root graph and graph-qualified layout snapshots; do not maintain a second editable signal topology. Example:

```text
The Distant Horizon
  Harmonic bed (visual group)
    Drone A (composite)
      Slow brightness drift (oscillator)
      Harmonic foundation (note)
  Melody (visual group)
    Motif sequencer (note sequencer)
    Performance cues (note)
  Spatial effects (visual group)
    Dark reverb (reverb)
```

Display root and nested canvas hierarchy, named groups, composites, authored notes and significant named nodes, with filters/search and optionally an expandable list of all nodes. Distinguish a visual group from real graph ownership: a group's member may be visually organized beneath it without implying a child graph, and a member in multiple groups (if supported) still represents one graph-qualified target. Use icons, type and breadcrumb context to distinguish same-name targets. Keep counts and expensive tree projections bounded; default to collapsed branches and lazy expansion. No per-sample telemetry required.

Selecting an entry navigates through the existing `CanvasNavigationService` to the owning graph, selects the target by stable ID, and uses shared measured viewport fit/focus geometry to bring its bounds into view. For a composite, support locating it in its parent and explicitly opening its child. For hidden notes, offer temporary reveal/focus and a clear path to restore clean view. Do not mutate authored collapsed state merely to navigate. A group selection may fit its frame/members, and a note/alias selection may open the relevant inspector when requested. Restore viewport and prior canvas preferences on normal back navigation. Follow draft Apply/Cancel rules if switching graphs while editing.

Update the outline after graph/layout commits, including group membership edits, renamed aliases, deleted nodes, removed descendants, undo/redo, imported or replaced patches. Clear stale search results safely; never follow an orphaned ID after replacement. Search committed content only (or clearly marked draft preview), avoid hidden secondary selection state and keep keyboard navigation accessible. Outline open/search/expanded branches can be transient UI preferences; these are not musical metadata required in portable documents. No graph commits or playback changes from outline navigation; ordinary visible-plot lease reconciliation on canvas changes remains the existing visualization lifecycle, and the outline itself requests no captures.

## 4. Integration and acceptance checklist

Keep the original state/persistence/history contracts above authoritative. When implementing reuse of composites, preserve annotations inside the selected/instantiated graph by remapping their IDs and graph/node/connection/member references with the same clone/transform transaction; leave unrelated parent annotations intact. Explicitly decide how to carry a relevant note from a source patch into a module and how to handle annotations targeting a crossing edge. Never silently share mutable annotation text between module instances. Existing old patch files have no annotation data and should remain valid; future fields must have explicit validation and compatible migrations.

| Milestone | Additional verification |
|---|---|
| Note cards | Create/edit/collapse/resize/duplicate at root and child; long/multiline text remains readable; draft navigation and native text undo; reload and portable JSON. |
| Aliases/labels | Long/duplicate names, canonical type and amount retained, dormant connections, deletion/graph undo restores correct target association. |
| Groups | Create from selection, manual membership and resizing, move once per member, no implicit deletion, frame behind nodes/wires, exact layout undo. |
| Patch overview | Named library metadata search, description/tags survive independent snapshots and export/import; no accidental patch rename or tempo change. |
| Visibility/zoom | Root/child independent defaults and persistence, hidden items not hit-testable, automatic detail changes keep committed text/bounds/collapse, drafts and focus protected. |
| Outline | Root/nested jump, same aliases, hidden note reveal, fit group/composite, keyboard access, deleted/restored targets and patch replacement; no stale references. |
| Audio isolation | During continuous playback perform every annotation and navigation action and verify unchanged root graph revision, DSP source/runtime generation and no graph-history entry. |
| Agents / examples | Annotated example patch explains roles and actual settings, overview provides musical purpose and manual instructions, optional metadata remains data-only and valid. |

These are separate deliverable increments in the [Phase 6.19 register](../roadmap/editor-and-effects.md#phase-619--patch-documentation-and-canvas-organization-planned). With 6.19.1–6.19.4 implemented and 6.19.7 implemented, implement 6.19.8 outline and navigation next. Record actual implementation PR links here and in the roadmap as features ship.
