# Phase 6.21.5 — Expandable node design and implementation contract

Status: design approved on 2026-11-23. **Implementation in review in [PR #95](https://github.com/wgabel/orbit2/pull/95)**; runtime/browser verification is a merge gate, not presumed complete.

This document supersedes the earlier Phase 6.21.5 proposal in [the editor roadmap](../roadmap/editor-and-effects.md). It describes the agreed target rather than claiming existing product behavior. Implementation must cross-check the actual registered node definitions and their active parameter/port rules.

## 1. Scope and firm decisions

- Use the same **compact-by-default, click/tap-to-expand** node behavior on desktop and mobile. Do not introduce a separate inspector.
- **All nodes remain visible on the canvas at all times. No inactive-node visibility filtering, hiding or collapsing entire nodes.**
- **Every input and output port remains individually visible in both states**, whether connected, disconnected, active or inactive. Do not collapse, aggregate, dynamically hide or substitute a port with an ambiguous anchor.
- **Every parameter with a modulation capability remains visible in the compact node** even without a cable or when the parameter is temporarily inactive. Display its base value, unit and label; visually distinguish inactive state without removing the corresponding port.
- **Compact base values are directly editable**, with an appropriate numeric editor or choice control. The editor should be reachable by clicking/tapping the value itself, without opening the expanded body. Live effective values are read-only.
- For a parameter receiving modulation, show **base → effective** when a valid live effective value is available. Keep the base visible if playback is stopped, telemetry is absent/stale, or the effective value is unavailable; never invent a live value. Optionally expose predicted range on demand rather than as mandatory extra rows.
- Show essential non-modulatable type-specific information in compact nodes, as enumerated below.
- **Composite nodes are exempt from the redesign.** Preserve their existing appearance, controls, port labels, exposed ports and Open action in both nominal presentation states. Do not collapse any composite content. Preset instrument templates that instantiate a composite follow this rule. Likewise do not needlessly redesign Input/Output Rack or Master Output shells in this milestone.
- The ports and compact readouts form a persistent section. Place the expandable editing content **after** that section so port anchor positions do not shift merely because the node opens.

## 2. Visual hierarchy and compact editing

One shared NodeFrame renderer presents: (1) header with icon/name, mute/bypass where supported, expansion toggle and pin; (2) persistent port and property rows; (3) optional short type-specific summary; (4) an expanded-only body containing the existing full parameter widgets, presets, rhythmic actions and visualization controls. Existing full controls remain reachable.

- A modulation input row shows its real socket and name, editable base value and, if available, a visually distinct live effective value. Reuse parameter-to-port identity from the node snapshot; do not join by display name. Audio/event inputs and output sockets retain their own individual labels and targets. A parameter without a modulation socket appears in the compact type-specific summary when required.
- The compact node is **not** a miniature collection of full sliders. Base-value readouts are compact; a tap/click, Enter or Space on the editable value opens its inline field/picker. Selectable choice values (waveform, filter mode, timing, etc.) use a small accessible choice editor. Direct editing must use the existing parameter command, validation, gesture/history grouping and audio update flow, not a second mutable parameter state.
- Numeric editors show units and validation errors. Enter commits a valid value; Escape cancels/reverts; focus loss commits only a valid completed edit (invalid drafts remain actionable). Editing, tapping a port, dragging a wire or moving a node never toggles expansion. Touch targets remain usable and do not overlap nearby sockets. The selected value stays legible at zoomed-out scales through existing zoom/navigation behavior; no invisible actions.
- The **base** value is not relabeled as the live value. Display unit conversions through existing readout/formatting services where appropriate, and display a bounded placeholder if an effective value is missing. Readouts should have stable widths to avoid moving the node as telemetry changes.
- Keep categories, node names, icons, activation states and important mute/bypass flags recognizable. Compact rows are always readable rather than relying on color alone.

## 3. Per-node compact inventory (in addition to all routable port rows)

The definitive source for modulatable parameters is each node's ParameterDefinition and associated ModulationPortId; **all of those values must be shown regardless of the shorter extra-summary column below**. Rows already displaying a property must not be duplicated unnecessarily.

| Registered leaf node(s) | Additional essential compact information |
| --- | --- |
| Oscillator | Waveform; free/synchronized timing mode; division in sync mode; pitch-tracking mode. All input parameters (frequency, amplitude, phase, pulse width, pitch) retain ports/readouts; inactive ones are dimmed, not removed. |
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
- Use short easing (~180 ms open/~140 ms close) with reduced-motion fallback. Keep the header and persistent ports fixed. Expanded controls appear **below** the port/property rows; render above overlapping nodes where needed without moving other nodes or changing their world coordinates.
- Retain existing responsive Blazor canvas, shared NodeFrame, parameter editors, node registry, graph commands and signal telemetry. Do not build a distinct mobile editor or a parallel parameter-state system. Expansion/pinning is presentation-only and must never cause DSP recompilation, history entries or playback interruption.
- Persist graph/node-qualified pin state in compatible presentation state, including nested canvases; restore without assuming deleted node IDs exist. Do **not** persist ephemeral auto-expanded selection. Composite/rack visual appearance is unaffected by this preference. Detailed/all-expanded workspace mode and inspector mode are **not required** for this increment.
- Preserve stable cable anchors on expansion. On legitimate parameter/port activation changes, measure from the real DOM and use existing snapping and connection-validation behavior. Never temporarily remove DOM sockets while animating. Current cable-edge panning, touch magnetic snapping and desktop pointer behavior must continue to work.
- Keep hidden expanded visualizations from consuming unnecessary telemetry/scope taps, while always-visible live effective readings are throttled and render efficiently without jitter; no change to DSP calculations is implied.

## 5. Acceptance criteria and regression coverage (before merging implementation)

1. Every leaf type in BuiltInNodes/CreateEditorRegistry has deliberate compact metadata or an explicit safe fallback. Tests compare visible compact sockets to snapshot inputs/outputs and compact modulation-capable parameter rows to actual parameter definitions, including inactive and unconnected ones.
2. All composite presets, arbitrary composite nodes, Input/Output Rack and Master Output keep their current controls and routing appearance; no hidden ports or actions. Composite child-graph editing and parent boundary mapping remain intact.
3. Desktop Chromium and mobile/touch tests prove compact numeric/choice edits, invalid-value handling, Enter/Escape/blur, graph history undo/redo, live audio update, and that inline editor/port interaction does not open/collapse/move a node.
4. Validate node-selection transition, outside click, multiple pin states, reload and nested navigation. Assert expansion does not move port marker centers and does not alter graph revisions, audio runtime or other node positions.
5. Cover both active modulation (base + effective correctly distinguished) and stopped/stale/unavailable telemetry (base still shown; no false live value), plus dim inactive inputs. Check stable readout width under fast modulation.
6. Validate pointer and touch snapping at 25–400% zoom, edge panning, cable deletion/context menu, pan/pinch takeover, accessible keyboard focus, reduced motion, and no loss of drafts.
7. Measure large patch rendering against the existing 7.3.3 responsiveness metrics and live tap budgets; do not claim performance gains without measured evidence.

Implementation is tracked in [PR #95](https://github.com/wgabel/orbit2/pull/95). The [patch editor usage guide](../usage/patch-editor.md) covers the proposed interaction. Keep the actual shipped status aligned with merge and browser verification results.
