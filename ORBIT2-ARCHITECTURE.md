# Orbit 2 — Modular audio engine and hierarchical graph architecture

**Status:** Living architecture, implemented contracts and implementation roadmap  
**Implementation audit:** 2026-11-21 · main through [`886d23f`](https://github.com/wgabel/orbit2/commit/886d23f)
**Target repository:** [wgabel/orbit2](https://github.com/wgabel/orbit2)  
**Original design date:** 2026-11-20

This document consolidates the design discussions for Orbit 2: the original browser boilerplate, separation of responsibilities, signal generation, modulation, audio processing, clocks, hierarchical node canvases, exposed input/output racks, and event-driven editing. It is intended as a reference for implementing the next iterations, not a claim that every proposed system already exists. Earlier implementation sketches, design rationale, examples and acceptance criteria are preserved even when later implementation records supersede the initial wording. The README remains the practical build/run guide. [Performance monitoring and diagnostics](performance.md) is the dedicated specification for measuring audio reliability, latency, DSP load/headroom, editor responsiveness, compilation and benchmark regressions (Phase 7.3).

## 1. Goals and current baseline

### Motivation

Orbit is a browser-based environment for designing synthesizers, drones, modulation systems and musical patches. The original React application provides inspiration for the graph editor, but Orbit 2 is intended to avoid tight coupling between the presentation, application state and real-time audio processing.

The goal is a modular architecture in which new generators, processors, parameter types and composite modules can be added without rewriting the rest of the application.

### Technology choices

| Technology | Responsibility |
|---|---|
| Blazor WebAssembly / C# | Browser UI, graph model, editing, validation and application services |
| Faust | Sample-level sound synthesis and DSP algorithms |
| WebAssembly | Execution format for compiled Faust DSP |
| Web Audio API / AudioWorklet | Real-time audio scheduling and output |
| Minimal JavaScript modules | Browser audio integration and lifecycle; not application business logic |
| GitHub Actions / Pages | Build, test and deployment of the static app |

**Original baseline (historical, Phases 1–2).** The initial implementation included the independent Orbit.Domain graph model (Phase 1) and a continuously running, fixed two-sine modulation graph (Phase 2). The browser bridge, build-time Faust compilation and separated application/UI/infrastructure projects support this playback path. Existing infrastructure concepts include AudioEngine, AudioContextManager, AudioClock, FaustModuleLoader, FaustNodeFactory, AudioVoice, AudioOutput and AudioEnvelope.

**Original next stage — now delivered in Phases 3–4:** validated commands and application events, then a reusable graph editor and general DSP compilation. The active modulation playback path and shared infrastructure provide the baseline.

### Design principles

1. Separation of concerns: UI, graph state, application commands and real-time DSP have different responsibilities.
2. Composition over specialization: a drone is a reusable graph, not a special monolithic synth class.
3. Depend on stable abstractions and use dependency injection at composition roots.
4. Generators and processors must not know which node is connected before or after them.
5. Modulation is a first-class feature of all appropriate continuous numerical parameters.
6. Every signal uses the same authoritative audio timing reference.
7. User-interface operations must not interrupt sound unnecessarily.
8. A new node should normally require a definition, a DSP implementation and registration, not changes to the graph editor's core.
9. Use descriptive names, small cohesive classes, and separate source files for independent objects.
10. Validate graph changes, preserve consistent state and test foundational behavior before adding numerous node types.

## Find the context for your task

Start here. Read the relevant module(s), source and tests; follow additional references as needed. This index is a discovery aid, not a limit on exploration. For projects spanning layers, combine the relevant modules. For a novel subsystem, inspect the existing architecture and code, make a reasonable design consistent with the principles above (or propose an explicit change), and create or extend documentation when the topic merits it. No matching document is required to begin work.

Domain owns graph primitives and validation; Application owns commands, immutable snapshots and services; Presentation owns layout and editor projections; Audio owns Faust compilation and DSP; Infrastructure owns browser integrations; UI composes them. See [project structure](docs/core/project-structure.md) for dependency direction. Read [ROADMAP.md](ROADMAP.md) for current status and priority; the detailed modules preserve both implemented contracts and future designs.

| Task | Suggested starting points |
|---|---|
| New node, connection or parameter | [Graph model](docs/core/graph-model.md), [signals/ports](docs/core/signals-and-ports.md), [modulation](docs/core/parameters-and-modulation.md), [DSP](docs/audio/dsp-engine.md) |
| Playback, clicking, latency | [DSP engine](docs/audio/dsp-engine.md), [performance index](performance.md), [transport](docs/audio/transport-and-timing.md) |
| Canvas, node frame or UI | [Canvas](docs/editor/canvas.md), [design system](docs/editor/design-system.md), relevant feature module |
| Composite routing | [Composite nodes](docs/editor/composite-nodes.md), [signals/ports](docs/core/signals-and-ports.md), [commands](docs/core/commands-and-events.md) |
| Reusing patches, grouping nodes into composites, custom module library and macros | [Reusable modules](docs/editor/reusable-modules.md), [composite nodes](docs/editor/composite-nodes.md), [persistence](docs/editor/persistence-and-history.md) |
| Plots and tap capacity | [Visualizations](docs/editor/visualizations.md), [signal observation](docs/audio/signal-observation.md) |
| Keyboard, sequencing and instruments | [Note Sequencer](docs/instruments/note-sequencer.md), [pitch control](docs/instruments/pitch-control.md), [envelopes](docs/audio/envelopes.md), [transport](docs/audio/transport-and-timing.md) |
| Generative music, ambient patches and whole-piece arrangement | [Generative ambient composition](docs/examples/generative-ambient-composition.md); for individual sounds also see [instrument recipes](docs/examples/instrument-recipes.md) and [effects](docs/audio/effects.md) |
| Harmony, tuning, timbre, musical quality, perceived loudness and spatial depth | [Music theory and sound design](docs/examples/music-theory-and-sound-design.md); use with the relevant instrument/effect or composition guide |
| Genre-specific music, electronic style, rhythm, sound palette and construction | [Electronic music genres](docs/examples/electronic-music-genres.md) routes to focused ambient, bass/breaks, dance and experimental families; [musical techniques](docs/examples/genres/orbit-techniques.md) connects style to current nodes |
| Hollow/basic timbres, prominent raw noise, finished instruments and reusable sound palette | [Sound quality and voice design](docs/examples/sound-quality-and-voice-design.md); use with instrument recipes and the relevant node contracts |
| Mix balance, mastering, loudness measurements, audio render analysis and iteration | [Mixing, mastering and analysis](docs/examples/mixing-mastering-and-analysis.md); [signal observation](docs/audio/signal-observation.md) for native measurement contracts |
| Save/load, undo/redo, presets | [Persistence](docs/editor/persistence-and-history.md), [commands](docs/core/commands-and-events.md), [node presets](docs/editor/node-presets.md) |
| Notes, aliases, visual groups, patch overview, documentation visibility or outline | [Patch annotations](docs/editor/patch-annotations.md), [canvas](docs/editor/canvas.md), [persistence](docs/editor/persistence-and-history.md) |
| Roadmap or feature priority | [Roadmap](ROADMAP.md); open only the relevant milestone register if needed |

<a id="contents"></a>

## Detailed documentation

**Core model and boundaries**

- <a id="2-fundamental-concepts"></a>[graph model](docs/core/graph-model.md)
- <a id="3-signals-and-node-contracts"></a><a id="5-ports-and-connections"></a>[signals and ports](docs/core/signals-and-ports.md)
- <a id="4-parameters-and-modulation"></a>[parameters and modulation](docs/core/parameters-and-modulation.md)
- <a id="9-commands-events-and-transaction-boundaries"></a>[commands and events](docs/core/commands-and-events.md)
- <a id="11-project-organization-and-dependencies"></a>[project structure](docs/core/project-structure.md)
- <a id="15-decisions-and-open-questions"></a><a id="to-decide-during-implementation"></a>[decisions](docs/core/decisions.md)
- <a id="16-references-and-glossary"></a>[references and glossary](docs/core/references-and-glossary.md)

**Audio engine**

- <a id="10-faust-and-real-time-execution"></a><a id="runtime-foundations-and-safety"></a>[dsp engine](docs/audio/dsp-engine.md)
- <a id="6-audio-time-oscillator-phase-and-transport"></a>[transport and timing](docs/audio/transport-and-timing.md)
- [oscillators and noise](docs/audio/oscillators-and-noise.md)
- <a id="algorithmic-reverb-phase-614"></a><a id="audio-processors-and-effects"></a><a id="chorus-phase-615"></a><a id="feedback-delay-phase-613"></a><a id="parametric-eq-phase-612"></a>[effects](docs/audio/effects.md)
- <a id="pitch-shifter-phase-620-planned"></a>[pitch shifter](docs/audio/pitch-shifter.md)
- <a id="envelope-generators-and-rhythmic-triggers-planned-phase-71"></a><a id="musical-control-performance-and-instrument-synthesis"></a>[envelopes](docs/audio/envelopes.md)
- <a id="signal-observation-and-analysis-phase-618"></a><a id="signal-observation-and-analysis-planned-phase-618"></a>[signal observation](docs/audio/signal-observation.md)
- <a id="sample-based-synthesis-and-audio-assets-planned-phase-8"></a>[sample assets](docs/audio/sample-assets.md)

**Editor and persistence**

- [canvas](docs/editor/canvas.md)
- <a id="7-hierarchical-signal-graphs-and-canvases"></a><a id="8-input-and-output-racks"></a>[composite nodes](docs/editor/composite-nodes.md)
- [reusable composite modules](docs/editor/reusable-modules.md) — selection-to-composite, whole-patch conversion, custom module library, portable definitions and macro controls (planned Phase 7.4)
- <a id="shared-canvas-and-component-standards"></a>[design system](docs/editor/design-system.md)
- <a id="signal-analysis-and-visualization-planned-phase-618"></a>[visualizations](docs/editor/visualizations.md)
- [node presets](docs/editor/node-presets.md)
- <a id="workspace-persistence-and-history-phases-68611"></a>[persistence and history](docs/editor/persistence-and-history.md)
- <a id="node-mute-and-processor-bypass-phase-616"></a>[audition](docs/editor/audition.md)
- <a id="connection-amount-badges-and-inline-editing-planned-phase-6195"></a>[connection visualization](docs/editor/annotations-and-connections.md)
- <a id="patch-documentation-and-canvas-annotations-planned-phase-619"></a>[patch annotations](docs/editor/patch-annotations.md) — note cards, node/wire descriptions and visual groups (6.19.1–6.19.3 implemented), patch metadata and library search (6.19.4 in review), visibility and nested outline (planned)

**Instruments and examples**

- [pitch control](docs/instruments/pitch-control.md)
- <a id="keyboard-performance-and-continuous-pitch-sequencer-phase-728--planned"></a><a id="keyboard-performance-and-continuous-pitch-sequencer-planned-phase-728"></a>[note sequencer](docs/instruments/note-sequencer.md)
- [note sequencer design](docs/instruments/note-sequencer-design.md)
- [physical modelling](docs/instruments/physical-modelling.md)
- [polyphony](docs/instruments/polyphony.md)
- <a id="musical-instrument-synthesis-and-physical-modelling-planned-phase-72"></a>[instrument recipes](docs/examples/instrument-recipes.md)
- <a id="12-worked-patch-example"></a>[drone](docs/examples/drone.md)
- [generative ambient composition](docs/examples/generative-ambient-composition.md) — musical layers, long-form arrangement and a worked multi-voice patch
- [music theory and electronic sound design](docs/examples/music-theory-and-sound-design.md) — interval/scale/chord recipes, voice leading, articulation, psychoacoustics, mix/depth and quality checks
- [electronic music genres](docs/examples/electronic-music-genres.md) — modular genre vocabulary, composition approaches and reusable Orbit 2 techniques
- [sound quality and voice design](docs/examples/sound-quality-and-voice-design.md) — transform noise, develop richer tonal voices and audition candidate preset recipes
- [mixing, mastering and analysis](docs/examples/mixing-mastering-and-analysis.md) — role balance, peak/LUFS/LRA interpretation, mastering limits, render–measure–refine and a worked feedback example

**Quality and delivery**

- <a id="14-testing-and-acceptance-criteria"></a>[testing](docs/quality/testing.md)
- [performance](performance.md)
- <a id="13-implementation-roadmap"></a><a id="roadmap-at-a-glance"></a><a id="reading-guide-and-current-implementation"></a>[ROADMAP](ROADMAP.md)

**Detailed milestone registers**

- <a id="phase-1--domain-primitives-graph-ownership-and-tests"></a><a id="phase-2--shared-timing-and-first-sample-rate-modulation"></a><a id="phase-3--commands-and-event-dispatch"></a><a id="phase-4--flat-canvas-and-dynamic-graph-compiler"></a><a id="phase-5--composite-nodes-and-rack-interfaces"></a>[foundation](docs/roadmap/foundation.md)
- <a id="phase-618--signal-analysis-and-visualization-planned"></a><a id="phase-619--patch-documentation-and-canvas-organization-planned"></a>[editor and effects](docs/roadmap/editor-and-effects.md)
- <a id="phase-7--advanced-functionality-and-performance"></a><a id="phase-71--envelope-generators-and-rhythmic-triggers-planned"></a><a id="phase-72--musical-instrument-synthesis-and-physical-modelling-planned"></a>[advanced](docs/roadmap/advanced.md)
