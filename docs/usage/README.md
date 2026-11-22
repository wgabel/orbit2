# Using Orbit 2 — user and agent guide

[Repository README](../../README.md) · [Architecture](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

**Audited:** main at [`f66fe0a`](https://github.com/wgabel/orbit2/commit/f66fe0a), through [PR #38](https://github.com/wgabel/orbit2/pull/38). Audit and guide introduced by [PR #40](https://github.com/wgabel/orbit2/pull/40). This guide is about features available in that source revision. A previously deployed browser build may be older. Examples are starting points, not required musical recipes; technical contracts remain in the linked modules. No new nodes or engine functionality are introduced by this guide.

## Choose what you want to do

| Goal | Read this first |
| --- | --- |
| Create, connect, play or save a patch; understand cables, composites and racks | [Patch editor — first patch and routing](patch-editor.md#1-build-and-play-a-patch) |
| Organize a large graph; add notes, aliases, groups, overview; hide or reveal annotations; count nested nodes | [Patch editor — organization and visibility](patch-editor.md#3-document-and-organize-a-patch) |
| Understand plots, frequency responses, live levels and unavailable taps | [Patch editor — inspecting signals](patch-editor.md#4-inspect-and-troubleshoot-signals) |
| Make notes articulate individually; use keyboard, patterns, clock, gate and envelopes | [Sound and instruments — pitch and articulation](sound-and-instruments.md#1-pitch-versus-articulation) |
| Use organ, strings, flute, brass, didgeridoo, impact, handpan, bell, gong and wood percussion | [Sound and instruments — instruments](sound-and-instruments.md#3-built-in-instruments-and-resonators) |
| Design evolving drones or an entire piece | [Generative ambient composition](../examples/generative-ambient-composition.md), [breathing harmonic drone](../examples/foundations/breathing-harmonic-drone.md), [genre index](../examples/electronic-music-genres.md) |
| Improve timbre, layer balance and final presentation | [Voice design](../examples/sound-quality-and-voice-design.md), [music theory](../examples/music-theory-and-sound-design.md), [production workflow](../examples/mixing-mastering-and-analysis.md), [contextual layer balance](../examples/contextual-layer-balance.md) |

## Recent features and earlier implementation trail

Use this as a discovery index, not a substitute for the feature contracts or roadmap. The links are actual merged implementation PRs except the explicitly identified documentation changes. The earlier direct-to-main foundations predate the PR register; see [foundation register](../roadmap/foundation.md).

| Feature | Where to find practical instructions | Evidence |
| --- | --- | --- |
| Annotation visibility, zoom-dependent detail, hidden-item finder, signal-only fit | [Organize and perform](patch-editor.md#3-document-and-organize-a-patch), [contract](../editor/patch-annotations.md#2-annotation-visibility-and-zoom-dependent-detail-6197--implemented) | [PR #35](https://github.com/wgabel/orbit2/pull/35) |
| Local and recursive entire-patch counts | [Understand counts](patch-editor.md#understand-the-two-counts), [canvas contract](../editor/canvas.md#the-canvases) | [PR #36](https://github.com/wgabel/orbit2/pull/36) |
| Editable modal percussion instruments | [Strike and retune modal instruments](sound-and-instruments.md#modal-percussion), [modal contract](../instruments/physical-modelling.md#phase-725--modal-instrument-composites-implemented) | [PR #37](https://github.com/wgabel/orbit2/pull/37) |
| Contextual layer analysis | [Research, method and case study](../examples/contextual-layer-balance.md) | [PR #38](https://github.com/wgabel/orbit2/pull/38) — production documentation, no new runtime meter |
| Resonator and built-in resonator presets | [Use a standalone resonator](sound-and-instruments.md#standalone-resonator), [processor contract](../instruments/physical-modelling.md#phase-724--resonator-implemented) | [PR #34](https://github.com/wgabel/orbit2/pull/34) |
| Sustained instruments | [Play sustained instruments](sound-and-instruments.md#sustained-instruments), [recipes](../examples/instrument-recipes.md) | [PR #31](https://github.com/wgabel/orbit2/pull/31) |
| Note cards, alias/description, groups, overview/search and annotated examples | [Organize a patch](patch-editor.md#3-document-and-organize-a-patch), [annotation contract](../editor/patch-annotations.md) | [PR #27](https://github.com/wgabel/orbit2/pull/27), [PR #28](https://github.com/wgabel/orbit2/pull/28), [PR #30](https://github.com/wgabel/orbit2/pull/30), [PR #32](https://github.com/wgabel/orbit2/pull/32) |
| Dynamic reusable plot taps | [Troubleshooting](patch-editor.md#4-inspect-and-troubleshoot-signals), [observation contract](../audio/signal-observation.md) | [PR #20](https://github.com/wgabel/orbit2/pull/20) |
| Keyboard pitch latch, loops/glide and recorded pitch takes | [Pitch and pattern workflows](sound-and-instruments.md#1-pitch-versus-articulation), [sequencer contract](../instruments/note-sequencer.md) | [PR #16](https://github.com/wgabel/orbit2/pull/16), [PR #17](https://github.com/wgabel/orbit2/pull/17), [PR #19](https://github.com/wgabel/orbit2/pull/19) |
| Locate nested output rack | [Composite routing](patch-editor.md#2-interpret-and-edit-connections), [boundary contract](../editor/composite-nodes.md) | [PR #18](https://github.com/wgabel/orbit2/pull/18) |
| Shared semitone control; AD; typed event routing; clock/gate; AR/ADSR/Loop; envelope plot | [Articulation and timing](sound-and-instruments.md#2-create-distinct-attacks-and-releases), [envelopes](../audio/envelopes.md), [pitch](../instruments/pitch-control.md) | [PR #6](https://github.com/wgabel/orbit2/pull/6), [PR #7](https://github.com/wgabel/orbit2/pull/7), [PR #8](https://github.com/wgabel/orbit2/pull/8), [PR #10](https://github.com/wgabel/orbit2/pull/10), [PR #12](https://github.com/wgabel/orbit2/pull/12), [PR #14](https://github.com/wgabel/orbit2/pull/14) |
| Filter/EQ response curves and cable amounts/display/editing | [Connections](patch-editor.md#2-interpret-and-edit-connections), [plots](patch-editor.md#4-inspect-and-troubleshoot-signals), [contracts](../editor/annotations-and-connections.md) | [PR #1](https://github.com/wgabel/orbit2/pull/1), [PR #2](https://github.com/wgabel/orbit2/pull/2), [PR #3](https://github.com/wgabel/orbit2/pull/3), [PR #4](https://github.com/wgabel/orbit2/pull/4) |

The pitch-only sequencer is not a Note/Gate/velocity source. Dedicated analyzer sinks, a searchable nested outline, user-defined reusable composite library/macros, compact modal bank, standalone pitch shifter, polyphony and sample assets remain planned or partially delivered as described in the [roadmap](../../ROADMAP.md). Do not infer availability from design-only PRs. Documentation PRs [#22](https://github.com/wgabel/orbit2/pull/22), [#24](https://github.com/wgabel/orbit2/pull/24), [#26](https://github.com/wgabel/orbit2/pull/26), [#29](https://github.com/wgabel/orbit2/pull/29), [#33](https://github.com/wgabel/orbit2/pull/33) and [#38](https://github.com/wgabel/orbit2/pull/38) provide musical guidance, not new audio nodes or presets.

For agents, read the relevant technical module and confirm the actual node registry, parameter definitions, editor controls and tests when exact patch JSON or implementation behavior matters. A feature usage update should identify its real implementation PR, a reproducible example, expected behavior, common mistakes and remaining limitations. Keep the usage index brief; put detailed contracts or additional examples where they are easiest to discover.
