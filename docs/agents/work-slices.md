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

## Before implementing

1. Read `docs/agents/contributor-instructions.md`, the architecture routing index and **only the relevant** feature/quality contracts and code. Locate the existing milestone in the appropriate `docs/roadmap/*.md` register, or add an entry near the related work.
2. Decompose any oversized milestone into ordered **`<existing-milestone>-S1`, `-S2`, ...** slices. Preserve existing milestone IDs. Record only the next few actionable slices; elaborate later work when it becomes ready.
3. Each slice needs **status, dependency (if any), one-sentence scope, acceptance check(s), and verification target**. Add the actual PR link once opened. Keep the detailed technical contract in its feature document, not copied into the roadmap.
4. Select **one ready slice**; give a brief plan, then implement and apply the relevant verification policy, including the documentation-only exception. If the requested feature already fits, it is one slice and needs no contrived decomposition.

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

Persist durable decisions in the closest authoritative feature document and work status in the milestone register; **do not paste chat transcripts, full diffs or detailed session notes into the roadmap**. The next agent should be able to continue from the roadmap, PR, source and tests without replaying the old conversation. Keep the existing short documentation-consulted/discoverability report, without repeating the full context.
