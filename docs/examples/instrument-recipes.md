# Musical instrument examples

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [pitch control](../instruments/pitch-control.md) · [envelopes](../audio/envelopes.md) · [physical modelling](../instruments/physical-modelling.md) · [composite nodes](../editor/composite-nodes.md).

These recipes address individual sounds. For combining voices into a structured musical piece, see [generative ambient composition](generative-ambient-composition.md).

For the principles behind tuning, harmonic content, articulation, level balancing and perceived sound quality, see [music theory and sound design](music-theory-and-sound-design.md).

For a richer musical sound palette and candidate editable instrument recipes, including intentional noise shaping and an audition/revision process, see [sound quality and voice design](sound-quality-and-voice-design.md). These complement the instrument approximations below rather than claiming additional deployed presets.

For an analyzed, listener-validated synthetic instrument foundation rather than an acoustic approximation, see [breathing harmonic drone](foundations/breathing-harmonic-drone.md): a heavy, evolving sound based on interacting oscillators, modulation and chorus. It is a reference graph, not a shipped composite preset.

<a id="musical-instrument-synthesis-and-physical-modelling-planned-phase-72"></a>

#### Musical instrument synthesis and physical modelling (Phase 7.2 — partial)

**Status:** shared oscillator pitch and Pitch Control (7.2.2) and pitch-only keyboard sequencing (7.2.8a–c) implemented. Five sustained instrument composites (7.2.1) are in review; articulated note sources, resonators and polyphony remain planned.

**Goal and scope.** Build editable synthesized instrument approximations from reusable oscillators, noise, filters, effects, envelopes, pitch controllers and resonators. Do not create a separate DSP class for each instrument. An instrument template is an ordinary composite graph with stable named rack inputs, independent state and an audio output. Preserve the possibility of opening it, inspecting its components, changing routing and saving a new configuration. This is sound design, not a claim of exact reproduction of acoustic instruments. Sample-based approximations and hybrid instruments can additionally use Phase 8 assets later.

##### Existing capabilities and worked instrument patches

The initial built-in composite catalog uses existing nodes and the registered template mechanism. No new DSP node or event source is needed for these sustained versions. Starting configurations are illustrative and should be adjusted by listening and examining the shared scope/FFT, not treated as measured acoustic spectra.

### Phase 7.2.1 — built-in instrument composites (in review)

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

| Instrument | Existing synthesis recipe | Later improvement |
|---|---|---|
| Organ | Additive bank of sines at a fundamental and integer multiples, weighted mixer, chorus/reverb. | Pitch tracking, gate, independent drawbar-style macro controls. |
| Flute | Dominant sine at 220 Hz (amplitude 0.3), second harmonic at 440 Hz (0.05), quiet white noise (0.03) through a band-pass around 1.8 kHz with Q 2, summed with modest reverb. | Breath/vibrato controls, shaped attack/release and consistent relative pitch tracking. |
| Analog brass | Saw/triangle layers, resonant low-pass, EQ and reverb. | Separate amplitude/filter envelopes and velocity-dependent brightness. |
| Bowed strings | Detuned saw/triangle oscillator layers, resonant filtering, chorus and reverb. | Bow-like excitation, slow attack, vibrato and resonant body. |
| Didgeridoo / vocal drone | Low sawtooth around 65 Hz, quiet brown noise, independently tuned parallel band-pass filters or EQ bands and slowly moving cutoff. | Several independently controllable vocal/formant resonances and breath excitation. |
| Handpan, bell, gong and struck wood | Approximate a fixed-pitch ring with an oscillator bank or filtered noise; natural repeated strikes require envelope or impulse excitation. | Damped modal resonators with independent frequency, decay and level; velocity and trigger input. |
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
