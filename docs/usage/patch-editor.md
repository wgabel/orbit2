# Using the patch editor

[Usage index](README.md) · [Canvas architecture](../editor/canvas.md) · [Persistence](../editor/persistence-and-history.md)

## Mobile workspace (6.21.1–6.21.2 implemented; 6.21.3 in review)

[PR #60](https://github.com/wgabel/orbit2/pull/60) introduces a canvas-first layout on phones and short touch landscape screens. Use the top BPM button to edit tempo or reset the transport; Play and Stop remain immediately available. The bottom toolbar opens existing controls without reducing the canvas rectangle:

- **+ Nodes:** open the visual Node Library; tap a node row to add it. The panel closes on success. Locate output rack (inside composites) is also available here.
- **Outline:** open/search the existing graph hierarchy; choose a target or open a nested composite.
- **Edit:** graph and annotation undo/redo, Create visual group, selection actions (marquee/multi-select and grouping/ungrouping) and contextual Describe/Delete selected. Enable Marquee mode to close the sheet and drag a selection; reopen Edit → Selection actions → Return to pan to exit.
- **View:** connection labels, annotations, local/entire-patch counts, gesture help and scope.
- **More:** Patches and Performance diagnostics. Toggle the bottom tool again to close it; apply/cancel an active overview draft before switching tools.
- The floating zoom control provides minus, plus, percentage, 100% and Fit. In a nested graph, use its parent breadcrumb to return.

With [PR #61](https://github.com/wgabel/orbit2/pull/61), close a tool via its Close button, the active bottom tab, Escape or browser/Android Back; changing tools dismisses the previous one. Apply/cancel a protected overview draft before leaving it. The color-coded Node Library ([PR #62](https://github.com/wgabel/orbit2/pull/62), in review) replaces the old dropdown. Automatic node expansion/pinning, new pinch gestures and fullscreen are planned in subsequent milestones. Resizing the browser retains the same active graph and playback controller. 

### Node Library

On desktop, click **Add node**; on mobile, tap **+ Nodes**. Search by name, keyword or purpose, or choose a category. Each row shows a recognizable icon, category accent, canonical node name and short description. Click/tap the row to add it at the current visible canvas; star a row to favorite it (tap again to remove). Favorites and up to six recently added types appear first when no filter is active, and persist in this browser. The list adapts to the graph (Master output is root-only); composite and instrument templates remain available. Close the library to return to the canvas without adding anything. Node frames and the outline share the category icons/colors; signal colors still indicate signal semantics. See [PR #62](https://github.com/wgabel/orbit2/pull/62).

## 1. Build and play a patch

1. Open **Patches** and create a new patch (stop playback first). It starts with a Master output. Alternatively, import a portable patch JSON or use the existing workspace.
2. Open **Add node** (or **+ Nodes** on mobile), then choose **Oscillator**. Choose Triangle or Sine, free frequency 110 Hz, amplitude 0.15. Add a **Gain** with gain 0.5. Connect the oscillator's signal output to Gain's audio input, and Gain's audio output to Master output. Use compatible sockets; drag from an output to an input or activate the sockets in order.
3. Click **Play patch**. Begin with comfortable device volume. For a richer sound add a second oscillator at 220 Hz and amplitude 0.04, use a Mixer to combine both, then route through Gain. Add Chorus or Reverb after the Mixer, keeping some headroom. Click Stop to stop playback.
4. Base parameter edits update the active DSP; routed structural changes may compile a replacement and crossfade. Replacing the whole patch requires stopped playback. If editing effects during playback, tails may be preserved for live scalar edits but not necessarily structural changes.

Use **M** (mute) to silence an eligible node's outputs, including modulation. Use **B** (bypass) on supported audio processors to hear the dry input, including Resonator. Mute an upstream oscillator and downstream effects may still ring; mute the final voice processor/composite to hear the lane without its tails. Bypass of a Gain removes its attenuation, so check the level first. See [audition controls](../editor/audition.md).

For individual node settings, use the Preset selector and its adjacent **…** menu to save, apply, import/export and manage custom presets. Presets contain parameter values (including inactive settings), not routes, graph IDs or layout. For a full instrument graph choose a built-in composite in Add node; save it within a named patch or export the patch JSON. A reusable custom composite library is still planned. See [node presets](../editor/node-presets.md) and [reusable modules](../editor/reusable-modules.md).

## 2. Interpret and edit connections

- Audio links carry sound. Modulation values add a contribution in the receiving parameter's unit. Pitch expects relative semitones; Frequency expects Hz. A unit-amplitude bipolar 0.5 Hz sine into a 440 Hz carrier's Frequency at +100 gives 340–540 Hz before constraints. A Pitch Control +12 into a semitone-tracked Pitch at unity doubles the base frequency; connecting it to Frequency instead adds 12 Hz.
- Click the wire or its numeric badge. Edit the amount in the anchored popover and **Apply** (or Cancel). For audio paths ×0.5 halves amplitude (about −6 dB); for a modulation connection, the amount is depth, not necessarily a fader. Typed Trigger/Gate/Clock links use unity; transform continuous signals through explicit adapters. Zero amount and dormant/inactive link are different: zero is a configured contribution of zero, dormant means the current node mode does not use that port. Neither deletes the cable.
- The canvas **Connection labels** dropdown supports All/Modified/Selected/Off. This is per-canvas presentation state, independent of the graph and annotation visibility. Hiding badges does not disconnect or prevent selecting cables. [PR #1](https://github.com/wgabel/orbit2/pull/1), [PR #2](https://github.com/wgabel/orbit2/pull/2), [PR #3](https://github.com/wgabel/orbit2/pull/3); [connection contract](../editor/annotations-and-connections.md).

**Composite example:** add `Impact — AD percussion`, connect its Audio output to a root Mixer or Master output and play. Open the composite and follow the internal oscillator to the exposed Audio socket of the Output Rack. Use **Locate output rack** if the rack is offscreen or hidden behind old layout geometry. The parent Audio socket is the same boundary signal, not a second output generator; do not add another Master inside the composite. The Input Rack supplies any exposed parent inputs to the child. Use the breadcrumbs to return to the parent; Fit fits the current canvas. [PR #18](https://github.com/wgabel/orbit2/pull/18); [composite routing](../editor/composite-nodes.md).

## 3. Document and organize a patch

For a complex piece, create meaningful names (e.g. `01 · Harmonic bed`, `02 · Lead`, `03 · Accents`) and document what their controls do. This makes patches easier to edit and understand, especially for an agent inspecting an exported document. None of the following annotation features changes DSP routing.

| Task | Actual UI workflow / result |
| --- | --- |
| Add a note card | Add node → **Note (annotation)**. Edit title, body and accent; Apply. Drag the header, resize or collapse. Use notes for musical timing, parameter ranges, routing rationale and warnings. [PR #27](https://github.com/wgabel/orbit2/pull/27) |
| Name or describe an oscillator/processor | Select one node → **Describe selected** → enter alias and description → Apply. Canonical node type and IDs remain unchanged. [PR #28](https://github.com/wgabel/orbit2/pull/28) |
| Explain a connection | Select one cable → **Describe selected** or **Edit connection label** in the amount editor. A label describes purpose, not a numerical amount. [PR #28](https://github.com/wgabel/orbit2/pull/28) |
| Organize a synth voice | Select related nodes using additive selection → **Create visual group** → set title/accent and membership. Drag its header to move members; resize the frame or remove it without deleting members. Groups are presentation only, not nested DSP graphs. [PR #30](https://github.com/wgabel/orbit2/pull/30) |
| Describe an entire piece | Patches → **Edit patch overview**: title, description, usage instructions and tags → Apply. Save a named snapshot. Search those metadata fields in the saved patch list; open a result to load its graph. [PR #32](https://github.com/wgabel/orbit2/pull/32) |
| Learn from an existing annotated example | Patches → **Annotated examples** → choose the modulation example or **Impact — AD percussion** → **Export example**. Then stop playback and **Import JSON** to inspect the exported patch. Export does not replace the current graph. [PR #32](https://github.com/wgabel/orbit2/pull/32) |

Use **Undo annotation** / **Redo annotation** for notes, descriptions and group edits. Normal Undo/Redo acts on graph commands separately. The workspace overview is patch-wide; notes/groups and descriptions belong to their own canvas. See [patch annotations](../editor/patch-annotations.md) for editing/history, portable JSON keys and an example.

### Switch between learning and performance views

**Annotations → Show / Titles only / Hide** is the implemented control from [PR #35](https://github.com/wgabel/orbit2/pull/35).

- **Show:** full annotation detail at close zoom; as you zoom out, compact markers replace fuller note content. The automatic transitions have hysteresis. Groups retain their background/frame in compact detail.
- **Titles only:** use compact note/group titles; select a marker (click or focus and Enter/Space) to reveal its editable full card temporarily. Selected nodes retain their description access; aliases remain recognizable.
- **Hide:** hide note cards and group frames, suppress secondary descriptive detail, without deleting metadata, cables, nodes or numeric connection badges. Use **Find annotation**, choose a current-canvas note or group, then **Reveal and fit** to select and locate it temporarily. Deselect to return to the stored visibility mode.
- **Fit signals** fits the measured node bounds, ignoring annotations; ordinary Fit considers visible detail. If switching/revealing is disabled, commit or cancel the open metadata draft first. The finder is local to the active canvas; the cross-graph outline is a separate implemented feature.

Try describing a composition, hiding its annotations during performance, then opening its nested instruments and revealing a warning via the finder. Each canvas remembers its own visibility. Neither zoom nor visibility overwrites authored note dimensions, manually collapsed state, metadata, signal revisions or sound. Export retains the preference. Full details: [annotation visibility](../editor/patch-annotations.md#2-annotation-visibility-and-zoom-dependent-detail-6197--implemented).

<a id="search-and-navigate-the-entire-patch-in-review"></a>

### Search and navigate the entire patch

[PR #39](https://github.com/wgabel/orbit2/pull/39) adds **Outline** to the workspace header. Expand composites or visual groups, or use **Search patch** and **Filter outline** to find committed names, aliases, descriptions and notes throughout the hierarchy, including canvases you have never opened.

For example, add a Flute instrument, open **Outline**, search `Flute Output Rack` and filter by Node. Select the matching **Locate node: Output Rack** action to enter the Flute's canvas, select its rack and fit it into view. **Locate composite** selects a composite in its parent; **Open canvas** enters its child. Locate a hidden note to reveal it temporarily without changing the canvas visibility setting. Apply or cancel an active annotation draft before navigating. These actions do not change the signal graph or interrupt playback. The outline is navigation, not a module library or an editing tool. See the [outline contract](../editor/patch-annotations.md#3-patch-outline-and-navigation-6198--implemented).

<a id="select-multiple-nodes-for-reuse-in-review"></a>

### Select multiple nodes for reuse

[PR #45](https://github.com/wgabel/orbit2/pull/45) adds selection tools. For example, prepare a drone by selecting its oscillator, filter and gain without selecting Master output. Shift-drag from empty canvas around their frames; even partly intersecting frames are selected. Hold Ctrl/Meta as well to add another rectangle to the selection. On touch or without modifiers, open **Selection → Marquee mode**, drag, and use Escape or **Return to pan** to leave the mode. Ordinary background dragging pans when not in selection mode.

For keyboard-only selection, open **Selection**, choose **Select all module nodes** or choose an **Individual module node** and activate **Toggle node selection**; **Clear selection** resets it. The panel reports selected and eligible counts and warns about protected master/rack nodes or cables in the ordinary selection. Apply/cancel an open annotation draft first. Bulk actions exclude protected targets and clear cable/annotation selection; selection belongs to the active canvas. This does not move nodes, group DSP graphs or change the sound. See [module selection](../editor/reusable-modules.md#implementation-multi-node-selection-741-implemented).

<a id="create-an-editable-composite-742a-in-review"></a>

### Create an editable composite (7.4.2a, implemented)

Implementation: [PR #47](https://github.com/wgabel/orbit2/pull/47).

For example, select a carrier oscillator and gain processor while leaving the external LFO and Master Output outside. In **Selection**, enter a **Composite name**, expand **Boundary preview** (the incoming LFO and outgoing audio each get a port), then choose **Create composite**. The two nodes become one editable composite without changing the mathematical signal routing. Double-click its header or choose **Open** to see the original nodes and racks. The original cable weights are preserved, not applied twice. Use rack port editing to give the generated socket names more musical labels.

**Undo** immediately reverses the structural operation and **Redo** restores it. You can save/reload the resulting patch normally. If grouping is unavailable, exclude master/rack nodes and cables, or remove selected nodes from visual groups first. If the selection surrounds an unselected intervening processor and produces a cycle, include that processor. Grouping is within the current patch; the cross-patch Module Library is still planned. See the [grouping implementation](../editor/reusable-modules.md#implementation-create-composite-742a-implemented).

### Ungroup an edited composite (7.4.2b, in review)

Implementation: [PR #48](https://github.com/wgabel/orbit2/pull/48).

Open the composite created above, change an internal parameter or cable amount, then return to the parent. Select only that composite and choose **Selection → Ungroup composite**. The current nodes appear around the former wrapper; an internal composite remains nested. Their current settings and relative positions are retained, and incoming/outgoing cables reconnect with composed amounts. Undo restores the edited composite, not its original pre-edit configuration; Redo expands it again.

If expansion is refused, read the error: unmute the composite; remove or connect unused rack cables on both sides; use explicit mixers instead of summed boundary inputs/outputs; move wrapper/rack descriptions, labels on disappearing cables, child notes or visual groups before expanding. No partial graph edit occurs on refusal. See the [ungrouping contract](../editor/reusable-modules.md#implementation-ungroup-composite-742b-in-review).

### Understand the two counts

The upper-right canvas overlay has local node/connection counts (and notes when present), plus **Entire patch: N nodes · M connections** ([PR #36](https://github.com/wgabel/orbit2/pull/36)). Local counts change when navigating between graphs. Patch totals stay the same across navigation and include all child graphs at every depth; nested edits and undo/redo update them.

Example: if root contains a composite and Master output (2 nodes, 1 connection) and that composite contains an oscillator, gain, Input Rack and Output Rack (4 nodes, 2 connections), the root shows 2/1, the child 4/2 and both show entire patch 6/3. Count each actual graph node, including wrapper and rack nodes, and every stored edge (even dormant). Notes/groups, mirrored sockets and boundary port definitions are not extra nodes. Total count is not DSP workload, count of live reachable sources, memory consumption or a timer measurement. See [canvas counts](../editor/canvas.md#the-canvases) and [performance](../../performance.md).

## 4. Inspect and troubleshoot signals

1. Choose **Show plot** on an eligible node or select its header and use Show scope. Select the relevant signal input/output and View → Waveform or Spectrum. Play the graph and allow fresh samples to arrive; use Expand for detailed controls. Scope peak/RMS is measured over the latest 20 ms, not perceived loudness or true peak.
2. For a slow oscillator select **One configured cycle** in the expanded scope. A 0.05 Hz oscillator has a 20 s configured cycle: wait for history to fill. An oscillator that is frequency-modulated can have varying actual cycle lengths. Use Spectrum to inspect overtones: 8192 samples at 48 kHz is roughly 5.86 Hz/bin, not enough to distinguish every small detune. A spectrum dBFS bin is peak-normalized amplitude, not power density.
3. For Filter or Parametric EQ choose **Frequency response** to compare configured and effective response; this is computed from parameters rather than a live spectrum or captured input. [PR #4](https://github.com/wgabel/orbit2/pull/4). For Envelope choose Envelope curve (see the [articulation guide](sound-and-instruments.md#2-create-distinct-attacks-and-releases)). Resonator's own dedicated response view is not implemented; use waveform/spectrum.
4. If a live tap is unavailable, verify playback, valid routing to Master, selected output/active port, and whether the capture is still warming up. [PR #20](https://github.com/wgabel/orbit2/pull/20) makes capacity depend on concurrently observed unique auxiliary signals: eight slots dynamically assigned, identical expressions share them, master is isolated. Close other plots/expanded scope or navigate away to release unused leases; one reachable plot in a large patch should not be denied merely because many other nodes exist. If it is, report the patch and steps, not an assumption of capacity exhaustion. The static response and configured envelope curves do not require the same live audio tap.

See [signal observation](../audio/signal-observation.md) and [visualizations](../editor/visualizations.md) for actual windowing, sample semantics, routing states and capacity. Whole-piece measurements and role balance use the [production workflow](../examples/mixing-mastering-and-analysis.md) and [contextual layer balance](../examples/contextual-layer-balance.md); live scope is not a standardized loudness meter.

## 5. Save, share and recover

Changes autosave browser-locally. Named snapshots (Patches → Save as/overwrite) are separate from workspace recovery; opening a snapshot or importing JSON validates it before replacement and requires stopped playback. Export JSON to retain a portable copy; browser data clearing/private browsing can lose local storage. The file contains the graph, tempo, layouts, saved metadata and committed base parameters, not a recording, active keyboard latch or sampled DSP state. The graph and annotation histories are independent; reopening starts fresh history. See [persistence](../editor/persistence-and-history.md) for schema compatibility.
