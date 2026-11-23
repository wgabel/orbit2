# Musical instrument examples

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [pitch control](../instruments/pitch-control.md) · [envelopes](../audio/envelopes.md) · [physical modelling](../instruments/physical-modelling.md) · [composite nodes](../editor/composite-nodes.md).

These recipes address individual sounds. For combining voices into a structured musical piece, see [generative ambient composition](generative-ambient-composition.md).

For the available catalog, exposed ports and quick-start examples, see [using built-in instruments](../usage/sound-and-instruments.md#3-built-in-instruments-and-resonators).

For the principles behind tuning, harmonic content, articulation, level balancing and perceived sound quality, see [music theory and sound design](music-theory-and-sound-design.md).

For classic electronic instrument candidates (Juno-style pad, Moog-style bass, brass lead, analog pluck, Reese bass, warped keys, FM bell and acid bass), consult the [synthesizer patch recipes](synthesizer-patch-recipes.md). These reference designs are distinct from the implemented composite instrument catalogue below.

For a richer musical sound palette and candidate editable instrument recipes, including intentional noise shaping and an audition/revision process, see [sound quality and voice design](sound-quality-and-voice-design.md). These complement the instrument approximations below rather than claiming additional deployed presets.

For an analyzed, listener-validated synthetic instrument foundation rather than an acoustic approximation, see [breathing harmonic drone](foundations/breathing-harmonic-drone.md): a heavy, evolving sound based on interacting oscillators, modulation and chorus. It is a reference graph, not a shipped composite preset.

<a id="musical-instrument-synthesis-and-physical-modelling-planned-phase-72"></a>

## Choose an instrument for the musical role

**This is the current selection checkpoint for music-creation agents.** The built-in Faust models below were implemented in [PR #81](https://github.com/wgabel/orbit2/pull/81), [PR #82](https://github.com/wgabel/orbit2/pull/82) and [PR #86](https://github.com/wgabel/orbit2/pull/86). They are *available sound candidates*, not automatically listener-approved acoustic replicas. The original composite and oscillator recipes below remain valid; choose based on a part's articulation, register, expression, production context and measured/listened result. For exact parameter constraints and DSP test status see [Faust instrument nodes](../instruments/faust-instrument-nodes.md) and [new bowed/brass/guitar candidates](../instruments/faust-violin-brass-guitar.md).

| Desired musical role | Current candidate | Performance/routing | Caution or contrasting option |
| --- | --- | --- | --- |
| Repeated plucked melody, arpeggio or discrete counterline | **Physical Pluck** | Pitch → Pitch; Trigger → Trigger; optional Velocity → Strike strength (base 0), all at amount 1 | Triggered, ringing mono string C3–C6; compare a filtered oscillator+AD for deliberately synthetic plucks. No acoustic body or overlapping independent strings. |
| Percussive pitched accents, mallet line, bell-like punctuation | **Physical Marimba** | Pitch → Pitch; Trigger → Trigger; optional Velocity → Strike strength (base 0) | 50-mode bar, mallet hardness and position; relatively costly DSP; compare Modal/Playable Handpan or Bell for a different, not interchangeable, ring. |
| Breath phrasing, soft sustained line or upper melodic answer | **Physical Flute** | Pitch → Pitch; Gate → Gate; optional Velocity → Expression (base 0) | Breathing physical model, not a sampled player; compare Playable — Flute composite and adjust register/attack. |
| Reed-like melody, moving countermelody or darker wind line | **Physical Clarinet** | Pitch → Pitch; Gate → Gate; optional Velocity → Expression (base 0) | Monophonic breath model; evaluate note changes, envelope transitions and actual sound. |
| Horn stabs, swells or expressive sustained melodic statement | **Physical Brass** | Pitch → Pitch; Gate → Gate; optional Velocity → Expression (base 0) | Preliminary tube tuning and extreme-control stability need audition; compare Playable — Brass or a synth-brass voice. |
| Bowed line, expressive sustain or slow orchestral color | **Physical Violin (experimental)** | Pitch → Pitch; Gate → Gate; optional Velocity → Expression (base 0) | Known octave/register-jump risk; never assume accurate pitches or orchestral realism; compare Playable — Bowed strings or restrained synth strings. |
| Picked string hook, short rock-like accent or textured guitar role | **Physical Electric Guitar** | Pitch → Pitch; Trigger → Trigger; optional Velocity → Pluck strength (base 0) | Karplus–Strong/electric-guitar hybrid, **no amp/cabinet**; compare Physical Pluck with EQ/chorus for cleaner parts. |
| Harmonic keyboard bed, organ ostinato or sustained chord tone | **Drawbar Organ** | Pitch → Pitch; Gate → Gate; Swell controls expression, optional Trigger for key click/percussion | Nine drawbars; monophonic tonewheel-style model, not pipe organ. Use independently tuned voices for chords. |
| Rotating-speaker movement on organ or other audio | **Rotary Speaker** (effect, not a generator) | Organ/other Audio → Rotary Speaker Audio → Gain/Mixer → Master | Mono approximation of rotor AM/Doppler; not stereo cabinet microphones. A bypassed/fully dry A/B distinguishes its contribution. |
| Clean sub, LFO, strongly electronic lead, unusual harmonics or custom hybrid | **Oscillators/noise + envelopes/effects** | Explicit semitone tracking and note articulation appropriate to the graph | Physical models are not a universal upgrade; use [synth recipes](synthesizer-patch-recipes.md) and [sound quality](sound-quality-and-voice-design.md). |

**Playable wiring:** Add a Note Source with reference **A4 (MIDI 69)** for these Faust instrument models and connect its typed Pitch at **amount 1** to the instrument's **Pitch** (semitones) socket, *not* to an oscillator's linear Frequency input. Trigger-driven voices use Trigger for each new strike; breath/bow/brass/organ sustained voices use Gate to open/close the note. Velocity modulation is optional; where connected to Strike strength or Expression, reset that destination base to 0 before applying amount 1. Drawbar Organ uses Swell separately. Connect generator Audio to an appropriately trimmed mixer/master; the Rotary Speaker accepts and returns **Audio**. For an *automatic* monophonic phrase, use Note Sequencer **Playback → Notes (gate + trigger)** and **Loop**, wiring its Pitch plus Gate **or** Trigger to the correct sockets; this mode shipped in [PR #87](https://github.com/wgabel/orbit2/pull/87). Pitch-only mode will not excite a Trigger/Gate instrument. See [sequencer usage](../usage/sound-and-instruments.md#1-pitch-versus-articulation).

**Worked arrangement sketch (original, not a measured patch):** For an A-minor instrumental passage, use one Physical Clarinet on an eight-bar E4–G4–A4–E4 question/answer phrase with held Gates and occasional breath-shaped expression. Use Physical Pluck on a separate short E4–C4–A3 response with individual Triggers and a short Delay after the voice. Build an Am(add9) bed with **several independently pitched** Drawbar Organ instances or the editable synthetic organ composite, and insert Rotary Speaker after their mixed Audio if the result warrants it. Keep the bass as a clean separate oscillator and give each role its own gain. Contrast the phrase's second half by changing the *notes or rests*, not merely changing instrument presets. Each Note Sequencer is monophonic; a single organ or violin instance is **not** a polyphonic chord instrument. A/B the lead and accompaniment against synthetic alternatives at matched levels before claiming an improved sound.

**Evaluation gate:** Check the complete attack, held note, legato/retrigger, rests, decay, register and patch end for each role, including the blend in the mix. Compare mono stems at similar loudness; render with the current engine if available and record the actual instrument/preset version, sample rate and device constraints. Physical Violin and other candidate models are not listener approved; [instrument audition](instrument-design-and-audition.md) distinguishes technical checks from hearing-based judgments. Never promote the example to a shipped preset or listener-validated composition without corresponding evidence.

For the next generation of instrument realism, see [Faust-first realistic instrument emulation](../instruments/realistic-instrument-emulation.md), including existing-node Hammond and pipe-organ candidates, rotary-speaker wiring, physical-model references and license/validation gates. The following original presets remain editable synthetic approximations; their technical tests do not establish acoustic realism.

#### Musical instrument synthesis and physical modelling (Phase 7.2 — partial)

**Status:** five sustained instrument composites (7.2.1), shared oscillator pitch and Pitch Control (7.2.2), [Monophonic Note Source](../instruments/pitch-control.md#phase-723--note-source-in-review) (7.2.3), single-mode [Resonator](../instruments/physical-modelling.md) (7.2.4), four editable [modal percussion composites](../instruments/physical-modelling.md#phase-725--modal-instrument-composites-implemented) (7.2.5 first stage) and pitch-only keyboard sequencing (7.2.8a–c) implemented. Five playable sustained templates (7.2.6a) are implemented; [four direct-velocity modal variants](../instruments/physical-modelling.md#phase-726b--playable-modal-instruments-in-review) (7.2.6b) are merged ([PR #46](https://github.com/wgabel/orbit2/pull/46)). Note-mode pattern articulation is implemented ([PR #87](https://github.com/wgabel/orbit2/pull/87)); optional compact modal bank and true polyphony remain follow-ups.

**Goal and scope.** Build editable synthesized instrument approximations from reusable oscillators, noise, filters, effects, envelopes, pitch controllers and resonators. Do not create a separate DSP class for each instrument. An instrument template is an ordinary composite graph with stable named rack inputs, independent state and an audio output. Preserve the possibility of opening it, inspecting its components, changing routing and saving a new configuration. This is sound design, not a claim of exact reproduction of acoustic instruments. Sample-based approximations and hybrid instruments can additionally use Phase 8 assets later.

For developing a distinct instrument palette across genres, plug candidate voices into the [TEST Melody Driver v1](instrument-audition/reference.orbit2.json) in the [instrument and sound design guide](instrument-design-and-audition.md). Its independent pitch/gate/trigger/velocity controls support the existing playable instruments as well as new experiments; match each instrument's reference tuning and compare on the same score at similar levels. Phase 7.6 plans the tested palette and workflow, not a second set of instrument DSP classes.

##### Existing capabilities and worked instrument patches

The initial built-in composite catalog uses existing nodes and the registered template mechanism. No new DSP node or event source is needed for these sustained versions. Starting configurations are illustrative and should be adjusted by listening and examining the shared scope/FFT, not treated as measured acoustic spectra.

### Phase 7.2.1 — built-in instrument composites (implemented)

Implementation: [PR #31](https://github.com/wgabel/orbit2/pull/31).

Select an **Instrument — …** entry in Add node and connect its Audio output to Master output or to a mixer. Open the composite to edit all constituent nodes, settings, and routes; adding the same preset again creates an independent graph. All five have a `Pitch (st)` modulation input (relative semitones, unity connection from Pitch Control or Note Sequencer) and a `Gain offset` input (additive normalized modulation on the final gain, base 0.5, clamped to 0–1). Zero pitch offset is A in the instrument's native register; +12 doubles every voice frequency, including detuned layers, without changing fixed body filter frequencies. Gain offset −0.5 silences the composite; unconnected gain defaults to 0.5. External envelope-controlled gain can articulate the sustained sound, but this is not yet a self-contained note-triggered or polyphonic instrument.

| Catalog entry | Base tuning | Internal voice and tone construction |
|---|---:|---|
| Organ | A2 (110 Hz) | Five sine partials at ratios 1:2:3:4:5, amplitudes .30/.15/.10/.05/.025, light chorus and reverb. |
| Flute | A3 (220 Hz) | Fundamental and second sine partial, quiet third triangle partial, white noise .025 through a 1.8 kHz band-pass (Q 2), reverb. |
| Brass | A2 (110 Hz) | Saw and triangle fundamental plus a quieter octave saw, 1.45 kHz low-pass, reverb. |
| Bowed strings | A2 (110 Hz) | Two saws (one +7 cents), octave triangle, pink noise .018 filtered at 950 Hz, 2.2 kHz body low-pass, chorus and reverb. |
| Didgeridoo | A1 (55 Hz) | Low saw feeding parallel low-pass and vocal-formant band-passes at 460/1150 Hz, triangle fundamental, filtered brown noise .025, reverb. |

The quiet filtered noise layers serve breath and bow textures rather than presenting exposed broadband hiss. The voices use conservative default levels, with a final gain of .5 and modest wet reverb. These are synthesized timbral approximations, not acoustic recordings or listening-validated replicas. Offline regression tests render the registered composites at 44.1/48 kHz, check finite non-silent audio, peak headroom and octave tracking; graph tests exercise editing, independent copies and JSON round trips. Audible character and perceived loudness still require listening in a musical context.

<a id="phase-726a--playable-sustained-instruments-in-review"></a>

### Phase 7.2.6a — Playable sustained instruments (implemented)

Implementation: [PR #44](https://github.com/wgabel/orbit2/pull/44).

Five additional **Playable — …** entries reuse the sustained voices' timbre and pitch routing, without replacing or migrating the original presets. Each exposes Pitch (st), Gate (typed, single source) and Velocity (0–1, modulation amount 1), plus Audio output. Connect Note Source outputs of the same name; use a reference matching the instrument's native tuning to make keyboard labels match actual fundamental pitches. An unconnected Gate or Velocity produces silence; a fixed velocity can also be supplied by an ordinary modulation source or set by editing the internal velocity Gain base to 1.

| Voice | Reference | Attack (ms) | Decay (ms) | Sustain | Release (ms) |
| --- | --- | ---: | ---: | ---: | ---: |
| Organ | A2 | 8 | 80 | .92 | 120 |
| Flute | A3 | 65 | 180 | .78 | 190 |
| Brass | A2 | 45 | 160 | .70 | 160 |
| Bowed strings | A2 | 180 | 300 | .85 | 420 |
| Didgeridoo | A1 | 100 | 220 | .88 | 300 |

The shared graph builder inserts two zero-base gains in series before reverb: velocity and ADSR. This multiplies rather than adds strike strength and the envelope, gates the entire tonal/noise mix, and permits the reverberant tail after release. The final gain remains .5. Open the composite to edit its envelope and tone; the existing Note Source last-note priority gives legato on overlapping notes, not independent attacks or simultaneous voice allocation. A new note during release can change the voice's pitch/velocity. These are editable approximations, not acoustically validated instruments. Offline tests render the full Note Source → composite → Master route at 44.1/48 kHz, verifying idle silence, octave pitch, velocity ratio, sustained level, headroom and decaying release. See [how to play](../usage/sound-and-instruments.md#playable-sustained-instruments).

The **Impact — AD percussion** worked patch is exportable from Patches → Annotated examples (6.19.4); import the JSON to inspect its root/nested descriptions and notes. See [patch overview and discovery](../editor/patch-annotations.md#patch-overview-and-discovery-6194).

| Instrument | Existing synthesis recipe | Later improvement |
|---|---|---|
| Organ | Additive bank of sines at a fundamental and integer multiples, weighted mixer, chorus/reverb. | Pitch tracking, gate, independent drawbar-style macro controls. |
| Flute | Dominant sine at 220 Hz (amplitude 0.3), second harmonic at 440 Hz (0.05), quiet white noise (0.03) through a band-pass around 1.8 kHz with Q 2, summed with modest reverb. | Breath/vibrato controls, shaped attack/release and consistent relative pitch tracking. |
| Analog brass | Saw/triangle layers, resonant low-pass, EQ and reverb. | Separate amplitude/filter envelopes and velocity-dependent brightness. |
| Bowed strings | Detuned saw/triangle oscillator layers, resonant filtering, chorus and reverb. | Bow-like excitation, slow attack, vibrato and resonant body. |
| Didgeridoo / vocal drone | Low sawtooth around 65 Hz, quiet brown noise, independently tuned parallel band-pass filters or EQ bands and slowly moving cutoff. | Several independently controllable vocal/formant resonances and breath excitation. |
| Handpan, bell, gong and struck wood | Modal composites with independent resonator frequencies, decays and weights, typed Trigger, shared pitch/strength, and optional audio excitation (implemented). | Direct note velocity ([playable modal variants](../instruments/physical-modelling.md#phase-726b--playable-modal-instruments-in-review), in review), dedicated response visualization and optional compact modal bank. |
| Piano / plucked strings | Limited with continuous oscillators; do not market a static harmonic patch as a natural piano. | Short excitation, multiple decaying modes, later string/waveguide or sample hybrid modelling. |

An organ example at 130.81 Hz uses five sine voices at ratios 1:2:3:4:5 and amplitudes 0.30, 0.15, 0.10, 0.05 and 0.025. Its fundamental and harmonics can be mixed before effects with sufficient peak headroom. A flute example uses both tonal and breath lanes; later an ADSR can shape the tonal amplitude while a separate envelope controls the breath lane. Apply vibrato to the shared pitch reference rather than modifying only the fundamental when harmonic tuning must remain intact.

~~~mermaid
flowchart TD
    P[Shared pitch] --> A[Fundamental oscillator]
    P --> B[Harmonic oscillators]
    A --> M[Weighted mixer]
    B --> M
    N[Noise] --> F[Breath filter]
    F --> M
    M --> G[Envelope-controlled gain]
    G --> R[Reverb]
    R --> O[Audio output]
~~~
