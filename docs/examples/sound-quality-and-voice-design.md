# Sound quality and voice design — beyond raw synthesis

[Architecture](../../ORBIT2-ARCHITECTURE.md) · [Theory and sound design](music-theory-and-sound-design.md) · [Instrument recipes](instrument-recipes.md) · [Genre techniques](genres/orbit-techniques.md) · [Mixing, mastering and analysis](mixing-mastering-and-analysis.md)

This is a practical companion for **finished musical voices**, rather than a catalog of new node types. A graph can be valid and its harmony and arrangement good while its instruments sound thin, hollow, synthetic in an unintended way, or tiring. Work on the instrument before trying to fix its identity on the master. A sound with few nodes can be excellent; complexity, extra oscillators and maximum wet effects do not automatically improve it.

## 1. Musical quality targets and feedback

For polished ambient, space music, melodic electronica and bass hybrids, prefer distinctive, dimensional and evolving voices. Avoid the feel of an isolated test oscillator or an exposed steady broadband noise source. This preference comes from the listening feedback on the original ten-minute *Event Horizon — A Signal Returns* patch: the composition and transitions worked, several voices were appreciated, but prominent recognizable noise and hollow/basic synth timbres reduced the quality of the whole. Preserve the successful form when evaluating a revised palette. It is not a rule banning experimental noise, deliberate chip-style tones, minimalist sine music or clear sub fundamentals; a different brief may deliberately choose those qualities.

| Audible symptom | First inspect | Try instead |
| --- | --- | --- |
| A naked sine sounds like a test tone | Partial content, envelope, isolation and context | Keep sine as fundamental/control; add a subordinate octave/third partial, shaped body, transients or evolving effects appropriate to the voice. |
| Thin or hollow lead | Weak middle harmonics, overly simple envelope, competing pads, excessive high-pass | Layer triangle with quiet saw or carefully selected harmonic partials; dynamic brightness and articulation; leave space in the other voices. |
| Static buzzy saw/square | Fixed cutoff/width, identical attack and decay, harsh upper partials | Filter its brightness, modulate cutoff or square duty moderately, mix quieter contrasting waveforms; vary attack/decay and rests. |
| Obvious white/pink hiss or wind | Dry noise path, broadband spectrum, exposed section, long reverb tails | Lower level at source and after effects; narrow/shifting resonant bands, envelope-shaped bursts, tonal anchors and selective wet processing. |
| Entire mix sounds distant/muddy | Reverb on every layer, low end in tails, overlapping chord voices | Controlled depth planes; dry foundation, fewer overlapping sources, high-pass selected mid/texture lanes and wet returns. |
| Harsh loud climax | Peak and short-term level, wideband noise, resonant sweeps, overlapping transients | Fix conflicting roles and reduce source levels or peak-producing modulation; reserve intensity for meaningful gestures, not clipping. |

## 2. Treat noise as material, not a finished texture

Noise has enormous musical value as an excitation source, breath, percussion, debris or an evolving atmosphere. For an unobtrusive background, begin with the noise contribution inaudible, slowly raise it until the texture subtly changes the **whole mix**, then back off if it attracts attention as plain hiss. Use a mute/bypass comparison in context at the quietest and loudest sections where it is present, not only soloed or in a busy climax. A low noise RMS does not guarantee unobtrusiveness: dense noise can be perceptually prominent in sensitive frequency bands, mask a melody, or become exposed in sparse passages. Pink/brown are spectral colors, not inherently quiet.

If a louder texture is intentional, transform its identity: narrow the spectrum, create moving resonance and amplitude contours, combine it with pitched partials, and use distance or echo selectively. Simply feeding white noise into a long reverb commonly leaves a recognizable wash; a cutoff high enough to pass most of its spectrum also changes very little. Make the modulation audible as a **musical gesture**, not just an imperceptible LFO. Build from silence; no fixed noise-to-tone RMS ratio works for every register or context.

### Three editable noise transformations

These are starting configurations using current mono nodes; audition and measure the outputs, particularly with high Q or feedback. In Eq/Gain/Filter ranges refer to [effects](../audio/effects.md); positive and negative modulation follows the [parameter contract](../core/parameters-and-modulation.md).

| Texture | Graph and suggested starting values | What to evaluate |
| --- | --- | --- |
| Distant ion cloud | Pink Noise amplitude 0.12 → high-pass 400 Hz Q 0.7 → band-pass 950 Hz Q 1.7 → Gain 0.045 → Reverb 7 s, damping 0.75, wet 0.8 → output trim 0.5. Two sine LFOs: 0.017 Hz ±300 Hz to band-pass, 0.013 Hz ±0.025 to pre-effect Gain. | Slow, faint moving color behind the pad. Turn up the tone-bearing layer first; if hiss remains audible lower the post-effect trim or narrow the band. |
| Resonant dust / alien breath | White Noise amplitude 0.15 → parallel band-pass branches near 380 Hz Q 4 and 1250 Hz Q 3, each Gain 0.15 → Mixer → high-pass 180 Hz → Gain base 0, AD amount 0.15 (Attack 1–3 s, Decay 3–6 s; free Clock 0.055 Hz) → Delay 0.65 s, feedback 0.28, mix 0.35 → Reverb 5 s, damping 0.7, mix 0.5. Optional very slow cutoff movement ±100 Hz and ±250 Hz respectively. | The sound should appear as occasional resonant moving events, not a continuous spray. High Q emphasizes a band, not an exact playable modal resonator. |
| Synthetic shimmer / distant radio | Short AD-shaped noise (amplitude base 0, envelope depth 0.06, attack 25 ms, decay 160 ms) → band-pass ~2.1 kHz Q 2 → Mixer with a quieter, separate tapered triangle or pitched bell partial → Delay 0.4 s, feedback 0.22 wet 0.3 → Reverb 3–5 s wet 0.45 → trim. Trigger occasionally with a free Clock. | A pitched transient with noisy edge and distant reflections; the noise should be hard to identify as a raw generator. Adjust event density, high end and dry level before turning up the whole voice. |

For drums, deliberately audible short filtered noise is useful (snare, clap, hat); it should not sound like identical unshaped bursts. Separate body and noise envelopes for snare (body shorter, noise longer), differentiate open/closed hats by decay and spectral band, use reduced ghost-hit levels and preserve silence. A high-frequency hat can dominate a quiet pad even if its broadband RMS is small. Do not fix noisy hats by cutting the high frequencies of every other voice.

## 3. Give foreground tones an identity

Distinguish four ingredients when useful: **fundamental** (pitch/weight), **body** (harmonic or inharmonic partials), **gesture** (articulation and independent timbral movement), **environment** (reflections, depth and decay). Not every voice needs all four as separate nodes; e.g. subtractive saw plus a good filter envelope may already supply pitch, body and gesture. Avoid unchanged sine or identical detuned sine layers as the entire expressive lead/pad in a polished piece. Deliberately pure sub, kick body, a very quiet partial, reference oscillator and LFO are useful exceptions; add upper bass content in a separate lane rather than smearing the deep fundamental with reverb. Reverb alone cannot create missing harmonic content, and a linear filter on a steady pure sine does not introduce harmonics.

| Method | Construction | Musical effect / caution |
| --- | --- | --- |
| Additive body | Fundamental plus lower-level octave, third harmonic and/or higher partial with its own decay | Weight with personality. Harmonic partial ratios 2:1, 3:1, etc.; a 3× partial is not the third scale degree. Avoid stacking several full-level oscillators. |
| Subtractive voice | Saw/triangle or pulse → resonant filter, separate amp/cutoff envelope | Bright attack, warm body, dark ending; use restrained Q and verify peak under sweeps. |
| Detuning and drift | Small relative detune (e.g. 4–9 cents), differing phase/amplitude, slow LFO or mono chorus | Animation; check cancellation and roughness in mono. Do not detune every note and do not call mono chorus stereo width. |
| Inharmonic / FM-like color | Audio-rate oscillator into free Frequency Hz modulation; separate decays or filter | Metallic/physical-feeling gestures, complex sidebands; limit modulation depth so effective frequency remains valid. Orbit is not a dedicated FM/PM operator bank. |
| Transient plus body | Short brighter burst/partial and longer darker body, shared Trigger, different AD timings | Played feel even with a fixed grid; time the envelopes and control accent levels. |

### Candidate instrument recipes (not shipped presets)

Use shared semitone tracking and pitch ratio preservation for note banks. Test in low/mid/high registers, and at different articulation rates. All numeric values below are suggested starts, not audition-validated final presets.

| Voice | Sources and shaping | Spatial treatment |
| --- | --- | --- |
| Luminous space lead | Triangle at 220 Hz, envelope depth 0.12; saw at 220 Hz detuned +5 cents depth 0.035; sine at 440 Hz depth 0.018. Shared AD 12/550 ms for body, separate 3/180 ms AD for saw/brightness. Mixer → low-pass base 900 Hz, Q 0.9, brightness AD depth +1900 Hz. | Delay 0.45 s / 0.28 feedback / 0.23 wet, Reverb 3.2 s / damping 0.65 / wet 0.16; clear front rather than a pure ringing sine. |
| Warm breathing pad | Three chord-part voices, each triangle plus quieter saw at same pitch (+4–7 cents); individual oscillator amplitudes roughly 0.04 and 0.015, independent pitch lanes where useful. Mix → high-pass 110 Hz if bass is separate → low-pass 850 Hz Q 0.75, 0.011 Hz LFO ±350 Hz. Gently shape gain on a long envelope or arrangement lane. | Chorus 0.18 Hz, depth 3 ms, base delay 19 ms, mix 0.25; reverb 5 s, damping 0.65, mix 0.25–0.4. |
| Felt-like pluck | Triangle fundamental depth 0.10, sine 2× depth 0.03, sine 3× depth 0.013, and optional very quiet band-passed attack noise; AD body 7/750 ms, partials 3/240 and 2/100 ms. Low-pass ~1800 Hz with gentle discrete or AD-driven opening. | Small room 1.5–2.4 s, wet 0.12–0.2; minimal echo, leave natural decay. A synthetic pluck, not an acoustic-piano emulation. |
| Moving wave/Reese voice | Two quiet detuned saws, same tracked pitch at nominal 82.4 Hz ±8 cents, each AD amplitude depth ~0.10; high-pass 130 Hz → resonant low-pass 500 Hz Q 1.2 with AD depth +1300 Hz, slow LFO ±180 Hz. Deliberate hit mask; optional second parallel shaped, higher mid-band filtered layer. | Dry core and clean separate sub. Very subtle delay only on selected mid-bass response; no reverb on sub. |

Use envelope base amplitude 0 for event-shaped oscillators. Pick envelope duration/clock timing that fits intended notes and rests; see [envelopes](../audio/envelopes.md), [musical techniques T2–T3](genres/orbit-techniques.md#t2--melody-plucks-and-accents). For a reworked palette, save a baseline, make one purposeful timbre change at a time, level-match, then compare solo and in arrangement. Avoid adding three layers solely because the earlier recipe contained three.

## 4. Build a reusable, auditioned palette

An instrument preset is not finished just because it imports, emits a tone or has attractive parameter settings. Establish a small set of complementary **roles** before creating a complete track: e.g. a warm pad, luminous lead, darker answering pluck, shaped air, clean sub, two bass timbres, and a tuned drum kit. Use a short pitch sweep/motif, repeated articulation, rests and a representative space/tail to audition each one. Write a short intent (foreground, support, texture, impact or foundation), usable register, input/trigger semantics, typical settings, dependencies, output level and expected weaknesses. Avoid over-designing a sound outside its eventual musical context.

For individual-node presets, follow [node presets](../editor/node-presets.md); for composite voices use existing editable composite graphs and the [reusable modules plan](../editor/reusable-modules.md) as it becomes available. Do not assume the planned independent user module library or macro ports exist. The existing composite output rack is the voice's Audio output; put Master only at the root. When available, [note cards](../editor/patch-annotations.md) can describe controls and musical purpose using the supported schema. Keep all presets editable, with a dry/wet and level comparison path where practical. A new preset implementation or library change is distinct from a documentation-only candidate recipe.

**Review loop:** isolate → measure/audition → modify → compare at matched level → test in mix → retain or reject. For an entire piece, use the [production and mastering guide](mixing-mastering-and-analysis.md) before delivery. Record which voices were actually rendered or human-auditioned; do not certify quality from a parameter list or numeric meter.

Further reading: [Ableton instrument reference](https://www.ableton.com/en/live-manual/11/live-instrument-reference/) illustrates oscillator, noise, filter and envelope sound design; [music theory and sound design](music-theory-and-sound-design.md) provides acoustic and psychoacoustic context. These sources inform techniques, not claims about available Orbit devices.
