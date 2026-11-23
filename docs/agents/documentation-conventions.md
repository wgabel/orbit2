# Documentation placement, ownership and navigation

[Documentation map](../README.md) · [Agent rules](../../AGENTS.md) · [Roadmap](../../ROADMAP.md)

## Where information belongs

- Keep `/README.md` short: purpose, quick start, commands, and links. Keep `/AGENTS.md`, `/ROADMAP.md` and `/ORBIT2-ARCHITECTURE.md` as stable root entry points.
- Keep the top-level `docs/README.md` as the single documentation directory; do not reproduce technical detail in it. Each topic has a single authoritative owner in `docs/core/`, `audio/`, `instruments/`, `editor/`, or `quality/`.
- Put agents' process rules in `docs/agents/`, including the [small-slice protocol](work-slices.md). Put milestone/slice **status** in the three `docs/roadmap/` registers, with `ROADMAP.md` as the canonical cross-project summary. Do not create a separate unlinked roadmap.
- Put practical interaction guides in `docs/usage/`; music research, genres, compositional guidance and reproducible patch references in `docs/examples/`.
- Keep former implementation notes in `docs/archive/` only when unique history is worth retaining; label their date and non-authoritative status.
- Keep `README.md` inside a directory when it genuinely serves as a GitHub folder landing page. Do not move `docs/usage/README.md` or the music/foundation indexes merely to remove the name.

## Maintenance rules

1. Before adding a document, check the [map](../README.md) and current owner. Prefer extending the existing contract with a short section over another near-duplicate document.
2. Do not copy identical implementation details or statuses between roadmap, feature contracts and usage guides; link to the authoritative owner. Keep status aligned with verified source/tests and real PRs.
3. When moving a file, update **all relative links** and relevant agent entry points. Preserve old URLs with a small, clearly marked pointer only when necessary for inbound links; the old location must contain no competing copy of the specification.
4. Run `node scripts/check-doc-links.mjs` and any related verification. The checker validates local Markdown destination paths; inspect fragment anchors, meaningful links and examples when changing headings. Update the map for a new area or a moved entry point.
5. Prefer focused reads and compact handoffs over loading the whole documentation tree into an agent's context; preserve traceability instead of appending transcripts.
