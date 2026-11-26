# Direct canvas connection amount slider

[Existing connection contract](annotations-and-connections.md) · [Roadmap](../roadmap/editor-and-effects.md#phase-619--patch-documentation-and-canvas-organization-partial-6195-core-and-6196-implemented) · [Interaction registry](user-interactions.md)

## Design (planned, not implemented)

The current cable badge selects the connection and opens an anchored amount editor; adjusting a value requires extra interaction. Make a normal horizontal range slider directly on the cable the primary amount control, with no popup, preliminary selection, or Apply button for ordinary slider edits. The existing inspector remains available for other connection actions and hidden/covered controls. Do not introduce a custom vertical-drag handle, knob, percentage-only representation, or a new connection parameter.

### Value contract

- Bind the slider to the same graph-qualified connection and its existing signed finite `SignalConnection.Amount`. Resolve the legal minimum, maximum, step/precision, default, formatting, unit and any existing display-scale mapping from the authoritative amount editor/validation and shared presentation helpers. Audit the source and tests first: do not infer a universal 0–1 or ±100% range, or reuse destination base-value bounds as modulation-depth bounds. If the current editor has no finite range or step, document that gap and obtain approval for a shared range policy before imposing a limit; preserve every existing legal amount and exact numeric entry.
- Reuse the normal shared slider styling and native range interaction; share value metadata with the existing amount editor rather than duplicate constants. Keep numeric values in actual units: direct parameter modulation uses the target unit; audio/intermediate routing uses a dimensionless multiplier. Preserve negative values, zero, unity and existing dormant semantics. A zero marker and signed fill may distinguish bipolar values; do not make color the only cue.
- Typed Trigger, Gate and Clock connections retain their unity event semantics and type badges; never show an adjustable amount slider where editing is not supported. Respect current active/dormant eligibility and validation.
- Show the current numerical amount above the rail. Clicking the value enables precise inline numeric entry on the canvas (Enter/blur commit valid values, Escape cancels, invalid/non-finite values do not mutate the graph). Reuse validation and formatting; keyboard range operations and accessible names/physical value text must work without capturing performance keys.

### Placement and visibility

Replace the numeric badge on eligible cables with a compact horizontal slider centered at the existing mathematical cubic Bézier midpoint. Reuse shared geometry and graph-local projection for root and nested canvases. Keep control dimensions and touch targets stable in screen space while the cable moves, pans or zooms; use existing theme tokens and connection colors. Preserve node and port positions, rigid sleeves, hit paths, cable selection, reconnection and context actions. No collision solver or manual slider positioning in the first version: sliders may overlap and remain behind nodes; covered controls can be accessed through normal cable selection/inspector.

In the default All connection-label mode, eligible sliders are directly visible without selecting cables. Preserve existing All/Modified/Selected/Off per-canvas visibility settings and event badges; in Off the inspector remains available. Do not introduce a second preference or silently change saved documents. Low-zoom automatic suppression is an existing optional refinement, not a prerequisite for the slider.

### Editing and runtime behavior

Pointer/touch/keyboard interaction with the standard rail must not open the inspector, pan the canvas, start a wire, select another edge, trigger long press, or count as a fit tap. A two-finger pinch retains universal navigation priority: cancel/restore incomplete edits without a history entry or loss of redo. Handle pointer cancellation, lost capture, deletion, graph changes and navigation safely; never write to stale graph-qualified IDs. Keep ordinary wire click/context actions outside the slider unchanged.

Update sound during an adjustment using the established validated command/playback path; reuse existing gesture transaction patterns to coalesce a completed drag into one undoable edit, and restore the original amount on cancellation. Undo/redo, load/import and inspector changes refresh the rail and numeric value from the graph. Do not rebuild graph topology, recompile DSP unnecessarily, create independent audio state or add per-edge polling. Audit the current amount-update runtime behavior before promising cheap live updates; if a safe live path requires audio-engine changes, document and route that dependency through the existing audio optimization gate before changing runtime code. Coalesce UI updates as needed and measure behavior rather than claiming performance gains.

### Verification and integration

Test the actual metadata/range/step/unit and round-trip for supported connection categories, signed/zero values, event-only exclusions, precise numeric entry and invalid input; compare slider and inspector edits and verify save/load and undo/redo. Test midpoint positioning and constant screen size on zoom/pan/node movement, overlapping/hidden sliders and nested canvases. Cover mouse, touch, keyboard, pinch cancellation, context actions, wire drag and performance-key isolation in published browser tests. Exercise stopped and active playback, check for unwanted compilation/history growth, measure representative dense-patch drag responsiveness and report physical-device limitations. Update actual usage and the interaction registry when behavior ships. Keep the view model renderer-independent for the planned GPU editor; no GPU migration is required for this feature.

Implementation status and ordered slices belong in the linked milestone register. This design does not change application behavior.
