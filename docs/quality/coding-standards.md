# Coding standards and implementation quality

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Agent instructions](../../AGENTS.md) · [Testing](testing.md) · [Project structure](../core/project-structure.md)

## Purpose and scope

These are the default engineering expectations for **new code, fixes and refactoring** in Orbit 2, including work by coding agents. The goal is reliable, understandable, reusable code that fits the current system—not maximum abstraction, maximum test count or a mandatory process for trivial edits.

Use this document with the relevant feature contract and source/tests. The repository's implemented behavior is the evidence for what exists; design proposals and roadmap entries are not proof of implementation. If a requirement conflicts with an established feature-specific contract, investigate and explicitly resolve the conflict rather than silently overriding it. Architectural improvements are allowed when justified and within the task's scope.

For small, low-risk changes, apply the principles proportionately. For DSP, persistence, concurrency, user interactions or broad cross-layer work, investigate and verify more deeply.

## Before writing code: understand and reuse

1. Read the relevant sections of [the architecture index](../../ORBIT2-ARCHITECTURE.md), then inspect the actual call sites, implementations and tests for the affected behavior. Follow related contracts where the change crosses layers.
2. Search for an existing owner of the behavior: services, validators, converters, coordinate transforms, parameter mappings, UI controls, DSP utilities, state machines and lifecycle helpers. **Extend or reuse the authoritative implementation** when it can serve the new case coherently.
3. Establish the invariants and observable behavior that must remain unchanged: port and parameter identities, graph revisions, command/undo boundaries, persistence, audio timing, rendering, accessibility and public interfaces as applicable.
4. Identify affected normal, invalid and boundary cases before deciding where the change belongs. For nontrivial changes, state the smallest design that meets the requirement and any relevant trade-offs.
5. When changing gestures or other user input, consult and update [the user interaction registry](../editor/user-interactions.md) as required by [AGENTS.md](../../AGENTS.md).

**Avoid duplicate sources of truth.** In particular, do not independently implement the same conversion, validation rule, lifecycle policy, node geometry or audio-smoothing policy in multiple layers merely because it is convenient. Move truly shared policy to the correct existing owner; callers should consume that contract. Conversely, do not extract a generic helper or introduce a new interface merely because two small snippets look alike: the behavior must actually be the same and likely to evolve together.

## Design and code quality

- **Single responsibility and clear ownership:** keep functions and classes focused. Separate business/domain rules, application orchestration, presentation state, UI rendering and runtime/browser integration. Avoid god components and services that accumulate unrelated behavior.
- **Readable code:** use intention-revealing names, consistent local conventions, small cohesive functions and explicit units (Hz, cents, seconds, milliseconds, samples) where ambiguity is possible. Prefer understandable control flow over clever compact code; explain non-obvious *why*, not every *what*.
- **Minimal necessary complexity:** choose the simplest correct extension. Do not add frameworks, configuration knobs, compatibility layers, parallel state caches or speculative abstractions without an actual need. Avoid unrelated cleanups in a feature PR; isolate necessary refactoring and preserve behavior with tests.
- **Correctness at boundaries:** validate untrusted files, graph edits, external/JS inputs and user values at the authoritative boundary. Handle null/missing, invalid, extreme and stale data intentionally. Keep errors actionable; do not swallow exceptions or turn unsupported cases into silent success.
- **Stable contracts:** preserve identifiers, connection semantics, serialization and existing patches unless migration is explicitly part of the task. Version or migrate changed persisted formats; load failures should not corrupt the active workspace.
- **State and lifetime:** define who creates, owns and disposes subscriptions, audio resources, timers, observers and interop objects. Avoid races from overlapping async operations, superseded graph/audio generations and stale callbacks. Keep cancellation, error handling and cleanup coherent.
- **Dependencies:** prefer existing libraries and APIs. Any new dependency needs a concrete reason, compatible licensing, a defined version and an understanding of bundle/runtime costs.
- **Consistency:** follow the surrounding C#, Razor, JavaScript, CSS and Faust conventions. Do not create a parallel style/utility framework when the repository already has shared components or design tokens.

### Keep architectural boundaries intact

| Layer | Owns | Do not put here |
| --- | --- | --- |
| `Orbit.Domain` | Graph invariants, nodes, typed ports, parameters and validation | Blazor, browser APIs, Faust runtime or UI state |
| `Orbit.Application` | Commands, sessions, immutable snapshots, services and interfaces | Browser-specific integration or direct view mutations |
| `Orbit.Presentation` | Testable canvas transforms, editor projections and selection | DSP execution or browser audio state |
| `Orbit.Audio` | Graph-to-Faust compilation and DSP code | UI interaction state or patch-edit policy |
| `Orbit.Infrastructure` | Browser audio/persistence adapters, interop and lifecycles | Independent graph business rules |
| `Orbit.UI` | Razor components, interaction wiring, styles and DI composition | A second graph model, duplicated validation or DSP policy |

Use [project structure](../core/project-structure.md) and the relevant feature modules for more detail. UI input should go through application commands rather than mutating the signal graph or the DSP directly. Prefer testable dependencies and narrow interfaces at real boundaries; avoid an interface for every class.

### Specific areas that require care

- **Audio and Faust:** sample-rate correctness, finite/bounded signal values, predictable trigger/gate/phase semantics, click-safe changes and DSP state continuity matter more than superficial code simplicity. Never move per-sample work into C# UI/JavaScript; avoid per-sample allocations, logging or host interop. Test at 44.1 and 48 kHz where the contract requires it.
- **Graph/editor:** keep one transform/geometry source for drawing, hit testing and dragging. Protect nested-graph and rack boundaries, stable IDs, atomic commands, undo/redo and persistence. Avoid recompilation for presentation-only changes.
- **Browser/Blazor:** keep JavaScript focused on browser integration. Reuse shared components/design tokens; preserve accessible hit targets, keyboard/pointer/touch behavior and small-screen interactions. Follow versioned asset paths and avoid stale-module coupling.
- **Performance:** consider cost and allocations on hot paths, audio lifecycle, graph compilation, event dispatch and rendering. Do not claim an optimization based on intuition alone; measure when performance motivates the change. Observability must not create new audible or editor regressions.

## Verification: prove the behavior, not just that code compiles

For each behavior change, add or adapt focused regression tests at the lowest appropriate layer. Cover a representative successful case, relevant failure/boundary cases and the invariant most at risk. Extend existing test suites before creating new harnesses. Prefer deterministic domain, compiler and offline DSP tests; use browser tests when actual interop, pointer/gesture behavior, AudioWorklet timing or deployment is essential.

Before handing off an implementation:

1. Review the diff for accidental changes, dead code, new duplicated policy, unnecessary abstractions, missing cleanup and compatibility breaks.
2. Run the relevant existing tests and builds. The [GitHub Actions workflow](../../.github/workflows/build-and-deploy.yml) currently runs .NET domain/presentation tests, JavaScript UI/audio/compiler tests, Faust builds/probes, browser smoke tests and publication checks; choose local checks appropriate to the touched subsystem and review PR CI results when available.
3. For audio changes, verify observable DSP behavior (not only compilation), including timing and relevant sample rates. For user interactions, test the actual input modes and zoom/viewport edge cases affected. For persistence, verify round-trip and rejection behavior.
4. State which checks passed, failed or could not run. Do **not** claim live listening, browser/device verification or performance improvement without corresponding evidence.
5. Update the closest authoritative documentation and usage guidance when contracts change. Keep implementation status and PR evidence accurate according to [AGENTS.md](../../AGENTS.md).

**Current automation versus aspiration:** build/tests and published smoke checks exist in CI. The workflow does not currently establish a repository-wide formatter, C# analyzer, JavaScript linter, duplicate-code threshold or blocking performance budget. Do not represent these as enforced checks; adding them would require separate tooling/configuration work.

## Lightweight implementation review checklist

Use these questions during implementation and PR review; avoid copying an elaborate form into every task.

- [ ] Did I inspect existing code/tests and reuse the right owner rather than duplicating logic?
- [ ] Is the implementation scoped, readable, cohesive and consistent with the dependency boundaries?
- [ ] Are stable IDs, graph/runtime invariants, error paths, resource cleanup and compatibility preserved where relevant?
- [ ] Are the important behavior and edge cases covered by appropriate tests?
- [ ] Did I run and report actual checks, and review the final diff for unexpected changes?
- [ ] Did I update applicable usage/technical docs and accurately report limitations and PR status?

For a documentation-only correction, relevant link/consistency review is normally enough; no new runtime test is required solely to satisfy the checklist.
