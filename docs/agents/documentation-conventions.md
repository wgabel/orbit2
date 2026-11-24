# Documentation placement, ownership and navigation

[Documentation map](../README.md) · [Agent rules](contributor-instructions.md) · [Roadmap](../roadmap/overview.md)

## Where information belongs

- The repository root permits exactly one Markdown file: `AGENTS.md`, the mandatory agent discovery entry point. Do not restore other root Markdown files. `docs/README.md` is the project introduction and navigation hub. Put setup, development commands and deployment detail under `docs/getting-started/`.
- The canonical project-wide roadmap is `docs/roadmap/overview.md`; keep its milestone registers adjacent in `docs/roadmap/`. Architecture overview and routing belong in `docs/architecture/`. Global contributor instructions belong in `docs/agents/contributor-instructions.md`; domain-specific obligations belong in the routed [development](development-instructions.md), [music](music-instructions.md) and [sound-design](sound-design-instructions.md) policies. Do not duplicate these obligations in the global bootstrap. Codex-style agents discover the root `AGENTS.md`; GitHub Copilot has `.github/copilot-instructions.md`. Both route to the same canonical contributor policy. For tools without automatic discovery, explicitly instruct agents to read root `AGENTS.md` first, including delegated tasks.
- Keep the top-level `docs/README.md` as the single documentation directory; do not reproduce technical detail in it. Each topic has a single authoritative owner in `docs/core/`, `audio/`, `instruments/`, `editor/`, or `quality/`.
- Put agents' process rules in `docs/agents/`, including the [small-slice protocol](work-slices.md) and [mandatory task routing configuration](task-routing.json). Keep route IDs stable; update affected repository-relative routing paths when moving a document. Do not duplicate feature contracts in route metadata. Put milestone/slice **status** in the three `docs/roadmap/` registers, with `docs/roadmap/overview.md` as the canonical cross-project summary. Do not create a separate unlinked roadmap.
- Put practical interaction guides in `docs/usage/`; music research, genres, compositional guidance and reproducible patch references in `docs/examples/`.
- Keep former implementation notes in `docs/archive/` only when unique history is worth retaining; label their date and non-authoritative status.
- Use `README.md` only for a *short* folder landing page; name detailed procedures, reference catalogues and case studies by subject. In `docs/examples/`, retain only the short section `README.md`; the foundations reference catalogue is `foundations/catalogue.md`.

## Maintenance rules

1. Before adding a document, check the [map](../README.md) and current owner. Prefer extending the existing contract with a short section over another near-duplicate document.
2. Do not copy identical implementation details or statuses between roadmap, feature contracts and usage guides; link to the authoritative owner. Keep status aligned with verified source/tests and real PRs.
3. When moving a file, update **all relative links** and relevant agent entry points. Preserve old URLs with a small, clearly marked pointer only when permitted by the repository layout; do not recreate root Markdown placeholders other than the required `AGENTS.md` discovery entry point. The old location must contain no competing copy of the specification.
4. For documentation-only changes, follow the [documentation-only continuation exception](work-slices.md#documentation-only-continuation): no required verification run or CI wait. For executable changes, run applicable verification, including `node scripts/check-doc-links.mjs` when relevant. The checker validates local Markdown destination paths and anchors for the reorganized pages; inspect other fragment anchors, meaningful links and examples when changing headings. Update the map for a new area or a moved entry point.
5. Prefer focused reads and compact handoffs over loading the whole documentation tree into an agent's context; preserve traceability instead of appending transcripts.
