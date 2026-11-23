# Architecture task routing and detailed documentation

[Architecture overview](overview.md) · [Documentation map](../README.md) · [Roadmap](../roadmap/overview.md)

## Find the context for your task

For a complete cross-area directory, start at the [documentation map](../README.md). This architecture file remains the stable technical routing overview, not another roadmap.

For hands-on workflows and concrete examples, start with the [user and agent usage guide](../usage/README.md). Each page links the authoritative feature contracts and actual implementation PRs; use it alongside the task routing below, not as a constraint on exploration.

For creating, analyzing or refining music, start with the [music creation index](../examples/README.md): routes by task and production stage, including the [sound-design foundations catalogue](../examples/foundations/catalogue.md) of reproducible references and evidence.

Start here. Read the relevant module(s), source and tests; follow additional references as needed. This index is a discovery aid, not a limit on exploration. For projects spanning layers, combine the relevant modules. For a novel subsystem, inspect the existing architecture and code, make a reasonable design consistent with the principles above (or propose an explicit change), and create or extend documentation when the topic merits it. No matching document is required to begin work.

Domain owns graph primitives and validation; Application owns commands, immutable snapshots and services; Presentation owns layout and editor projections; Audio owns Faust compilation and DSP; Infrastructure owns browser integrations; UI composes them. See [project structure](../core/project-structure.md) for dependency direction. Read [ROADMAP.md](../roadmap/overview.md) for current status and priority; the detailed modules preserve both implemented contracts and future designs.

| Task | Suggested starting points |
|---|---|
| New code, bug fix, refactoring or code review | [Coding standards and implementation quality](../quality/coding-standards.md), [testing](../quality/testing.md), and the relevant feature contract |
| New node, connection or parameter | [Graph model](../core/graph-model.md), [signals/ports](../core/signals-and-ports.md), [modulation](../core/parameters-and-modulation.md), [DSP](../audio/dsp-engine.md) |
| Stereo routing, panning, oscillator unison | [Stereo audio, panning and oscillator unison](../audio/stereo-panning-and-unison.md) — planned Phase 7.5; prioritize the Phase 7.3 performance baseline first |
| Playback, clicking, latency | [DSP engine](../audio/dsp-engine.md), [performance index](../quality/performance.md), [transport](../audio/transport-and-timing.md) |
| Canvas, node frame or UI | [User interaction/gesture registry](../editor/user-interactions.md) (mandatory when changing inputs), [Canvas](../editor/canvas.md), [contextual canvas actions](../editor/canvas.md#contextual-canvas-actions), [design system](../editor/design-system.md), [canvas-first workspace and node library (6.21.1–6.21.3 implemented)](../roadmap/editor-and-effects.md#phase-621--canvas-first-workspace-and-node-library-planned), relevant feature module |
| Composite routing | [Composite nodes](../editor/composite-nodes.md), [signals/ports](../core/signals-and-ports.md), [commands](../core/commands-and-events.md) |
| Reusing patches, grouping nodes into composites, custom module library and macros | [Reusable modules](../editor/reusable-modules.md), [composite nodes](../editor/composite-nodes.md), [persistence](../editor/persistence-and-history.md) |
| Plots and tap capacity | [Visualizations](../editor/visualizations.md), [signal observation](../audio/signal-observation.md) |
| Keyboard, sequencing and instruments | [Note Sequencer](../instruments/note-sequencer.md), [pitch control](../instruments/pitch-control.md), [envelopes](../audio/envelopes.md), [transport](../audio/transport-and-timing.md) |
| Generative music, ambient patches and whole-piece arrangement | [Generative ambient composition](../examples/generative-ambient-composition.md); for individual sounds also see [instrument recipes](../examples/instrument-recipes.md) and [effects](../audio/effects.md) |
| Musical monotony, melody/groove changes, distinct choruses/drops and section development | [Compositional development](../examples/compositional-development.md) — micro/meso/macro, score-level contrast, evaluation and listener feedback |
| Harmony, tuning, timbre, musical quality, perceived loudness and spatial depth | [Music theory and sound design](../examples/music-theory-and-sound-design.md); use with the relevant instrument/effect or composition guide |
| Scales, chord construction, consonance, historical tuning and wolf intervals, functional harmony, modulation and voice leading | [Harmony, scales and temperament](../examples/harmony-scales-and-temperament.md) — research, interval math, chord/voicing and progression examples |
| Melody writing, historically informed classical craft, contrapuntal and keyboard composition | [Classical composition and melody](../examples/classical-composition-and-melody.md) — Bach/Mozart repertoire study, partimenti, phrase forms, score development and Orbit realization |
| Genre-specific music, electronic style, rhythm, sound palette and construction | [Electronic music genres](../examples/electronic-music-genres.md) routes to focused ambient, bass/breaks, dance and experimental families; [musical techniques](../examples/genres/orbit-techniques.md) connects style to current nodes |
| Hollow/basic timbres, prominent raw noise, finished instruments and reusable sound palette | [Sound quality and voice design](../examples/sound-quality-and-voice-design.md); use with instrument recipes and the relevant node contracts |
| Realistic acoustic/electromechanical instruments, Faust physical models, organs and rotary effect | [Faust instrument nodes](../instruments/faust-instrument-nodes.md) — actual new node contracts and examples; [Faust-first instrument design](../instruments/realistic-instrument-emulation.md) — model candidates, recipes, audit and roadmap |
| Instrument and sound design, repeatable audition and A/B testing across genres | [Instrument and sound design — repeatable audition](../examples/instrument-design-and-audition.md) — Prism Rush listener feedback and measurements, [TEST Melody Driver v1](../examples/instrument-audition/reference.orbit2.json), adaptable test profiles, candidate palette, usage and Phase 7.6 |
| Evolving sounds, emergent harmonics, interacting modulation and breathing voices | [Sound-design foundations](../examples/foundations/catalogue.md) — reference graphs, analysis, listener feedback, evidence statuses and maintenance |
| Mix balance, mastering, loudness measurements, audio render analysis and iteration | [Mixing, mastering and analysis](../examples/mixing-mastering-and-analysis.md); [signal observation](../audio/signal-observation.md) for native measurement contracts |
| Relative instrument/voice levels, over-loud or masked melodies, volume automation and balance validation | [Contextual layer balance](../examples/contextual-layer-balance.md) — research, practical level rides, aligned windowed role analysis and measured case study |
| Save/load, undo/redo, presets | [Persistence](../editor/persistence-and-history.md), [commands](../core/commands-and-events.md), [node presets](../editor/node-presets.md) |
| Notes, aliases, visual groups, patch overview, documentation visibility or outline | [Patch annotations](../editor/patch-annotations.md), [canvas](../editor/canvas.md), [persistence](../editor/persistence-and-history.md) |
| Roadmap or feature priority | [Roadmap](../roadmap/overview.md); open only the relevant milestone register if needed |

<a id="contents"></a>

## Detailed documentation

**Core model and boundaries**

- <a id="2-fundamental-concepts"></a>[graph model](../core/graph-model.md)
- <a id="3-signals-and-node-contracts"></a><a id="5-ports-and-connections"></a>[signals and ports](../core/signals-and-ports.md)
- <a id="4-parameters-and-modulation"></a>[parameters and modulation](../core/parameters-and-modulation.md)
- <a id="9-commands-events-and-transaction-boundaries"></a>[commands and events](../core/commands-and-events.md)
- <a id="11-project-organization-and-dependencies"></a>[project structure](../core/project-structure.md)
- <a id="15-decisions-and-open-questions"></a><a id="to-decide-during-implementation"></a>[decisions](../core/decisions.md)
- <a id="16-references-and-glossary"></a>[references and glossary](../core/references-and-glossary.md)

**Audio engine**

- <a id="10-faust-and-real-time-execution"></a><a id="runtime-foundations-and-safety"></a>[dsp engine](../audio/dsp-engine.md)
- <a id="6-audio-time-oscillator-phase-and-transport"></a>[transport and timing](../audio/transport-and-timing.md)
- [oscillators and noise](../audio/oscillators-and-noise.md)
- [stereo audio, panning and oscillator unison](../audio/stereo-panning-and-unison.md) — Phase 7.5 planned; mono unison, stereo channel routing, modulated pan, stereo voice width and effects; performance prerequisites
- <a id="algorithmic-reverb-phase-614"></a><a id="audio-processors-and-effects"></a><a id="chorus-phase-615"></a><a id="feedback-delay-phase-613"></a><a id="parametric-eq-phase-612"></a>[effects](../audio/effects.md)
- <a id="pitch-shifter-phase-620-planned"></a>[pitch shifter](../audio/pitch-shifter.md)
- <a id="envelope-generators-and-rhythmic-triggers-planned-phase-71"></a><a id="musical-control-performance-and-instrument-synthesis"></a>[envelopes](../audio/envelopes.md)
- <a id="signal-observation-and-analysis-phase-618"></a><a id="signal-observation-and-analysis-planned-phase-618"></a>[signal observation](../audio/signal-observation.md)
- <a id="sample-based-synthesis-and-audio-assets-planned-phase-8"></a>[sample assets](../audio/sample-assets.md)

**Editor and persistence**

- [canvas](../editor/canvas.md)
- [Canvas-first workspace and node library](../roadmap/editor-and-effects.md#phase-621--canvas-first-workspace-and-node-library-planned) — Phase 6.21.1 responsive mobile shell implemented ([PR #60](https://github.com/wgabel/orbit2/pull/60)); 6.21.2 contextual panels implemented ([PR #61](https://github.com/wgabel/orbit2/pull/61)); 6.21.3 categorized icon/color library implemented ([PR #62](https://github.com/wgabel/orbit2/pull/62)); 6.21.4 assisted cable dragging implemented ([PR #65](https://github.com/wgabel/orbit2/pull/65)), gesture-only navigation in review ([PR #69](https://github.com/wgabel/orbit2/pull/69)); remaining touch gestures and auto-expanding/pinnable compact nodes; measurable canvas-area/performance checks; fullscreen deferred
- <a id="7-hierarchical-signal-graphs-and-canvases"></a><a id="8-input-and-output-racks"></a>[composite nodes](../editor/composite-nodes.md)
- [reusable composite modules](../editor/reusable-modules.md) — selection-to-composite, single-level expansion, whole-patch conversion, custom module library, portable definitions and macro controls (Phase 7.4; selection and grouping implemented, ungrouping in review)
- <a id="shared-canvas-and-component-standards"></a>[design system](../editor/design-system.md)
- <a id="signal-analysis-and-visualization-planned-phase-618"></a>[visualizations](../editor/visualizations.md)
- [node presets](../editor/node-presets.md)
- <a id="workspace-persistence-and-history-phases-68611"></a>[persistence and history](../editor/persistence-and-history.md)
- <a id="node-mute-and-processor-bypass-phase-616"></a>[audition](../editor/audition.md)
- <a id="connection-amount-badges-and-inline-editing-planned-phase-6195"></a>[connection visualization](../editor/annotations-and-connections.md)
- <a id="patch-documentation-and-canvas-annotations-planned-phase-619"></a>[patch annotations](../editor/patch-annotations.md) — note cards, node/wire descriptions, visual groups, patch metadata and library search (6.19.1–6.19.4 implemented), annotation visibility and zoom detail (6.19.7 implemented), nested outline (6.19.8 implemented)

**Instruments and examples**

- [pitch control](../instruments/pitch-control.md)
- <a id="keyboard-performance-and-continuous-pitch-sequencer-phase-728--planned"></a><a id="keyboard-performance-and-continuous-pitch-sequencer-planned-phase-728"></a>[note sequencer](../instruments/note-sequencer.md)
- [note sequencer design](../instruments/note-sequencer-design.md)
- [physical modelling](../instruments/physical-modelling.md)
- [polyphony](../instruments/polyphony.md)
- <a id="musical-instrument-synthesis-and-physical-modelling-planned-phase-72"></a>[instrument recipes](../examples/instrument-recipes.md)
- <a id="12-worked-patch-example"></a>[drone](../examples/drone.md)
- [generative ambient composition](../examples/generative-ambient-composition.md) — musical layers, long-form arrangement and a worked multi-voice patch
- [compositional development](../examples/compositional-development.md) — development of musical material and form beyond changes to instrumentation; listener feedback case
- [music theory and electronic sound design](../examples/music-theory-and-sound-design.md) — interval/scale/chord recipes, voice leading, articulation, psychoacoustics, mix/depth and quality checks
- [electronic music genres](../examples/electronic-music-genres.md) — modular genre vocabulary, composition approaches and reusable Orbit 2 techniques
- [classic synthesizer patch recipes](../examples/synthesizer-patch-recipes.md) — eight user-supplied reference voice designs; palette selection, translation and verification guidance
- [sound quality and voice design](../examples/sound-quality-and-voice-design.md) — transform noise, develop richer tonal voices and audition candidate preset recipes
- [music creation index](../examples/README.md) — task routing and end-to-end workflow for music
- [sound-design foundations](../examples/foundations/catalogue.md) — reusable, evidence-labeled reference graphs and maintenance; first entry: [breathing harmonic drone](../examples/foundations/breathing-harmonic-drone.md)
- [mixing, mastering and analysis](../examples/mixing-mastering-and-analysis.md) — role balance, peak/LUFS/LRA interpretation, mastering limits, render–measure–refine and a worked feedback example
- [contextual layer balance](../examples/contextual-layer-balance.md) — relative levels, masking, research-backed mixing decisions, paired A/B validation and the Submerged Relay case

**Quality and delivery**

- [coding standards and implementation quality](../quality/coding-standards.md)
- <a id="14-testing-and-acceptance-criteria"></a>[testing](../quality/testing.md)
- [performance](../quality/performance.md)
- <a id="13-implementation-roadmap"></a><a id="roadmap-at-a-glance"></a><a id="reading-guide-and-current-implementation"></a>[ROADMAP](../roadmap/overview.md)

**Detailed milestone registers**

- <a id="phase-1--domain-primitives-graph-ownership-and-tests"></a><a id="phase-2--shared-timing-and-first-sample-rate-modulation"></a><a id="phase-3--commands-and-event-dispatch"></a><a id="phase-4--flat-canvas-and-dynamic-graph-compiler"></a><a id="phase-5--composite-nodes-and-rack-interfaces"></a>[foundation](../roadmap/foundation.md)
- <a id="phase-618--signal-analysis-and-visualization-planned"></a><a id="phase-619--patch-documentation-and-canvas-organization-planned"></a>[editor and effects](../roadmap/editor-and-effects.md)
- <a id="phase-7--advanced-functionality-and-performance"></a><a id="phase-71--envelope-generators-and-rhythmic-triggers-planned"></a><a id="phase-72--musical-instrument-synthesis-and-physical-modelling-planned"></a>[advanced](../roadmap/advanced.md)
