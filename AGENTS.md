# Orbit 2 contributor instructions

- Read `ORBIT2-ARCHITECTURE.md` before modifying the application; preserve the existing architectural boundaries and roadmap identifiers.
- For every implementation PR (feature, improvement or fix), update the relevant implementation description and roadmap entry in `ORBIT2-ARCHITECTURE.md` with its actual linked PR number: `[PR #N](https://github.com/wgabel/orbit2/pull/N)`. Update the status table where applicable. List follow-up PRs alongside the affected feature, rather than replacing earlier evidence.
- If the number is unknown, open the PR, then commit the links to that same branch before merge. Distinguish planned, partial, in-review, and merged work; never cite a documentation-only PR as implementation evidence. For historical changes made directly on main, cite the actual commit instead of inventing a PR.
- Add regression coverage for changed behavior and report what was actually verified.
