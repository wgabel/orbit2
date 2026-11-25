# Full GPU-rendered editor — Phase 7.3.11 (planned)

[Roadmap slices](../roadmap/advanced.md#7311-gpu-renderer) · [Coding standards](../quality/coding-standards.md) · [Architecture](../architecture/overview.md) · [Performance baseline](../quality/performance.md#large-patch-editor-responsiveness-739)

## Objective and boundaries

The target is a fully WebGL-rendered visible editor (nodes in every expansion state, all ports, cables, properties, knobs, sliders, sequencers, plots, annotations, menus, workspace and piano), not a permanent HTML overlay for expanded nodes. Native browser input, file selection, clipboard, focus and accessible semantics may remain as minimal support adapters; they do not constitute a second visible renderer or a per-property DOM tree. PixiJS/WebGL is a candidate, not an approved dependency. Compare optimized DOM/SVG, Canvas 2D and WebGL in controlled experiments; consider WebGPU and worker/OffscreenCanvas only with measured benefit and a supported fallback. Full migration is conditional on evidence and explicit owner approval.

Maintain the C# domain/application model, command validation, stable graph-qualified IDs, persistence/history, shared timing and independent Faust/Web Audio/AudioWorklet execution. Do not migrate or optimize DSP under this milestone. Changes to audio-generation execution require the independent [7.3.10 gate](../quality/audio-engine-optimization.md). Graphical capacity is not DSP capacity. Preserve root/nested canvas semantics, current expansion contract, all real ports, design tokens, accessible workflows and the [gesture registry](user-interactions.md). Viewport culling must not hide inactive nodes as a product feature; any visible-detail reduction needs reviewed UX rules and unambiguous ports/hit targets.

## Mandatory source-tree and dependency isolation

The GPU editor is a distinct presentation subsystem, not a collection of modifications scattered through the legacy Razor editor. Reserve **`src/Orbit.GpuRenderer/`** as the single dedicated source root for all new renderer-specific implementation, including experimental backends, browser code, interop adapters, view components, assets and renderer-specific diagnostics. This root is mandatory; the internal layout below is a proposed organization to refine during S3, not permission to create empty scaffolding or adopt unnecessary layers. Prefer a separate project/package under that root where compatible with the existing .NET/browser build; the S3 ADR must establish the exact build, module, asset-publication and project-reference boundaries before production implementation. Keep tests under a correspondingly dedicated test root (for example `tests/Orbit.GpuRenderer.Tests/`); shared browser test runners may invoke renderer-specific suites without mixing their implementations into legacy tests.

```text
src/
  Orbit.Domain/              # unchanged graph invariants and node definitions
  Orbit.Application/         # shared commands, snapshots, services
  Orbit.Presentation/        # genuinely shared renderer-neutral policies
  Orbit.Audio/               # unchanged Faust DSP
  Orbit.Infrastructure/      # unchanged audio and persistence adapters
  Orbit.UI/                  # existing host and legacy HTML/SVG renderer
  Orbit.GpuRenderer/         # NEW: one isolated renderer source root
    Contracts/               # renderer-specific boundary types
    Interop/                 # narrow host bridge and lifecycle
    Browser/
      Core/                  # renderer, camera, scene, scheduling
      Components/            # reusable graphical primitives
      Nodes/                 # node presentation and specialized views
      Connections/           # port and wire views
      Interaction/           # gesture arbitration and hit testing
      Resources/             # textures, geometry, fonts, asset ownership
      Workspace/             # GPU shell, menus, dialogs and piano
      Diagnostics/           # renderer-specific instrumentation
      Assets/                # renderer-owned source assets
      Prototypes/            # isolated comparative experiments
tests/
  Orbit.GpuRenderer.Tests/   # renderer-specific test source
```

**Allowed dependency direction:** the existing UI composition root selects and mounts a renderer through a narrow, reviewed lifecycle/intent/projection boundary. The GPU subsystem consumes authoritative shared application and presentation contracts and communicates with browser APIs through its own adapters; it never depends on legacy Razor components, their DOM measurement logic, CSS, or internal JS helpers. Domain, Application, Audio and Infrastructure must not depend on the GPU subsystem, PixiJS, WebGL or GPU-specific types. The legacy renderer must not import the GPU implementation. Shared renderer-neutral contracts belong to the existing authoritative owner (normally Application or Presentation), not a duplicated model in either renderer. No circular references or service locator coupling. Only the composition root and a deliberately small host/mount adapter may know which backend is active.

**File-placement rules:** never place new GPU implementation files in existing `Orbit.UI/Components`, legacy JS/CSS folders, audio runtime modules or unrelated projects simply because a similar feature already exists there. Do not copy or fork entire existing UI, graph, validation, persistence, timing, design-token, or telemetry implementations into the GPU root. Reuse shared policy via its canonical owner. If a missing renderer-neutral seam requires a change outside the GPU root, isolate the minimum enabling change in a focused, documented slice with its own regression coverage and explain why the existing contract is insufficient. Host registration, build configuration, dependency manifests, test runner integration, routing docs and generated distribution assets are explicit limited exceptions; keep publication paths namespaced (e.g. `gpu-renderer/`), with one source of truth and no hand-edited duplicate bundles. Prototype entry points must be opt-in and must not load or initialize the GPU engine in the normal legacy path.

**Coexistence and migration:** keep the legacy renderer independently usable throughout migration and A/B comparisons; one authoritative patch/session and one active interactive renderer at a time. Switching or context recovery disposes obsolete listeners, subscriptions, GPU resources and frame callbacks, and must preserve graph identity, history, playback and persistence. Do not refactor legacy internals merely to imitate the new renderer. Shared extractions must be justified by actual consumers, not speculative abstractions. Removal of legacy code is a distinct, approved final cleanup after parity and benchmark gates.

**Enforcement:** S3 must record the directory tree, allowed dependency graph, public contracts, host/build integration, and ownership exceptions in an ADR. Before the first production slice, add focused structural checks for forbidden imports/project references, GPU files outside the dedicated source root (with explicit reviewed exceptions), and absence of GPU dependencies in Domain/Application/Audio or the legacy backend. Test independent renderer mounting/disposal and the unchanged legacy path. Apply these checks to later GPU PRs alongside existing pre-CI test-impact review; a directory name alone is not architectural isolation. The owner-approved root is fixed unless a later explicit decision changes it.

## Mandatory architecture and quality gate (before production implementation)

Apply the existing [coding standards](../quality/coding-standards.md), [development policy](../agents/development-instructions.md), [testing contracts](../quality/testing.md) and [work-slice protocol](../agents/work-slices.md). Do not start production migration merely because a prototype is fast. Commit source-linked findings and reviewed architecture decisions before production implementation. First audit actual implementations, consumers, tests, workflows and existing owners (including JS and interop), not just architectural prose. Record current call graphs, update frequency, state ownership, lifecycle, contention and dependency directions. Measure optimized 7.3.9 baseline before comparison. Isolated prototypes must not modify production behavior.

Design deliverables:

- A subsystem inventory and dependency/ownership matrix (inputs, outputs, authoritative state, consumers, event ordering, thread, lifetime, failure/cancellation and tests for each boundary). Include graph commands/undo, persistence, selection, geometry/layout, viewport/navigation, ports/wires, node metadata/parameter editors, composite navigation/macros, presets, annotations, sequencer/automation, telemetry/visualizations, transport/piano, audio bridge, theme/design tokens, accessibility and shell/dialogs.
- Component and deployment diagrams plus sequence diagrams for drag/pinch, wiring, parameter gesture, graph change/undo, nested navigation, telemetry, note actions, context loss and disposal. Explicitly show synchronous vs asynchronous paths, transient preview vs committed state, JS/C# ownership and what never crosses into audio.
- Written architecture decision records comparing concrete alternatives with existing code, trade-offs, testability, coupling, cohesion, extensibility, runtime allocations/interop, memory, failure modes and maintenance burden. Select patterns for demonstrated problems, not by fashion; document rejected patterns and why. Review single responsibility, composition, dependency inversion, interface segregation, one source of truth, readability, error handling and dependency/license/build size. Prefer focused modules and typed/validated contracts; no god renderer, generic event-bus for everything, unnecessary per-class interfaces, duplicate graph/validation/design token sources or premature ECS.
- A reviewed incremental migration/rollback plan, dependency version/license and mobile support audit, pre-change regression and comparative performance acceptance tests. A feature flag/renderer selection at the composition root permits A/B and rollback without separate user graph state. Approve the design and go/no-go before production code; if evidence is missing or unacceptable, stop and record why.

## Candidate system boundaries and patterns (evaluate, do not blindly implement)

| Boundary | Responsibility and candidate pattern |
| --- | --- |
| Domain/Application | Existing validated commands, immutable graph snapshots, domain events and history remain authoritative; Command and explicit query/projection separation. No dependency on renderer or browser APIs. |
| Presentation projection | Renderer-neutral graph-qualified scene description and shared geometry, parameter mapping and token semantics. Immutable/versioned snapshots plus incremental change sets; adapter/mapper only where necessary. Reuse existing owners; define one geometry source for drawing, hit tests and snapping. |
| Renderer backend | Narrow lifecycle/render/resize/update contract; Strategy/Adapter for existing DOM and experimental backends, injected at composition root. Do not abstract unrelated UI or force feature-specific logic into the generic renderer. |
| Scene and components | Retained scene graph and composition of reusable controls; evaluate Composite for visual hierarchy, reusable component/layout primitives and focused controllers. Prefer stable IDs, explicit z-order and ownership. Scene nodes are visual projections, not domain nodes. |
| Input | Explicit interaction state machines for pan, pinch, drag, wire, selection and parameter gestures; centralized capture, priority, focus and cancellation; dispatch validated application commands at existing commit boundaries. Native input/IME/accessibility adapters delegate to the same intent path. |
| C#–JS boundary | Versioned, batched deltas and typed intents; bounded queues/coalescing, stale revision rejection and full snapshot recovery; no per-frame full-graph serialization or per-object interop. Transient pointer previews remain local; preserve exact undo and continuous parameter/audio-action semantics. |
| Telemetry | Demand-scoped subscriptions/Observer only for independent consumers, coalesced latest display snapshots and generation-qualified data; no per-sample UI messages, no stale value presented as live. Close hidden/nested consumers and release taps through existing owners. |
| GPU resources and scheduling | Explicit scoped ownership/disposal of buffers, textures, fonts, caches, listeners and frame callbacks. Spatial indexing, batching/instancing where useful, bounded cache, dirty flags, on-demand scheduling. Evaluate pooling/flyweights only if allocation profiles justify them; test resource recovery. |
| Native/browser adapters | Focus/accessibility, input/IME, clipboard, files, pointer capture and context loss/visibility lifecycle, separate from scene and domain. Progressive fallback, no second business-logic implementation. |

Use existing C# DI and project dependency directions. JS/TypeScript choice and module organization require an ADR; if introducing TypeScript or PixiJS, assess build/deployment integration and compatibility with the existing toolchain and deterministic asset versioning. Avoid React and unnecessary new frameworks. Rendering and input preview may be local to JS; authoritative graph edits, validation and history stay in C#. Determine how shared layout, theme and hit geometry are represented without duplicating policy. Prefer explicit interfaces only at real boundaries and unit-test deterministic state/geometry independently of GPU. Never let rendering await audio samples or drive the musical clock.

## GPU resource and batching strategy

The mandatory companion [GPU rendering efficiency and resource management guide](gpu-renderer-optimization.md) records the Unity lesson, native PixiJS mechanisms, resource ownership, invalidation, render scheduling and diagnostic requirements. S3 must produce a resource ADR and S4c must evaluate the specific mechanisms with controlled paired measurements before S5 approval. Prefer native engine features, shared assets and geometry, and bounded cache policies; do not add a custom batcher or per-node texture cache by default.

## Benchmark, parity and migration gates

Reuse 7.3.9 benchmark infrastructure and before/after policy. Use identical fixtures, saved bytes, viewport, zoom, DPR, fonts, visual detail, interactions, machine/browser/settings, warmup and repeated samples; report median/p95/max and raw data. Benchmark stopped and playing, including pan/zoom, dragging/cable preview, expanded control and live telemetry, nested navigation, fit-all, cold load, memory/lifecycle and idle. Graphical stress fixtures: 50/250/1,000/5,000/10,000 nodes and approximately twice as many cables; separate mostly offscreen and fit-all cases, folded and expanded workloads; do not execute thousands of DSP processors. Add a representative real musical patch with identical audio across candidates. Measure FPS/frame gaps, main-thread work/long tasks, render/update latency, CPU/memory, GPU where supported, allocations, interop and audio reliability (including underrun evidence with its limitations). Test reference desktop and physical Android; distinguish shared CI diagnostics from device acceptance. Agree numerical gain thresholds and resource budgets before evaluating prototypes; existing cadence-relative targets remain the starting point. No claim of superiority or capacity without evidence.

Test parity with existing deterministic unit/contract and browser suites: transforms/port anchors/cable hit geometry, gestures/cancellation/keyboard/focus/IME, command count/validation, undo/redo, compatible saved patches, nested graphs, presets/macros, live parameter semantics, transport/piano and sequencing, visualizations and demand lifecycle, DPR/resize, themes, screen readers, context loss/recovery, repeated switching/disposal and unchanged audio/revisions when editing presentation. Preserve pre-CI test-impact review and fix obsolete DOM-selector assumptions without weakening acceptance intent. Test one representative successful case plus failure and lifecycle invariants per slice; retain full runtime CI and focused before/after reports.

Proceed only when architecture/design review, pre-change tests, measured comparison and owner decision pass. Migrate in small independently verifiable slices with a fallback to the existing renderer. Maintain one behavioral contract and shared state during coexistence; retire replaced HTML/SVG, CSS, JS adapters and stale tests only after verified parity and final device comparison. If full GPU rendering fails the gate, record no-go or narrower evidence-backed alternative rather than forcing migration.
