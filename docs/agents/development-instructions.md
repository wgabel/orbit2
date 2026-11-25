# Development and implementation instructions

[Global contributor rules](contributor-instructions.md) · [Task routing](task-routing.json)

Apply the relevant sections for implementation, code review, UX/input changes, roadmap execution and user-facing feature changes. These are mandatory when their conditions apply; do not load unrelated feature contracts.

## Working with code and features

- **Audio-generation performance changes:** the owner requires the [7.3.10 evidence-first optimization gate](../quality/audio-engine-optimization.md). Before changing a production DSP, scheduling, compilation or playback/replacement algorithm for performance, commit strict code analysis, a measured baseline, reviewed technical design and passing pre-change regression tests for the individual candidate. No speculative production changes; preserve sound and timing, report missing evidence, stop at the gate when blocked. This applies regardless of which agent or roadmap phase performs the optimization.

- **For any implementation, fix or refactoring task**, apply the [coding standards and implementation quality guide](../quality/coding-standards.md). Inspect existing owners and tests before adding code; avoid duplicated policy and unnecessary abstractions, preserve contracts and lifecycle invariants, and validate relevant behavior. Scale effort to the change rather than imposing heavy process on trivial edits.
- **For any user-input or UX interaction change** (click/tap, double tap, long-press, drag, hover/focus, mouse wheel, keyboard, touch/pen, context menus or shortcuts), **read and maintain the [user interaction and gesture registry](../editor/user-interactions.md)** before assigning or modifying gestures. Check existing owners, gesture precedence, collision surfaces and tests; update the registry in the same PR as the behavior change. Avoid claiming documented/planned actions are implemented without code/test verification. Do not reserve a gesture on a connector that already participates in cable creation.
- Start with [architecture overview](../architecture/overview.md), the short overview and task routing index. Read relevant feature modules, source code, tests and any other material that helps solve the task. The architecture routing table is advisory beyond the mandatory minimum in the agent routing configuration, not an allowlist or a limit on context; cross-cutting work can require several modules. Do not load every specification by default. Adjust the depth of investigation to the complexity and risk of the task.
- Use [the canonical roadmap](../roadmap/overview.md) and its linked milestone registers when planning or changing implementation status. Use [local setup](../getting-started/installation.md) and [development/testing](../getting-started/development.md) for practical commands. The implementation and tests are the evidence for current behavior; reconcile stale documentation instead of assuming that every design proposal is implemented.
- For using implemented features in patches, or explaining them to a user, start with the [usage guide](../usage/README.md) and follow only the relevant walkthrough and technical contracts. When adding or changing a user-facing feature, update its usage example (actual UI actions, port/parameter settings, limitations and PR provenance) as appropriate. Reconcile status after merges; do not require new pages for changes already adequately covered.

## Pre-CI test-impact review (mandatory for executable changes)

Before the first implementation push/PR and before each corrective push, review the relevant existing tests and workflow steps against the intended final behavior. Do not use a full CI run as the first discovery mechanism for obvious incompatibilities.

1. Trace the changed contracts and removed/renamed symbols, components, selectors, events, data fields and assumptions into their consumers. Search the repository for old and new references, including `tests/`, `scripts/` (especially benchmark and smoke helpers), and relevant `.github/workflows/` entry points. If code search is unavailable or incomplete, enumerate the relevant workflow commands and inspect their invoked files and helpers directly.
2. Inspect affected unit/contract, integration, published-browser and benchmark scenarios proportionately. Check selector uniqueness and lifetime, event/commit semantics, model-backed assertions, geometry/overlap, desktop/touch and small/large fixture assumptions, playback/stopped paths, cleanup and undo/redo where applicable. Check the whole affected scenario, not only the first failing line.
3. Fix obvious stale assumptions and update/add focused regression coverage in the same change. Preserve the acceptance intent: never skip a workload, weaken an assertion, or change production behavior merely to make a test green. If the workload needs a different interaction method, make it explicit and document measurement limitations.
4. Run the cheapest available relevant checks before pushing (syntax/static checks, focused unit tests, targeted integration/browser cases where the environment supports them). Review the final diff and search for obsolete references again. If local execution is unavailable, perform the static review anyway and state what remains CI-only.
5. Record a compact test-impact summary in the PR: affected suites/helpers inspected, key assumptions corrected, checks actually executed, and remaining CI-only verification. For CI failures, inspect the failing step and adjacent dependent checks before another push.

This review is not a substitute for required CI or the audio optimization gate. Keep it targeted to affected contracts and workflow consumers, not a mandatory full-repository audit. Documentation-only changes retain their existing waiver.

## Before/after optimization evidence (mandatory for Phase 7.3.9)

For each editor optimization PR, record the exact pre-change runtime revision and capture the focused workload baseline before changing production code; rerun with the identical saved fixture bytes, benchmark harness, machine, browser and settings on the candidate revision. Preserve both raw reports, compare repeated samples and document measured gains, regressions, unchanged/unavailable metrics and any confounders in the PR and [performance history](../quality/performance.md#large-patch-editor-responsiveness-739). If baseline measurement is blocked, record the reason and do not claim a measured gain; stop before further production optimization unless the owner explicitly authorizes proceeding. Existing deterministic regression assertions and full runtime CI remain mandatory. After merge, reconcile the actual main revision and successful checks; rerun when runtime inputs differ from the verified candidate. Use focused comparisons per PR, full suite for runtime changes and controlled physical desktop/Android runs at major milestones and final verification. Shared CI runner timings are diagnostic, not absolute FPS gates. Benchmark-only infrastructure slices may establish the missing baseline without changing production behavior.

## Implementation quality check (before handoff)

For code changes, confirm proportionately that:

1. The pre-CI test-impact review was completed; existing implementations, shared abstractions and relevant tests were checked without introducing a second source of truth.
2. Responsibility, naming and complexity fit the existing architecture; stable IDs, error paths, resource cleanup and compatibility were considered.
3. Important behavior and relevant edge cases have regression coverage; the final diff was reviewed for unrelated changes.
4. Relevant builds/tests or browser/DSP checks were actually run, and failures or unverified claims are reported explicitly.
5. Technical and usage documentation was updated if behavior or contracts changed. Documentation-only corrections follow the owner-authorized verification exception in the work-slice protocol.

See [coding standards](../quality/coding-standards.md) for the detailed guidance; do not replicate it in task-specific documents.

## Implementation traceability and verification

- **Close out every verified implementation work slice in its roadmap register before handoff:** once the required CI checks are green, update the slice status to `Implemented (CI verified; awaiting merge)` (or the register's equivalent), link the successful CI run and PR, and identify the next dependent slice. Make this status/documentation update in the **same PR**, not a separate documentation PR. Do not wait for a rerun solely because the closeout commit changes documentation; retain the exact verified implementation revision and report any known failures; do not create an endless cycle of CI-link-only updates. Do not call an open PR merged; after the actual merge, reconcile its status to `Merged` when next touching the register. See the [work-slice closeout protocol](work-slices.md#finish-the-slice-enable-a-clean-restart).
- For every implementation PR (feature, improvement or fix), record its actual linked PR number in the relevant implementation description and milestone register: `[PR #N](https://github.com/wgabel/orbit2/pull/N)`. Update the roadmap summary/index where applicable. For a small fix with no milestone, a concise record in the relevant feature module and appropriate register is sufficient. Keep earlier PR evidence and add follow-ups.
- If the number is unknown, open the PR and then add the links to the same branch before merge. Distinguish planned, partial, in-review and merged work; never cite a documentation-only PR as implementation evidence. For older direct-to-main changes, cite the actual commit rather than inventing a PR.
- Add regression coverage for changed behavior and report what was actually verified. Scale testing and documentation to the change; a typo or a purely editorial correction need not create a new feature specification.
