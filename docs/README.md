# Orbit 2 documentation map

[Project README](../README.md) · [Roadmap](../ROADMAP.md) · [Architecture](../ORBIT2-ARCHITECTURE.md) · [Agent rules](../AGENTS.md)

**Start here for any documentation search.** This index routes to authoritative owners; it is not a second copy of the specifications or roadmap. Read only the area needed for your current task.

## I want to…

| Goal | Start with |
| --- | --- |
| Run or build the app | [Installation](getting-started/installation.md), [development](getting-started/development.md) and [deployment](getting-started/deployment.md) |
| Test the app | [Development checks](getting-started/development.md) and [testing contracts](quality/testing.md) |
| See the current implementation status or next work slice | [One central roadmap](../ROADMAP.md) and its owning milestone register |
| Understand system architecture and source ownership | [Architecture overview](architecture/overview.md), [task routing](architecture/task-routing.md) and [project structure](core/project-structure.md) |
| Edit a patch or use nodes | [Usage guide](usage/README.md), [patch editor](usage/patch-editor.md) and [sound/instruments](usage/sound-and-instruments.md) |
| Create or refine a musical composition | [Music creation](examples/README.md), [genres](examples/electronic-music-genres.md) and [foundations](examples/foundations/catalogue.md) |
| Add a feature or fix code | [Agent instructions](../AGENTS.md), [coding standards](quality/coding-standards.md), [work-slice protocol](agents/work-slices.md) |
| Modify input, gesture or node behavior | [Interaction registry](editor/user-interactions.md), [canvas](editor/canvas.md), [node expansion](editor/expandable-nodes.md) |
| Diagnose DSP, UI or playback performance | [Performance overview](quality/performance.md), [metrics](quality/performance-metrics.md), [diagnostics](quality/performance-troubleshooting.md) |

## Technical documentation by owner

The [architecture task-routing index](architecture/task-routing.md) provides more precise subsystem links. The root architecture file is a stable compatibility entry point.

| Area | Canonical documents |
| --- | --- |
| Graph, commands, events and ports | [Core](core/graph-model.md), [ports](core/signals-and-ports.md), [commands/events](core/commands-and-events.md), [modulation](core/parameters-and-modulation.md) |
| DSP and signal processing | [DSP engine](audio/dsp-engine.md), [generators](audio/oscillators-and-noise.md), [effects](audio/effects.md), [envelopes](audio/envelopes.md), [timing](audio/transport-and-timing.md) |
| Instruments and sequencers | [Note sequencer](instruments/note-sequencer.md), [pitch control](instruments/pitch-control.md), [physical models](instruments/physical-modelling.md), [Faust instruments](instruments/faust-instrument-nodes.md) |
| Editor and interaction | [Design system](editor/design-system.md), [canvas](editor/canvas.md), [connections](editor/annotations-and-connections.md), [reusable modules](editor/reusable-modules.md) |
| Reliability and performance | [Coding quality](quality/coding-standards.md), [testing](quality/testing.md), [performance](quality/performance.md), [profiling](quality/dsp-profiling.md) |
| Music and sound-design guidance | [Music creation index](examples/README.md), [task index](examples/task-index.md), [production workflow](examples/production-workflow.md) and [reference foundations](examples/foundations/catalogue.md) |
| Implementation planning | [Central roadmap](../ROADMAP.md): [foundation register](roadmap/foundation.md), [editor/effects register](roadmap/editor-and-effects.md), [advanced register](roadmap/advanced.md) |
| Agent process and documentation maintenance | [Work slices](agents/work-slices.md), [documentation conventions](agents/documentation-conventions.md), [contributor rules](../AGENTS.md) |

## Document lifecycle and precedence

1. **Current implementation:** code and tests are behavioral evidence. Relevant subsystem specifications explain the contracts.
2. **Planning and status:** `ROADMAP.md` summarizes; `docs/roadmap/{foundation,editor-and-effects,advanced}.md` own individual milestones and slice closeouts. Do not invent competing roadmaps.
3. **How to use:** `docs/usage/` documents tested interaction and patch workflows; `docs/examples/` documents music-production techniques and validation limits.
4. **Historical reference:** `docs/archive/` holds snapshots of superseded prose only. It must never override live contracts or verified PR status.

The `README.md` files under `usage/` and `examples/` are intentional, short section entry points. Specialized catalogues (such as `examples/foundations/catalogue.md`) use descriptive filenames. See [documentation conventions](agents/documentation-conventions.md) before changing locations.
