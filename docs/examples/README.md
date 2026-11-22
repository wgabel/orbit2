# Music creation — start here

[Architecture](../../ORBIT2-ARCHITECTURE.md) · [App usage](../usage/README.md) · [Sound-design foundations](foundations/README.md)

Index and maintenance guidance introduced in [PR #42](https://github.com/wgabel/orbit2/pull/42); the linked guides retain their original provenance.

This is the entry point for users and agents creating, analyzing or improving music in Orbit 2. It routes to existing detailed guides rather than duplicating them. Read this short page first for a musical task, then select the relevant sections. A new style, technique or unusual brief is not a reason to stop: investigate the available nodes, experiment and extend the knowledge when useful.

## Find your task

| Task | Start with |
| --- | --- |
| Individual effect or modulation | [Effects](../audio/effects.md), [parameters and modulation](../core/parameters-and-modulation.md), [signal observation](../audio/signal-observation.md) |
| Individual voice, instrument or preset | [Voice quality](sound-quality-and-voice-design.md), [instrument recipes](instrument-recipes.md), [instrument usage](../usage/sound-and-instruments.md) |
| Drone, evolving texture or sound-design experiment | [Foundations catalogue](foundations/README.md), [worked drone](drone.md), [timbre](music-theory-and-sound-design.md#5-timbre-why-one-oscillator-is-not-always-enough) |
| Whole piece, ambient or otherwise | [Composition and layers](generative-ambient-composition.md#1-the-structure-of-a-musical-piece), [compositional development](compositional-development.md), [genre index](electronic-music-genres.md), workflow below |
| Feedback on monotony, repetitive melody, indistinct choruses/drops or lack of progression | [Compositional development](compositional-development.md) — micro/meso/macro development, section contrast and listener feedback |
| Why a piece works or fails; tension/release, predictability, surprise, listener experience | [Musical quality and expectation](musical-quality-and-expectation.md) — research, genre/intent-aware criteria and listening experiments |
| Existing patch analysis | [Production analysis](mixing-mastering-and-analysis.md#2-what-to-measure-and-what-each-measurement-cannot-tell-you), [theory and quality checks](music-theory-and-sound-design.md#11-agent-workflow-and-quality-checks); inspect the actual graph, saved state and signal path |
| User feedback on timbre, noise or hollow sounds | [Voice design](sound-quality-and-voice-design.md), [production feedback example](mixing-mastering-and-analysis.md#6-worked-feedback-example-event-horizon--a-signal-returns) |
| Feedback on loud melodies, weak accents, masking or depth | [Contextual layer balance](contextual-layer-balance.md), [spectral balance and register](music-theory-and-sound-design.md#7-spectral-balance-masking-and-register) |
| Export, load, organize or verify a patch | [Patch editor usage](../usage/patch-editor.md), [persistence](../editor/persistence-and-history.md), [node and port contracts](../core/signals-and-ports.md) |

## End-to-end workflow

For complete pieces and substantial voice design, use the [detailed production iteration](mixing-mastering-and-analysis.md#5-agent-workflow-render--analyze--refine--re-render) alongside the [musical quality checklist](music-theory-and-sound-design.md#11-agent-workflow-and-quality-checks). These remain the authoritative procedures; the following is a routing summary.

| Stage | Decision or evidence | Read as needed |
| --- | --- | --- |
| 1. Composition design | Brief, roles, genre, emotional arc, tonal and rhythmic material; timeline, contrasting phrases/sections and transitions for a piece | [Composition](generative-ambient-composition.md), [development](compositional-development.md#2-compose-the-changes-before-building-the-voices), [genre families](electronic-music-genres.md), [theory](music-theory-and-sound-design.md), [expectation and intent](musical-quality-and-expectation.md#3-cognition-anticipation-uncertainty-and-surprise) |
| 2. Sound design | Build distinct voices, articulations, interacting timbres, intentional noise and depth; use foundations only if suitable | [Voice design](sound-quality-and-voice-design.md), [foundations](foundations/README.md), [graph techniques](genres/orbit-techniques.md) |
| 3. Technical validation | Actual node/parameter/port definitions, amounts, bounds, timing, reachability, hierarchy and import compatibility | [Usage](../usage/README.md), [persistence](../editor/persistence-and-history.md), relevant source and tests |
| 4. Audio analysis | Keep a baseline; render actual native engine where possible, inspect important stems, master, transient and representative/full-duration windows | [Measurements](mixing-mastering-and-analysis.md#2-what-to-measure-and-what-each-measurement-cannot-tell-you), [signal observation](../audio/signal-observation.md), [contextual balance](contextual-layer-balance.md) |
| 5. Musical evaluation | Harmony/interaction, motif, timbre, noise, masking, depth, dynamics, transitions and micro/meso/macro evolution in context; audition if possible | [Theory](music-theory-and-sound-design.md), [development evaluation](compositional-development.md#4-musical-evaluation-and-refinement), [listener experience](musical-quality-and-expectation.md#6-apply-these-criteria-in-the-existing-production-iteration), [production iteration](mixing-mastering-and-analysis.md#5-agent-workflow-render--analyze--refine--re-render) |
| 6. Refinement | Change weak elements deliberately, preserve strengths, revalidate, re-render and compare equivalent passages and matched playback levels; repeat as useful | [Production iteration](mixing-mastering-and-analysis.md#5-agent-workflow-render--analyze--refine--re-render), [paired balance case](contextual-layer-balance.md#5-measured-case--submerged-relay-three-minutes) |
| 7. Deliver and learn | Editable patch, actual evidence, limitations, notes and meaningful new discoveries; collect listening feedback when available | [Foundation maintenance](foundations/README.md#maintaining-and-expanding-the-catalogue), [agent handoff](../../AGENTS.md#task-handoff-documentation-feedback) |

Refinement is part of creative work, not just a final checklist. A valid graph is not proof of good music. Scale the loop to the task: a small effect may need one representative input and a few checks; a three-minute composition should be considered over its musical form. If native rendering, measurement or listening is unavailable, continue with what is feasible, distinguish approximations from verified results, and state pending checks. Do not invent a measurement or a listening verdict. There is no required genre, node count, foundation, numeric loudness target or number of iterations.

## Maintain this entry point

- Before proposing new musical guidance, check this index, the relevant linked section(s) and the existing foundation catalogue for overlapping work. Improve the existing source of knowledge; do not create a second competing workflow.
- Keep this index short and route by task or decision, including new topics that do not yet fit existing categories. Add focused guides when the knowledge merits its own module; link them here and from related documents. Preserve historical examples and actual PR provenance.
- When a run reveals a useful general principle, record the evidence and scope in the appropriate existing guide. For a reproducible, distinctive sound relationship, follow the [foundation maintenance process](foundations/README.md#maintaining-and-expanding-the-catalogue). An ordinary patch request does not automatically authorize publishing private material or modifying the repository; report useful candidates for later inclusion if outside the current task scope.
- On handoff, report documents actually consulted, information that was hard to find, what was verified, and what new knowledge was added or should be added. Keep the workflow adaptable to new creative and technical methods.
