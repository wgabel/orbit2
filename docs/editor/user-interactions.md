# User interaction and gesture registry

> **Authoritative, agent-facing interaction inventory.** Audited against the `main` source and tests on 2026-11-23. This document records currently implemented UI interactions, known discrepancies, and the collision policy for future changes. A design proposal is **not** an implemented gesture. Recheck code and browser tests before changing an entry, especially while other PRs are open.

## Mandatory workflow for UI agents

Before adding or modifying any mouse, touch, pen, keyboard, hover, focus, or gesture behavior:

1. Read this registry, then the affected feature's UI/JS code and existing browser tests. Check relevant open PRs for overlapping gesture work.
2. Identify the target surface, pointer types, gesture recognition thresholds, current owner, and cancellation/precedence rules. Search across all controls that could receive the same event, including capture-phase listeners.
3. Prefer explicit visible controls when a gesture is already assigned or ambiguous. Do **not** silently repurpose a click, long-press, double-tap, triple-tap, modifier, or key. Preserve keyboard and accessible alternatives.
4. Keep DOM gesture detection in the existing shared adapter and graph mutations in the established commands; do not create competing listeners per node or alternate desktop/mobile graph state.
5. Update this registry **in the same PR** as a gesture or shortcut change, including behavior, surface, platforms, conflict resolution, source/test references, and PR evidence. Update related usage documentation, and add focused unit/UI and published-browser tests as appropriate.
6. Mark new gestures *planned* until implementation and verification are complete. Clearly label discrepancies instead of inferring behavior from outdated prose.

For general canvas behavior see [canvas](canvas.md), [design system](design-system.md), [usage](../usage/patch-editor.md), and [keyboard/pitch usage](../usage/sound-and-instruments.md).

## Event ownership and priorities

The root and nested canvas share `GraphCanvas`, `NodeFrame`, `PortConnector` and the same DOM adapters; do not introduce different gesture meanings in child graphs.

- `src/Orbit.UI/wwwroot/js/canvasContext.js` routes touch contacts at document capture phase, provides contextual actions and universal two-finger navigation. It excludes interactive controls from long-press targeting; the 450 ms touch hold is **not** a generic tooltip gesture.
- `src/Orbit.UI/Components/Canvas/GraphCanvas.razor.cs` and `GraphCanvas.Navigation.cs` own pan, node drag, cable drag, fit-tap recognition, wheel zoom, and keyboard navigation.
- `src/Orbit.UI/Components/Canvas/PortConnector.razor[.cs]` owns socket activation and output pointer-down, with `GraphCanvas` handling connection completion. `SignalGraphValidator` remains authoritative for routing.
- `src/Orbit.UI/wwwroot/js/workspacePanels.js` and workspace components own sheet/popover dismissal, Escape and browser Back.
- While two touch contacts are used in graph-world UI, **pinch/navigation wins** over pending node movement, cable dragging, or parameter interaction. The takeover cancels or rolls back incomplete edits; cancellation must not create graph changes. A new help interaction must respect this.
- Native buttons/inputs, annotation editors, plots and performance keys retain their own gestures and are normally excluded from the one-finger context-menu target set. Explicit help controls must suppress pointer-down propagation to the node drag handler and avoid re-triggering downstream click/keyboard actions.

## Implemented interaction matrix (audited main)

| Surface | Input / action | Behavior | Collision / qualification | Implementation evidence |
| --- | --- | --- | --- | --- |
| Empty canvas | Primary click or single tap | Clear selection; start background gesture | A quick completed tap can count toward triple-fit; a drag pans instead | `GraphCanvas.razor.cs` `BeginBackground`, `GraphCanvas.Navigation.cs` |
| Empty canvas | Primary drag / one-finger drag | Pan | In Marquee mode or with Shift modifier, selects instead | `GraphCanvas.razor.cs` `BeginBackground`, `PointerMoved` |
| Empty canvas | Three quick clicks/taps at nearby points | Fit active graph | Three eligible completed taps within 600 ms / 24 CSS px; each tap has no >8 px drag or >250 ms hold. Only empty canvas, not node, connector, note or button | `CanvasFitTaps.cs`, `GraphCanvas.Navigation.cs`, `tests/browser/CanvasNavigationSmoke.mjs` |
| Canvas world (even controls/large node) | Two-finger pinch / midpoint motion | Zoom and pan | Takes priority over existing one-finger actions; range/slider and unfinished edits are restored/cancelled; one remaining finger can continue panning | `canvasContext.js`, `GraphCanvas.Navigation.cs`, `tests/browser/CanvasNavigationSmoke.mjs` |
| Canvas | Mouse wheel | Zoom around mouse pointer | Wheel on an open context overlay must not zoom the canvas | `GraphCanvas.razor.cs` `OnWheelAsync`, `canvasContext.js` |
| Node header | Primary click/tap | Select node; focus relevant performance target where applicable | On pointer down; Shift/Ctrl/Meta on desktop adds/toggles selection via existing selection logic. Do not assign general single tap to help | `NodeFrame.razor`, `GraphCanvas.razor.cs` `BeginNode` |
| Node header | Primary drag | Move node | Middle-button header drag is canvas pan instead; two-finger takeover cancels/reverts | `NodeFrame.razor`, `GraphCanvas.razor.cs`, `GraphCanvas.Navigation.cs` |
| Composite macro slider (S3b, merged, [PR #161](https://github.com/wgabel/orbit2/pull/161)) | Native range drag, touch or keyboard; numeric input | Edit normalized macro base | Same shared parameter slider, gesture history and two-finger pinch cancellation; no new gesture, no node drag or expansion | `NodeFrame.razor`, `GraphWorkspace.razor.cs`, `tests/browser/PublishedPlaybackSmoke.mjs` |
| Composite wrapper / macro editor (S3c.2, merged, [PR #165](https://github.com/wgabel/orbit2/pull/165)) | Edit macros button, native inputs/selects, Apply or Cancel | Draft and commit mapping definitions | Explicit controls on both layouts; pointer-down on wrapper button cannot drag node; panel outside canvas, no new gesture; navigation/tool changes discard draft; revision guard rejects stale Apply | `NodeFrame.razor`, `CompositeMacroEditor.razor`, `GraphWorkspace.Macros.cs`, `tests/browser/PublishedPlaybackSmoke.mjs` |
| Noninteractive node surface | One-finger drag | Pan (not edit controls) | Target determined by `isNodeNavigationSurface`; buttons, sliders, ports retain their actions | `GraphCanvas.Navigation.cs` `BeginNodeSurfaceAsync`, `canvasDom.js` |
| Eligible canvas/node/cable/note/group surfaces | Touch-and-hold for 450 ms (<=8 CSS px movement) | Open contextual actions sheet | Movement/release/cancel/second contact cancels. The original release/click is consumed. Port and parameter controls are deliberately excluded from long-press targets | `canvasContext.js` `HOLD_MS`/`HOLD_SLOP`, `GraphCanvas.Context.cs`, `tests/ui/CanvasContext.test.mjs` |
| Eligible canvas/node/cable/note/group surfaces | Right-click / context menu | Open contextual actions popup | Context target excludes native interactive controls including connector buttons | `canvasContext.js` `contextTarget`, `GraphCanvas.Context.cs` |
| Focused eligible canvas element | Context Menu key / Shift+F10 | Contextual actions | Only where `contextTarget` recognizes the focused target | `canvasContext.js` `key` |
| Output connector | Mouse/touch/pen drag | Draft wire; snap to compatible input; release connects | Wire action begins from output pointer-down; requires movement (6 CSS px threshold for assisted drag); do not use connector hold for help. Touch target is offset above finger | `PortConnector.razor.cs`, `GraphCanvas.razor.cs`, `TouchWireAssist.cs` |
| Output then input connector | Click/tap output, then click/tap input | Connect via port activation | Keyboard activation (Enter/Space button click) also supported. A tap on an input without pending output selects its node | `PortConnector.razor.cs`, `GraphCanvas.razor.cs` `ActivatePortAsync` |
| Wire drag near viewport edge | Hold dragged cable near edge for 150 ms after movement | Auto-pan toward off-screen target | Outer 50 CSS px; pinch takeover, cancellation or acquiring target stops panning | `GraphCanvas.TouchWire.cs`, `WireEdgePan.cs`, `tests/browser/WireEdgePanSmoke.mjs` |
| Connector button | Pointer hover / keyboard focus | Native HTML `title` / accessible name showing direction, name, signal type and inactive state | Currently a basic browser tooltip, **not** an implemented rich help popup | `PortConnector.razor`, `PortConnector.razor.cs` |
| Cable / numeric badge | Click/tap | Select connection; opens its amount/inspection editor when single selected | Does not mean help. Cable SVG hit path is focusable; Enter/Space selects | `ConnectionPath.razor`, `ConnectionAmountBadge.razor`, `GraphCanvas.razor` |
| Canvas background | Shift + drag, or Edit > Selection actions > Marquee mode | Rectangular multi-node selection | Ctrl/Meta can add a rectangle to the selection; explicit mode on touch. Escape/Return to pan exits | `GraphCanvas.Selection.cs`, `GraphCanvas.razor.cs`, `docs/editor/reusable-modules.md` |
| Note / group | Header drag; resize handle; action buttons | Move, resize, select/edit, duplicate/delete (as supported) | Handles own pointer events; keyboard arrows resize when handle is focused | `NoteCard.razor`, `VisualGroupFrame.razor` |
| Composite node | Explicit `Open …` button / contextual `Open composite` | Navigate into nested graph | Confirmed. See discrepancy below for claimed double-click behavior | `NodeFrame.razor`, `GraphCanvas.Context.cs` |
| Node with user-authored description | `ⓘ` button (when displayed) | Edit/read existing authored node description | Different from a proposed built-in `?` help control | `NodeFrame.razor`, `DescriptionEditor.razor` |
| Visible toolbar/control | Click/tap | Executes its named action (Play, Stop, Nodes, Edit, etc.) | Desktop anchored popovers / mobile sheets share state; these controls are not empty canvas | `GraphWorkspace.razor`, `workspacePanels.js` |
| Open tool sheet or context dialog | Escape, Back, Close, or backdrop as supported | Dismiss current overlay | Protected drafts may block dismissal; modal/focus behavior is owned by the respective component | `workspacePanels.js`, `GraphCanvas.Context.cs` |

### Keyboard shortcuts and keyboard-specific behavior

| Scope | Key | Behavior / caveat |
| --- | --- | --- |
| Focused canvas, not an editor/control | `F` | Fit current graph |
| Focused canvas, not an editor/control | `+` / `=` / `-` | Zoom in/out |
| Focused canvas, not an editor/control | `0` | Reset canvas viewport |
| Canvas | `Escape` | Cancel gesture/pending cable and/or selection/marquee; an open contextual UI may handle Escape first |
| Canvas or eligible focused element | `ContextMenu` / `Shift+F10` | Open context actions |
| Armed performance keyboard | `A S D F G H J K`; `W E T Y U`; `Z`/`X` | White/black piano keys and octave controls; **armed piano owns `F` instead of canvas Fit**, even with canvas focus. Editable fields and modified shortcuts are excluded |
| Focused connection hit path | `Enter` / `Space` | Select connection |
| Focused standard button / port | `Enter` / `Space` | Native activation via click; preserve semantic button accessibility |

Sources: `GraphCanvas.razor.cs`, `canvasContext.js`, `PerformanceKeyboard.razor`, `docs/instruments/pitch-control.md`, `docs/usage/sound-and-instruments.md`.

## Documented but not confirmed in main

- **Double-click / double-tap composite header to open:** `docs/usage/patch-editor.md` previously instructed double-click, and Phase 6.21 planning mentions double-tap, but the audited shared `NodeFrame.razor` does **not** bind `@ondblclick`, nor is a composite double-tap recognizer present in the reviewed canvas handlers. Use the explicit `Open` button/context action. Treat this as **planned/unverified**, not an available gesture; verify after relevant PRs merge.
- **Compact-node expansion (implemented, [PR #100](https://github.com/wgabel/orbit2/pull/100)):** Full controls remain visible by default until S4. Each leaf node has a chevron to collapse/expand its details; stationary primary header click/tap or noninteractive body tap reopens collapsed details. Drag movement over 8 CSS px, modified selection, connectors, native editing controls and two-finger pinch do not toggle. Composite/rack/master nodes remain full. Automatic collapse/pinning follow in the [expandable node roadmap](../roadmap/editor-and-effects.md). Planned property pin toggle is separate from the node keep-open pin: button click/tap or keyboard activation toggles visibility override and folded live eligibility without initiating node expansion, selection, drag or a parameter edit; retain focus and protected drafts.
- **Original compact property editor (S3a, in review, [PR #135](https://github.com/wgabel/orbit2/pull/135)):** The duplicate port-side numeric input from PR #101 is removed. No property editor remains mounted while folded, even for modulated properties; expand the node to edit. Numeric edits use the original input validation and command behavior, not the former separate Enter/blur/Escape draft implementation. All sockets remain visible; no gestures are reassigned.

## Node and connector help (in review, PR #94)

The interaction behavior below is implemented in the [feature PR #94](https://github.com/wgabel/orbit2/pull/94), **not yet confirmed merged into main**. Other gestures remain as audited. Review the PR and browser regressions before treating these changes as released.

| Surface | Desktop | Mobile/touch | Reason |
| --- | --- | --- | --- |
| Connector | Noninteractive short explanation on hover and keyboard focus; preserve click/drag | **No long-press or ordinary tap for help**; explanations available inside node `?` popup | Connector hold conflicts with output wire preparation/drag and existing touch priorities; tap already selects/creates connections. Hover is unavailable on touch |
| Node `?` control | Click / Enter / Space opens a small accessible anchored popup | Tap opens a dismissible, screen-space popup/sheet | A distinct button does not steal node selection, move, context or connector gestures |
| Node help content | Brief node purpose, connector meanings, two or three practical wiring examples | Same information in mobile layout | No Related Topics, document-link browsing, or topic navigation feature |
| Exit | Close, Escape or backdrop; focus returns to trigger | Close, browser/Android Back, Escape or backdrop; pinch remains available on the canvas after dismissal | A temporary mobile history entry closes the sheet without leaving the patch |

Requirements for implementation: use the shared node/connector components, keep help UI **outside the transformed world** so it remains readable at low zoom, prevent pointer-down propagation from `?`, ensure no accidental node move or wire activation, close on navigation/removal, keep focus management and reduced-motion behavior accessible, and do not mutate graph state or trigger audio compilation. Tests must exercise real desktop hover/focus and mobile `?` tapping alongside existing long-press, wire drag, pinch, triple-fit, context actions, selection and overlays.

## Maintenance/verification checklist

- Verify primary click/tap, double click/tap, long-press, right-click, drag, multi-touch and keyboard for **every modified target** on both root and nested graphs.
- Test at low/high zoom, for dense or overlapping nodes, with port hit targets, with a modal/sheet open, and with the performance piano armed.
- Check cancellation, pointer capture, focus restoration, event propagation, and stale graph identity; ensure no accidental graph changes or playback recompilation.
- For each newly supported action record: target + input, precedence, conflict strategy, accessible alternative, source/test path, status, and actual implementation PR.
- Use actual code/tests as implementation evidence; keep aspirational milestones distinguishable from current behavior.

### Note Sequencer: selected note length (merged, [PR #111](https://github.com/wgabel/orbit2/pull/111))

The existing Edit notes panel exposes a labeled native numeric input for populated steps in Notes mode. Click/tap or focus to edit the selected slot's gate fraction; normal change commits through the existing parameter-edit command. Keyboard capture remains excluded inside the input, and the input is disabled during recording. No canvas, grid or piano gesture is reassigned. Source: `PerformanceKeyboard.razor`; regression: `tests/browser/PublishedPlaybackSmoke.mjs`.

### Note Sequencer: selected note velocity (CI verified; awaiting merge, [PR #115](https://github.com/wgabel/orbit2/pull/115))

In the existing Edit notes panel, a populated Notes-mode step exposes a labeled native select with Default and 0–100% options. Mouse/touch or native keyboard selection commits through the normal validated parameter edit command; Undo/Redo restores the saved slot value. Focus in the select excludes piano capture, recording disables editing, and Rest/Tie or Pitch-only mode hides the control. No new gestures or node layout changes. Source: `PerformanceKeyboard.razor`; regression: `tests/browser/PublishedPlaybackSmoke.mjs` (desktop and narrow viewport).

### Note Sequencer: selected note timing (merged, [PR #119](https://github.com/wgabel/orbit2/pull/119))

In Edit notes, a populated Notes-mode slot exposes a labeled native Timing offset select and a calculated millisecond readout. Pointer, touch and native keyboard selection use the existing validated parameter edit path and undo/redo; input focus excludes piano capture. Recording disables editing and Rest/Tie or Pitch-only hides the control. No new gestures or alternate mobile editing path. Source: `PerformanceKeyboard.razor`; regression: `tests/browser/PublishedPlaybackSmoke.mjs`.

### Note Sequencer: Humanize tab (in review, [PR #121](https://github.com/wgabel/orbit2/pull/121))

The existing melody editor tab bar opens Humanize. Native select/number inputs configure timing direction, amount, velocity variation, seed and selected-step/range/whole-pattern scope. Apply humanization is an explicit button; changing inputs does not edit the graph. Disabled during recording, in Pitch-only mode (no controls), on invalid or empty ranges and at zero amounts. One undo/redo entry restores all changed values. Editable focus follows the existing keyboard exclusion policy; no new gestures or global shortcuts. Source: `PerformanceKeyboard.razor`, `GraphWorkspace.razor(.cs)`; regression: `tests/browser/PublishedPlaybackSmoke.mjs`.
