# Phase 6.21.5 — Expandable node design contract (approved; staged implementation)

Status: revised design approved; S1 and S2 merged ([PR #100](https://github.com/wgabel/orbit2/pull/100), [PR #101](https://github.com/wgabel/orbit2/pull/101)). Their compact-specific value editors are superseded by the planned refactor. Track implementation and verification in [the editor roadmap](../roadmap/editor-and-effects.md).

This document supersedes the earlier Phase 6.21.5 proposal in [the editor roadmap](../roadmap/editor-and-effects.md). It describes the agreed target rather than claiming existing product behavior. Implementation must cross-check the actual registered node definitions and their active parameter/port rules.

## 1. Scope and firm decisions

- Use the same **compact-by-default, click/tap-to-expand** node behavior on desktop and mobile. Do not introduce a separate inspector.
- **All nodes remain visible on the canvas at all times. No inactive-node visibility filtering, hiding or collapsing entire nodes.**
- **Every input and output port remains individually visible in both states**, whether connected, disconnected, active or inactive. Do not collapse, aggregate, dynamically hide or substitute a port with an ambiguous anchor.
- **Every parameter with a modulation capability remains visible in the compact node** even without a cable or when the parameter is temporarily inactive. Keep its original property editor, base value, unit and label; visually distinguish inactive state without removing the corresponding port.
- **Compact base values are directly editable**, with an appropriate numeric editor or choice control. Use the original editor directly without expanding the node. Live effective values are read-only.
- **Folded nodes are static with respect to playback telemetry.** Keep the editable base value, unit, label and modulation/connection state; omit instantaneous effective values from folded editors. Expanded nodes show **base → effective** when valid live telemetry is available. Never display a frozen value as current. Keep base values when stopped or telemetry is absent/stale; predicted ranges are authored-graph estimates, not live telemetry.
- Show essential non-modulatable type-specific information in compact nodes, as enumerated below.
- **Composite nodes are exempt from the redesign.** Preserve their existing appearance, controls, port labels, exposed ports and Open action in both nominal presentation states. Do not collapse any composite content. Preset instrument templates that instantiate a composite follow this rule. Likewise do not needlessly redesign Input/Output Rack or Master Output shells in this milestone.
- All sockets and labels form a persistent top section, separate from property editors. Natural node height changes and endpoint movement are permitted; measure actual socket geometry.

## 2. Visual hierarchy and compact editing

One shared NodeFrame renderer presents (1) icon/name title row, (2) buttons-only action row, (3) persistent top port section, and (4) the original property editors in their existing order. Folded and expanded states share a single renderer and property state.

- Folded visibility is driven by per-property importance metadata or modulation capability. Hide only less important original controls; never create duplicate base-value boxes, compact-specific editors or alternate summaries of existing controls. Preserve appropriate static previews, presets and actions; no animated previews or live playheads in folded nodes.
- Show base values in either state and read-only live effective values only in expanded nodes when applicable; retain existing units, validation, undo/redo, draft protection, audio updates and inactive styling. Resolve port identity by stable IDs, not labels. Missing telemetry must not invent live values.
- Remove category text from the header; strengthen existing shared category accents by approximately 5%, retaining semantic port/cable colors. Preserve accessible action labels and existing button behavior.
- Remove the duplicate icon and visible Preset label from the preset selector; give the dropdown the recovered width, retain accessible naming and management actions, and move no-match explanatory text into the menu.
- Remove replaced code as each slice lands. A mandatory post-refactor audit removes obsolete compact renderers, flags, components, bindings, event handlers, CSS, geometry calculations and stale tests/docs. Retain fallback code only with documented justification; preserve regression coverage and a single source of truth.

## 3. Per-node compact inventory (original editors to retain in addition to all routable ports)

The definitive source for modulatable parameters is each node's ParameterDefinition and associated ModulationPortId; **all of those values must be shown regardless of the essential-settings column below**. Each property editor must appear exactly once.

| Registered leaf node(s) | Additional essential compact information |
| --- | --- |
| Oscillator | Waveform; free/synchronized timing mode; division in sync mode; pitch-tracking mode. All input parameters (frequency, amplitude, phase, pulse width, pitch) retain ports and original editors; inactive ones are dimmed, not removed. |
| Noise | Noise color; amplitude input/value. |
| Pitch Control | Transposition in semitones and fine tune in cents (has no modulation input ports). |
| Note Source | Reference note, keyboard velocity and clear selected performance-keyboard target indication when applicable. |
| Note Sequencer | Pitch-only/drone versus Notes articulation, Live/Loop, step count, selected step timing, reference note, initial pitch and abbreviated pattern preview. Keep all four Pitch/Gate/Trigger/Velocity outputs visible. Do not render 64 step choice editors in compact mode. |
| Envelope | AD/AR/ADSR/Loop, trigger source when applicable, a small static envelope shape; all modulated attack/decay/curve/release/sustain/rest values and all trigger/gate sockets remain visible, dimmed when inactive. |
| Clock | Free/synchronized timing and rate/division. |
| Gate | Manual/trigger source and duration (plus the duration modulation readout). |
| Manual Trigger | A directly usable trigger action and its Trigger output. |
| Signal to Trigger / Signal to Gate | Conversion identity and respective input/output sockets; no invented threshold control. |
| Gain | Gain. |
| Mixer | All individual ports; no non-existent extra numeric setting. |
| Filter | Mode (low/high/band variants as supported), cutoff and resonance. |
| Parametric EQ | Frequency, gain and Q. |
| Delay | Free/synchronized mode; active time/division, feedback and wet mix. |
| Reverb | Decay, damping and wet mix. |
| Resonator | Frequency, pitch offset, decay, excitation gain, output gain and wet mix. |
| Chorus | Rate, depth, base delay and wet mix. |
| Rotary Speaker | Speed, amplitude depth, Doppler depth and wet mix. |
| Physical Pluck | Pitch, strike strength, decay and output gain. |
| Physical Marimba | Pitch, strike strength, mallet hardness, strike position, decay and output gain. |
| Physical Clarinet | Pitch, expression, reed stiffness, attack/release, vibrato depth/rate and output gain. |
| Physical Flute | Pitch, expression, breath noise, attack/release, vibrato depth/rate and output gain. |
| Physical Violin | Pitch, bow expression, bow pressure/position, attack/release, vibrato depth/rate and output gain. |
| Physical Brass | Pitch, breath expression, lip tension, mute opening, attack/release, vibrato depth/rate and output gain. |
| Physical Electric Guitar | Pitch, pluck strength/position, string sustain and output gain. |
| Drawbar Organ | Pitch, swell, key click, percussion, **all nine drawbar values**, and all Gate/Trigger/modulation sockets. It will remain taller than a simple processor because every port/value stays visible. |
| Composite node / instrument composites | **Unchanged full current node UI in both states.** Exposed ports and Open action never hidden. |
| Input Rack / Output Rack / Master Output | Retain existing routing UI, individual sockets, labels and actions without inventing parameter summaries. |

Future leaf node registrations must add or verify their compact metadata; a safe generic fallback lists **every** modulation-capable parameter and **every** socket rather than silently hiding either.

## 4. Expansion, pinning and state

- All eligible non-composite nodes start compact on both desktop and mobile. Clicking/tapping a noninteractive node surface opens it. Activating a different eligible node collapses the previous **unpinned** one. Clicking/tapping empty canvas closes all unpinned expanded nodes. Multiple nodes may stay open via explicit pinning.
- Use an obvious chevron for explicit expansion/collapse, including keyboard activation. Pin is a separate action with a clear pressed/selected state. Selection alone, port gestures, inline-value editing, slider drags, touch pinches, long presses and composite navigation must not toggle expansion inadvertently. Honor protected drafts; don't destroy a pending edit when outside-click or selecting another node.
- Use short easing (~180 ms open/~140 ms close) with reduced-motion fallback. Keep the header and persistent ports fixed. Additional original controls appear **below** the persistent port section in their original order; render above overlapping nodes where needed without moving other nodes or changing their world coordinates.
- Retain existing responsive Blazor canvas, shared NodeFrame, parameter editors, node registry, graph commands and signal telemetry. Do not build a distinct mobile editor or a parallel parameter-state system. Expansion/pinning is presentation-only and must never cause DSP recompilation, history entries or playback interruption.
- Persist graph/node-qualified pin state in compatible presentation state, including nested canvases; restore without assuming deleted node IDs exist. Do **not** persist ephemeral auto-expanded selection. Composite/rack visual appearance is unaffected by this preference. Detailed/all-expanded workspace mode and inspector mode are **not required** for this increment.
- Allow natural endpoint movement on expansion or folding; measure from the real DOM after layout changes and use existing snapping and connection-validation behavior. Never temporarily remove DOM sockets while animating. Current cable-edge panning, touch magnetic snapping and desktop pointer behavior must continue to work.
- Couple expansion with the live presentation consumer lifecycle: a folded leaf receives no effective-value, playhead, animated preview, meter or other playback-driven node updates. Do not merely hide changing DOM with CSS. Suppress telemetry before it reaches the folded renderer; release node-local plot leases/observers on collapse and remove stale live readouts. Expand/pin subscribes to current graph-qualified values, displaying unavailable until valid current data arrives; resume configured plots when appropriate.
- Committed edits, selection, position, connection/port state, undo/redo, labels, and other actual editor changes still update folded nodes. No blanket render freeze. Expansion and subscriptions never affect DSP, playback, or the parameter command path. Composite/rack/master shells remain exempt from folding; permanently full shells consume only telemetry needed by actual live controls. Independently opened scope overlays, performance keyboard and opt-in performance monitor retain their own active consumer lifecycle, even if their associated node is folded.
- Implement the shared telemetry gating through [7.3.9-S3b](../roadmap/advanced.md#739-delivery-slices), coordinated with 6.21.5-S3/S7; no duplicate node-rendering or telemetry policy.

## 5. Acceptance criteria and regression coverage (before merging implementation)

1. Every leaf type in BuiltInNodes/CreateEditorRegistry has deliberate compact metadata or an explicit safe fallback. Tests compare visible compact sockets to snapshot inputs/outputs and visible original modulation-capable property editors to actual parameter definitions, including inactive and unconnected ones.
2. All composite presets, arbitrary composite nodes, Input/Output Rack and Master Output keep their current controls and routing appearance; no hidden ports or actions. Composite child-graph editing and parent boundary mapping remain intact.
3. Desktop Chromium and mobile/touch tests prove compact numeric/choice edits, invalid-value handling, Enter/Escape/blur, graph history undo/redo, live audio update, and that inline editor/port interaction does not open/collapse/move a node.
4. Validate node-selection transition, outside click, multiple pin states, reload and nested navigation. Assert cable endpoints track real port marker centers through layout changes and expansion does not alter graph revisions, audio runtime or other node positions.
5. Cover expanded active modulation (base + effective correctly distinguished), stopped/stale/unavailable telemetry and folded base-only editing; dim inactive inputs. Check stable expanded readout width under fast modulation. Assert zero telemetry-driven renders for all folded eligible leaf frames across multiple fresh playback frames, updates only for expanded consumers, correct current values on reopening, and release/rebind of node-local visualization leases. Independently active overlays/docks remain correct.
6. Validate pointer and touch snapping at 25–400% zoom, edge panning, cable deletion/context menu, pan/pinch takeover, accessible keyboard focus, reduced motion, and no loss of drafts.
7. Measure large patch rendering against the existing 7.3.3 responsiveness metrics and live tap budgets; do not claim performance gains without measured evidence.

Before final acceptance, verify one original editor per property in either state, removal of obsolete compact code, and consistent header/preset presentation. Implementation is intentionally out of scope for this specification. Update the usage guide and real status only when code and browser tests ship.
