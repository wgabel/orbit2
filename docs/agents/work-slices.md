# Granular agent work slices and restartable handoffs

[Roadmap](../roadmap/overview.md) · [Agent instructions](contributor-instructions.md) · [Coding standards](../quality/coding-standards.md)

## Default execution rule

For a nontrivial feature, redesign, refactor or multi-part fix, **plan the whole requested outcome but implement only one small roadmap slice per agent run** (ChatGPT, Work, Codex or delegated agent) by default. A phase or numbered milestone in the roadmap is a **container**, not automatically a small enough unit of work. Do not silently proceed to the next slice after finishing the current one. A fresh "continue" request can start the next ready slice in a new context.

This rule is for **shorter, recoverable sessions and lower repeated-context/token cost**, not a reason to cut corners on correctness, tests or documentation. An explicitly requested broader delivery may use multiple separately verifiable slices, but pause at a safe boundary instead of extending an unstable session.

## Documentation-only continuation

For changes limited to documentation content and agent instructions (including non-executable routing metadata), the repository owner has waived separate verification and CI waiting. Commit one requested slice, record its actual status and PR in the owning register, report completion and stop until the user instructs continuation. Consecutive documentation slices may use the same open branch/PR when requested; the prior slice need only be committed on that branch, not merged. Do not start another slice automatically.

This exception does not cover executable scripts, CI workflows, tests, application code, or changes that affect runtime behavior. Keep relevant validation requirements for those changes. Do not mark untested changes CI verified or merged. Existing repository-required merge checks are unchanged; this rule changes agent handoff timing, not branch protection.

## How small is a slice?

- **One independently testable result:** one user-observable behavior, bug, or narrowly necessary enabling change; it should be independently reviewable and leave the repository buildable/mergeable. Do not make "create scaffolding", "write tests" and "fix tests" separate PRs when none is useful or safe on its own.
- **Few acceptance checks:** aim for 1–3 specific pass/fail outcomes and the smallest relevant regression test/build. A useful heuristic is a single narrow implementation area and roughly 2–4 related production files; this is **not** a cap. Cross-layer vertical changes legitimately need more files and tests.
- **Split when scope expands:** if a task contains multiple distinct behaviors, needs unrelated subsystems, substantial refactoring plus a feature, or requires large-context investigation, split by behavior or contract boundary before continuing. Keep required compatibility and safety work with the slice it protects.
- **No artificial bureaucracy:** a typo, tiny self-contained fix or documentation correction need not be pre-split or receive a new roadmap identifier. Maintain the existing PR/implementation traceability rules.

## Concurrent work and slice ownership

**Mandatory before starting or resuming any repository-changing task:** reconcile the current target branch, owning roadmap, existing pull requests and remote work branches. Multiple chats, Codex sessions, delegated agents and human contributors can work in the repository concurrently. A planned entry on main is not proof that the work is unclaimed.

1. Fetch the latest target revision, review the owning milestone and inspect all open PRs (including drafts) plus recent merged/closed PRs. Search by stable slice ID, milestone, related scope and touched files; do not rely solely on PR title or the previous conversation. Inspect relevant remote branches, including branches without PRs; account for pagination. Distinguish stale historical branches from active work using their PR status, ancestry and commits. Reconcile actual CI evidence and merged status before selecting a slice.
2. If a matching active PR or branch exists, treat it as the existing work item, not permission to create a competing implementation. Inspect its scope, owner and state; continue on it only when the owner has requested continuation and coordination permits it. Otherwise report the existing PR/branch and stop affected work or select an independent explicitly authorized slice. Do not silently take over, close, merge or delete another contributor's work. If the slice is already merged, reconcile the roadmap with verified evidence and select the next ready dependency.
3. Before editing, reserve the selected slice with a uniquely determined remote branch name containing its stable slice ID. For new work, use the canonical convention `work/<normalized-slice-id>` (lowercase, replace dots with hyphens, retain sub-slice suffixes); for existing work reuse the existing branch. Atomic remote branch creation is the claim: if it already exists, refresh and inspect its work rather than append a random suffix or create another branch for the same slice. A branch without a PR may be another agent's active claim; do not assume abandonment. For tasks without a roadmap ID, use a stable descriptive work identifier and the same duplicate-scope check.
4. Recheck open PRs, relevant remote refs and latest target revision immediately before opening a PR and before resuming after a handoff, failure or changed revision. If another implementation appeared, stop and reconcile with the coordinator/owner; retain any unique findings without silently merging conflicting designs. Use expected-head checks when updating a shared branch. Record the selected slice, checked revision, matching PR/branch (or none found), claim branch and any overlap/blocker in the handoff/PR evidence. No access to remote state means the concurrency gate is blocked, not passed.
5. Agents coordinating parallel work must assign non-overlapping scopes and shared-contract dependencies explicitly. Only one active implementation of a slice may proceed by default; an alternative experiment requires explicit owner authorization and must be clearly labeled noncanonical.

This is a required workflow check, not a distributed lock across legacy branch names or independent repositories. The canonical claim branch reduces races between compliant agents; it does not prevent humans or noncompliant tools from changing main. Existing PRs and historical branches keep their names. Never rewrite a merged implementation merely to match the new naming convention.

## Before implementing

1. Read `docs/agents/contributor-instructions.md`, the architecture routing index and **only the relevant** feature/quality contracts and code. Locate the existing milestone in the appropriate `docs/roadmap/*.md` register, or add an entry near the related work.
2. Decompose any oversized milestone into ordered **`<existing-milestone>-S1`, `-S2`, ...** slices. Preserve existing milestone IDs. Record only the next few actionable slices; elaborate later work when it becomes ready.
3. Each slice needs **status, dependency (if any), one-sentence scope, acceptance check(s), affected subsystems, and verification target**. For multi-slice features identify explicit full-verification checkpoints at shared/runtime-to-editor integration, user-facing integration and final end-to-end verification (combine coincident checkpoints). Follow the [CI impact and checkpoint contract](../getting-started/deployment.md#s5-subsystem-aware-test-selection-and-roadmap-checkpoints): label runtime checkpoint PRs `full-verification` as soon as the PR exists, before relying on its CI, and verify actual full-suite evidence; earlier slices may omit editor benchmarks only when the automatic audited path classifier permits it. A roadmap declaration cannot weaken CI. Add the actual PR link once opened. Keep the detailed technical contract in its feature document, not copied into the roadmap.
4. Select **one ready slice**; complete the [concurrent work and slice ownership check](#concurrent-work-and-slice-ownership), give a brief plan, then implement and apply the relevant verification policy, including the documentation-only exception. If the requested feature already fits, it is one slice and needs no contrived decomposition.

Example register format (illustrative, not an actual scheduled milestone):

```md
- **X.Y-S1 — One narrow observable outcome (Planned)** — Depends: none;
  Done: expected behavior A and regression B; Verify: focused test command.
- **X.Y-S2 — Next independently testable outcome (Planned)** — Depends: X.Y-S1;
  Done: expected behavior C; Verify: focused test command.
```

Use the repository's normal vocabulary: `Planned`, `In progress`, `In review`, `Partial`, `Implemented`, `Deferred` as appropriate; do not mark unmerged work as merged. Reference `[PR #N](https://github.com/wgabel/orbit2/pull/N)` only when the real PR exists. A documentation-only PR does not prove implementation.

## During the slice: limit context growth

- Inspect existing owners and tests before changing code; use targeted file/section reads rather than loading whole documentation trees, full history, huge logs or unrelated feature specs.
- Do not combine opportunistic cleanup, extra UX changes or the next roadmap slice in the same PR by default; consecutive documentation-only slices explicitly requested by the owner may share the current PR. If a new dependency is discovered, note it in the roadmap and finish or stop at a safe, buildable checkpoint.
- Test the focused behavior and relevant regression surfaces; do **not** omit needed broader validation for DSP, persistence, gesture collisions or shared contracts merely to save tokens.
- If context/time becomes constrained, record verified progress and remaining work in the roadmap or PR, label partial/unverified claims accurately, and stop. Never report a failing or untested implementation as verified; report documentation-only completion without verification under the exception above.

## Finish the slice; enable a clean restart

**Required green-CI closeout for implementation changes:** When all required implementation CI checks pass, update the authoritative `docs/roadmap/*.md` slice entry **in the same implementation PR before reporting the slice done**. Set `Implemented (CI verified; awaiting merge)` (or equivalent explicit wording), link the actual PR and successful run, state the verified acceptance outcomes, and name the next dependent slice. A passing build alone is not enough if required browser, interaction, or performance checks remain incomplete. If checks fail, leave `In progress`/`In review` and fix them instead. Once merged, the next roadmap edit may mark the entry `Merged` with the verified PR; never mark an open PR merged. A documentation-only closeout commit may trigger CI again; do not wait for that rerun solely for documentation. Preserve the exact verified implementation revision and report any known failure. Do not repeatedly edit the register only to replace one passing CI run URL with another.

Complete the existing `docs/agents/contributor-instructions.md` quality and documentation checks. Keep the handoff compact (normally **at most ~8 short lines**), containing:

1. Slice ID, current status and PR link/commit (if any).
2. Observable result and important changed files/contracts.
3. Tests actually run, failures and what could not be verified.
4. One blocker/decision only if needed, and the **next ready slice ID** with its dependency.

For every task divided into steps or slices, include the [mandatory visual progress summary](compliance-reports.md#mandatory-visual-progress-summary-for-multi-step-tasks) at each user-facing handoff. Show the full known feature plan, not just the slice being delivered; reconcile actual verification and merge status and preserve IDs. This tracker is additional to the compact prose handoff, even when the current run is blocked or only one slice is implemented.

Persist durable decisions in the closest authoritative feature document and work status in the milestone register; **do not paste chat transcripts, full diffs or detailed session notes into the roadmap**. The next agent should be able to continue from the roadmap, PR, source and tests without replaying the old conversation. Keep the [standard compliance report](compliance-reports.md), without repeating the full context. Update the cumulative PR evidence block after each slice on a shared branch.
