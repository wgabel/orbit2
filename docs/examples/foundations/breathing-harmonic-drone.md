# Sound-design foundation 01 — breathing harmonic drone

[Voice quality and palette](../sound-quality-and-voice-design.md) · [Composition](../generative-ambient-composition.md) · [Theory](../music-theory-and-sound-design.md) · [Production analysis](../mixing-mastering-and-analysis.md) · [Genre techniques](../genres/orbit-techniques.md)

## The principle: design relationships, not just layers

**A distinctive evolving voice can emerge from the relationships between sound generators, modulation sources and processors.** Where the desired sound calls for it, design the nodes to influence one another in musically consequential ways: shared or offset modulation, overlapping partials, controlled nonlinear transformations, and independently moving timescales. A sound should be able to breathe, change character and interact with the rest of the music, rather than merely hold a note while its volume changes. The musical interest can arise without new notes, extra noise, or a large number of effects.

This is a foundational sound-design principle, not a requirement to use the specific drone below in every track, to make all parameters move, or to prefer complex graphs. A clean sub, a restrained sustained note, a deliberate pure tone or a static interval may be precisely what a composition needs. Explore other techniques, build a varied palette, and judge each voice according to its musical purpose. Emergent means the combined audible result arises from the relationship of components; it does not imply randomness, literal feedback, physical modelling or a new DSP feature.

## 1. Listening-validated reference character

The user's original `orbit2-patch-harmonics.json` is a successful reference for this approach. The listener described it as **monumental, heavy, wet, industrial, grunge, dark and grim**, and agreed with the graph-derived description: a deep breathing, slightly distorted harmonic drone with organic, mechanical movement, evocative of a large analog synthesizer, bowed resonance or a spacecraft engine. These are subjective character descriptions, not claims about exact acoustic emulation. The wet impression is achieved with chorus and changing harmonics; this particular patch does not contain a reverb or noise generator.

Useful roles: a monumental foreground drone, dark-tonal foundation, industrial soundscape, evolving analog-style pad, or a sculpted bass texture. This is a **single evolving sound**, not a complete musical piece. Independent melody, harmony, sparse accents, rhythmic contrast and arrangement can grow around it. Keep register and density in mind when combining it with another bass or pad.

## 2. Reconstruct the original graph

All oscillators are Free Hz, phase 0°, base amplitude 1 unless noted; pitch tracking is Off. Patch BPM is 120, but the active sound's timing is free-running, not tied to transport tempo. The analysis below describes the saved, settled mute states. These are the exact *reference* settings, not a safe mastered preset; see the gain warning below before auditioning.

| Active source or processor | Settings | Route / amount |
| --- | --- | --- |
| Saw A1 | 55 Hz | Harmonic Mixer audio / 0.85 |
| Saw A2 | 110 Hz | Harmonic Mixer audio / 0.50 |
| Saw A3 | 220 Hz | Harmonic Mixer audio / 0.25 |
| Pitch Control | Transpose +1.43 st, fine −100 cents, output +0.43 | Each saw's **Frequency** (not Pitch) / 1 |
| Shaping sine | 0.1 Hz, base amplitude 0.28 | Harmonic Mixer → sine Amplitude / 1; sine audio → Shape Mixer / 1 |
| Dry harmonic sum | Harmonic Mixer output | Shape Mixer audio / 1 |
| Chorus | Rate 0.45 Hz, depth 7 ms, base delay 22 ms, mix 0.50 | Shape Mixer → Chorus / 1 |
| Low-pass Filter | Cutoff 1190.05 Hz, Q 0.50 | Chorus → Filter / 1 |
| Filter sine | 0.1 Hz, amplitude 1 | Filter Cutoff / +800 Hz |
| Final Mixer and Master | No separate final attenuation in original | Filter → final Mixer → Master / 1 each |

There is also a 0.5 Hz triangle (amplitude 1) → Frequency of a 1 Hz sine (amplitude 1) with amount +100 Hz, then into the final Mixer at unity. The 1 Hz sine is **muted** in the original file, so this branch has no audible contribution. Do not attribute the reference sound to it; if exploring it separately, note that Frequency is clamped to the supported positive range and the unmuted branch can generate both subsonic and audible output and overload the mix.

~~~mermaid
flowchart TD
    S[Three weighted saws] --> H[Harmonic Mixer]
    H --> M[Shape Mixer]
    H -->|Amplitude modulation| W[0.1 Hz shaping sine]
    W --> M
    M --> C[Chorus]
    C --> F[Low-pass Filter]
    L[0.1 Hz filter sine] -->|Cutoff modulation| F
    F --> O[Final Mixer and Master]
~~~

The Pitch Control fan-out, omitted from the diagram for clarity, adds the same +0.43 Hz to all three saw frequencies. Use the real node contracts: [oscillators](../../audio/oscillators-and-noise.md), [pitch](../../instruments/pitch-control.md), [additive modulation](../../core/parameters-and-modulation.md), [effects](../../audio/effects.md), [mute](../../editor/audition.md). The topology is feed-forward: routing one source to both audio and modulation inputs and recombining them downstream is not an editable graph cycle.

## 3. Why the sound breathes

**Harmonic overlap and slow beating.** A saw contains all integer harmonics with an idealized amplitude falloff of 1/n. Octave layering emphasizes a common fundamental and reinforces overlapping partials. The descending weights keep A1 dominant. Pitch Control normally outputs semitones, but connecting its numeric offset at unity to Frequency interprets it as Hz. Actual frequencies in this patch are therefore 55.43, 110.43 and 220.43 Hz, which are not exact octaves. For example, `2 × 55.43 = 110.86 Hz` and `110.43 Hz` differ by 0.43 Hz (about 2.33 s beat period); `4 × 55.43 = 221.72 Hz` and `220.43 Hz` differ by 1.29 Hz (about 0.78 s). Other overlapping partials beat at different rates, continuously changing shape and intensity. Perfect octave tracking is valuable for many instruments, but this intentional or discovered additive-Hz detuning is part of the reference's character. Do not silently replace it with exact semitone tracking. For a transposable version, preserve the designed relative offsets deliberately and audition across registers; do not assume the same effect scales unchanged.

**Reverse-direction modulation.** Ordinarily an LFO modulates a pitched oscillator. Here an audible harmonic sum modulates the amplitude of a slow oscillator, and its result is added to the original sum. In the settled implementation, let `x(t)` be the weighted saw sum and `s(t) = sin(2π × 0.1 × t)`. Then the shape mixer receives:

```
a(t) = clamp(0.28 + x(t), 0, 1)
y(t) = x(t) + s(t) × a(t)
```

Within the unclamped region this behaves as `x(t) × (1 + s(t)) + 0.28 × s(t)`. At the bounds it behaves differently; the nonnegative amplitude clamp changes the waveform and harmonic/sideband content. The slow sine does not simply become a heard 0.1 Hz note. Its output contains audio-rate components because its amplitude is being driven at audio rate. Reinforcement and partial cancellation change with the sign and magnitude of the slow sine, creating a dynamic spectral character and asymmetric nonlinear color. This is not four-quadrant ring modulation, a dedicated saturation node, or a feedback loop. The nonlinear color and eventual delivery clipping are distinct phenomena; preserve the former, avoid the latter.

**Coordinated spectral breathing.** Both 0.1 Hz sines start at the same phase and have a 10-second period. The cutoff sweeps in phase between 390.05 and 1990.05 Hz: the reinforcing half of the shaping cycle coincides with a more open filter, the cancelling half with a darker cutoff. This is meaningful coordination rather than arbitrary modulation. At Q 0.50 the filter provides a smooth darkening rather than a strong resonance peak. Increasing Q would change that character and may raise peaks.

**Interleaved timescales.** The 0.45 Hz chorus (period ≈2.22 s, nominal delay modulation 15–29 ms) combines the dry sound with two moving, fractionally delayed copies. The shifting phase relationships add depth/texture and changing spectral interference. It is mono, not stereo widening. At the same time the harmonic beating operates on its own sub-second and multi-second cycles, and the 10-second macro breath changes their audibility. The breath repeats, but the whole signal need not repeat exactly every ten seconds. The different timescales make the recurring gesture less mechanical without requiring random pitch changes.

## 4. Measurements and safety

The reference patch was numerically reconstructed for 30 seconds at 48 kHz in mono using the saved graph, additive/clamped controls, band-limited saws and independent implementations of the filter and chorus. This was **not native Faust/WASM playback**; the listener confirmed the descriptive character but did not confirm these measured values in the app. The measurements below are pre-clipping floating-point signal estimates, not perceived loudness or true-peak meter results. No changes were made to the reference file.

| Signal stage / comparison | Sample peak (unit full scale = 1) | RMS | Samples above ±1 | Estimated 0–20 Hz spectral energy |
| --- | ---: | ---: | ---: | ---: |
| Harmonic Mixer alone | 1.593 | 0.585 | 7.51% | 0.0% |
| Shape Mixer, before chorus | 2.583 | 0.697 | 14.36% | 12.9% |
| Master as saved | 2.142 | 0.462 | 3.82% | 30.3% |
| Master with shaping branch omitted (same chorus/filter) | 1.266 | 0.335 | 0.19% | 0.0% |
| Master with hypothetical 20 Hz high-pass | 1.963 | 0.375 | 1.78% | ≈0.0% |

Energy percentages were estimated with Welch power spectra (10 s segments). The shaping sine's effective amplitude is clamped at 0 for 33.4% and at 1 for 12.2% of samples; this is direct evidence of the nonlinear interaction. After the hypothetical 20 Hz high-pass, a bright window (2–3 s) had RMS 0.594 and 4.2% of its estimated spectral energy above 500 Hz; a dark window (7–8 s) had RMS 0.149 and 0.4% above 500 Hz (1 s windows, 16,384-sample spectral segments). The approximately 12 dB RMS difference illustrates the coordinated breathing; it is not a perceived-loudness measurement. The 0–20 Hz energy is largely outside normal hearing and wastes significant headroom; it may also move speakers and interact with real output processing. A sample peak above 1 signals potential clipping at normalized output/delivery, not proof of how the app or device actually clipped.

For a cautious audition, add a Gain at the end of the reference lane before Master (start low or muted). Investigate a local high-pass at about 20–25 Hz after the existing filter, then a final Gain around 0.4 as a starting point: the independently measured 20 Hz high-passed peak × 0.4 is approximately 0.79 (−2.1 dBFS sample peak). This is a hypothetical starting adjustment, not a native-verified ceiling, limiter, finished composite or an instruction to normalize every voice to that value. The high-pass reconstruction used a second-order 20 Hz Butterworth response; actual Orbit settings, other sources, long beating cycles and phase shifts can change peaks. Re-measure over the intended duration with actual routing and leave margin for other musical layers. Altering the harmonic mixer *upstream* also changes the shaping sine's clamp behavior, so compare at matched listening level before deciding to do so. A final trim preserves the internal relationship more faithfully. If an artist likes intentional grit, retain designed harmonic character without relying on uncontrolled master clipping.

## 5. Transfer the principle to new sounds and pieces

Ask what changes in the sound beyond its level; what creates the changes; how those movements relate; and what the listener is meant to notice. Some useful experiments, not required ingredients:

| Design relationship | Possible audible result | What to check |
| --- | --- | --- |
| Adjacent partials or small unequal tuning offsets | Organic beating and evolving overtones without noise | Pitch stability, mono cancellation, useful harmonic movement across registers |
| Parallel audio and audio-rate modulation of a controlled parameter, recombined downstream | Changing nonlinear color or new sidebands | Clamp behavior, spectral products, subsonic offset, peak margin; no editable cycle |
| One shared contour on cutoff and amplitude/body, or a purposeful phase offset | Coordinated breath, attack and decay, or counter-motion | Are both effects musically audible, and does the shared rhythm become repetitive? |
| Differently timed modulation of select partials or delay/chorus | Evolving texture with an underlying stable pitch or motif | Unwanted pitch jumps, roughness, tail overlap, depth rather than indiscriminate wetness |
| Independent voices responding to an authored shared harmonic or rhythmic control | Call/response and cohesion at the piece level | Leave room for other instruments, allow silence, shape beginnings, returns and endings |

Try replacing some saws with triangle or pulse, changing partial ratios/modulation depth, sharing or offsetting the two LFO phases, moving the cutoff more subtly, or directing an envelope or a shaped audio-rate source into another appropriate modulation input. These are exploration directions rather than verified variants. A brighter melodic pad, a delicate bell, a restrained bass phrase or an aggressive experimental voice may need entirely different relationships. Preserve a clean low-frequency anchor when several bass layers coexist. For a whole composition, introduce and withdraw the drone or let its harmonic center evolve; the 10-second breath alone is not a long-form arrangement strategy. Design the other voices to complement or answer it, not copy it.

When evaluating candidates, retain a baseline; compare the interacting branch on/off at matched level; measure the dry source, transformed branch, final voice, and full mix in bright and dark phases; verify actual routing, mute states, parameter units and bounds; and obtain human listening feedback where possible. Distinguish measured behavior, inferred timbre and listener-confirmed preference. A graph with one well-chosen interaction can be more interesting than a hundred unrelated LFOs. Build new foundations when a distinct musical relationship proves useful, and document both its sound and the reason it works.
