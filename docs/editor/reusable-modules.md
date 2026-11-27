Warning: truncated output (original token count: 15111)
Total output lines: 284

# Reusable composite modules and module library

[Architecture overview](../architecture/overview.md) · [Roadmap, Phase 7.4](../roadmap/advanced.md#phase-74--reusable-composite-modules-planned)

**Status:** Phase 7.4.1 multi-node selection and actions, 7.4.2a grouping and 7.4.2b conservative ungrouping are implemented. Phase 7.4.6 macro authoring and offline DSP lowering are merged; live playback integration is in progress. The module library and portability work remain planned. This is the design contract and implementation record for general-purpose reusable graphs. Existing composite graphs, boundary racks, built-in drone/impact factories and individual node presets are already implemented; see [composite nodes](composite-nodes.md) and [node presets](node-presets.md).

Related: [canvas](canvas.md) · [persistence and history](persistence-and-history.md) · [signals and ports](../core/signals-and-ports.md) · [commands and events](../core/commands-and-events.md) · [annotations](annotations-and-connections.md) · [generative ambient composition](../examples/generative-ambient-composition.md).

## 1. Purpose and terminology

A musician who creates a good sound or useful routing pattern in one patch should be able to preserve it and insert it into any other patch without rebuilding the graph. A drone is one use of this feature, not a special node family or a separate editor. The same mechanism should support instruments, modulation systems, rhythmic generators, effect chains, layered textures and even complete compositions.

| Concept | What it contains | How it is reused |
|---|---|---|
| Individual node preset | Base parameter configuration for one registered leaf type | Apply to an existing node without altering wiring (Phase 6.17) |
| Composite node | An owned, editable child graph with Input/Output Racks | Open and edit in the shared nested canvas (Phase 5) |
| Module preset/template | Immutable, reusable composite definition including graph, boundary and layout | Instantiate an independent composite in another graph (Phase 7.4) |
| Patch | Root graph, master destination, project transport and workspace | Open as a whole workspace or deliberately convert to a module |
| Visual group | Presentation-only collection of nodes (Phase 6.19.3) | Organize the canvas without changing signal topology |

Use **Module Library** for the user-facing catalog of reusable composites. “Drone”, “Instrument”, “Texture”, “Modulation” and “Effect” are usage categories, not different domain types. Do not extend the leaf parameter preset format to pretend a graph is a parameter list.

## 2. Musician workflows

Support the following independent entry points:

1. **Selection → Create composite:** select related nodes in any active canvas, choose Create composite, supply a name and review the automatically detected boundary. Replace the selection with a composite in the same patch; double-click to open its internal canvas.
2. **Composite → Save to Module Library:** capture a composite directly from its menu, choose a name, category and optional description/tags; the source remains unchanged. No need to ungroup and regroup an existing module.
3. **Patch → Save as Module:** capture a complete saved or current patch as an independent template without modifying or closing the original. Convert the Master Output connection into an internal Output Rack port. Review its external interface and timing assumptions before saving.
4. **Add Node → Module Library:** browse or search built-in and custom modules, inspect inputs/outputs, description, complexity and optional transport assumptions; insert a new independent instance at the intended position. It can be edited like any ordinary composite.
5. **Composite → Ungroup:** flatten exactly one composite level into its parent graph, preserving valid signal routing; nested composites remain composites.

The current editor already has additive node selection via Shift/Ctrl/Meta and graph-scoped selection state. Extend and reuse this foundation rather than build a second selection model. Add rectangle/marquee selection, keyboard access and a contextual count/actions if useful; keep connection selection distinct from moving nodes. Selection never changes playback, graph revision or history.

<a id="implementation-multi-node-selection-741-in-review"></a>

### Implementation: multi-node selection (7.4.1, implemented)

Implementation: [PR #45](https://github.com/wgabel/orbit2/pull/45).

Open **Selection** beside Connection labels. **Select all module nodes** replaces the selection with all eligible nodes in the current graph; **Individual module node** and **Toggle node selection** permit precise keyboard-accessible selection; **Clear selection** clears it. The summary distinguishes selected, eligible, protected, and cable targets. Rack nodes and the Master output remain selectable in ordinary editing but are excluded by the module selection actions. Eligibility here is only an ownership/type check: actual grouping must also validate the crossing connections and graph equivalence in 7.4.2.

Drag empty canvas with Shift, or activate **Marquee mode** and drag without a keyboard. An outlined rectangle previews the area until release; any measured node frame with positive-area intersection is selected. Ctrl/Meta adds to the existing eligible node set; otherwise the rectangle replaces it. A click or thin line is ignored. Escape cancels a marquee or exits selection mode without changing the existing selection. Use **Return to pan** or Escape to resume normal background panning. Bulk actions clear cable and annotation selection; drafts lock these actions. Canvas navigation resets the transient mode but preserves the existing graph-local selection as before.

`ModuleSelection` defines eligible node IDs and measured hit testing; `CanvasStateService.SelectModuleNodes` validates all requested IDs, deduplicates, filters protected types, and publishes one selection change without graph history. `GraphCanvas` previews in screen space and resolves the final rectangle against freshly measured world-space node geometry, rejecting stale graph/layout/viewport snapshots during the gesture. No new persistence schema or DSP behavior is required. Grouping is the separate structural operation below; saving reusable modules remains planned.

<a id="implementation-create-composite-742a-in-review"></a>

### Implementation: create composite (7.4.2a, implemented)

Implementation: [PR #47](https://github.com/wgabel/orbit2/pull/47).

Open **Selection**, select 1–512 eligible nodes, enter a composite name and expand **Boundary preview** to inspect generated names and actual signal types. Choose **Create composite**. The selected nodes become one editable composite, selected at the top-left of the previous selection. Open it to inspect the moved nodes, Input Rack and Output Rack; rename exposed ports using the rack editors. Empty boundaries and nested composites are supported. Master/rack nodes, selected cables and members of visual groups cannot be grouped; remove the affected visual-group membership first. Note cards and unrelated visual groups stay in the parent.

`CompositeGrouping` builds a detached candidate through the same saved-graph reconstruction and domain validation used by persistence/history. `GroupNodesCommand` verifies the expected revision; `NodesGrouped` describes the atomic ownership change. One port per crossing uses the original **source** type, preserving adapters (Audio → Modulation and Clock → Trigger). The segment entering the original receiver retains the original edge ID and amount; the new segment has unity amount. Internal edges are unchanged, even dormant modulation. Connection order at receivers is retained. Non-contiguous selections that create cycles fail before commit. No new schema or DSP emitter is required.

`CanvasNavigationService` creates a child layout with translated relative positions, rack placement, node/edge descriptions and visualization preferences; existing descendant canvases are retained. The parent layout keeps unrelated positions. One graph Undo restores the nodes and retained parent metadata; Redo restores the composite and retained child layout using existing bounded history caches. Grouping requires the normal structural recompile/crossfade, not guaranteed uninterrupted oscillator phase. Undo is not a general ungroup action: use the explicit expansion below for a composite's current edited routing. The independent Module Library is 7.4.3.

<a id="implementation-ungroup-composite-742b-in-review"></a>

### Implementation: ungroup composite (7.4.2b, implemented)

Implementation: [PR #48](https://github.com/wgabel/orbit2/pull/48).

Select exactly one composite in its parent canvas, open **Selection** and choose **Ungroup composite**. The wrapper and its two racks disappear; its current internal ordinary nodes (including nested composites) move into the parent and become selected. The operation uses the current wiring, not the original grouping recipe. For example, an incoming boundary cable at 2 and its internal receiver cable at -25 become one cable at -50. Rack-to-rack passthrough and fan-out are supported when every used boundary has one source. The receiving edge retains its ID and dormant/zero amounts, and the product must be representable as a finite float. Event paths retain unity and the final direct type must pass domain validation.

`CompositeUngrouping.Preview` constructs the candidate routes; `UngroupCompositeCommand` verifies revision and restores a detached complete root candidate through domain validation. `CompositeUngrouped` publishes one structural fact. Reject muted wrappers, used one-sided boundaries, sums of multiple boundary sources, duplicate or colliding identities/routes and any incompatible or cyclic candidate. Completely unused ports are harmless. This deliberately conservative scope avoids inventing zero/default sources, hidden mixers or a replacement mute control; connect or remove one-sided cables, or expose a separate mixer before expanding. A refusal leaves graph, revision and history unchanged.

Before dispatch, `CanvasNavigationService.UngroupComposite` preflights `CompositeExpansionLayout` using the current child presentation. Relative positions are translated around the wrapper's location, including edited positions; neighboring nodes are untouched. Node aliases/descriptions, retained receiving-cable labels and node visualization preferences are transferred rather than recovered from stale grouping caches. Notes/visual groups within the child, parent visual membership of the wrapper, wrapper/rack descriptions and labels on discarded boundary segments prevent expansion until the user moves or removes the affected metadata. Wrapper-only plot state cannot survive its removal. Existing descendant canvases remain accessible with rebuilt breadcrumbs. Undo/redo use the bounded removed-layout caches and restore one structural edit; standalone annotation history remains separate. Normal save/reload and structural crossfade apply, with no promise of live phase continuity.

### Example: Deep Space Drone

Construct two oscillators, pink noise, a mixer and reverb. Select all five, leaving the Master Output outside. Group them into a composite named Deep Space Drone. The existing reverb-to-master edge becomes a composite Audio output; optional Pitch and Intensity inputs can be exposed deliberately through its Input Rack. A self-contained module can have no inputs. Open the composite to tune it, save it under Drones, then add it twice in a different patch and set their pitches independently.

```mermaid
flowchart TD
    S[Selected sound graph] --> C[Create composite]
    C --> B[Review exposed ports]
    B --> L[Save module definition]
    L --> A[Insert independent instance A]
    L --> D[Insert independent instance B]
```

### Example: reusable effect and musical layers

Select the delay/reverb/EQ chain in a patch, leaving its source and destination outside. Grouping exposes an Audio input and Audio output. Save it as Dark Echo Chamber in Effects. In another composition connect a melodic voice to its input. For a whole ambient piece, insert different modules for the harmonic bed, bass, melodic accents, environmental texture and slow modulation, and coordinate them with the shared patch transport. Use the [composition guide](../examples/generative-ambient-composition.md) for arrangement and independent musical timescales. Saving a sound module alone does not create a complete arranged piece.

## 3. Grouping and ungrouping contract

Grouping is a structural graph edit, not a visual group or a sequence of UI-driven delete/add calls. Build and validate a complete candidate from the current immutable root snapshot, then commit it through the existing session/command machinery as one atomic edit.

- Limit each selection to one owning graph, excluding its mandatory racks and root Master Output. Selecting a composite moves its owned descendants as a unit. Reject stale/missing IDs and invalid or empty selections clearly. Allow a single ordinary node if it is useful to wrap it. Disconnected selections may be useful as modules if valid.
- Keep selected node/parameter/port identities where compatible with ownership and graph-qualified addressing; move internal edges, including dormant connections and their signed/zero amounts. Do not duplicate internal DSP nodes, discard configuration or implicitly activate dormant routing.
- For each crossing edge generate typed boundary ports from actual endpoint contracts (signal type, channel count, direction, receiving fan-in); display candidate names derived from node/port labels and let the user rename them. Use stable IDs, not names, for both rack projections. Preserve Audio, Modulation, Trigger, Gate and Clock semantics, including allowed adapters and event unity rules.
- Initially favor a conservative one-to-one boundary per crossing edge or a provably equivalent shared port. Place original continuous-edge weight on exactly one segment; the other must be unity. Preserve distinct external sources, fan-out, receiving sums, modulation units and event alignment. Never multiply weights twice, drop a negative weight, merge independent sources illegally or introduce an artificial DSP operation. Reuse one output for identical sources only if equivalent.
- Keep the composite boundary public contract explicit. Grouping creates necessary crossings, not guessed pitch or volume ports for every internal parameter. Expose additional named ports deliberately via existing rack tools.
- Validate the parent graph, new child graph, boundary consistency, ownership, nesting, port compatibility, cycles, cardinalities and compiler reachability. Non-contiguous selections can produce a parent-level cycle (e.g. selected A and C with unselected B in A→B→C). Reject a currently unrepresentable selection with a useful explanation and offer selecting the intervening nodes; never silently add feedback or alter dependencies.
- Preserve existing neighboring placements. Place the new composite near the selection, transform selected relative positions into child coordinates, and use the established rack placement policy. Move/remap supported graph-qualified annotations and view preferences; keep unrelated parent layout intact. Reconcile selection and navigation after commit.
- One successful grouping/ungrouping operation yields one revision, coherent facts, one undo step and one autosave/playback update. Undo/redo restores both configurations and the associated layout/navigation mapping. If the current history mechanism cannot coordinate layout provenance, extend it deliberately; do not claim ordinary position history is already implemented.

### Crossing-edge example

Before grouping, a parent LFO reaches an oscillator's modulation input with amount -25 in the destination's units. After grouping, the LFO controls an exposed Modulation input and the corresponding Input Rack output connects to the oscillator. Apply -25 on the internal edge and unity on the external edge if the boundary contract preserves those units. A reverb output reaching an external mixer with amount 0.4 becomes an internal unity edge to an Audio Output Rack and an external 0.4 edge. Verify actual flattening, not just visible labels.

```mermaid
flowchart TD
    L[Parent LFO] -->|1| I[Input Rack]
    subgraph C[Composite internal graph]
        I -->|-25| O[Oscillator]
        O --> R[Reverb]
        R -->|1| U[Output Rack]
    end
    U -->|0.4| M[Parent mixer]
```

Ungrouping inverts the actual boundary routing, not merely the original grouping recipe. A user may have added internal branches, changed rack names, weights, fan-in, mute or nested graphs after grouping. Resolve current flattened dependencies, compose continuous weights once and preserve event unity. If a sum, unused/default input, macro, mute gate or other boundary behavior cannot be represented exactly by ordinary parent edges/nodes, reject with a concrete reason or explicitly materialize an equivalent supported adapter after preview/confirmation. Never silently change the sound. Ungroup can remove the wrapper/racks only after proving equivalence; preserve unrelated parent wiring and generate unique names/IDs where necessary.

Grouping and ungrouping should preserve the audible mathematical graph. A structural edit may currently require the ordinary compile/voice crossfade and thus restart some runtime state. Avoid gratuitous recompilation; test deterministic flattened signal equivalence and use the existing last-good-voice protection. Do not promise phase/tail continuity for a recompiled graph unless separately proven; reuse improved topology equivalence when available.

## 4. Module data, ownership and persistence

A module definition is an immutable, schema-versioned data document; it is neither compiled DSP nor a running voice. Suggested fields (names are design sketches):

- Definition ID, schema version, name, category, description, tags, optional attribution and template revision.
- Canonical composite boundary definitions (including port order and identity) and internal graph snapshot: recursive nodes/types, stable configuration keys, parameters, ports, connections, weights, nested composites, supported persisted note patterns, audition flags and future supported payloads.
- Child canvas layouts keyed by local graph/node IDs, supported annotation/visualization preferences, and optional macro definitions as Phase 7.4.6 becomes available.
- Optional descriptive source metadata and transport assumptions (e.g. intended BPM, whether using beats or free seconds). No dependency on the source patch ID, active canvas, history, open captures, transport phase, live telemetry or compiled binaries.

Reuse validated reconstruction/serialization infrastructure (currently `PatchDocumentSerializer`, `SavedGraph`, `SavedComposite`, `SavedCanvas`) where practical. Extend versioned payloads rather than silently dropping non-scalar graph features. Persist module definitions independently of named patches and individual node presets, through an application-facing repository and a dedicated IndexedDB logical namespace. Follow existing atomic compare-and-replace and storage error patterns; load the library independently of patch recovery.

Saved module IDs identify definitions, not graph instances. Insertion deep-clones the entire definition, assigns fresh graph/node/parameter/port/connection/rack IDs and remaps assignments consistently, including mirrored boundary ports, nested graph addresses, layouts, visualization settings, annotations and future macro mappings. Use one shared identity-remapping utility, not string replacement or shallow copies. A boundary's mirrored socket ID must be remapped once to both projections. No mutable object, musical playhead or DSP state is shared between inserted instances.…5111 tokens truncated…pth is a valid no-contribution route.
- Each active binding evaluates `mapped = (1 - pow(u, exponent)) * atZero + pow(u, exponent) * atOne`. Clamp normalized input before the exponent, preserve reversed/constant endpoints, and preserve the domain curve rather than inventing another mapping. Example: b=0.5, source=-0.5 to +0.5, amount=0.4 gives u=0.3 to 0.7; 220–440 Hz linear gives 286–374 Hz. An inverse gain mapping 1→0 produces 0.7→0.3. Curves are independent per target.
- Replace only the target's *base expression*. Existing downstream native modulation, pitch multiplier, parameter and Nyquist limits retain their ordering and units. Do not feed mapped absolute values into native additive modulation sockets or subtract the manual base as a compensation trick. Do not smooth the per-sample mapped expression a second time; only the normalized base slider is smoothed. Dormant targets do not consume dynamic dependencies or override manual values.
- No per-sample C#/JS/UI commands; normalized control, fan-out and curves execute in Faust/WASM. Authored base, default, curve, manual fallback and history never depend on DSP observations.

#### Lowering and lifecycle

Prefer extending existing flattened graph lowering with backend-only stable macro-control node/dependency metadata and explicit per-target base-expression binding, rather than a new editable node family, duplicated rack wiring, or UI-rate updates. Use graph/composite/macro identities for stable synthetic IDs; test collisions and nested same-local-ID cases. Connect dependencies for active mapped targets even if they lack a native modulation socket. The synthetic node must be reachable only through actual active consumers; existing graph dependency/cycle checks must see the new edges. Inspect all reachable emitter parameter access paths before committing the lowering design; unsupported target paths must be rejected explicitly until implemented, not silently remain static. The exact internal metadata shape is deferred to S4d after baseline tests.

Use an explicit typed macro control address (graph, wrapper node, macro ID) in the compiler/playback manifest, not a fake authored ParameterSnapshot. Distinguish macro base writer from passive target meters. Keep stable names independent of labels. Dynamic topology comparison must include association, active connectivity, target addresses, endpoints/exponents and active modes whenever they affect source; exclude ordinary base/manual values and names where safe. Existing voice/source reuse must remain for plain static macros, no-op edits and dynamic base-only updates. Link/unlink, active/dormant transitions or curve edits may require a structural compile; use existing last-good voice and replacement lifecycle; do not claim state continuity across recompilation. No source or DSP generation is stored in patches.

Range estimator: sum signed intervals, clamp normalized range, transform both endpoints through each monotone curve and sort; combine with existing target-native modulation and multiplier rules. Unknown sources conservatively remain unknown (or a proved full [0,1] bound clearly distinguished from a precise prediction). Meter macro normalized effective value and actual target effective value, with graph-qualified accepted revision/generation; do not replace manual or mapped static UI values with live readings. Preserve no polling or rendering for inactive/folded consumers.

#### Static macro reference (S4b, merged, [PR #171](https://github.com/wgabel/orbit2/pull/171))

Baseline production revision [`58fa1d6`](https://github.com/wgabel/orbit2/commit/58fa1d629a4fe1ef80e991727ce9f5e31a167a0c). The existing native probe generates an importable nested composite fixture with an unused Modulation boundary input, one oscillator and gain, and static curved frequency, reversed amplitude, constant gain and conditional pulse-width mappings. For each normalized base 0, 0.5 and 1, plus square-wave activation and mapping removal, compare the authored macro snapshot to the identical graph with the same physical values set manually. Preserve independent authored bases; check graph-qualified control origins, exact source equality between paired cases, and unchanged source across sine base edits. The legacy fixture has no input association field. S4d must add the explicit associated-but-unconnected comparison when that contract exists.

`tests/Orbit.Audio.Probe/MacroBaselineFixtures.cs` generates `static-macro-baseline.json` containing exact serialized patches, paired sources and control values. `tests/compiler/StaticMacroBaseline.test.mjs` compiles the real generated Faust source with the existing pinned Faust WASM 0.19.0 toolchain, renders fresh independent 128-frame processors at 44.1/48 kHz, compares every sample with absolute tolerance 1e-6 and records peak, RMS, maximum adjacent-sample difference, settled positive-crossing frequency, max paired sample error and source/sample SHA-256. Assert finite bounded audible output and expected settled frequency. CI retains both input fixture and report as `static-macro-audio-reference`; identities are generated by native commands, so retain the actual fixture for a paired rerun, not a cross-run source-hash equality assumption. The playback service regression also exercises the existing curved base control and no voice replacement/manual fallback. No production changes. Baseline verified in [build #778](https://github.com/wgabel/orbit2/actions/runs/45827561950), head `4d5a792ac91867e7c83ec9e0a1787499b53a114b`; [retained artifact](https://github.com/wgabel/orbit2/actions/runs/45827561950/artifacts/15119809951). Ten cases (five per rate), all max paired sample error 0; maximum absolute measured frequency error 0.00257 Hz (440 Hz at 48 kHz). Peak range 0.0999985–0.2999981, RMS 0.07034–0.21110; square-wave steps are intentional. Native and published editor tests passed. Physical-device timing is not measured by this offline reference.

**Native association (S4c.1, merged, [PR #175](https://github.com/wgabel/orbit2/pull/175)):** `CompositeMacroDefinition.ModulationInputId` is optional and immutable; shared validation accepts only distinct existing mono Modulation wrapper inputs. Boundary replacement validates reservations before changing sockets or deleting edges. Native schema-1 saves omit null and restores missing as null; duplication remaps association through existing boundary identity map. Macro base edits and existing mapping editor preserve the association. No new UI association action or dynamic DSP yet: imported associations remain metadata and static audio behavior is unchanged. Agent Kit compatibility (S4c.2, merged, [PR #177](https://github.com/wgabel/orbit2/pull/177)) exports a separate deferred-feature declaration and native-accepted fixtures with and without association; the generated DTO schema includes the optional field. The root-leaf Python validator/load gate rejects both before mutation, and composite creation remains deferred. No offline structural editing is advertised. See [Agent Kit contract](../core/agent-kit.md).

**Flattened dependency projection (S4d.1, merged, [PR #181](https://github.com/wgabel/orbit2/pull/181)):** The existing boundary pass-through already provides the weighted sum; avoid a second synthetic DSP node or virtual editable socket. Opt-in `GraphFlattener.Flatten(root, includeMacroDependencies: true)` projects a dictionary keyed by flattened leaf ParameterAddress, with immutable MacroSource (parent graph, composite, macro ID, default/base and the mapped rack output PortAddress) and the original binding. Only connected associated inputs and active targets appear; zero-depth cables count as connected. `MacroDependencies` deduplicates source-node/target-node pairs, and `GraphDependencyOrder.Get` accepts those additional dependency links without introducing a second authored edge or changing native modulation amounts. The normalized expression is consumed by mapped target base expressions; no additional persisted nodes or ports. Dependency count includes implicit links when opted in. At S4d.1, default compiler/range/playback calls did not opt in; S4d.3 enables opt-in playback while static or disconnected cases remain on the existing path. Tests cover nested target origins, signed input coefficient, dormant/unconnected filtering, reachability and dependency cycle detection. Review of built-in emitters found shared Parameter calls (including oscillator pitch helpers and physical instrument aliases) and separate host scheduling for note patterns; unsupported host-managed parameters are rejected rather than silently ignored.

**Opt-in Faust expressions (S4d.2a, merged, [PR #186](https://github.com/wgabel/orbit2/pull/186)):** `Compile(..., enableMacroModulation: true)` requests the projected dependencies. `FaustMacroExpressions` generates a single normalized smoothed base plus the compiled weighted boundary signal per graph/composite/macro address; clamp before the independent power curves. Each target replaces only its base expression in `FaustNodeContext.Parameter` and retains existing pitch multiplication, additive modulation and clamps. A typed `FaustMacroControl` manifest supplies the initial base and normalized passive meter; no independent target writer. Host-managed Note Source and Note Sequencer targets fail closed until explicit semantics are designed. Base-only edits retain the same source.

**Offline DSP verification (S4d.2b, merged, [PR #193](https://github.com/wgabel/orbit2/pull/193)):** Generated Faust WASM checks at 44.1/48 kHz cover signed, inverted, clipped, zero-depth, constant-only, downstream pitch/modulation and disconnected reference cases. Faust-pruned zero-depth driver controls are optional while macro and carrier controls remain required; measured reports are retained as the `static-macro-audio-reference` CI artifact. Playback activation is the next increment.

**Playback integration (S4d.3, merged, [PR #195](https://github.com/wgabel/orbit2/pull/195); [build CI](https://github.com/wgabel/orbit2/actions/runs/46006912410), [compliance CI](https://github.com/wgabel/orbit2/actions/runs/46006912389); merge `9586ea8`):** The playback service opts into the verified lowering and resolves accepted macro controls by graph/composite/macro identity. Macro base edits update that control on the current voice; mapping, association, connectivity and existing structural edits flow through the topology comparison and ordinary compile/replace lifecycle. The topology signature excludes macro base values and display names but includes defaults, associations and target curve structure. Disconnecting the associated input drops the dynamic control and returns to the static mapped base. The program reports whether it contains active macro modulation. Last-good playback and generation-qualified acceptance continue to use the existing playback lifecycle. CI passed the .NET domain and presentation suites, generated Faust/WASM checks, published playback and editor checks, and documentation compliance. Physical-device audition and end-to-end Kick Drum verification remain in S4g.

**Range estimation and effective-value telemetry (S4e, merged in [PR #198](https://github.com/wgabel/orbit2/pull/198), merge `9e288da`; [build CI](https://github.com/wgabel/orbit2/actions/runs/46015775478), [compliance CI](https://github.com/wgabel/orbit2/actions/runs/46015840686); tested revision `80c26c7`):** The estimator opts into active macro dependencies for analysis only. It sums signed weighted source intervals, adds the current normalized macro base, clamps to 0–1, transforms both endpoints through each monotone target curve, then combines those mapped-base bounds with the target's existing modulation and pitch rules. Unknown inputs remain unknown; disconnected associations retain the exact static mapped base. Playback exposes normalized macro readings by graph/composite/macro identity and adds passive meters for each mapped target's final effective value. Both use the existing accepted graph revision and playback-generation guard; telemetry is transient and never changes authored macro bases or parameter values. CI passed domain and presentation tests, Faust/WASM generation and rendering, browser playback, published editor playback and the visualization lifecycle.

#### Acceptance and failure gates

| Slice | Required evidence before proceeding |
| --- | --- |
| S4b | Pin native baseline source/toolchain and fixtures; test static curve endpoints, reverse and constant curves, nested origins, dormant activation, removal and manual fallback, source-identical/live updates; deterministic 44.1/48 kHz baseline render. Keep known baseline results and existing test assertions. |
| S4c | Atomic valid/invalid association, connected input deletion guard, rename/reorder, undo/redo, clone remap, legacy JSON and new round-trip; update native/Agent Kit metadata and fixtures or explicitly gate unsupported kit edits. No public activation until S4d–f. |
| S4d | Native/generated Faust and offline render at 44.1/48 kHz: normalized sum and clipping, signed depth, zero amount, multiple independent linear/reverse/power targets, native additive modulation and pitch ordering, dormant behavior, nested IDs and rejected cycles. Static/unconnected legacy path should be source-identical to baseline; compare render with the same deterministic schedule. Connected control updates preserve program and generation; structural edits correctly recompile. Assert finite bounded output, not merely compilation. Record error tolerances from actual float/control precision; avoid promises of bit-exact results for changed DSP. |
| S4e–f | Predicted bounds enclose sampled values, unknown and dormant cases, accurate graph-qualified meters, stale rejection, exact manual fallback, desktop/touch association and removal, no added folded telemetry or gesture regression. |
| S4g | End-to-end import/export, clone, live edits, disconnect, undo/redo, nested navigation, both rates, paired offline and existing browser performance fixtures; record actual results with revision, workload and limitations. Physical Android audition/benchmark is separate evidence; do not claim it if unavailable. |

Go/no-go: no dynamic feature is exposed until native validation, compiler, live update and UI tests agree. Keep static macro behavior unchanged and avoid global smoothing, sample-rate, routing or schema migrations. If required compiler paths or metadata compatibility prove unsuitable, stop and revise the owning design before further production code. No Kick Drum implementation until S4 is verified.

### Unified percussion composites

The first Kick Drum is an ordinary editable composite, not a separate opaque engine. Its external interface is one typed Trigger input and Audio output, plus three normalized front-panel controls with optional modulation sockets: Power (strike force), Size (perceived drum dimensions), Damping (resonant to tight). The base controls remain usable without modulation. Sequencer Velocity should influence Power; define normalized additive input and a documented velocity mapping/offset so a zero-velocity event is silent when configured for velocity control. No separate required Gate or Velocity port. Trigger starts a natural decay; strike-specific controls must be held for the hit where necessary. Verify whether phase reset and velocity latching are supported before claiming them.

Power changes overall energy, transient intensity, harmonic balance, pitch excursion and optional saturation, not just volume. Size changes fundamental, relative modal frequencies and resonant duration, not just pitch. Damping shapes independent body, transient and shell/noise tails, not a single master envelope. Start with complementary sub/body/punch/beater/noise layers; each has independent envelope/filter behavior. Prefer distinct mappings for each control; when effects overlap use explicit independent signal contributions or a supported combination rule, never conflicting overrides or hidden last-writer-wins behavior. Preserve headroom and assess at comparable loudness. Use only shipped nodes; optional Distortion follows 7.6.6. Factory presets can change internal character without additional front-panel sliders. Extend the same three concepts to other percussion after Kick passes native measurements and listener audition. See the [advanced roadmap](../roadmap/advanced.md) for dependencies and status.

Example front panel for Deep Space Drone: Warmth, Movement, Space sliders, Pitch modulation input and Audio output, with Open internals always available. Rich macros can come after basic grouping/library support; the library must not depend on them.

## 8. Independent copies first; optional linked/versioned templates later

Initial implementation inserts independent copies only. Two instances can have different tuning, internal edits and runtime state, and a portable patch is self-contained.

Later investigate optional provenance-aware revision updates: user explicitly previews differences, maps old/new boundary IDs, resolves conflicts with local edits, and chooses which instances update. Support pinned revisions, safe undo, compatibility/migration and missing/deleted library entries without broken playback. Never replace a live graph just because a library entry changed. Avoid mandatory external references and do not require linked-template support before delivering useful independent modules.

## 9. Implementation boundaries and verification

Follow [project responsibility boundaries](../core/project-structure.md). Domain validates graph transformation and ownership; Application owns commands, template capture/clone and library services; Presentation owns selection/layout projection and preview; Infrastructure provides storage/portable files; UI assembles reusable controls; the existing compiler flattens normal composites, without module-name-specific synthesis rules. Use existing code and tests to determine precise implementation touchpoints; this design is not a fixed class-name prescription.

Deliver and test in [Phase 7.4 increments](../roadmap/advanced.md#phase-74--reusable-composite-modules-planned). Important acceptance cases:

| Area | Acceptance evidence |
|---|---|
| Selection | Additive/marquee selection, nested canvas isolation, rack/master exclusion, no audio revision for visual selection |
| Group/ungroup | Weighted Audio/Modulation, trigger/gate/clock, fan-out and sums, dormant links, nested graphs, invalid cyclic selections rejected atomically |
| Sound | Equivalent flattened routing and offline deterministic samples at 44.1/48 kHz where meaningful; no leakage or duplicate DSP roots; last-good live runtime on failed compilation |
| History/layout | One undo/redo for structural edit, restored parent/child positions, independent selections, retained annotations/preferences, save/reload |
| Module cloning | Repeated insert in root/child, distinct IDs and runtime state, no external references, rename/delete/overwrite leaves placed instances intact |
| Patch conversion | Original unchanged; same master-feed signal before host gain; destination transport rules explicit; no child Master Output |
| Storage/import | Restart and cross-tab conflict, malformed/oversized data and unsupported versions, no partial commits, repeat export/import and independent portable copies |
| Macros | Direct/multi-target mappings, inverse/log ranges, conflicts and removal, undo, modulation, independent instances and no unnecessary compile |

Use a substantial ambient graph as a worked end-to-end fixture alongside small deterministic unit cases. Profile resource usage for repeated large modules under Phase 7.3; reusable definitions are not an optimization that automatically shares DSP computation. Record actual implementation PRs in this module and the milestone register as work ships, distinguishing design, in-review and merged evidence.
