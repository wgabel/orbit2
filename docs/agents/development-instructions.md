# Development and implementation instructions

[Global contributor rules](contributor-instructions.md) · [Task routing](task-routing.json)

Apply the relevant sections for implementation, code review, UX/input changes, roadmap execution and user-facing feature changes. These are mandatory when their conditions apply; do not load unrelated feature contracts.

## Working with code and features

- **For any implementation, fix or refactoring task**, apply the [coding standards and implementation quality guide](../quality/coding-standards.md). Inspect existing owners and tests before adding code; avoid duplicated policy and unnecessary abstractions, preserve contracts and lifecycle invariants, and validate relevant behavior. Scale effort to the change rather than imposing heavy process on trivial edits.
- **For any user-input or UX interaction change** (click/tap, double tap, long-press, drag, hover/focus, mouse wheel, keyboard, touch/pen, context menus or shortcuts), **read and maintain the [user interaction and gesture registry](../editor/user-interactions.md)** before assigning or modifying gestures. Check existing owners, gesture precedence, collision surfaces and tests; update the registry in the same PR as the behavior change. Avoid claiming documented/planned actions are implemented without code/test verification. Do not reserve a gesture on a connector that already participates in cable creation.
- Start with [architecture overview](../architecture/overview.md), the short overview and task routing index. Read relevant feature modules, source code, tests and any other material that helps solve the task. The architecture routing table is advisory beyond the mandatory minimum in the agent routing configuration, not an allowlist or a limit on context; cross-cutting work can require several modules. Do not load every specification by default. Adjust the depth of investigation to the complexity and risk of the task.
- Use [the canonical roadmap](../roadmap/overview.md) and its linked milestone registers when planning or changing implementation status. Use [local setup](../getting-started/installation.md) and [development/testing](../getting-started/development.md) for practical commands. The implementation and tests are the evidence for current behavior; reconcile stale documentation instead of assuming that every design proposal is implemented.
- For using implemented features in patches, or explaining them to a user, start with the [usage guide](../usage/README.md) and follow only the relevant walkthrough and technical contracts. When adding or changing a user-facing feature, update its usage example (actual UI actions, port/parameter settings, limitations and PR provenance) as appropriate. Reconcile status after merges; do not require new pages for changes already adequately covered.

## Implementation quality check (before handoff)

For code changes, confirm proportionately that:

1. Existing implementations, shared abstractions and relevant tests were checked; the change does not introduce a second source of truth.
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
