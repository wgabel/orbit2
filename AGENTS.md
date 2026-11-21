# Orbit 2 contributor instructions

## Working with the repository

- Start with [ORBIT2-ARCHITECTURE.md](ORBIT2-ARCHITECTURE.md), the short overview and task routing index. Read relevant feature modules, source code, tests and any other material that helps solve the task. The routing table is advisory, not an allowlist or a limit on context; cross-cutting work can require several modules. Do not load every specification by default. Adjust the depth of investigation to the complexity and risk of the task.
- Use [ROADMAP.md](ROADMAP.md) and its linked milestone registers when planning or changing implementation status. Use [README.md](README.md) for practical build/run instructions. The implementation and tests are the evidence for current behavior; reconcile stale documentation instead of assuming that every design proposal is implemented.
- Respect established architectural boundaries and stable identifiers, while remaining free to propose and implement justified architectural improvements within the user's requested scope. Documentation guides decisions; it is not a substitute for engineering judgment.
- A new task may have no matching document or roadmap entry. Explore adjacent contracts and the code, design the new capability, and continue within the authorized scope. Extend the closest module or create a new one when that makes the knowledge easier to find; add or revise the index and roadmap if relevant. Do not require a separate documentation PR, a previously assigned milestone, or a fixed directory/template before beginning work.

## Documentation and verification

- For every implementation PR (feature, improvement or fix), record its actual linked PR number in the relevant implementation description and milestone register: `[PR #N](https://github.com/wgabel/orbit2/pull/N)`. Update the roadmap summary/index where applicable. For a small fix with no milestone, a concise record in the relevant feature module and appropriate register is sufficient. Keep earlier PR evidence and add follow-ups.
- If the number is unknown, open the PR and then add the links to the same branch before merge. Distinguish planned, partial, in-review and merged work; never cite a documentation-only PR as implementation evidence. For older direct-to-main changes, cite the actual commit rather than inventing a PR.
- Prefer one authoritative technical contract with relative links from related modules; avoid duplicating detailed specifications. Preserve useful rationale, examples and historical implementation records, clearly distinguishing them from current contracts. Split or combine modules when the topic warrants it; filenames and target sizes are not rigid requirements.
- Add regression coverage for changed behavior and report what was actually verified. Scale testing and documentation to the change; a typo or a purely editorial correction need not create a new feature specification.
