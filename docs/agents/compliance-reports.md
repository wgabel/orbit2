# Agent compliance reports

[Global instructions](contributor-instructions.md) · [Task routes](task-routing.json) · [Work slices](work-slices.md)

## Initialization (before substantive work)

Use three compact lines, with actual repository-relative paths, not merely document titles. The declaration is not permission to skip a missing prerequisite.

- **Routes / revision:** selected route IDs; target branch and exact revision read.
- **Documentation consulted:** paths actually read; append section names only when useful.
- **Constraints / acceptance:** the important applicable rules and intended outcome; identify missing required docs and any explicitly owner-authorized exception.

For repository-changing tasks, include the [concurrent work and slice ownership](work-slices.md#concurrent-work-and-slice-ownership) check in the declaration and handoff: target revision, selected slice/work identifier, matching PRs and relevant branches or none found, claim branch and overlap resolution. Record the same evidence in the existing PR compliance report constraints/checks/limitations as appropriate; no new machine-readable fields are required.

If scope changes, report only new routes, newly consulted paths and changed constraints before affected work. Do not repeat unchanged context.

## Completion (every task)

Reuse the initialization declaration; add only new paths and routes unless handing off to a fresh context. Include result/status, PR or commit if any, actual verification and limitations, documentation discoverability (or "No issues found"), and next slice/blocker when applicable. A delegated agent reports to its coordinator; the coordinator consolidates actual evidence and never infers a read from another agent's silence. Each delegate must still initialize independently. No machine-readable PR report is required for read-only tasks without a PR; the user-facing report is still mandatory for **every Orbit 2 response**, including short conversational follow-ups.

### Compact response evidence (including no-PR answers)

Append a concise evidence footer to each project response. Include the selected route IDs, checked branch/revision (short SHA is sufficient for conversational display), actual repository-relative documentation paths consulted, and discovery status or limitations. Cite the authoritative file/section or reference asset in the answer itself for existing features, experiments or design decisions. A citation to an index alone is insufficient when the underlying owner was required. Do not claim source verification without reading the source.

For a same-task follow-up at a confirmed unchanged revision, explicitly label any previously consulted documents reused from the current context, and list new reads separately; a compact shared path prefix may reduce repetition but must identify the actual files. Do not reuse prior evidence across a new task, session or changed revision without reinitializing. If access fails, report the failure and alternative retrieval attempts before answering the affected question; clearly label any general/unverified explanation, and identify missing documents. Do not make unsupported repository-state claims.

Illustrative footer (not an attestation of actual reads):

> **Docs:** `sound-foundations`; main @ `<checked SHA>`; consulted: `AGENTS.md`, `docs/agents/{contributor-instructions.md,task-routing.json,compliance-reports.md,music-instructions.md,sound-design-instructions.md}`, `docs/README.md`, `docs/examples/foundations/{catalogue.md,breathing-harmonic-drone.md}`. Discovery: owner located; no issues found.

List all actually applicable prerequisites in a real report, not just those in this illustration. Keep the existing full PR evidence format for repository changes; it does not replace the conversational report.

### Mandatory visual progress summary for multi-step tasks

Whenever work is divided into multiple steps, subfeatures, milestones, work slices or a checklist of deliverables, **include a visible graphical progress tracker in every user-facing task summary and handoff**, regardless of the task's size. This applies to implementation, planning, research, documentation, reviews, experiments, bug fixes, delegated work, resumed tasks and blocked or partial deliveries. A response reporting one current slice of a larger feature must still show the full known feature roadmap. Ordinary questions and genuinely single-step tasks need no tracker; do not invent unnecessary steps merely to show one.

- Prefer a compact rendered card/box, segmented progress bar and rows with status icons, stable step/slice identifiers, names and explicit textual statuses. Use the available client-native rich response components when supported. Color must supplement, never replace, text or icons. When graphical components are unavailable (e.g. a terminal or plain Markdown client), use a visually distinct Markdown or text block with a Unicode/ASCII progress bar, status symbols and labels.
- Show the completed count / total known steps and percentage, based on actual completed steps, not estimated effort. Include all known feature steps (completed, current and future); if the plan is still being elaborated, identify the denominator as currently known and do not invent missing slices. Update the tracker after each reported step, continuation or status change; retain IDs and reconcile roadmap, PR and verification evidence before reporting.
- Use consistent status semantics: green/check = completed (for repository implementation, required verification passed and actually merged); blue/in-progress icon = underway; orange/clock = implemented but verification pending, verified awaiting merge, or other pending approval (label these separately); red/error = failed or blocked; gray/empty circle = planned. Partial, unverified, awaiting-merge, failed and blocked items do not count as complete. For non-code or documentation-only work apply its actual acceptance and verification policy; never pretend waived verification was executed. Label deferred steps separately and explain any exclusion from the denominator.
- Link real PRs or relevant evidence where applicable and identify the next actionable step or blocker. Never imply progress from an open PR alone or claim success without evidence. Use the tracker for communication, not as a replacement for the canonical roadmap or machine-readable PR compliance report. The compact prose handoff length guideline does not exclude or limit the tracker.

Illustrative plain-text fallback (not actual task evidence):

~~~text
Feature progress: 1/4 (25%)  [█░░░]
✓ S1  Contract audit       Completed / merged
◷ S2  Catalog exporter     Verified; awaiting merge
▶ S3  Patch builder        In progress
○ S4  Compatibility        Planned
Next: merge S2, then finish S3.
~~~

This is a mandatory agent response-format requirement. It applies even if a client cannot render color; it is not automatically enforced by the existing PR compliance checker.

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

Local commands: `node --test tests/tooling/AgentCompliance.test.mjs` and `node scripts/check-agent-compliance.mjs`. The latter checks routing and repository-wide documentation coverage outside a GitHub pull-request event; PR-body checks use the event payload and Git history in CI. Run both tooling test files with `node --test tests/tooling/AgentCompliance.test.mjs tests/tooling/DocumentationCoverage.test.mjs`.

Repository owners can require the status check named **Agent documentation compliance** in the main branch's GitHub ruleset or branch protection. Until configured as required, a failed check does not necessarily prevent a merge. Keep existing build/test checks required as appropriate. Protect review of changes to the workflow, checker and route policy themselves; a PR can modify its own checker, and this is not an adversarial attestation system.

This check verifies structural consistency, not actual reading, understanding, semantic completeness, claimed test execution or owner authorization. It cannot infer every task category from paths; reviewers and agents must still apply the semantic routes. Read-only work without a PR remains governed by initialization and handoff policy. The workflow cannot block or inspect a standalone conversational response; its evidence footer is an agent obligation, not independently enforced proof.

## Repository-wide documentation coverage

The coverage audit is part of the existing compliance and documentation-link checks. It inventories tracked Markdown using Git (including files outside docs), validates local destinations and supported Markdown/HTML anchors, and builds a directed discovery graph. The forward walk starts at AGENTS.md, follows real links to the documentation map and routing JSON, then adds declared route edges. A separate Markdown-only path check prevents routing entries from hiding disconnected navigation. The reverse walk follows inbound edges from each file back to AGENTS.md, detecting unreachable islands and cycles. `node scripts/check-doc-links.mjs --report` prints each file's priority, classification, shortest navigation/route/reverse chains and the reason for an exception. Deep navigation beyond the configured depth is reported as a warning for review, not a reason to force bulk reads.

Every active Markdown file needs an explicit routing owner and an actual navigation path. Required/recommended/reference are different consultation levels: task-relevant reference files are mandatory when they directly answer the task under the contributor policy. Narrow semantic routes make frequently missed research and feature owners mandatory only for the matching task. The coverage metadata in [task-routing.json](task-routing.json) permits exact, reasoned classifications for archived references, the PR template, alternate entry points and compatibility redirects. Archives/templates must still be reachable; alternate entries and redirects must link directly to a reachable canonical target. No wildcard exemption or stale exception is permitted. Review the exact route and link when adding a document.

The scenario catalogue asserts explicit route unions and expected mandatory documents for representative work (harmonizing oscillators, layered bass, phrasing, genres, instrument models, sequencing, GPU layout and performance). It guards declared routing semantics, **not natural-language understanding or proof that an external agent read the file**. Agents still classify tasks semantically and must consult all applicable owners. The checker covers ordinary Markdown inline/reference links, explicit HTML links/anchors, headings and same-repository main links; external sites, historical commit links and nonstandard/generated anchor syntax need manual review where relevant. No network requests or npm dependencies are needed. The executable check is not covered by the documentation-only test waiver.

## Compact examples

These are illustrative task summaries, not claims of completed reads. Every real declaration includes the actual global prerequisites and required route documents.

| Task | Example routes | Example constraints / handoff |
|---|---|---|
| Node fold and click behavior | implementation, roadmap, editor-ui, node-layout, interaction-change, usage, documentation if editing docs | Preserve ports, avoid gesture collisions, one slice; report actual UI tests and CI. |
| Documentation reorganization | documentation, roadmap when tracked | Preserve canonical owners and links; owner-waived documentation-only verification; next slice. |
| Pitched musical piece | music, pitched-composition, music-theory, synth-voice, instrument-selection, composition-development and mix-analysis as applicable | Verify current node behavior and distinguish listening from measurements; report unavailable audition honestly. |
| Delegated DSP fix | implementation, dsp, performance if latency/glitches are involved | Delegate reads relevant rules independently; coordinator records actual consulted paths and regression evidence. |
