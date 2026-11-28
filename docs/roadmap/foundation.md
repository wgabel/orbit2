# Milestone register: foundations

[Architecture overview](../architecture/overview.md) · [Roadmap](overview.md)

Related: [graph model](../core/graph-model.md) · [signals and ports](../core/signals-and-ports.md) · [commands and events](../core/commands-and-events.md) · [transport and timing](../audio/transport-and-timing.md) · [oscillators and noise](../audio/oscillators-and-noise.md) · [effects](../audio/effects.md) · [persistence and history](../editor/persistence-and-history.md).

<a id="phase-1--domain-primitives-graph-ownership-and-tests"></a>

### Phase 1 — Domain primitives, graph ownership and tests (implemented)

- Define SignalGraph, ISignalNode, generator/processor contracts, Parameter, PortDefinition and SignalConnection.
- Define stable ID rules, signal compatibility and fan-in/fan-out.
- Add registry with a minimal sine generator, gain and master output.
- Implement graph validation and reject dependency cycles.
- Add unit tests for the independent graph model, without requiring a browser or audio device.

Deliverable: create and validate a small graph without a UI or a real audio device.

<a id="phase-2--shared-timing-and-first-sample-rate-modulation"></a>

### Phase 2 — Shared timing and first sample-rate modulation (implemented)

- Use the actual AudioContext sample rate/frame clock.
- Introduce transport model and a time snapshot interface; add BPM synchronization incrementally.
- Create reusable Faust sine function taking a signal-valued frequency.
- Compile an initial fixed graph: LFO -> target frequency -> audible oscillator -> output.
- Support base frequency, modulation depth, start/stop with smoothing.
- Verify continuous phase and parameter behavior at 44.1/48 kHz.

Deliverable: audible two-sine modulation proof, no per-sample C#/JS message loop.

<a id="phase-3--commands-and-event-dispatch"></a>

### Phase 3 — Commands and event dispatch (implemented)

- Implemented: typed ConnectNodes, RemoveConnection, SetParameterValue, AddNode and RemoveNode commands.
- Implemented: validated single-command commits, immutable snapshots and revisioned change sets.
- Implemented: patch-session dispatcher with disposable typed and whole-change-set subscriptions, isolation and failure reporting.
- Implemented: the fixed modulation patch is constructed through the session commands.
- Implemented: independently tested canvas state projection, layout-only events, node placement, selection, pan and pointer-anchored zoom in Orbit.Presentation.
- Historical next steps — delivered in Phase 4 (canvas renderer and dynamic audio graph subscriber) and Phase 5 (multi-graph transactions and composite support).

Deliverable: a graph edit reaches independent subscribers once, with valid model state.

<a id="phase-4--flat-canvas-and-dynamic-graph-compiler"></a>

### Phase 4 — Flat canvas and dynamic graph compiler (implemented)

- Reuse the implemented JSON design token system for canvas, node, port, connection and parameter styles.
- Implemented: reusable canvas renderer with role-colored nodes, typed ports and visible Bézier connections, plus pointer pan/zoom and fit.
- Implemented: drag-to-connect, click-to-connect, delete selected connections/nodes, node placement and shared base parameter editor.
- Implemented: dependency ordering and extensible C# graph-to-Faust source compiler with stable parameter controls (sine, gain, master output), plus offline WASM probe tests.
- Implemented: on-demand graph playback from the editor, with a lazy Faust compiler worker, revision status and safe start/stop. The original fixed UI is retired.
- Implemented: live base-value updates on the running worklet, including startup queuing, revision ordering, smoothed controls and structural-change detection.
- Implemented (follow-up, [PR #75](https://github.com/wgabel/orbit2/pull/75)): [parameter-specific base-control smoothing](../core/parameters-and-modulation.md#base-control-smoothing) shortens manual oscillator frequency transitions while retaining continuous phase, modulation bandwidth and anti-click protection; rapid retune regression and device listening check.
- In review ([PR #80](https://github.com/wgabel/orbit2/pull/80)): centralized smoothing on every registered parameter definition, including the shared Pitch Control and resonator. The compiler rejects missing policies; the shared pitch bank regression checks immediate transposition/fine tune and rapid retargets.
- Implemented: automatic structural replacement, stale-revision invalidation, same-source reuse, 50 ms audio-clock crossfade and last-good-graph recovery.
- Implemented: effective parameter telemetry and animated modulation indicators; conservative ranges are estimated from the graph and live values come directly from Faust passive meters.
- Historical next step — delivered: composite nodes and nested canvases (Phase 5).
- Implemented: compile asynchronously, track graph revision and safely swap runtime.
- Implemented: tests for fan-out and multiple modulation sources.
- Delivered later in Phases 6.6–6.7: low-pass effect and mixer.

Deliverable: construct and edit a graph of two generators, a filter, mixer/output and modulation edges on the canvas while audio runs.

<a id="phase-5--composite-nodes-and-rack-interfaces"></a>

### Phase 5 — Composite nodes and rack interfaces (implemented)

- Implemented: ICompositeNode, graph-owned Input Rack and Output Rack, immutable shared boundary identities, recursion checks and nested snapshots.
- Implemented: child graph command routing, patch-wide committed snapshots, exact graph event filters and independent graph-ID-targeted canvas projections.
- Implemented: recursive DSP boundary flattening, stable graph-qualified control/meter mappings, child modulation range estimation and live child parameter edits.
- Implemented: graph navigation, breadcrumb component, cached independent canvas layouts, starter drone catalog and root playback across canvas changes.
- Implemented: editable Input Rack and Output Rack ports, projected automatically onto composite node sockets.
- Implemented: transactional add/rename/reorder/remove-port commands crossing parent/child boundaries, with one changeset and affected-graph notification filters.
- Implemented: reuse the same canvas UI for all levels.
- Implemented: selected-wire amount editing in root and nested canvases. Keep immutable edge identity and endpoints; commit a graph-local `ConnectionAmountChanged` with before/after values. Signed finite amounts support polarity inversion and silent connected routes. Direct parameter targets use their own unit; boundary/audio edges use multipliers. Existing range estimation, topology comparison and crossfade apply the revised coefficient from the committed snapshot.

Deliverable: open a drone node's internal canvas, connect external LFO to a named input, route it internally and hear the modulation.

### Phase 6 — Transport synchronization, drone presets and editor features (partial)

#### Phase 6.1–6.15 — Foundations, nodes and workspace management (implemented)

##### Timing and sound generation

- Implemented (6.1): shared patch-wide transport service, BPM editor, sample-frame-derived musical position, start/stop/resume/reset semantics, and musical divisions.
- Implemented (6.2): audio-rate shared transport phase, tempo-synchronized sine LFO, validated musical division parameter choices, root/nested and crossfade phase sharing.
- Implemented (6.3): one reusable oscillator with waveform selector (sine, triangle, square, sawtooth), phase, normalized output and structural change crossfade.
- Implemented (6.4): unified free/synchronized oscillator timing modes, conditional rate controls, dormant wires and active DSP lowering.
- Implemented (6.5): separate white/pink/brown noise generator with modulatable amplitude and structural color selection.
- Implemented (6.6): independent layered drone preset, generic composite preset catalog registration and mono weighted mixer.
- Implemented (6.7): unified resonant filter processor (low-pass, high-pass, band-pass, notch), with audio-rate cutoff and Q modulation.

##### Workspace persistence and history

- Implemented (6.8): versioned `PatchDocument` serialization, including root/nested graph identities, rack definitions, primitive parameter values and separate canvas layout. Reconstruction uses registered leaf factories and validated domain graph operations, not persisted DSP definitions.
- Implemented (6.9): browser-local IndexedDB, debounced autosave and startup recovery of all visited layouts, active canvas and BPM; playback starts stopped.
- Implemented (6.10): named independent patch snapshots, new/open/overwrite/delete, JSON import/export, validated workspace replacement while stopped.
- Implemented (6.11): bounded patch-wide graph command history with atomic undo/redo and fresh committed revisions, including nested boundaries and stable edge/node identities.

##### Audio effects

- Implemented (6.12): single-band parametric EQ with continuously modulatable frequency, gain in dB and Q, sharing the state-variable filter core.
- Implemented (6.13): mono feedback delay with free and synchronized timing, fractional delay, audio-rate modulation and live shared BPM.
- Implemented (6.14): mono algorithmic reverb with audio-rate decay, damping and wet mix, bounded feedback combs and all-pass diffusion.
- Implemented (6.15): two-voice mono chorus with modulatable rate, depth, base delay and wet mix; shared millisecond parameter unit and conservative output bounds.

Deliverable: several independently configurable drones from one reusable modular architecture.

## Agent documentation discovery and compliance (AGENT-DOCS)

Repository infrastructure, independent of the audio/editor phase numbering. Keep the [canonical contributor policy](../agents/contributor-instructions.md) authoritative and load only task-relevant documentation. Automated checks can verify structural compliance and declared evidence, not an agent's comprehension.

- **AGENT-DOCS-S1 — Restore native discovery (In review; committed, CI not verified)** — Depends: none; preserve root `AGENTS.md` as the only permitted root Markdown file, link canonical instructions, align Copilot and documentation conventions. Done: entry point exists; other root Markdown is rejected; links resolve. Verify: documentation checker and PR CI. [PR #105](https://github.com/wgabel/orbit2/pull/105).
- **AGENT-DOCS-S2 — Mandatory initialization (Implemented; documentation committed, awaiting merge)** — Depends: S1 committed on this branch; define the canonical pre-work protocol for every task, including read-only and delegated work, scope changes, unavailable docs and concise initialization declaration. Done: all entry points route to the same protocol; representative tasks have clear prerequisites. Verification: waived for documentation-only changes by owner; no CI claim. [PR #105](https://github.com/wgabel/orbit2/pull/105).
- **AGENT-DOCS-S3 — Explicit task routing (Implemented; documentation committed, awaiting merge)** — Depends: S2; define additive mandatory/recommended/reference routes using one small machine-readable configuration and distinguish semantic task classification from deterministic path-based checks. Done: route IDs are unique, all required paths resolve, cross-cutting and novel tasks have a fallback. Verification: waived for documentation-only metadata by owner; automated validation and fixtures deferred to S6. [PR #105](https://github.com/wgabel/orbit2/pull/105).
- **AGENT-DOCS-S4 — Reduce mandatory context (Implemented; documentation committed, awaiting merge)** — Depends: S3; split global and domain-specific contributor rules without losing obligations or creating competing copies. Done: mandatory global policy stays compact; all prior rules remain reachable by appropriate routes. Verification: waived for documentation-only changes by owner; moved domain rules into routed development, music and sound-design policies. [PR #105](https://github.com/wgabel/orbit2/pull/105).
- **AGENT-DOCS-S5 — Auditable handoffs (Implemented; documentation committed, awaiting merge)** — Depends: S4; standardize compact initialization, completion and PR evidence with selected routes, actual paths read, applicable constraints and verification limitations. Done: examples cover code, documentation, music and delegated work. Verification: waived for documentation-only changes by owner; report contract and PR template committed. [PR #105](https://github.com/wgabel/orbit2/pull/105).
- **AGENT-DOCS-S6 — Enforce structural compliance (In review; CI pending)** — Depends: S5; validate routing and PR declarations against objectively classifiable changed paths, required documentation and exceptions; add negative fixtures and wire checks into CI. Document how repository owners can make the check required for main, and distinguish declaration from proof of reading or understanding. Verify: passing/failing fixtures and PR CI. [PR #105](https://github.com/wgabel/orbit2/pull/105).

- **AGENT-DOCS-S7 — Repository-wide documentation coverage (In review; [PR #148](https://github.com/wgabel/orbit2/pull/148); verification pending)** — Depends: S6 existing tooling; register all active Markdown owners with narrow semantic routes and explicit historical/redirect classifications. Extend existing checks with Git inventory, forward and reverse navigation, local paths/anchors and declared semantic scenarios; warn on deep paths. Verify: focused positive/negative fixtures and full repository CI audit. See [coverage contract](../agents/compliance-reports.md#repository-wide-documentation-coverage).

Next: verify S7 repository audit and CI; resolve any remaining actual link or anchor issues before closeout. Owner may require the compliance status check on main. Owner-authorized documentation-only slices can proceed without intermediate CI or merge; S1 includes a validator change and remains unverified in review. Executable changes retain their verification requirements.

## Documentation-aware build and deploy (CI-DOCS)

- **CI-DOCS-S1 — Shared classification and safe workflow/deployment gating (Merged; [CI verified](https://github.com/wgabel/orbit2/actions/runs/45661056344), [PR #132](https://github.com/wgabel/orbit2/pull/132)):** one coordinated change preserves the existing build/compliance check names, skips runtime work only for known documentation-only paths, runs full CI on mixed or unknown changes, and permits an already built artifact to deploy across documentation-only main changes under the Pages lock. Unit/integration Git fixtures cover classification, rename/deletion, missing history, mixed PRs and deployment freshness. See [deployment contract](../getting-started/deployment.md#documentation-only-ci).
- **CI-DOCS-S2 — End-to-end workflow evidence (Verified):** [PR #133](https://github.com/wgabel/orbit2/pull/133) merged; both its [documentation PR](https://github.com/wgabel/orbit2/actions/runs/45662694426) and [main push](https://github.com/wgabel/orbit2/actions/runs/45663005951) passed lightweight checks and skipped runtime build/deploy. [PR #132](https://github.com/wgabel/orbit2/pull/132) passed full CI; its [main build and Pages deployment](https://github.com/wgabel/orbit2/actions/runs/45662482468) succeeded across the intervening documentation merge, with the deployment guard explicitly accepting unchanged runtime inputs. Regression tests cover stale runtime and failed fetch. See [verification evidence](../getting-started/deployment.md#documentation-only-ci). Additional optimization of documentation-only closeout commits on code PRs is deferred pending a proven runtime-input cache/verification design.

## CI benchmark optimization (CI-BENCH)

Goal: reduce verification wall time without weakening regression assertions or mixing instrumented and ordinary timings. See [CI benchmark contract](../getting-started/deployment.md#ci-benchmark-optimization).

- **CI-BENCH-S1 — Workflow and timing audit (Completed, read-only):** successful [November 28 run](https://github.com/wgabel/orbit2/actions/runs/46132950294) took 14m43s; browser benchmark and lifecycle steps occupied most of the critical path. The earlier [November 27 run](https://github.com/wgabel/orbit2/actions/runs/46058671464) took 10m20s. Separate runners are not a controlled comparison.
- **CI-BENCH-S2 — Consolidate editor benchmark host (Merged; CI verified)** — Depends: S1; [PR #202](https://github.com/wgabel/orbit2/pull/202), [successful runtime CI](https://github.com/wgabel/orbit2/actions/runs/46135388344) at implementation head `6c271260aff17f3a6216ba42d5744188faf308e1` (PR merge revision `fa187490bfef92b91b7ebcd30e9c52aba675deb1`) and [agent compliance](https://github.com/wgabel/orbit2/actions/runs/46135388307). All three suite tests and all eight editor scenarios passed; artifacts uploaded. Shared suite logged 301.172 seconds, excluding fixture generation and host startup; hosted-runner variability prevents attributing a speedup to consolidation. Merged on 2026-11-28 at main `d9b79d554142e50062c8782a0e1455ef3fcf00de`. Next: S3a.
- **CI-BENCH-S3 — Remove duplicated scenario work (Completed; merged)** — Depends: S2 merged; first audit the overlap between ordinary editor-50 idle/pan/zoom and the all-expanded matrix, then repeated setup in the instrumented render-count, folded-resource and composite scenarios. Reuse an observation only if fixture identity, viewport, playback, expansion IDs, instrumentation, preceding state, warmup and workload are equivalent; keep instrumented passes isolated and retain every deterministic assertion. Verify: explicit workload/assertion inventory, report schema/provenance compatibility, coverage tests and measured wall time. See [CI benchmark contract](../getting-started/deployment.md#ci-benchmark-optimization).
  - **CI-BENCH-S3a — Consolidate bulk expansion state reads (Merged; CI verified)** — [PR #203](https://github.com/wgabel/orbit2/pull/203), [runtime CI](https://github.com/wgabel/orbit2/actions/runs/46137850684) and [agent compliance](https://github.com/wgabel/orbit2/actions/runs/46137850616) passed at implementation head `d8dd2dbe7af6746421f3a883a20220cff2e5a420`; merged at `9b98e0248f926b83c3482775f2f6d64cbb75775d`. Four focused tests passed. Ten expansion transitions used 20 snapshots instead of the previous 500 state reads, preserving 196 sequential toggles and 24 timing windows. Folded-resource bulk setup and restore each changed 48 nodes with two snapshots. All eight scenarios passed, suite duration 263.511s (uncontrolled comparison).
  - **CI-BENCH-S3b — Finish workload equivalence audit (Merged; documentation-only)** — Depends: S3a merged; audited the eight reports, nine fresh contexts, 82 timing windows, associated lifecycle probes and offline/live DSP. No remaining timed workload or fresh context was proven equivalent enough to remove without changing the coverage contract. Retain the distinct cases; do not spend additional slices on unproven micro-optimizations. See the [workload inventory and S4 handoff](../getting-started/deployment.md#s3b-workload-equivalence-audit). Verification: source and successful CI log inspection; documentation-only verification waived. [PR #204](https://github.com/wgabel/orbit2/pull/204) merged at main `c8603d56a1bec6473ba45a15cebedce84a91aed6`. Next: S4a.

- **CI-BENCH-S4 — Separate regression gates and diagnostics (Completed; merged)** — Depends: S3a/S3b merged; use the S3b inventory to build an explicit fast regression execution contract that keeps all existing deterministic assertions, including large and mobile fixture import/edit and resource lifecycle checks. Separate repeated quantitative timing work into an explicit diagnostic mode with unchanged full-report support; do not treat mixed-purpose scenarios as timing-only. Keep full runtime CI until regression/diagnostic parity is demonstrated. Verify: assertion inventory and negative cases, workload provenance, complete existing full suite and equivalent mandatory regression coverage.
  - **CI-BENCH-S4a — Shared correctness-only editor windows (Merged; CI verified)** — [PR #205](https://github.com/wgabel/orbit2/pull/205), [runtime CI](https://github.com/wgabel/orbit2/actions/runs/46139105082) and [agent compliance](https://github.com/wgabel/orbit2/actions/runs/46139105053) passed at implementation head `c410d0687aac0e6a4d30279cd37a6896ff081bf8`; merged at `2f937508dc762ae1aa267f1ca8d5eb438a6a2895`. Three focused tests and real editor-50 shadow parity (ten action windows and folded lifecycle) passed; all eight ordinary scenarios retained. Next: S4b.
  - **CI-BENCH-S4b — Full editor regression plan and parity (Merged; CI verified)** — [PR #206](https://github.com/wgabel/orbit2/pull/206), [runtime CI](https://github.com/wgabel/orbit2/actions/runs/46140446751) and [agent compliance](https://github.com/wgabel/orbit2/actions/runs/46140446804) passed at implementation head `52aaed834e90aadf3c65adf0c079d426092793ad`; merged at `fc75b504232f99d96037d84c0747d79e7bdcd3fe`. Eight report pairs, nine contexts and 82 action windows passed parity including lifecycle sequences. Full 317.078s versus regression 303.878s on the same runner (one sequential observation, not a repeated controlled comparison). Next: S4c.1.
  - **CI-BENCH-S4c — DSP regression and diagnostic split / CI policy (Completed; merged)** — Depends: S4b; preserve offline/live correctness and lifecycle assertions, then change regular CI only when all mandatory gates are covered.
    - **CI-BENCH-S4c.1 — DSP correctness modes (Merged; CI verified)** — [PR #207](https://github.com/wgabel/orbit2/pull/207), [runtime CI](https://github.com/wgabel/orbit2/actions/runs/46142048042) and [agent compliance](https://github.com/wgabel/orbit2/actions/runs/46142048014) passed at implementation head `2dbf82d3bc3647d1aab237ea41a972283816a280`; merged at `1b09ba8a2a372d48ef5c738b9ea0ea589601cd0a`. Real DSP parity passed 18 offline rows and two live profiles; editor parity passed eight reports, nine contexts and 82 windows. Next: S4c.2.
    - **CI-BENCH-S4c.2 — Activate verified regression policy (Merged; CI verified)** — [PR #208](https://github.com/wgabel/orbit2/pull/208), [runtime CI](https://github.com/wgabel/orbit2/actions/runs/46145594938) and [agent compliance](https://github.com/wgabel/orbit2/actions/runs/46145594902) passed at implementation head `475a0c4c20f1519e3ccee82fc0502ec29573d6fa`; merged at `08252712572154769f8d3ddfc3545b4b294508fd`. Both-mode runtime checks passed. Next: S5a.

- **CI-BENCH-S5 — Subsystem-aware test selection, roadmap checkpoints and parallel jobs (In progress)** — Depends: S4 merged; reduce unnecessary editor verification only for demonstrably isolated scopes, require full verification at integration checkpoints, then build once and run independent jobs on distinct runners. Unknown/shared/mixed changes fail closed. No skip labels or agent declarations alone reduce coverage. Preserve required status checks and deployment guard. See [CI impact policy](../getting-started/deployment.md#s5-subsystem-aware-test-selection-and-roadmap-checkpoints).
  - **CI-BENCH-S5a — First isolated-scope exemption and checkpoints (Merged; CI verified)** — [PR #209](https://github.com/wgabel/orbit2/pull/209), [runtime CI](https://github.com/wgabel/orbit2/actions/runs/46147514077) and [agent compliance](https://github.com/wgabel/orbit2/actions/runs/46147513954) passed at implementation head `0bc3ef1675318abb5624659ef984c2196b5e3587`; merged at `cf1c182005b81fa98fb0e3833e925d6920629e11`. All 12 classifier tests and full DSP/editor parity passed; actual isolated runtime omission remains to be verified on a representative PR. Next: S5b.
  - **CI-BENCH-S5b — Audit and expand subsystem impact rules (In review; CI pending)** — Audited native project references, editor/DSP fixture generation and compiler tests. Extend exemptions to standalone native Agent Kit exporter source and its contract tests, native unit-test-only and offline compiler-test-only changes; retain all other runtime checks. Production graph/serialization/compiler/audio/presentation sources remain editor-relevant because they are published dependencies and may affect shared playback/telemetry contracts. Verify classifier cases and full harness CI; verify actual isolated execution in S6. Next: S5c.
  - **CI-BENCH-S5c — Parallelize required independent jobs (Planned)** — Depends: S5b; publish once, transfer the exact versioned artifact and fixtures to independent browser/DSP/editor jobs on distinct runners; keep required aggregate status and deployment guard, with explicit skipped-versus-failed semantics. Split further if needed. Verify artifact identity, failure propagation and complete applicable coverage.
- **CI-BENCH-S6 — End-to-end timing and resource verification (Planned)** — Depends: S5; compare representative PR durations, runner minutes, scenario coverage and artifacts; document measured results and limitations. Verify: successful focused and full CI runs.

## Published playback resource diagnostics (CI-PLAYBACK)

Independent CI reliability follow-up; do not treat existing green builds as proof that all requested resources loaded. [Resource diagnostic contract](../getting-started/deployment.md#published-playback-resource-diagnostics).

- **CI-PLAYBACK-S1 — Identify failed resource requests (Planned)** — Depends: none; add URL, HTTP status, resource type and test context to published playback network failure diagnostics, including failed requests without a response. Reproduce the existing anonymous 404s and establish their actual paths. Verify: diagnostic tests and published playback CI with actionable logs. Coordinate with any active PR touching `tests/browser/PublishedPlaybackSmoke.mjs` before editing.
- **CI-PLAYBACK-S2 — Fix missing resources and enforce required assets (Planned)** — Depends: S1; fix confirmed missing assets or incorrect paths, distinguish explicitly justified optional requests from required runtime assets, and make unexpected required-resource failures fail the test. Verify: positive published playback and negative missing-critical-asset tests; preserve stale/versioned-asset checks. No blanket 404 suppression. A favicon is only a hypothesis until S1 identifies the request.

## CI test suite optimization (CI-TEST)

Goal: reduce expensive repeated test operations rather than the number of named tests. Separate mandatory fast correctness, affected-subsystem integration and exhaustive checkpoint/diagnostic coverage. Keep the existing required aggregate check, dependency-based selection and full-verification checkpoints from CI-BENCH. See [test workload contract](../quality/testing.md#ci-test-suite-optimization).

- **CI-TEST-S1 — Workload inventory and published scenario timing (In review; CI pending)** — Inventory the successful PR #210 run and mark 16 sequential published playback sections with stable names and status/wall milliseconds. Preserve every assertion, action, order, wait and context. Verify focused timer unit tests, unchanged smoke operations and actual full published run with timing output; no claimed speedup. Avoid editing the concurrent slider/macro feature assertions. [PR #212](https://github.com/wgabel/orbit2/pull/212). Next priority: S1a.
- **CI-TEST-S1a — Separate test-infrastructure and application verification (Planned; priority before S2)** — Depends: S1 and CI-BENCH-S5c.1; replace unconditional both-mode selection for test-only/workflow changes with explicit per-suite impact decisions. Standalone test edits run their owning checks; published smoke-only edits run the changed browser suite against a valid published build; classifier/gate changes run targeted tests plus representative positive/negative workflow paths; runner/validator changes exercise affected benchmark workloads. Full/regression shadow parity is required only when their shared correctness contract, diagnostic equivalence or coverage selection changes; unknown dependencies retain full coverage. Distinguish application publish inputs, test inputs, required job applicability and diagnostic mode. Verify exact PR-diff fixtures, dependency union, unknown/rename cases, required aggregate rejection of unexpected skips, and real focused and full CI. Split implementation into safe independently verified sub-slices before editing.
- **CI-TEST-S1b — Audit redundant CI triggers and preparation (Planned)** — Depends: S1a; measure PR synchronization, main pushes, doc-only closeouts and label events; cancel superseded PR runs when safe, avoid irrelevant label-triggered expensive work and avoid repeating diagnostics on main for isolated test-only changes while retaining exact revision/freshness and deployment gates. Audit repeated .NET exporter/probe build invocations, publish prerequisites and browser installs; use one proven build with no-build invocations where compatible, and measure independent preparation jobs against runner overhead. Verify no loss of relevant required checks or deployment protection; do not assume prior PR artifacts are reusable on a different main revision.
- **CI-TEST-S2 — Modular published editor integration tests (Planned)** — Depends: S1 verified and S1a's first targeted-policy slice; extract independent scenario modules and explicit fixture/reset contracts, split further by scenario family. Preserve current all-mode coverage, asset checks, stale-entry simulation, contexts and order until equivalence is demonstrated. Establish minimal mandatory playback smoke and full-suite orchestration. Verify old/new behavior and assertion inventory on the same published revision.
- **CI-TEST-S3 — Dependency-based integration selection (Planned)** — Depends: S2 and S1a; run mandatory smoke and only demonstrably affected modules on ordinary PRs. Unknown, mixed, shared changes and full-verification checkpoints retain all applicable suites. No title/body-based skips. Verify classification and actual focused/full coverage.
- **CI-TEST-S4 — Smaller editor correctness matrix (Planned)** — Depends: S1, S1a and CI-BENCH-S4/S5; use existing S3b inventory, distinguish diagnostic interactions from assertion-bearing cases. Reduce only independently demonstrated redundant correctness work, retain full diagnostic matrix and lifecycle assertions. Verify negative cases and parity.
- **CI-TEST-S5 — Shared Faust compilation and factory reuse (Planned)** — Depends: S1; audit byte-identical source/options/name and module lifetime; reuse immutable factories, never mutable DSP instances, preserve both rates, independent initialization and state-isolation assertions. Verify paired output equality and compile counts.
- **CI-TEST-S6 — Representative and exhaustive instrument matrices (Planned)** — Depends: S5; design boundary/known-failure regression matrix and retain full chromatic/expression/character cross-product for instrument-related changes and full checkpoints. Verify coverage and known-negative mutants at both rates; compare measured renders and time.
- **CI-TEST-S7 — Offline DSP correctness repetition audit (Planned)** — Depends: S5; distinguish statistical repetitions from state-isolation, nondeterministic and paired-hash checks; preserve full diagnostics, use fewer repeats only with an explicit validated correctness contract. Verify both rates, source identity, outputs and negative cases.
- **CI-TEST-S8 — Coverage parity and measured verification (Planned)** — Depends: S1a/S1b and S2–S7; compare representative ordinary, affected-subsystem and full-checkpoint CI runs, mandatory assertion coverage, wall time and runner minutes. Record trade-offs and limitations before claiming measured speedup.
