# Electronic music genres — composition reference

[Music creation index](README.md) · [Architecture](../architecture/overview.md) · [Generative composition](generative-ambient-composition.md) · [Music theory and sound design](music-theory-and-sound-design.md)

Documentation provenance: [PR #26](https://github.com/wgabel/orbit2/pull/26) introduces the genre reference, technique bridge and agent routing (documentation only; not evidence of new engine capabilities).

This is a modular reference for composing **whole pieces** and designing their constituent sounds in Orbit 2. Genre is a musical vocabulary, not a preset or a mandatory formula. The same synthesizer voice can belong to many genres; rhythm, harmonic language, sound selection, arrangement, contrast and production choices together establish the identity. The tables describe useful listening/composition starting points, not exhaustive definitions, historical boundaries or fixed BPM rules. Some entries are established scenes, others are crossovers, processes or descriptive tags; these are identified where helpful.

## Find a genre

| Module | Use for |
| --- | --- |
| [Ambient and atmospheric music](genres/ambient.md) | Space, melodic, drone, dark, ritual, cinematic, generative, granular, environmental, dub and rhythmic ambient; especially long-form pieces |
| [Bass music and broken rhythms](genres/bass-and-breaks.md) | Experimental/freeform bass, dubstep, halftime, wave, trap, drum and bass, jungle, footwork, breakbeat and related mutations |
| [Dance and club music](genres/club-and-dance.md) | House, techno, trance, electro, hardcore, disco and regional club traditions |
| [Experimental music and crossovers](genres/experimental-and-hybrids.md) | IDM, glitch, noise, electroacoustic, downtempo, psybient, synthwave, industrial, electronic hip-hop and hybrid approaches |
| [Orbit 2 musical techniques](genres/orbit-techniques.md) | Reusable graph recipes for pulse/rest patterns, sub, percussion, melodic and harmonic voices, evolving bass, space, and long-form control; links to actual node contracts |

For each chosen musical role, consult the [current instrument selection matrix](instrument-recipes.md#choose-an-instrument-for-the-musical-role) **before** deciding to build all sounds from the classic oscillator recipes. A triggered Physical Pluck/Marimba/Electric Guitar, gated Flute/Clarinet/Brass/experimental Violin, Drawbar Organ, and the Rotary Speaker effect may serve different roles across all genres; none is mandatory or listener-approved by implementation alone. Pair the matrix with [exact physical-model routing](../usage/sound-and-instruments.md#faust-instrument-nodes) and audition each important sound in context.

For choosing synthesized roles and timbral families within a genre or hybrid, consult the [synthesizer patch recipes](synthesizer-patch-recipes.md) before designing voices. Use suitable techniques as reference material, not as a constraint on the genre or a required instrument roster.

For sound quality and production rather than genre selection, open [voice design](sound-quality-and-voice-design.md) (noise treatment, harmonic body, candidate presets), [instrument and sound design — repeatable audition](instrument-design-and-audition.md) (genre-independent A/B testing, reusable sounds and role-specific profiles) and [mixing, mastering and analysis](mixing-mastering-and-analysis.md) (balance, full-render iteration and measurement).

Start with just the relevant family and selected technique sections. Cross genre boundaries freely; for example, a space-ambient track with a halftime climax combines ambient form, a bass foundation and a broken percussion grid. Do not read all modules by default. Unknown genres are not blockers: listen/research if possible, identify the audible ingredients, investigate the relevant node contracts and build an appropriate patch. Extend this reference when a recurring musical need is missing.

For cinematic, orchestral or hybrid soundtrack composition, use the focused [cinematic composition and orchestration guide](genres/cinematic-orchestration.md). It explains how to turn a small score into a complete cue rather than treating orchestral color as a collection of presets.

## Turn a style into a composition

1. **Brief.** Establish purpose, duration, energy arc, genre(s), references, rhythmic emphasis, and whether the deliverable is one sound, one voice, a loop or a complete piece. For a reference recording, identify audible roles, density, motif, groove, timbral evolution and perceived depth; do not just copy its BPM, title or melody. If listening is unavailable, disclose that and work from reliable descriptions and musical analysis. Create original motifs.
2. **Musical plan.** Choose a tonal center or deliberate atonality, a harmonic palette and an identifiable main gesture. Select only useful roles from the [composition guide](generative-ambient-composition.md#1-the-structure-of-a-musical-piece): foundation, harmonic bed, lead/motif, rhythm, accents, texture, space and form. A bass piece may have a strong sub, moving mid-bass and drums instead of a continuous pad; a drone may have no drums at all. Establish register, timing, event density and interaction. Use the [theory guide](music-theory-and-sound-design.md) for pitch, voice leading, timbre and masking.
3. **Arrangement.** Write a small section plan with time/bar ranges, active roles, changes, transitions and ending. Arrange meaningful contrast in actual musical material (motifs, groove, harmony) as well as spectral shape, density and dynamics. Use [compositional development](compositional-development.md) to plan and evaluate distinct phrases and sections without imposing a song template. For an individual sound, skip the whole-track arrangement and define its intended role and articulation.
4. **Translate into graphs.** Choose physical, composite or synthetic instruments based on role and articulation using the [instrument selection table](instrument-recipes.md#choose-an-instrument-for-the-musical-role), rather than defaulting to oscillator banks. Select technique sections from the [bridge](genres/orbit-techniques.md), then consult the relevant linked technical specifications and implementation where necessary. Decide which parameter controls correspond to the musical decisions. Use named voices/composites, independent gain stages and clear routing; prefer the simplest effective graph. For exported patches, consult [persistence](../editor/persistence-and-history.md) and [composite nodes](../editor/composite-nodes.md). Count reachable sources and verify actual node IDs, sockets, parameter bounds, serialized schema and feature status. Genre guidance does not establish a new node contract.
5. **Validate and audition.** Verify timing, pitch, rests, retriggering, mix and reachable output. Check the form through its complete duration, including the first boundary, final fade and loop wrap. Compare the result to intended musical roles and stylistic characteristics, not just graph validity. Follow the [render–analyze–refine workflow](mixing-mastering-and-analysis.md#5-agent-workflow-render--analyze--refine--re-render) for substantial pieces: use baseline and revised renders when possible, measure important voices and master, address weak timbres and balance without destroying the form. Listen when possible; distinguish heard results, rendered measurements and untested design. Report approximations, dependencies, manual performance requirements and capability gaps. A ten-minute loop that never develops is not automatically a ten-minute composition.

## Sound design, arrangement and verification are different tasks

| Question | Primary reference |
| --- | --- |
| What makes this genre recognizable and what roles usually interact? | Selected genre module |
| How do those roles form a complete piece with emergence, contrast and resolution? | [Generative composition](generative-ambient-composition.md) (adapt its layering principles beyond ambient) |
| Why do these notes, chords, timbres and levels work? | [Music theory and sound design](music-theory-and-sound-design.md) |
| How can I approximate the sound or rhythm using current nodes? | [Musical techniques](genres/orbit-techniques.md) + [instrument recipes](instrument-recipes.md) |
| How do I avoid basic or hollow voices and exposed noise? | [Voice design](sound-quality-and-voice-design.md) |
| How do I refine the mix, check the master and measure improvement? | [Mixing, mastering and analysis](mixing-mastering-and-analysis.md) |
| What can the engine really do, what are valid inputs, and what is planned? | [Architecture/router](../architecture/overview.md), [roadmap](../roadmap/overview.md), actual contracts, source and tests |

Use this information as a decision aid, not a mandatory checklist or a restriction on experimentation. Do not invent unsupported sequencing, sampling, nonlinear processing or stereo capabilities. An approximation can still make excellent music: if the defining feature cannot be reproduced, describe the tradeoff, find a musically useful substitute or propose a future feature separately from the composition. Agent handoffs follow [AGENTS.md](../agents/contributor-instructions.md), including documentation consulted and discoverability feedback.

## Reading the genre tables

Tempos are approximate beat-grid starting points, not classification criteria. Half-time and double-time may describe the same physical groove (70/140 BPM); distinguish pulse from perceived accents. `Free` means no global grid is necessary, not an inability to use clocks. A “kick”, “sample” or “sidechain” in a description refers to the genre's usual production vocabulary, not necessarily an Orbit 2 node. The linked techniques are intentionally reusable across families. Each table supplies a construction pattern; deeper listening and variation are encouraged.

Sources: [Ableton Learning Music — tempo and genre](https://learningmusic.ableton.com/make-beats/tempo-and-genre.html), [backbeats](https://learningmusic.ableton.com/make-beats/backbeats.html), [drum sounds](https://learningmusic.ableton.com/make-beats/what-are-these-sounds.html). Family modules link production perspectives and reference examples. These sources illustrate approaches; genre boundaries and vocabulary change with scenes, artists and listeners.
