# Realistic instrument emulation — Faust-first design

[Architecture](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md) · [Instrument recipes](../examples/instrument-recipes.md) · [Resonators and modal synthesis](physical-modelling.md) · [Repeatable audition](../examples/instrument-design-and-audition.md)

**Status:** Phase 7.2.7 research plus [new instrument nodes](faust-instrument-nodes.md): Physical Pluck, Physical Marimba, Drawbar Organ and Rotary Speaker merged ([PR #81](https://github.com/wgabel/orbit2/pull/81)); [Physical Clarinet and Physical Flute](faust-sustained-instruments.md) in review (wind implementation PR (pending link)). The following primitive-node recipes remain candidate experiments, not listening-validated acoustic replicas. User feedback (2026-11-22): the existing implemented instruments are all lacking in realism. Prefer the Faust Physical Modeling Toolkit where appropriate rather than recreating every acoustic model from primitive oscillators. Retain existing preset identities, saved graphs and useful synthetic sounds.

Design provenance: [PR #78](https://github.com/wgabel/orbit2/pull/78) (documentation only; no shipped implementation or release clearance).

## 1. Choose the synthesis method for the instrument

The existing sustained organ uses five fixed sine ratios (1:2:3:4:5), amplitudes .30/.15/.10/.05/.025, chorus and reverb; the playable version adds an ADSR (8/80 ms, .92 sustain, 120 ms release) and velocity gain. Flute uses sine/triangle partials and filtered noise; brass combines saw/triangle and a fixed low-pass; strings use detuned saws; didgeridoo uses saw/triangle, fixed formants and filtered noise. The modal instruments use a common AD noise strike with three to six fixed resonances. These make editable synthetic approximations (see [actual registered templates](../../src/Orbit.Application/Graphs/InstrumentTemplates.cs) and [modal templates](../../src/Orbit.Application/Graphs/ModalInstrumentTemplates.cs)), not measured models of particular instruments. Existing compiler tests establish finite output, gate/release, pitch and headroom, not acoustic realism.

| Reference instrument or plugin | Documented technique | Implication for Orbit |
|---|---|---|
| [Arturia B-3 V](https://www.arturia.com/products/software-instruments/b-3-v/overview), [GSi VB3-II manual](https://www.genuinesoundware.com/products/vb3-2/2023/Manual/help.htm) | Electromechanical tonewheel model, drawbar registration, leakage/contact/percussion, scanner chorus, rotary cabinet; GSi models a shared 91-wheel bank. | Additive is a suitable foundation; model the mechanism and cabinet rather than a generic organ timbre. |
| [Modartt Organteq](https://www.modartt.com/organteq_overview) | Air jet/reed, pipe resonances, wind pressure, transient chiff, radiated sound and room. | Distinct ranks with their own spectra and transients; investigate acoustic excitation feedback. |
| [Hauptwerk](https://www.hauptwerk.com/features/) and [user guide](https://www.hauptwerk.com/wp-content/uploads/dlm_uploads/2023/08/HauptwerkInstallationAndUserGuide.pdf) | Recordings of individual pipes, sustain loops and release samples; avoids pitch-stretching a few notes across the keyboard. | Phase 8 licensed multisampling for demanding sources; do not acquire commercial sample sets without rights. |
| [SWAM Flutes](https://www.audiomodeling.com/products/swam-flutes), [SWAM strings](https://static.audiomodeling.com/manuals/strings/SWAM%20Solo%20Strings%20-%20User%20Manual%20-%20v3.0.3.pdf) | Continuous excitation/expression, breath, bow pressure/speed/position, articulations. | A held Gate is not a breath or bow. Expose continuous expression alongside typed note signals. |
| [Samplemodeling Trumpet](https://www.samplemodeling.com/product/the-trumpet/) | Recorded acoustic timbre plus continuous harmonic-aligned dynamic transitions. | Pressure changes spectrum as well as loudness; sample/physical hybrids are valid alternatives. |
| [Pianoteq](https://www.modartt.com/pianoteq_features) | Hammer/string interaction, stiffness/inharmonicity, sympathetic strings, soundboard and pedal behavior. | Do not promise acoustic piano from static oscillators; consider hybrid/sample path or dedicated model. |
| [Arturia Stage-73 V](https://www.arturia.com/products/software-instruments/stage-73-v/overview) | Tine, hammer, pickup and amp modelling. | Modal electric piano with intensity-dependent transient and nonlinear pickup is promising. |
| [Physical Audio Modus](https://physicalaudio.co.uk/products/modus/) | Coupled strings/plate with nonlinear connections. | Measured modes and coupling can improve handpan, gong and experimental resonators. |

Manufacturer descriptions establish the stated techniques, not comparative sound quality or access to their proprietary implementations. No VST code, proprietary samples or brand assets are dependencies.

## 2. Faust Physical Modeling Toolkit — preferred next implementation

Official [physmodels.lib reference](https://faustlibraries.grame.fr/libs/physmodels/) and [source](https://github.com/grame-cncm/faustlibraries/blob/master/physmodels.lib); [library index](https://faustlibraries.grame.fr/libs/), [Faust/STK flute example](https://github.com/grame-cncm/faust/blob/master-dev/examples/physicalModeling/faust-stk/flute.dsp), [mesh2faust](https://github.com/grame-cncm/faust/tree/master-dev/tools/physicalModeling/mesh2faust). A digital waveguide simulates travelling waves and reflections using short delay/filter loops; a modal model sums resonant vibration modes. Nonlinear excitation (bow friction, reed/lip interaction, air jet, strike) affects the oscillation itself, rather than merely filtering a fixed tone.

| Candidate function / building block | Intended use | Controls to investigate |
|---|---|---|
| `pm.fluteModel` | Flute, experimental pipe/air instruments | Pitch/tube length, pressure, jet/embouchure, vibrato, attack |
| `pm.clarinetModel`, `pm.reedTable`, `pm.openTube` | Clarinet, reed/tube experiments | Bore length, reed stiffness/pressure, articulation |
| `pm.brassModel`, `pm.brassLips` | Trumpet, horn, brass, experimental lip-reed/didgeridoo | Lip frequency, breath pressure, tube length, transient, expression |
| `pm.violinModel`, `pm.violinBody` | Bowed strings and separate body resonance | String length, bow pressure, velocity, position |
| `pm.ks`, `pm.guitarModel`, `pm.nylonGuitarModel` | Plucked strings, acoustic/classical guitar, harp-like candidates | Pluck position/strength, damping, string stiffness, body resonance |
| `pm.marimbaModel`, `pm.djembeModel`, `pm.churchBellModel` | Modal percussion | Strike position, hardness, decay, body/mode tuning |
| `pm.formantFilterbank` | Voice/body resonances, vocal drone | Formant center, bandwidth, vowel; separate from excitation |
| `mesh2faust` (offline tool, not browser runtime) | Generate modal coefficients from geometry for experiments | Measured/validated mode frequencies, losses and radiation; verify generated data license |

Names are upstream reference candidates, not promises of compatibility or an exact signature in Orbit's pinned package. Check the functions' actual arguments, supported sample rates, internal delay bounds and gates. Prefer parameterized DSP functions over upstream `*_ui`/MIDI demo wrappers to preserve Orbit's parameter and event contracts. A model that sounds at nonzero pressure without a note must define an explicit silent idle and release contract.

### 2.1 License and dependency review (deferred distribution follow-up)

Project owner decision (2026-11-23): proceed with instrument integration using the existing pinned Faust package; defer the existing compiler distribution compliance work as a separate roadmap task. The audit and its outstanding obligations remain open; this decision does not make a legal clearance claim. The checklist below describes the compliance follow-up, not a blocker for the current instrument development.

**Audit result:** [Pinned Faust dependency audit](faust-dependency-audit.md) verifies compiler 2.90.0, standard libraries 2.74.2 and physmodels.lib 1.2.1 in the actual browser bundle. Two minimal `pm.ks` and `pm.fluteModel` probes compile; their license evidence supports local experiments. Generated-code exceptions do not remove compiler distribution obligations; final wrapper dependencies, required notices and corresponding source/replacement arrangements remain open. No upgrade is needed merely to obtain the physmodels.lib header.

As checked on 2026-11-22, current upstream [physmodels.lib](https://github.com/grame-cncm/faustlibraries/blob/master/physmodels.lib) includes a GRAME LGPL-2.1-or-later header with a Faust exception permitting the compiled code of a larger Faust program importing the file to use a separately chosen license. This is not a blanket grant over all Faust code, and source redistribution/modification obligations are distinct from generated-output licensing. The missing header was explicitly reported and fixed on 2026-09-02 in [issue #250 / commit 85368ff](https://github.com/grame-cncm/faustlibraries/issues/250); do not assume an older bundled revision contains that text.

Orbit currently pins `@grame/faustwasm@0.19.0` in its [build](../../scripts/prepare-faust-compiler.mjs) and ships the browser compiler bundle, including its COPYING notice. For the dependency-compliance follow-up:

1. Identify the actual bundled compiler and library versions/commit or artifact hash (not just an npm version), inspect the package's embedded library files, headers and compilation dependencies, including imported tables/data.
2. Identify licenses separately for compiler/runtime, library source, exact selected functions and their transitive DSP/data dependencies. Check per-function declarations and exceptions. [Faust contribution licensing guidance](https://faustlibraries.grame.fr/contributing/) distinguishes permissive and copyleft terms; [STK 4.3 license](https://github.com/grame-cncm/faustlibraries/blob/master/licenses/stk-4.3.0.md) applies only where designated, not automatically to the whole toolkit.
3. If the pinned source predates explicit grants, establish upstream provenance or upgrade to a verified revision through a separately tested dependency change. Do not assume the current header retroactively covers all old binaries. Resolve ambiguity or exclude the function. Do not silently import unapproved GPL/AGPL code or proprietary assets.
4. Record a versioned inventory: function, source revision, license/exception, dependency closure, notice/source obligations, artifact distribution path (including browser WASM, JS bundles and any library files), and decision. Preserve applicable copyright/license notices, attribution and any required source or offer; seek legal review where unclear. Confirm compatibility with Orbit's actual distribution/license.
5. Document the audit, reproducible build and regression test. Do not label any specific model cleared until its inventory is verified. Check the same process for future upgrades, physical model coefficient data and samples.

### 2.2 Integration design

- [Native offline probes](faust-physical-model-probes.md) render `pm.ks` pluck and `pm.fluteModel` sustained flute at 44.1/48 kHz with the exact pinned compiler. The initial flute exhibited register failures; the [calibrated wind follow-up](faust-sustained-instruments.md) verifies both flute and clarinet across C3–C6 and independent expression/character settings. The [calibrated Physical Pluck research wrapper](faust-physical-pluck.md) passes C3–C6 at both sample rates with a continuous T60 mapping and trigger-latched velocity/pitch. It is now registered alongside the marimba, drawbar organ and rotary effect. Exact graph A/B and listener approval remain open. Proof-of-concept should precede adding many nodes; if a model does not compile or fails acoustic tests, investigate or evaluate an alternative. Record dependencies for the deferred compliance work.
- Use existing domain node registry, `IFaustNodeEmitter`, `FaustNodeContext`, parameter definitions/ranges, audio-rate modulation, typed event inputs, stable serialization, compatible history and graph flattening. Keep feedback/delay states internal to DSP, not illegal graph cycles. No second audio engine or VST host.
- Keep reusable physical primitives (exciter, body/resonator, processor where suitable) and instrument-specific parameterized cores only where needed; wrap playable variants as editable composites with pitch reference, Gate or Trigger, Velocity (where physically meaningful), Expression and Audio output. No per-note compilation. Prefer phase-continuous controls and bounded state, clear unit mapping (semitones/Hz/normalized pressure) and stable defaults, with intentionally shaped initial attack and release.
- Expression is continuous breath/bow strength, not velocity repeated every sample. For a note interface, use Velocity for the onset gesture, Gate for sustain/release, and Expression for evolving excitation/brightness. For a pluck, latch strike energy, preserve state until decay. Do not retune old tails when future per-voice allocation is available.
- Model in mono first. Preserve existing synthetic presets and IDs. Add separate candidates with clear names, read-only provenance and ordinary editable routing. Provide a practical usage example and candidate test patch before registering a broad catalog. Use the [performance benchmark baseline](../quality/dsp-profiling.md), not UI FPS as DSP load; on-demand monitor sessions must be explicit and closed during steady-state external sampling where appropriate.

## 3. Organ design with existing nodes

### 3.1 Hammond-type additive organ (candidate)

The official [Hammond drawbar guide](https://hammondorganco.com/wp-content/uploads/2022/09/Sk-Reference-Guide-for-web-1.pdf) gives nine footage registrations. Use sine oscillators, free timing, Semitones tracking, shared Pitch input at unity (reference A3 = 220 Hz). Model each drawbar independently, using approximate ratios below. Real tonewheels are a shared bank with geared/tuned pitches and foldback; exact harmonic ratios here are a convenient approximation.

| Drawbar | Ratio to 8' | At A3 (Hz) | Bright registration position |
|---|---:|---:|---:|
| 16' | .5 | 110 | 8 |
| 5 1/3' | 1.5 | 330 | 8 |
| 8' | 1 | 220 | 8 |
| 4' | 2 | 440 | 6 |
| 2 2/3' | 3 | 660 | 3 |
| 2' | 4 | 880 | 0 |
| 1 3/5' | 5 | 1100 | 0 |
| 1 1/3' | 6 | 1320 | 0 |
| 1' | 8 | 1760 | 0 |

Suggested gain law for position d: zero when d=0, otherwise amplitude `0.1 * 10^((d - 8) * 3/20)` (approximate 3 dB steps, not measured wiring). Start with 888000000 (jazz), 808400000 (warm), 888630000 (bright), or 888888888 (full; sum and trim conservatively). Set ADSR attack 3–5 ms, sustain 1, release 8–20 ms, zero-based Gain with envelope input; no decaying pluck. Classic sustained loudness is not velocity-dependent. Expression/swell is separate. Add a quiet short band-limited key-click onset (Trigger → AD → zero-based Noise amplitude, 0.5–1 ms attack, 3–8 ms decay) and optional 2× or 3× sine percussion (gate rising-edge AD, 1–3 ms attack, 200–500 ms decay). Gate-legato and individual key clicks are a monophonic approximation, and true single-trigger percussion/contact release/foldback/shared tonewheels require model-specific behavior. Intentional mechanical clicks must be controllable and distinct from DSP discontinuities. Add faint tonal leakage only after audition (no unshaped hiss). Use the rotary recipe below; optionally 1.5–2 s reverb mix .10–.15 after the gate/effect. Do not claim this is a Hammond replica.

### 3.2 Rotary speaker approximation — hands-on existing-node patch

A rotating horn and bass rotor alter amplitude, time of arrival and perceived pitch; real cabinets have separate speeds, inertia, crossover and spatial radiation. The [GSi VB3-II manual](https://www.genuinesoundware.com/products/vb3-2/2023/Manual/help.htm) reports measured averages: horn .77 Hz slow/6.9 Hz fast, acceleration ~1 s, deceleration .8 s; bass .72 Hz slow/6.4 Hz fast, acceleration ~6 s, deceleration ~3.5 s.

Try this using current Orbit nodes (see [effects contract](../audio/effects.md) and [usage guide](../usage/sound-and-instruments.md)). Start with a **Sawtooth 220 Hz**, amplitude .15, so energy exists above and below 800 Hz; a 220 Hz sine has no higher harmonics. Split the same Signal into two Filter audio inputs, Low-pass and High-pass, 800 Hz, Q .707 each. Each filter gets its own Gain and Delay. Two **additional sine oscillators** are LFOs; free timing and amplitude 1. They never connect to the mixer as audio.

~~~mermaid
flowchart TD
    S["Audio signal"] --> LP["Low-pass 800 Hz"]
    S --> HP["High-pass 800 Hz"]
    LP --> G1["Bass Gain"]
    HP --> G2["Treble Gain"]
    B["Bass LFO"] -->|Gain modulation| G1
    H["Treble LFO"] -->|Gain modulation| G2
    G1 --> D1["Bass Delay"]
    G2 --> D2["Treble Delay"]
    B -->|Time modulation| D1
    H -->|Time modulation| D2
    D1 --> M["Mixer"]
    D2 --> M
    M --> O["Trim / Master"]
~~~

| Node/connection | Bass | Treble |
|---|---:|---:|
| Gain base | .70 | .70 |
| LFO slow frequency | .72 Hz | .77 Hz |
| LFO fast frequency | 6.4 Hz | 6.9 Hz |
| LFO amplitude | 1 | 1 |
| LFO Signal → Gain modulation cable amount | .15 | .25 |
| Delay timing | Free | Free |
| Delay time base | .020 s | .020 s |
| Feedback | 0 | 0 |
| Wet mix | 1 | 1 |
| LFO Signal → delay time modulation cable amount (seconds) | .0004 | .0008 |

Click the cable/badge and set its amount. Audio links unity; parameter modulation adds signed LFO times coefficient to base. Thus bass gain varies .55–.85, treble .45–.95. Delay time sweeps 19.6–20.4 ms and 19.2–20.8 ms. Recombine only the two branch outputs in Mixer, add conservative Gain trim, then Master. Initially bypass delays to hear independent tremolo; enable them for variable-delay pitch motion (increasing time lowers pitch, decreasing raises it). Vary speed to taste; fast motion can exaggerate pitch excursions, so reduce depth if necessary. Set delay mix fully wet and feedback zero to avoid intentional comb/delay echoes within each branch. Shared LFO per rotor makes a simple coupled amplitude/time motion; phase offsets between amplitude and delay LFOs can be tested separately.

The current low/high filters are not a phase-matched complementary crossover, so summing can cause a spectral notch or coloration near crossover; compare dry and processed spectra, characterize cancellation and avoid clipping; treat this as an approximation. A proper standalone Rotary Speaker node should use complementary crossover, Doppler fractional delays, amplitude directionality, rotor acceleration/deceleration with phase continuity, cabinet/speaker coloration, optional gain-compensated saturation (7.6.6), speed slow/fast/brake, wet/dry and bounded real-time state. Stereo microphone simulation follows 7.5. Do not add separate Leslie-style delay effect to a cathedral organ by default.

### 3.3 Pipe organ ranks (candidate)

Unlike a tonewheel organ, each pipe rank has its own timbre, onset, slight tuning/wind variations and acoustics. [Organteq pipe models](https://www.modartt.com/organteq_physical_modeling) and [research on historical organ spectra and transient behavior](https://doi.org/10.1007/978-3-031-57892-2_13) motivate distinct additive and excitation models. Start with a rank bank with the following **illustrative unmeasured** relative sine partial amplitudes (harmonics 1–8); normalize each rank and trim the mix.

| Rank | Harmonic multipliers, relative to fundamental amplitude 1 |
|---|---|
| Principal / Diapason | 1, .45, .26, .12, .07, .04, .025, .015 |
| Gedackt / Bourdon (stopped) | 1, .035, .26, .02, .09, .01, .035, .005 |
| Reed / Trumpet | 1, .75, .62, .43, .36, .26, .20, .15 |

Example registration: Bourdon 16' at .5× pitch, weight .45; Principal 8' at 1×, weight 1; Principal 4' at 2×, weight .35; Principal 2' at 4×, weight .15; Trumpet 8' at 1×, weight .12. Instantiate each selected rank with shared semitone Pitch and separate AR/ADSR onset; lower pipes generally need slower onset. Model short independent harmonic buildup and transient chiff (quiet filtered noise around 1.5–4 kHz, AD 20–80 ms; include tonal precursors where useful), restrained shared wind pressure, subtle pipe variation and release. Begin with dry timbre; use mono Reverb 5–8 s and adjustable wet mix for cathedral space, preserving a distinct direct voice. Don't use Hammond percussion/key click or Leslie by default. Investigate flute/jet and reed/tube tools for physically generated pipe motion, but do not claim the Faust flute is already an organ pipe model. Add measured rank spectral data or licensed per-pipe samples if necessary.

## 4. Additional instrument candidates and acceptance

| Instrument | Next candidate improvement | Relevant physical mechanism |
|---|---|---|
| Flute | Licensed `pm.fluteModel` experiment; A/B with current sine/noise. | Jet feedback, breath-dependent harmonics, tongue attack. |
| Clarinet | Licensed reed/bore model | Reed pressure discontinuity, resonant tube and register changes. |
| Brass | Licensed lip/tube model; varying pressure and tonguing. | Stronger dynamics change brightness, not just amplitude. |
| Bowed strings | Licensed `pm.violinModel` with expressive controls. | Stick-slip, bow speed/pressure/position, body. |
| Didgeridoo | Lip/tube plus vocal/formant resonance; continuous breath. | Excitation/resonator interaction and moving formants. |
| Plucked strings, guitar, harp | Licensed pluck/waveguide model, body filter/resonator. | Transient, string losses, position-dependent harmonics, sympathetic vibration. |
| Rhodes/electric piano | Strike-excited long fundamental and short metallic modes, velocity-dependent pickup coloration; tremolo. | Tine, hammer, pickup. |
| Handpan/bell/gong/wood percussion | Measure and fit modes and decay, vary strike position/strength, preserve independent ringing notes, investigate coupled nonlinearities and measured or mesh-derived coefficients. | Modal vibration and energy transfer. |
| Acoustic piano | Later dedicated model or licensed multisampling/hybrid after voice allocation. | Stiffness inharmonicity, hammer hardness, multi-string beating, dampers and soundboard. |

For every candidate, native compilation/renderer checks: valid graph/import round-trip, silent idle, pitch at low/mid/high notes and octave transposition, stable sample-rate-dependent tuning, behavior under bounded expression/velocity/modulation, note/gate/trigger timing, repeated onset/legato/release, finite signal at extremes, no unintended clicks, peaks/headroom, bounded delay/memory/state and no structural recompilation on live note or expression changes. Test abrupt parameter input and graph swaps, and benchmark cost at 44.1/48 kHz, with representative desktop/mobile patches, recording compile and steady/external renderer metrics separately. Polyphony must preserve independent releases and use bounded voice allocation (7.2.6c); test chords/voice stealing once available, or use independent mono lanes for a reference chord.

Use [TEST Melody Driver v1](../examples/instrument-audition/reference.orbit2.json) with correct reference pitch and fixed performance inputs; for sustained models add sustained notes, an expression ramp, separated and legato notes; for percussion add strike repetitions and full tail; for organ add register changes and independent chord lanes. Compare original synthetic preset, new candidate and lawful reference with matched levels; assess dry sound, then space/color and musical context. Record exact version, settings, source, native render and measurements, user feedback and license decisions separately. A graph that passes tests does not automatically sound realistic. Label unvalidated candidates as such. Promote only listener-approved revisions, keep rejected/old designs available and preserve approved compositions. No commercial model or recording needs to be copied.

## 5. Delivery and maintenance

See [Phase 7.2.7](../roadmap/advanced.md#phase-727--faust-first-physical-models-planned) for dependency-aware increments. Prefer license audit and small physical model proof-of-concept first; existing-node organ and rotary prototypes can be auditioned independently while licensing/benchmark work proceeds. Expand only after evidence, and coordinate stereo, saturation, note articulation, polyphony, and asset support with their existing milestones rather than duplicate them. Add actual implementation PR references after code lands; this document is a design/recipe guide, not implementation evidence.
