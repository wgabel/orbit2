# Musical instrument examples

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [pitch control](../instruments/pitch-control.md) · [envelopes](../audio/envelopes.md) · [physical modelling](../instruments/physical-modelling.md) · [composite nodes](../editor/composite-nodes.md).

These recipes address individual sounds. For combining voices into a structured musical piece, see [generative ambient composition](generative-ambient-composition.md).

<a id="musical-instrument-synthesis-and-physical-modelling-planned-phase-72"></a>

#### Musical instrument synthesis and physical modelling (Phase 7.2 — partial)

**Status:** shared oscillator pitch and Pitch Control (7.2.2) implemented. The instrument recipes below use existing capabilities; additional packaged instrument templates, playable note sources, keyboard pitch sequencing, resonators and polyphony are planned.

**Goal and scope.** Build editable synthesized instrument approximations from reusable oscillators, noise, filters, effects, envelopes, pitch controllers and resonators. Do not create a separate DSP class for each instrument. An instrument template is an ordinary composite graph with stable named rack inputs, independent state and an audio output. Preserve the possibility of opening it, inspecting its components, changing routing and saving a new configuration. This is sound design, not a claim of exact reproduction of acoustic instruments. Sample-based approximations and hybrid instruments can additionally use Phase 8 assets later.

##### Existing capabilities and worked instrument patches

Start with a built-in example-patch/composite catalog using existing nodes and the existing registered template mechanism. No DSP node or event source is needed for these initial sustained versions. Examples contain documented settings and safe gain staging, not just a name on an oscillator preset. Add the Phase 6.19 notes and labels when available; independent examples can ship before that work. Starting configurations are illustrative and should be adjusted by listening and examining the shared scope/FFT, not treated as measured acoustic spectra.

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
