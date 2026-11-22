# Orbit 2 — Implementation roadmap

[Architecture overview](ORBIT2-ARCHITECTURE.md)

## Reading guide and current implementation

**Status vocabulary:** Implemented = verified on the audited main branch or in an explicitly identified implementation PR; Partial = a defined subset delivered; In progress = implementation underway; Planned = design or future deliverable; Deferred = deliberately outside current scope.

**Implementation traceability (mandatory):** Every delivered feature, improvement and bug fix must cite its actual implementation pull request(s) using `[PR #N](https://github.com/wgabel/orbit2/pull/N)` directly in the relevant implementation description and roadmap entry, and in the status table when applicable. Include follow-up PRs beside the affected feature. Open the PR first if its number is not known, then update the architecture on that PR's branch before merge. Never substitute a documentation-only PR for runtime evidence or mark unmerged work as merged. Older direct-to-main implementations predating the PR workflow have no PR number; cite the actual commit when extending their implementation record. Do not invent historical PRs.

| Capability | Current status | Design / evidence |
|---|---|---|
| Graph, validated commands, nested racks, dynamic Faust compilation, live parameter updates | Implemented · Phases 1–5 | [Foundation register](docs/roadmap/foundation.md) |
| Shared BPM, oscillators, noise, mixer, filter, EQ, delay, reverb, chorus | Implemented · 6.1–6.7, 6.12–6.15 | [Timing](docs/audio/transport-and-timing.md), [generators](docs/audio/oscillators-and-noise.md), [effects](docs/audio/effects.md) |
| Recovery, named patches, portable JSON, undo/redo | Implemented · 6.8–6.11 | [Persistence](docs/editor/persistence-and-history.md) |
| Mute/bypass and individual presets | Implemented · 6.16–6.17 | [Audition](docs/editor/audition.md), [presets](docs/editor/node-presets.md) |
| Scope, spectrum, level and filter/EQ response | Implemented · 6.18.1–6.18.5; dedicated analyzer and advanced history planned | [Visualizations](docs/editor/visualizations.md), [observation](docs/audio/signal-observation.md); [PR #4](https://github.com/wgabel/orbit2/pull/4) |
| Demand-driven signal tap allocation | In review · 6.18.8 | [Observation](docs/audio/signal-observation.md); [PR #15](https://github.com/wgabel/orbit2/pull/15) (design), [PR #20](https://github.com/wgabel/orbit2/pull/20) (implementation) |
| Cable badges, per-canvas preferences, anchored editing | Implemented core · 6.19.5; optional refinements planned | [Connection visualization](docs/editor/annotations-and-connections.md); [PR #1](https://github.com/wgabel/orbit2/pull/1), [#2](https://github.com/wgabel/orbit2/pull/2), [#3](https://github.com/wgabel/orbit2/pull/3) |
| Patch documentation: notes, aliases, cable descriptions, visual groups, overview, annotation visibility and outline | Notes, descriptions, groups and overview implemented · 6.19.1–6.19.4; visibility/zoom detail in review · 6.19.7; outline planned · 6.19.8 | [Patch annotations](docs/editor/patch-annotations.md); [PR #27](https://github.com/wgabel/orbit2/pull/27), [PR #28](https://github.com/wgabel/orbit2/pull/28), [PR #30](https://github.com/wgabel/orbit2/pull/30), [PR #32](https://github.com/wgabel/orbit2/pull/32), [PR #35](https://github.com/wgabel/orbit2/pull/35) |
| Shared semitone tracking and Pitch Control | Implemented · 7.2.2 | [Pitch control](docs/instruments/pitch-control.md); [PR #6](https://github.com/wgabel/orbit2/pull/6) |
| Five sustained instrument composites | Implemented · 7.2.1 | [Instrument recipes](docs/examples/instrument-recipes.md); [PR #31](https://github.com/wgabel/orbit2/pull/31) |
| Single damped-mode Resonator | In review · 7.2.4 | [Physical modelling](docs/instruments/physical-modelling.md); [PR #34](https://github.com/wgabel/orbit2/pull/34) |
| AD envelope and typed Trigger/Gate/Clock ports/adapters | Implemented · 7.1.1–7.1.2 | [Envelopes](docs/audio/envelopes.md); [PR #7](https://github.com/wgabel/orbit2/pull/7), [#8](https://github.com/wgabel/orbit2/pull/8) |
| Manual Trigger, timed Gate and Clock sources | Implemented · 7.1.3 | [Envelopes](docs/audio/envelopes.md); [PR #10](https://github.com/wgabel/orbit2/pull/10) |
| AR/ADSR/Loop envelope modes | Implemented · 7.1.4 | [Envelopes](docs/audio/envelopes.md); [PR #12](https://github.com/wgabel/orbit2/pull/12) |
| Envelope curve, measured output and live stage | Implemented · 7.1.5 | [Envelopes](docs/audio/envelopes.md); [PR #14](https://github.com/wgabel/orbit2/pull/14) |
| Live keyboard and pitch pattern sequencer | Implemented · 7.2.8a–c | [Note Sequencer](docs/instruments/note-sequencer.md); [PR #16](https://github.com/wgabel/orbit2/pull/16), [PR #17](https://github.com/wgabel/orbit2/pull/17), [PR #19](https://github.com/wgabel/orbit2/pull/19) |
| Composite boundary visibility | Implemented · 6.19.6 | [Composite nodes](docs/editor/composite-nodes.md); [PR #18](https://github.com/wgabel/orbit2/pull/18) |
| Reusable custom composite modules, grouping, patch-to-module conversion, macros | Planned · 7.4; existing additive selection and built-in composite factories are foundations | [Reusable modules](docs/editor/reusable-modules.md) |
| Playable note source, Note-mode articulation, resonators, polyphony | Planned · remaining Phase 7 | [Sequencer design](docs/instruments/note-sequencer-design.md), [physical modelling](docs/instruments/physical-modelling.md), [polyphony](docs/instruments/polyphony.md); [PR #9](https://github.com/wgabel/orbit2/pull/9) is design documentation only |
| Post-mixer pitch shifter; recorded audio assets and sample synthesis | Planned · 6.20, Phase 8 | [Pitch shifter](docs/audio/pitch-shifter.md), [audio assets](docs/audio/sample-assets.md) |

Use the [roadmap](#13-implementation-roadmap) for delivery status, milestone identifiers and dependencies. Use the relevant architecture sections for implementation contracts and acceptance criteria. Where a design sketch and a later implementation record coexist, follow the latter for the current state.

## 13. Implementation roadmap

Do not attempt to build the entire modular synth in one change. Establish and test each foundation.

### Roadmap at a glance

Keep identifiers stable even when independent milestones ship out of numerical order. The following is a recommended dependency-aware delivery order, not a deadline or a requirement to finish every earlier number.

| Track | Delivered now | Next work |
|---|---|---|
| Foundation and editor | 1–5; 6.1–6.17 | Deferred version-aware reload / additional graph reuse |
| Reusable modules | Composite graphs, built-in composite factories, additive selection, leaf preset library | 7.4.1–7.4.5 selection/grouping, independent module library, whole-patch reuse and portable module JSON; 7.4.6 macros; optional 7.4.7 linked versions |
| Visualization | 6.18.1–6.18.5 | 6.18.8 dynamic signal taps in review ([PR #20](https://github.com/wgabel/orbit2/pull/20)); then 6.18.6 analyzer sink, 6.18.7 advanced comparisons and profiling |
| Patch organization | 6.19.1 note cards ([PR #27](https://github.com/wgabel/orbit2/pull/27)); 6.19.2 descriptions ([PR #28](https://github.com/wgabel/orbit2/pull/28)); 6.19.3 visual groups ([PR #30](https://github.com/wgabel/orbit2/pull/30)); 6.19.4 overview/discovery ([PR #32](https://github.com/wgabel/orbit2/pull/32)); 6.19.5 core; 6.19.6 boundary visibility | Review 6.19.7 annotation visibility/zoom detail ([PR #35](https://github.com/wgabel/orbit2/pull/35)); 6.19.8 patch outline; optional badge refinements |
| Rhythmic articulation | 7.1.1–7.1.5 | 7.1.6–7.1.7 |
| Keyboard and sequencing | 7.2.2 shared pitch; 7.2.8a–c live pitch, loops and recording ([PR #19](https://github.com/wgabel/orbit2/pull/19)) | 7.2.3 and 7.2.8d for event/gated articulation |
| Instruments | Existing oscillator/effect capabilities and AD impact preset; 7.2.1 five sustained instrument templates ([PR #31](https://github.com/wgabel/orbit2/pull/31)); 7.2.4 resonator in review ([PR #34](https://github.com/wgabel/orbit2/pull/34)) | 7.2.5 modal synthesis, 7.2.6 playable templates/polyphony, 7.2.7 physical models |
| Standalone audio transposition | Design only | 6.20.1–6.20.2; independent of keyboard tracking |
| Performance and diagnostics | Existing clock, playback lifecycle and signal observation foundations | 7.3.1–7.3.4 passive monitor, compilation/UI metrics and profiling; 7.3.5 musical timing; 7.3.6 reports and regressions ([specification](performance.md)) |
| Audio assets | Design only | Phase 8 |

**Immediate priority:** review and merge the signal tap allocation regression fix (6.18.8, [PR #20](https://github.com/wgabel/orbit2/pull/20)) before continuing feature work. A single visible plot on a reachable signal must work even in a large patch; the capacity must limit simultaneous observed signals, not the size of the graph. This fix is independent of the analyzer sink and Phase 7.3.

The most direct drone-performance path is 7.2.2 and 7.2.8a–b (delivered) → 7.2.8c (delivered, [PR #19](https://github.com/wgabel/orbit2/pull/19)). Gated instruments depend on rhythmic sources, ADSR and monophonic note delivery. Presentation-only annotations, dedicated analysis and the post-mixer pitch shifter can proceed independently.

**Detailed milestone register.** The following retains the original deliverables and implementation records, including earlier milestones for regression and architectural context.


- [Phases 1–6.15: foundations, timing, sound generation, workspace and effects](docs/roadmap/foundation.md)
- [Phases 6.16–6.20: audition, presets, plots, organization and pitch shifting](docs/roadmap/editor-and-effects.md)
- [Phases 7–8: envelopes, musical control, instruments, performance, reusable modules and samples](docs/roadmap/advanced.md)
