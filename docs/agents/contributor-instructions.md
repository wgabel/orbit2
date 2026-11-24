# Orbit 2 contributor instructions

## Mandatory initialization

Every new task, resumed slice, read-only investigation, review, planning session and delegated task must complete this protocol before substantive analysis, design, implementation or repository modifications. Reading instructions and discovering the relevant documents is permitted during initialization.

1. Read root [AGENTS.md](../../AGENTS.md) and this contributor policy from the target branch or revision. If the environment does not discover the entry point automatically, fetch it explicitly.
2. Read the [documentation map](../README.md) and [mandatory task routing configuration](task-routing.json). Classify the task semantically using every matching route, the linked domain policies, and the [architecture routing index](../architecture/task-routing.md) or usage/music indexes as needed. Routes are additive; do not pick just one for cross-cutting work.
3. Read the actual required documents and relevant sections, not only their filenames or summaries. For code tasks consult the coding standards, relevant architecture and testing contracts; for nontrivial implementation locate the owning roadmap slice and read the work-slice protocol. For documentation changes consult the documentation conventions. Read the routed domain policies as applicable.
4. Identify the current scope, applicable constraints and acceptance criteria; give a compact pre-work declaration of selected task areas, actual repository-relative paths consulted and key constraints. Report unavailable required documents. Do not fabricate a read or claim full compliance based only on a link or another agent's report.
5. Begin substantive work only when the applicable mandatory documentation has been consulted. If a required document is inaccessible, attempt recovery and report the blocker; do not perform affected work until resolved or the owner explicitly authorizes an exception. A genuinely new topic with no existing contract is not a blocker: consult the nearest existing contracts and identify the gap.

### Route resolution

The [routing configuration](task-routing.json) owns the minimum named document sets; this policy owns behavioral obligations. All paths are repository-relative. The configuration includes mandatory domain policy files as well as feature documents. Read `alwaysRequired` once per task, then take the deduplicated union of `required` for every matching route. Read relevant sections; follow additional feature contracts and domain-specific rules even when the configuration does not enumerate them. `recommended` guides normal investigation; `reference` is optional unless the actual task or another rule requires it. For a roadmap task, read the relevant owning register, not all three. The `novel-topic` fallback does not replace other matching routes.

Select routes by meaning, not literal keyword matching. For example: node folding with a new gesture selects implementation, roadmap (if nontrivial), editor-ui, node-layout and interaction-change; a new pitched composition selects music, synth-voice, pitched-composition, music-theory, instrument-selection, mix-analysis (for a complete piece), and other applicable routes; a documentation correction selects documentation and roadmap when tracking a slice. New or changed scope adds routes before affected work.

Optional `pathPrefixes` specify conservative, case-sensitive, repository-relative directory prefixes for future deterministic changed-path checks. A matching changed path can require a route but an unmatched path never proves a semantic route is unnecessary. Do not infer gestures, music intent or all feature behavior from filenames. There is no executable routing validator or CI enforcement yet; that belongs to the later compliance slice. Report selected route IDs and actual paths read.

If scope changes, resolve additional documentation requirements before affected work. On a new session or resumed slice, reinitialize against the current branch rather than assuming old instructions are current. Each delegated agent must independently initialize; the coordinator provides the entry point, task scope and target revision and collects the actual consultation report. Keep declarations short; reuse documents already read in the same task without rereading unchanged sections unnecessarily.

## Working with the repository

- Begin documentation discovery with the [documentation map](../README.md). Follow the [documentation ownership and placement rules](documentation-conventions.md) when reorganizing, adding, moving, or consolidating docs; keep one authoritative home for each contract/status and repair references. The canonical project entry point is `docs/README.md`; keep it short and navigational.
- **For nontrivial implementation, redesign or multi-part work, follow [granular agent work slices](work-slices.md):** break large milestones into small, independently verifiable roadmap slices *before coding*. Default to **one slice per agent run and one focused PR**, subject to the owner-authorized documentation continuation exception in the work-slice protocol; stop with a compact handoff and next-slice ID instead of automatically starting another slice. This applies in ChatGPT Work, Codex and delegated work. Keep necessary regression coverage and quality checks; trivial self-contained edits need no artificial subdivision.
- Respect established architectural boundaries and stable identifiers, while remaining free to propose and implement justified architectural improvements within the user's requested scope. Documentation guides decisions; it is not a substitute for engineering judgment.
- A new task may have no matching document or roadmap entry. Explore adjacent contracts and the code, design the new capability, and continue within the authorized scope. Extend the closest module or create a new one when that makes the knowledge easier to find; add or revise the index and roadmap if relevant. Do not require a separate documentation PR, a previously assigned milestone, or a fixed directory/template before beginning work.

## Domain policies

The routing configuration requires [development instructions](development-instructions.md), [music instructions](music-instructions.md) and/or [sound-design instructions](sound-design-instructions.md) when their task routes apply. Read those policies before substantive work; apply their conditional obligations without loading unrelated feature documents. For a new task, select all applicable routes, including domain policies, even if the task spans areas.

## Implementation quality check (before handoff)

For code changes, follow the [development quality checklist](development-instructions.md#implementation-quality-check-before-handoff).

## Documentation and verification

- Follow the [documentation-only continuation exception](work-slices.md#documentation-only-continuation) when applicable. Documentation-only edits need no test run, CI wait or intervening merge; report them as committed without verification. Never extend this exception to executable changes.
- Prefer one authoritative technical contract with relative links from related modules; avoid duplicating detailed specifications. Preserve useful rationale, examples and historical implementation records, clearly distinguishing them from current contracts. Split or combine modules when the topic warrants it; filenames and target sizes are not rigid requirements.

## Task handoff: documentation feedback

At the end of every task, include a short documentation report in the user-facing summary (or to the coordinating agent when delegated):

- **Documentation consulted:** List the repository-relative paths of documentation and routing files actually read for the task, including this file and any selected domain policies, overview, roadmap or feature modules consulted. If only specific sections were read, indicate that when useful. Do not claim to have read files you did not inspect. If none were consulted, say so.
- **Documentation discoverability:** Report whether finding the relevant information was straightforward. Mention any missing topics, unclear navigation, misleading links, conflicting or outdated descriptions, or other search difficulties that affected the task. Say how the gap was resolved (for example, inspecting the source or tests), and flag any useful documentation improvement. If there were no difficulties, say "No issues found."

Keep this report proportionate and factual, including for read-only tasks. It is feedback on the documentation system, not a requirement to read additional files or a reason to delay work on a new topic. When coordinating multiple agents, collect their reports and consolidate the relevant paths and findings in the final handoff.
