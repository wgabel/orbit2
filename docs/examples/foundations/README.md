# Sound-design foundations — reference catalogue

[Music creation](../README.md) · [Voice design](../sound-quality-and-voice-design.md) · [Production iteration](../mixing-mastering-and-analysis.md#5-agent-workflow-render--analyze--refine--re-render)

Catalogue, evidence labels and reference asset maintenance introduced in [PR #42](https://github.com/wgabel/orbit2/pull/42).

A foundation is a reusable musical or sound-design relationship demonstrated by a concrete graph, with its sonic identity, evidence, limitations and transferable experiments. It is not necessarily a finished preset, an entire composition, or a required technique. Build a diverse palette; do not force a successful drone into every track or assume complexity is quality.

## Catalogue

| ID / reference | Character and possible roles | Principle | Evidence |
| --- | --- | --- | --- |
| 01 — [Breathing harmonic drone](breathing-harmonic-drone.md) · [Original patch JSON](breathing-harmonic-drone/reference.orbit2.json) | Monumental, heavy, wet, industrial, grunge, dark, grim; drone, bass texture, evolving pad | Slightly detuned octave partials, audio-rate amplitude shaping, coordinated filter and chorus | Listener validated; independently audio analyzed; technically validated (serializer/graph restoration). **Original can overload and contains subsonic energy.** Native playback safety and composition validation pending for this exact revision. [PR #33](https://github.com/wgabel/orbit2/pull/33) |

Download the original JSON using GitHub's raw/download action, then stop playback and use **Patches → Import JSON** in Orbit 2. Read its safety notes before playing; lower final gain/monitor level and work on a copy. See [patch import](../../usage/patch-editor.md#5-save-share-and-recover). References are assets in this repository, not new built-in presets or the planned [user module library](../../editor/reusable-modules.md).

## Evidence and validation statuses

Use independent evidence labels, not an automatic ranking or a single escalating badge. One may be listener validated without a native re-render, or measured without listener approval. Attach each claim to the exact revision/variant, evidence source, test conditions and scope.

| Label | What qualifies | What it does not imply |
| --- | --- | --- |
| Experimental | Design hypothesis, new candidate or untested variation | Correct import, useful sound or endorsement |
| Technically validated | Actual app deserialization/graph validation, compiler/runtime or appropriate behavior checks; identify what passed and on which version | Audio quality; schema-only checks are explicitly structural inspection, not full native execution |
| Audio analyzed | Rendered signal measured; record native versus independent renderer, duration, sample rate/channels, metrics/windows and assumptions | Human perception, native performance from a simulation, or safety in untested sections |
| Listener validated | Recorded human feedback on an identified sound/patch or excerpt; state the positive and negative findings and whether listener played the app or a preview if known | Universal taste, improved mix or automatic approval of a modified variant |
| Composition validated | Demonstrated use in an identified complete piece with evidence of its musical role and human feedback on that use | Every other register, genre, arrangement or later revision |

Record pending, unavailable or failed checks alongside positive evidence. A graph may have genuine artistic merit despite a technical risk; preserve the original evidence and provide a separately identified safer variant if verified. Only relabel a variant when its own evidence supports the claim.

## Maintaining and expanding the catalogue

Agents should consider recording a foundation when a new experiment or listener feedback reveals a distinctive, generalizable musical relationship. Before adding one, compare existing entries; extend a related entry with a clearly named variant if it illustrates the same mechanism. A finished composition can reveal foundations, but not every delivered patch needs a catalogue entry. Use authorized, shareable data only; do not upload personal or third-party reference material without appropriate authorization.

1. Give a new foundation a stable ID and descriptive slug. Keep the catalogue row brief, with character, roles, principle, evidence labels and links. Avoid encoding a genre or quality ranking into IDs.
2. Preserve the actual source patch (data-only exported JSON) unchanged when available. Put reference assets in a folder named for the slug; retain the analysis page's stable URL. Record filename, source/provenance, schema, revision or hash, and dependencies. If reconstructing from prose, explicitly mark it as a reconstruction, not the original. Store candidate/edited versions separately and keep earlier evidence associated with its source graph.
3. In the analysis page, cover musical intent and roles, sonic character, actual graph/critical routing, why the interactions work, import/playback instructions, measurements and method, comparison or ablation when useful, risks/limitations, variants and applications. Keep listener feedback separate from inferred sound and measured facts. Link related theory, technical node contracts and relevant case studies rather than reproducing them.
4. Validate with the current app serializer/registry and relevant tests where feasible; check IDs, connection endpoints, parameter ranges/units, mute states, active graph, output, timing and bounds. Render a baseline using the native engine when available, measure relevant stages/windows and compare changes at matching conditions; do not substitute a synthetic preview for native verification without labeling it. For potentially loud or subsonic sources add a prominent caution and conservative audition procedure. See the [production workflow](../mixing-mastering-and-analysis.md#5-agent-workflow-render--analyze--refine--re-render).
5. Request or record human listening feedback where appropriate. Describe precisely which revision/excerpt the listener evaluated, what they heard and what did not work. Retain unsuccessful variants or lessons when useful; label unverified ideas as experimental. For in-context claims identify the actual composition and evidence, not just that a similar topology was used.
6. Update entry and catalogue evidence when feedback or tests arrive, not simply because a node was changed. Keep an import regression for checked-in reference assets when practical; if schema changes, verify recovery or provide a clearly identified compatible copy without overwriting the historical reference. Link the actual documentation PR and any relevant implementation PRs; record their distinct roles.
7. Make useful findings discoverable from the [music creation index](../README.md) or relevant specialist guide. During handoff report additions, validation status, limitations and discovery issues. If repository updates were not part of the requested scope, propose the candidate instead of silently publishing it.

The catalogue and examples are starting points for exploration, not an allowlist of musical techniques. Agents may investigate new forms, syntheses, arrangements and evaluation methods; add focused documentation or adjust these conventions when a new category warrants it.
