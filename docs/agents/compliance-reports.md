# Agent compliance reports

[Global instructions](contributor-instructions.md) · [Task routes](task-routing.json) · [Work slices](work-slices.md)

## Initialization (before substantive work)

Use three compact lines, with actual repository-relative paths, not merely document titles. The declaration is not permission to skip a missing prerequisite.

- **Routes / revision:** selected route IDs; target branch and exact revision read.
- **Documentation consulted:** paths actually read; append section names only when useful.
- **Constraints / acceptance:** the important applicable rules and intended outcome; identify missing required docs and any explicitly owner-authorized exception.

If scope changes, report only new routes, newly consulted paths and changed constraints before affected work. Do not repeat unchanged context.

## Completion (every task)

Reuse the initialization declaration; add only new paths and routes unless handing off to a fresh context. Include result/status, PR or commit if any, actual verification and limitations, documentation discoverability (or "No issues found"), and next slice/blocker when applicable. A delegated agent reports to its coordinator; the coordinator consolidates actual evidence and never infers a read from another agent's silence. Each delegate must still initialize independently. No PR report is required for read-only tasks without a PR.

## Pull request evidence

For every PR, maintain one machine-readable block in the PR description. CI cannot reliably distinguish human-authored from agent-authored changes, so the structural check applies uniformly to both. Keep normal scope and test summaries outside it. Use the exact marker and JSON fence shown in the [PR template](../../.github/pull_request_template.md). Replace placeholders, do not duplicate blocks, do not use comments or trailing commas inside JSON.

The schema is version 1:

- `schemaVersion`: integer 1.
- `routes`: nonempty array of unique route IDs; include the union across the entire PR, including routes added by later slices or delegates.
- `consulted`: unique repository-relative documentation/configuration paths actually read, unioned across the PR. Include global prerequisites and applicable route requirements; do not include section anchors in this array. State any unresolved or retrospective reads in `limitations`.
- `revision`: full Git commit SHA of the policy/tree used for the latest initialization or reconciliation. This is not a claim that all reads happened at that revision or that it is the tested head.
- `constraints`: nonempty array of brief concrete applicable obligations.
- `verification`: object with `status` (one of `passed`, `failed`, `partial`, `not-run`, `documentation-only-waived`), `checks` (array of actual commands or CI evidence URLs; empty when none) and `testedRevision` (full SHA or null). A passed status requires actual checks and tested SHA. Do not use the documentation-only waiver for a PR containing executable changes.
- `limitations`: array of remaining blockers, missing evidence, retrospective reads or unavailable verification; empty only when none.
- `discoverability`: short factual summary of navigation issues or "No issues found."
- `exceptions`: array of objects with `path`, `reason`, and `authorization` (specific owner approval reference). Empty unless explicitly authorized; self-declared exceptions do not bypass automated checks. Never list an unread document as consulted.

An updated PR must describe cumulative scope, not only the last slice. If onboarding a pre-existing PR, consult any missing required documents now, explicitly label retrospective reads, and never imply earlier agents read them before working. The report is a declaration, not independent proof of reading or comprehension. Automated validation checks the declared evidence; merge requirements and existing testing rules still apply. A report with incomplete verification and truthful limitations can pass structural checks, because requiring successful CI in the report before CI runs would create a circular dependency.

## Automated enforcement

The [Agent documentation compliance workflow](../../.github/workflows/agent-compliance.yml) runs on main pushes and on PR opening, synchronization, reopening, description edits and ready-for-review events. It runs Node's built-in test runner and validates the routing configuration against repository files. On PR events it also parses the single JSON report, validates its fields, requires the union of global and selected-route documentation, and adds conservative route requirements from the PR's changed paths. Deleted paths count; renames are treated as delete/add. The documentation-only waiver is rejected for non-documentation paths. An exception declaration never bypasses a missing required consultation.

Local commands: `node --test tests/tooling/AgentCompliance.test.mjs` and `node scripts/check-agent-compliance.mjs`. The latter checks only routing outside a GitHub pull-request event; PR-body checks use the event payload and Git history in CI.

Repository owners can require the status check named **Agent documentation compliance** in the main branch's GitHub ruleset or branch protection. Until configured as required, a failed check does not necessarily prevent a merge. Keep existing build/test checks required as appropriate. Protect review of changes to the workflow, checker and route policy themselves; a PR can modify its own checker, and this is not an adversarial attestation system.

This check verifies structural consistency, not actual reading, understanding, semantic completeness, claimed test execution or owner authorization. It cannot infer every task category from paths; reviewers and agents must still apply the semantic routes. Read-only work without a PR remains governed by initialization and handoff policy.

## Compact examples

These are illustrative task summaries, not claims of completed reads. Every real declaration includes the actual global prerequisites and required route documents.

| Task | Example routes | Example constraints / handoff |
|---|---|---|
| Node fold and click behavior | implementation, roadmap, editor-ui, node-layout, interaction-change, usage, documentation if editing docs | Preserve ports, avoid gesture collisions, one slice; report actual UI tests and CI. |
| Documentation reorganization | documentation, roadmap when tracked | Preserve canonical owners and links; owner-waived documentation-only verification; next slice. |
| Pitched musical piece | music, pitched-composition, music-theory, synth-voice, instrument-selection, composition-development and mix-analysis as applicable | Verify current node behavior and distinguish listening from measurements; report unavailable audition honestly. |
| Delegated DSP fix | implementation, dsp, performance if latency/glitches are involved | Delegate reads relevant rules independently; coordinator records actual consulted paths and regression evidence. |
