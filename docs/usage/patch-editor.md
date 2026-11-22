# Using the patch editor

[Usage index](README.md) · [Canvas architecture](../editor/canvas.md) · [Persistence](../editor/persistence-and-history.md)

## Responsive workspace

In review ([PR #77](https://github.com/wgabel/orbit2/pull/77)): desktop and mobile share the same **Nodes**, **Outline**, **Edit**, **View** and **More** buttons. Desktop puts all five in the single top toolbar beside breadcrumbs and transport; mobile keeps them in its bottom bar. Desktop menus open as floating popovers below their trigger; click outside, press Escape or use Close to dismiss. On mobile, sheets keep their existing Close and Back handling. Neither layout resizes the canvas when a menu opens. The optional piano sits at the bottom on desktop and above navigation on mobile.

[PR #60](https://github.com/wgabel/orbit2/pull/60) introduces a canvas-first layout on phones and short touch landscape screens. Use the top BPM button to edit tempo or reset the transport; Play and Stop remain immediately available. The bottom toolbar opens existing controls without reducing the canvas rectangle:

- **Nodes:** open the visual Node Library; click/tap a node row to add it. The panel closes on success.
- **Outline:** open/search the existing graph hierarchy; choose a target or open a nested composite.
- **Edit:** graph and annotation undo/redo, Create visual group, selection actions (marquee/multi-select and grouping/ungrouping) and contextual Describe/Delete selected. Enable Marquee mode to close the sheet and drag a selection; reopen Edit → Selection actions → Return to pan to exit.
- **View:** connection labels, annotations, local/entire-patch counts, gesture help, scope and Locate output rack (inside composites).
- **More:** Patches and Performance diagnostics. Toggle the bottom tool again to close it; apply/cancel an active overview draft before switching tools.
- Navigate the canvas without zoom buttons. In a nested graph, use its parent breadcrumb to return.

With [PR #61](https://github.com/wgabel/orbit2/pull/61), close a tool via its Close button, the active bottom tab, Escape or browser/Android Back; changing tools dismisses the previous one. Apply/cancel a protected overview draft before leaving it. The color-coded Node Library ([PR #62](https://github.com/wgabel/orbit2/pull/62), implemented) replaces the old dropdown. Automatic node expansion/pinning remains planned; fullscreen is deferred. Resizing the browser retains the same active graph and playback controller.

### Gesture-only canvas navigation (6.21.4)

Implemented: [PR #69](https://github.com/wgabel/orbit2/pull/69).

- On touchscreens, drag empty space to pan. Put two fingers on the canvas and spread/pinch to zoom around their midpoint; move both together to pan. Lift one finger to continue panning without a jump.
- On desktop, drag the background to pan and use the mouse wheel to zoom around the pointer.
- To fit the current graph, quickly tap or click the same empty canvas area three times. Fit eases to the new viewport (instant with reduced motion). On desktop, focus the canvas and press `F` to fit, `+`/`-` to zoom, or `0` to reset to 100%.
- Header drags still move nodes, and parameter controls keep their own interactions. Adding a second finger to a canvas drag takes over for navigation and cancels any unfinished cable or node movement. Zoom stays within 25–400%; the viewport remains independent of audio and saved per graph.
- The former zoom toolbar, percentage, reset and Fit buttons are removed on both layouts. Annotation-specific Fit signals and Outline locate actions remain available.

### Contextual canvas actions

Implemented: [PR #71](https://github.com/wgabel/orbit2/pull/71).

If a node fills the entire screen, put two fingers directly on the node—even on a slider, button, port or plot—and pinch inward to zoom out, or move them together to pan. There is no need to find empty canvas. Adding the second finger cancels a still-active local drag/edit; a single finger still edits or moves as before. Lift one finger to continue panning with the remaining finger. Tool sheets and open editors retain their own controls.

On Android or another touchscreen, touch and hold for about half a second. On desktop, right-click (or focus the canvas and press Shift+F10). A menu appears next to the mouse pointer or as a bottom sheet for touch. Tap/click outside, press Escape, or choose Close to dismiss it. Moving your finger or adding a second finger before the hold finishes cancels the menu, so drag and pinch continue normally.

| Hold or right-click | Available actions (when applicable) |
| --- | --- |
| Empty canvas | Add node/note, create visual group, marquee select, select all module nodes, undo/redo and annotation history |
| Node header or noninteractive surface | Mute/bypass, show/hide plot, scope, presets, describe/rename, focus, open/ungroup composite, rack ports, grouping and delete |
| Selected nodes | Create composite or visual group, delete selection, clear selection |
| Cable or amount badge | Edit amount, describe, focus either endpoint, delete connection |
| Note or visual group header/marker | Edit, delete; notes also allow collapse/expand, duplicate and visual grouping |

Regular buttons, port sockets, parameter inputs, plots and note body text keep their current interactions. The menu supplements the existing controls for now; composite creation still uses its name and boundary-preview workflow. Add node opens the Node Library and uses its existing placement behavior.

### Delete a connection on mobile

Alternatively, hold a cable or its badge and choose **Delete connection** directly. To restore it, use Undo in the empty-canvas menu or Edit.

1. Tap the cable or its numeric amount badge. The selected cable is highlighted and, for a single connection, its amount editor opens. Do not press Cancel or Close in that editor; doing so clears the selection.
2. Tap **Edit** on the bottom toolbar, check that the summary shows 0 nodes and 1 connection selected, then tap **Delete selected**. Only the selected connection is removed.
3. To restore it, tap **Undo** in Edit.

If badges obscure a cable, open **View → Connection labels → Off**, close View and tap the exposed cable. Triple-tap empty canvas to fit, or zoom/pan to make it easier to reach. Setting a connection amount to zero does not delete it. See [PR #63](https://github.com/wgabel/orbit2/pull/63) for the mobile touch regression coverage.

### Node Library

On either platform, open **Nodes**. Search by name, keyword or purpose, or choose a category. Each row shows a recognizable icon, category accent, canonical node name and short description. Click/tap the row to add it at the current visible canvas; star a row to favorite it (tap again to remove). Favorites and up to six recently added types appear first when no filter is active, and persist in this browser. The list adapts to the graph (Master output is root-only); composite and instrument templates remain available. Close the library to return to the canvas without adding anything. Node frames and the outline share the category icons/colors; signal colors still indicate signal semantics. See [PR #62](https://github.com/wgabel/orbit2/pull/62).

## 1. Build and play a patch

1. Open **More → Patches** and create a new patch (stop playback first). It starts with a Master output. Alternatively, import a portable patch JSON or use the existing workspace.
2. Open **Nodes**, then choose **Oscillator**. Choose Triangle or Sine, free frequency 110 Hz, amplitude 0.15. Add a **Gain** with gain 0.5. Connect the oscillator's signal output to Gain's audio input, and Gain's audio output to Master output. Use compatible sockets; drag from an output to an input or activate the sockets in order.
3. Click **Play patch**. Begin with comfortable device volume. For a richer sound add a second oscillator at 220 Hz and amplitude 0.04, use a Mixer to combine both, then route through Gain. Add Chorus or Reverb after the Mixer, keeping some headroom. Click Stop to stop playback.
4. Base parameter edits update the active DSP; routed structural changes may compile a replacement and crossfade. Replacing the whole patch requires stopped playback. If editing effects during playback, tails may be preserved for live scalar edits but not necessarily structural changes.

To audition the faster [base-control smoothing](../core/parameters-and-modulation.md#base-control-smoothing) ([PR #75](https://github.com/wgabel/orbit2/pull/75) and centralized-policy follow-up [PR #80](https://github.com/wgabel/orbit2/pull/80)), use one Triangle oscillator in Free mode connected to Master output, at a comfortable amplitude (e.g. 0.15). While playing, click repeatedly between widely separated positions on Frequency (e.g. approximately 220, 880 and 1760 Hz, then back down); try dragging quickly too. Then enable the oscillator's Pitch tracking = Semitones, wire Pitch Control → Pitch at unity, and repeatedly change Transpose and Fine tune. Both ways of retuning share the 5 ms policy, with no new volume attack or phase reset. Listen for short clicks; if audible, record the from/to frequencies, waveform and device. A large pitch jump itself can sound abrupt without being a sample discontinuity.

Use **M** (mute) to silence an eligible node's outputs, including modulation. Use **B** (bypass) on supported audio processors to hear the dry input, including Resonator. Mute an upstream oscillator and downstream effects may still ring; mute the final voice processor/composite to hear the lane without its tails. Bypass of a Gain removes its attenuation, so check the level first. See [audition controls](../editor/audition.md).

For individual node settings, use the Preset selector and its adjacent **…** menu to save, apply, import/export and manage custom presets. Presets contain parameter values (including inactive settings), not routes, graph IDs or layout. For a full instrument graph choose a built-in composite in Add node; save it within a named patch or export the patch JSON. A reusable custom composite library is still planned. See [node presets](../editor/node-presets.md) and [reusable modules](../editor/reusable-modules.md).

### Assisted touch cable dragging

Implemented ([PR #65](https://github.com/wgabel/orbit2/pull/65)). On a touchscreen, touch and hold an output port, then drag. Aim with the small cursor above your finger, not the fingertip itself. Compatible input sockets show rings even when zoomed out; invalid or already connected destinations dim. Move until the larger ring and floating label name the intended parameter, for example **Oscillator → Amplitude**. Release to connect. Move away from all highlighted ports (or cancel the gesture) to abandon it without adding a cable. Closely spaced sockets switch as you aim directly at the next one; near the lower edge the offset reduces. The cursor and magnetic radius are screen-sized at every zoom. No extra picker or node expansion is necessary. Touch vibration is optional. On desktop, drag with the mouse as usual: the same highlighted sockets and magnetic snapping work directly at the pointer, without the upward offset. Keyboard and click-output then click-input activation are unchanged. If a port is covered, reposition the view or nodes; drag toward the canvas edge to reach off-screen ports. Undo removes a successful connection as usual.

The alignment and desktop snapping follow-up (in review, [PR #74](https://github.com/wgabel/orbit2/pull/74)) keeps rings centered on the actual compatible sockets even if the browser scrolls or the node layout changes during the gesture. Release uses the latest measured target, not a stale highlight.

### Drag a cable to an off-screen node

Implemented ([PR #73](https://github.com/wgabel/orbit2/pull/73)): on Android/touch or desktop, start dragging from an output and keep the finger or mouse near the edge of the canvas. After a short pause the canvas scrolls automatically, faster nearer the edge; corners scroll diagonally. You can keep holding until the destination comes into view, without zooming out or releasing the cable. Move away from the edge or aim at the compatible input to stop the scrolling, then release to connect. Magnetic rings and the target label work on mouse and touch, with a lifted cursor only on touch; a second finger takes over for pinch navigation and cancels the cable. Escape or dropping the panned cable away from a target cancels the connection. See [canvas behavior](../editor/canvas.md#cable-edge-panning).

## 2. Interpret and edit connections

- Audio links carry sound. Modulation values add a contribution in the receiving parameter's unit. Pitch expects relative semitones; Frequency expects Hz. A unit-amplitude bipolar 0.5 Hz sine into a 440 Hz carrier's Frequency at +100 gives 340–540 Hz before constraints. A Pitch Control +12 into a semitone-tracked Pitch at unity doubles the base frequency; connecting it to Frequency instead adds 12 Hz.
- Click the wire or its numeric badge. Edit the amount in the anchored popover and **Apply** (or Cancel). For audio paths ×0.5 halves amplitude (about −6 dB); for a modulation connection, the amount is depth, not necessarily a fader. Typed Trigger/Gate/Clock links use unity; transform continuous signals through explicit adapters. Zero amount and dormant/inactive link are different: zero is a configured contribution of zero, dormant means the current node mode does not use that port. Neither deletes the cable.
- The **View → Connection labels** dropdown supports All/Modified/Selected/Off. This is per-canvas presentation state, independent of the graph and annotation visibility. Hiding badges does not disconnect or prevent selecting cables. [PR #1](https://github.com/wgabel/orbit2/pull/1), [PR #2](https://github.com/wgabel/orbit2/pull/2), [PR #3](https://github.com/wgabel/orbit2/pull/3); [connection contract](../editor/annotations-and-connections.md).

**Composite example:** add `Impact — AD percussion`, connect its Audio output to a root Mixer or Master output and play. Open the composite and follow the internal oscillator to the exposed Audio socket of the Output Rack. Use **View → Locate output rack** if the rack is offscreen or hidden behind old layout geometry. The parent Audio socket is the same boundary signal, not a second output generator; do not add another Master inside the composite. The Input Rack supplies any exposed parent inputs to the child. Use the breadcrumbs to return to the parent; Fit fits the current canvas. [PR #18](https://github.com/wgabel/orbit2/pull/18); [composite routing](../editor/composite-nodes.md).

## 3. Document and organize a patch

For a complex piece, create meaningful names (e.g. `01 · Harmonic bed`, `02 · Lead`, `03 · Accents`) and document what their controls do. This makes patches easier to edit and understand, especially for an agent inspecting an exported document. None of the following annotation features changes DSP routing.

| Task | Actual UI workflow / result |
| --- | --- |
| Add a note card | Nodes → **Note (annotation)**. Edit title, body and accent; Apply. Drag the header, resize or collapse. Use notes for musical timing, parameter ranges, routing rationale and warnings. [PR #27](https://github.com/wgabel/orbit2/pull/27) |
| Name or describe an oscillator/processor | Select one node → **Describe selected** → enter alias and description → Apply. Canonical node type and IDs remain unchanged. [PR #28](https://github.com/wgabel/orbit2/pull/28) |
| Explain a connection | Select one cable → **Describe selected** or **Edit connection label** in the amount editor. A label describes purpose, not a numerical amount. [PR #28](https://github.com/wgabel/orbit2/pull/28) |
| Organize a synth voice | Select related nodes using additive selection → **Create visual group** → set title/accent and membership. Drag its header to move members; resize the frame or remove it without deleting members. Groups are presentation only, not nested DSP graphs. [PR #30](https://github.com/wgabel/orbit2/pull/30) |
| Describe an entire piece | More → Patches → **Edit patch overview**: title, description, usage instructions and tags → Apply. Save a named snapshot. Search those metadata fields in the saved patch list; open a result to load its graph. [PR #32](https://github.com/wgabel/orbit2/pull/32) |
| Learn from an existing annotated example | More → Patches → **Annotated examples** → choose the modulation example or **Impact — AD percussion** → **Export example**. Then stop playback and **Import JSON** to inspect the exported patch. Export does not replace the current graph. [PR #32](https://github.com/wgabel/orbit2/pull/32) |

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
2. For a slow oscillator select **One configured cycle** in the expanded scope. A 0.05 Hz oscillator has a 20 s configured cycle: wait for history to fill. An oscillator that is frequency-modulated can have varying actual cycle lengths. Use Spectrum to inspect overtones: 8192 samples at 48 kHz is roughly 5.86 Hz/bin, not enough to distinguish every small detune. A spectrum dBFS bin is peak-normalized amplitude, not power density. The 6.18.3 follow-up ([PR #66](https://github.com/wgabel/orbit2/pull/66), in review) corrects a false rise at the lowest bin for off-bin audio tones by using window-weighted DC removal; true sub-20 Hz components are not excluded. Slow LFOs below the FFT resolution are better inspected in Waveform / One configured cycle.
3. For Filter or Parametric EQ choose **Frequency response** to compare configured and effective response; this is computed from parameters rather than a live spectrum or captured input. [PR #4](https://github.com/wgabel/orbit2/pull/4). For Envelope choose Envelope curve (see the [articulation guide](sound-and-instruments.md#2-create-distinct-attacks-and-releases)). Resonator's own dedicated response view is not implemented; use waveform/spectrum.
4. If a live tap is unavailable, verify playback, valid routing to Master, selected output/active port, and whether the capture is still warming up. [PR #20](https://github.com/wgabel/orbit2/pull/20) makes capacity depend on concurrently observed unique auxiliary signals: eight slots dynamically assigned, identical expressions share them, master is isolated. Close other plots/expanded scope or navigate away to release unused leases; one reachable plot in a large patch should not be denied merely because many other nodes exist. If it is, report the patch and steps, not an assumption of capacity exhaustion. The static response and configured envelope curves do not require the same live audio tap.

See [signal observation](../audio/signal-observation.md) and [visualizations](../editor/visualizations.md) for actual windowing, sample semantics, routing states and capacity. Whole-piece measurements and role balance use the [production workflow](../examples/mixing-mastering-and-analysis.md) and [contextual layer balance](../examples/contextual-layer-balance.md); live scope is not a standardized loudness meter.

## 5. Save, share and recover

Changes autosave browser-locally. Named snapshots (Patches → Save as/overwrite) are separate from workspace recovery; opening a snapshot or importing JSON validates it before replacement and requires stopped playback. Export JSON to retain a portable copy; browser data clearing/private browsing can lose local storage. The file contains the graph, tempo, layouts, saved metadata and committed base parameters, not a recording, active keyboard latch or sampled DSP state. The graph and annotation histories are independent; reopening starts fresh history. See [persistence](../editor/persistence-and-history.md) for schema compatibility.
