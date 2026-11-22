# Music theory, psychoacoustics and electronic sound design

For applying these principles to a particular style, see the [electronic music genre reference](electronic-music-genres.md) and its linked [Orbit 2 techniques](genres/orbit-techniques.md).

For quality-focused production, see [sound quality and voice design](sound-quality-and-voice-design.md) for rich voices, quiet/shaped noise and candidate presets, and [mixing, mastering and analysis](mixing-mastering-and-analysis.md) for true peak/LUFS, balance and iterative rendering.

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Generative ambient composition](generative-ambient-composition.md) · [Instrument recipes](instrument-recipes.md) · [Roadmap](../../ROADMAP.md)

**Purpose:** A practical musical reference for people and agents creating Orbit 2 patches, instruments, effects and complete pieces. It explains why particular choices may work, how to implement them with the available nodes, and how to evaluate the result. This is not a rulebook for what music must sound like or an implementation specification for new DSP features. All numerical recipes are starting points, not claims of measured or listening-tested sound quality.

Read only the sections relevant to the task. Technical capabilities and limits belong to the linked feature documentation and code; the [generative composition guide](generative-ambient-composition.md) owns long-form arrangement examples. Use both when composing a complete ambient piece.

## 1. What makes music work?

Good music generally establishes a relationship between **coherence and contrast**. The listener learns a sound world (pitch material, recurring motif, pulse, timbral palette), then discovers something new within it. A meaningful change need not be a new chord: moving a filter, bringing a distant texture forward, changing register, or withholding a familiar note can be equally expressive.

| Dimension | What to ask | Common failure |
|---|---|---|
| Harmony | Which pitches coexist, change and resolve? | Every oscillator is tuned independently with no intentional relationship |
| Melody | What can the listener remember or anticipate? | Endless unrelated notes or no phrasing |
| Rhythm | When does energy arrive, repeat or disappear? | All voices have the same pulse or all are unrelated |
| Timbre | What makes each sound recognizable? | Many similar bright waveforms compete for attention |
| Dynamics | Which elements are prominent and which are background? | Everything is loud all the time |
| Space | Which sounds feel close, distant, dry or diffuse? | Every layer has the same long reverb |
| Form | What changes over seconds, minutes and the whole piece? | A short loop repeats indefinitely without development |

For an individual sound, focus on timbre, pitch, articulation, useful controls and a suitable output level. For an effect, consider the input/output transformation, including dry/wet behavior. For a whole piece, first choose its intent, harmonic language, roles and form; only then design the voices. A beautiful evolving drone may be the entire artistic concept, but do not mistake adding many nodes or effects for actual composition.

These practices apply especially to electronic music because a synth lets the composer control everything. That freedom is an opportunity, not a requirement to modulate every available parameter. Noise music, atonality, minimalism, dissonance, microtonality and static drones can be deliberate aesthetics. Avoid treating Western tonal conventions or any production genre as universal laws.

## 2. Pitch: frequencies, octaves, notes and intervals

Pitch is the perceived highness or lowness of a sound; frequency is a physical rate in Hz (cycles per second). For a periodic harmonic sound the fundamental usually anchors its pitch, although perception can infer a missing fundamental from its harmonics. An octave doubles frequency; two octaves multiply it by four. A semitone is a step of the usual 12-tone equal-tempered (12-TET) scale; one cent is 1/100 of a semitone.

Orbit's [musical pitch contract](../instruments/pitch-control.md) uses A4 = 440 Hz, MIDI note 69:

```
frequency(note) = 440 * 2^((midiNote - 69) / 12)
relativePitch(f, reference) = 12 * log2(f / reference)   # semitones
cents(f, reference) = 1200 * log2(f / reference)
transposedFrequency = baseHz * 2^(semitones / 12)
```

Examples: A1 = 55 Hz, A2 = 110 Hz, A3 = 220 Hz, A4 = 440 Hz. Middle C is C4 ≈ 261.63 Hz. A useful A-minor vocabulary in one octave:

| Note | Semitones above A2 | Frequency (Hz) | Function relative to A |
|---|---:|---:|---|
| A2 | 0 | 110.00 | Root |
| B2 | 2 | 123.47 | Second / ninth |
| C3 | 3 | 130.81 | Minor third |
| D3 | 5 | 146.83 | Fourth |
| E3 | 7 | 164.81 | Fifth |
| F3 | 8 | 174.61 | Minor sixth |
| G3 | 10 | 196.00 | Minor seventh |
| A3 | 12 | 220.00 | Octave |

When sharing pitch between several oscillators, use Free (Hz), enable semitone tracking and fan out the same Pitch Control or Note Sequencer output to their Pitch inputs at unity amount. Do not send identical additive Hz amounts to carriers with different frequencies if musical intervals must remain fixed. Raising 110 and 220 Hz by 110 Hz gives 220 and 330 Hz (a fifth, not an octave); +12 semitones gives 220 and 440 Hz. A configured relative pitch of zero means the oscillator's own base tuning, not necessarily A4. Set reference notes consistently and use the actual units of the destination port. Do not use the frequency of an LFO as its modulation depth.

### Interval palette

These are common Western 12-TET interval names within one octave. Ratios in the third column are equal-tempered multipliers, not exact small-integer tuning.

| Semitones | Interval | Ratio | Common musical use (context-dependent) |
|---:|---|---:|---|
| 0 | Unison | 1.0000 | Unity, doubled timbre |
| 1 | Minor second | 1.0595 | Close tension, friction |
| 2 | Major second | 1.1225 | Suspended or neighboring tone; ninth if spread |
| 3 | Minor third | 1.1892 | Minor chord color |
| 4 | Major third | 1.2599 | Major chord color |
| 5 | Perfect fourth | 1.3348 | Open suspension, quartal harmony |
| 6 | Tritone | 1.4142 | Ambiguity, tension, symmetry |
| 7 | Perfect fifth | 1.4983 | Stable open interval, spacious pedal |
| 8 | Minor sixth | 1.5874 | Dark or rich color depending on context |
| 9 | Major sixth | 1.6818 | Open color; melodic lift |
| 10 | Minor seventh | 1.7818 | Seventh-chord color, modal tension |
| 11 | Major seventh | 1.8877 | Close-to-octave tension, lush upper voice |
| 12 | Octave | 2.0000 | Stable doubling in another register |

Intervals are relationships, not absolute Hz differences. The same 10 Hz difference has very different musical meanings at 100 Hz and 1000 Hz.

### Consonance, dissonance and tuning

Simple ratios such as 2:1 (octave), 3:2 (fifth), 4:3 (fourth), 5:4 (just major third) and 6:5 (just minor third) align many harmonics when the timbres are harmonic. Nearby partials can create audible beating or roughness; the effect depends on their spacing, register, spectrum and level ([Plomp & Levelt, 1965](https://doi.org/10.1121/1.1909741)). Consonance is also a cultural and musical preference, not merely a property of a ratio ([McDermott et al., 2016](https://doi.org/10.1038/nature18635)).

For an open drone at 110 Hz, try exactly 165 Hz (3:2) and 220 Hz (2:1). In 12-TET the fifth is about 164.81 Hz. The exact ratio can feel settled on a fixed drone; equal temperament is convenient for tracking a chromatic keyboard and modulating between tonal centers. Do not combine just-tuned and equal-tempered doubles unknowingly. Their difference can be a desired slow shimmer or an unwanted beat. No tuning system or interval is intrinsically good or bad.

## 3. Scales, harmony and tonal color

A tonal center is the pitch that feels like home. A scale is a pitch collection; a chord is a simultaneous pitch combination; a mode gives a collection a particular tonal center and interval pattern. Keeping a scale does not force every possible simultaneous note to be consonant. Decide which pitches are structural chord tones and which are passing, suspended or intentionally tense.

| Scale / mode | Semitone offsets from tonic | Example on A | Possible character, not a guarantee |
|---|---|---|---|
| Major (Ionian) | 0,2,4,5,7,9,11 | A B C# D E F# G# | Clear, expansive |
| Natural minor (Aeolian) | 0,2,3,5,7,8,10 | A B C D E F G | Reflective, dark |
| Dorian | 0,2,3,5,7,9,10 | A B C D E F# G | Minor with a brighter sixth |
| Mixolydian | 0,2,4,5,7,9,10 | A B C# D E F# G | Open, less leading-tone tension |
| Lydian | 0,2,4,6,7,9,11 | A B C# D# E F# G# | Floating, raised-fourth color |
| Phrygian | 0,1,3,5,7,8,10 | A Bb C D E F G | Close second tension |
| Minor pentatonic | 0,3,5,7,10 | A C D E G | Sparse melodic vocabulary |
| Major pentatonic | 0,2,4,7,9 | A B C# E F# | Open melodic vocabulary |
| Harmonic minor | 0,2,3,5,7,8,11 | A B C D E F G# | Strong leading tone, exotic to some listeners |

Choose a vocabulary that suits the target, not one automatically imposed on all patches. A stable pedal/drone can support a modal improvisation even with no conventional chord progression. If a minor pentatonic melody moves over a chord outside the pentatonic collection, check the actual vertical intervals at that moment; scale membership alone is not an error detector.

### Chords as interval recipes

| Chord | Semitone offsets from root | Example on A |
|---|---|---|
| Open fifth | 0,7 (optional 12) | A E A |
| Minor triad | 0,3,7 | A C E |
| Major triad | 0,4,7 | A C# E |
| sus2 | 0,2,7 | A B E |
| sus4 | 0,5,7 | A D E |
| minor seventh | 0,3,7,10 | A C E G |
| major seventh | 0,4,7,11 | A C# E G# |
| dominant seventh | 0,4,7,10 | A C# E G |
| minor add9 | 0,3,7,14 | A C E B |
| major add9 | 0,4,7,14 | A C# E B |
| diminished | 0,3,6 | A C Eb |
| augmented | 0,4,8 | A C# F (spelled E# in this chord) |

An inversion changes which chord tone is lowest. Voicing means how the chord tones are distributed over octaves. For a synth pad, try spreading the root and fifth in the low range and placing the third, seventh or ninth higher. Several strong low-frequency thirds and sevenths can sound congested with rich saws; the same chord spread across registers may sound spacious. This is a practical default, not a ban on low clusters.

### Worked harmony: space-ambient progression

Play these chords slowly (e.g. 20–30 seconds each), with the bass and upper pitches assigned to independent tracks. The chord names use the notes explicitly shown; the voicing is more important than the label.

| Step | Bass | Upper voice 1 | Upper voice 2 | Upper voice 3 | Color |
|---|---|---|---|---|---|
| 1 | A2 | E3 | C4 | B4 | Am(add9) |
| 2 | F2 | F3 | C4 | A4 | F major |
| 3 | C3 | E3 | B3 | G4 | Cmaj7 |
| 4 | G2 | D3 | B3 | A4 | G(add9) |

This is one possible A-natural-minor progression. Return to step 1 or leave the last chord suspended before returning later. For a pedal variant, keep A2 constant and listen to how the upper chords change against that reference; the resulting harmonies differ from the original names. A continuous A against G(add9) emphasizes tension rather than making a standard G chord. Change tension purposefully.

**Voice leading** means managing how each individual note moves between harmonies. In the example C4 is shared by the first two chords; B3 is shared by the last two. Keep common tones or move a voice by a semitone/whole tone when convenient; it makes change coherent. Avoid automatically transposing every voice in lockstep when the intention is a genuinely changing chord quality. Parallel fifth motion is useful for a drone, but a fixed chord transposition is different from independent voice leading.

In Orbit, use one pitch sequence per independently moving voice, with matching reference/base notes and a common transport step grid. Choose four equally timed steps in each lane and start them from the same transport reset. Shared Pitch Control is ideal for fixed-interval transposition; separate Note Sequencers are needed for the chord changes above. Glide 1–4 s for a fluid transition, or use brief independent amplitude fades. Glide is continuous motion through intermediate pitches, not instantaneous note replacement; listen for unwanted clashes during the transition, especially with reverb tails. See [Note Sequencer](../instruments/note-sequencer.md) for actual implementation limits and timing.

## 4. Melody, rhythm and controlled variation

Melody is not simply a succession of correct pitches. Begin with a motif: a small distinctive pattern (perhaps A4–C5–E5, then E5–D5–A4), and vary its ending, rhythm, octave or silence. Give the motif a contour (rise, fall, arch) and space for anticipation. For a supporting chord, place accented or long sustained notes on its structural tones; passing notes need not be chord tones. A sparse minor-ninth color may be effective in ambient music, while a sustained clashing lead may distract when unintended.

- Let one element carry the main melodic identity at a time. Use a less active answering voice, not two equally busy leads in the same register.
- Repeat enough for recognition, but vary one aspect at a time. A recognizable motif transformed over minutes feels related; wholly random sequences often lose identity.
- Leave genuine rests. In the current pitch-only Note Sequencer, **Hold preserves pitch, not silence**. Use Clock/manual Trigger, AD/AR envelopes or a separately designed amplitude path for articulation. Do not describe Hold as Note Off or depend on planned Note-mode gate/velocity output.
- Select musical notes deliberately; unquantized continuous noise on frequency gives pitch wander, not automatically scale-constrained melody. For generative pitch, choose a bounded authored pattern or use only an actually available quantization feature. Label hypothetical probability/scale-quantizer nodes as future features.

Rhythm organizes events and perceived pulse. Ambient can have a pulse without drums. For BPM = B, one quarter-note beat is 60/B seconds (in a quarter-note beat convention); half note = twice this, eighth note = half, dotted quarter = 1.5 times and triplet eighth = 1/3 of a beat. At 72 BPM one beat ≈ 0.833 s and a four-beat bar ≈ 3.33 s. A dotted-eighth delay at that tempo is 0.625 s. Use supported musical divisions or free-seconds settings as appropriate, and verify actual node labels.

| Technique | Example | Why use it |
|---|---|---|
| Repetition | 4 equally spaced quiet plucks | Establish a reference pulse |
| Syncopation | Place an accent between regular beats | Surprise without losing the pulse |
| Polymetric pattern lengths | 5-step melody with 7-step accent loop at common step duration | Changing combinations; joint pattern repeats after 35 steps |
| Swing / humanization | Deliberate small timing deviations | Less mechanical feel; do not claim unsupported swing controls |
| Density changes | Gradually bring in and remove accents | Create long-form dynamics without speeding up |
| Call and response | Motif followed by a quieter answer | Space and dialogue |

Use shared transport for timing that should remain synchronized; free-running modulation for drift that should not align to that grid. A changing BPM changes musical-time durations but not free seconds. If free-running LFOs start at different phases, that can be the intended movement; if they must align choose shared synchronization support. Two slow, independent LFO periods of 37 and 61 seconds give slowly shifting relative phase; inherited period and phase reset behavior matters.

## 5. Timbre: why one oscillator is not always enough

Timbre is the sound's identity beyond fundamental pitch and level. Harmonics are integer multiples of a fundamental, partials may also be inharmonic. A 110 Hz harmonic series starts 110, 220, 330, 440, 550 Hz. Those components shape one perceived tone; they are not the same as several equally prominent chord notes. Harmonic n has frequency n × fundamental, and adding harmonics in different ratios changes the spectrum and often perceived loudness.

| Source | Idealized harmonic content | Typical role |
|---|---|---|
| Sine | Fundamental only | Smooth sub, pure pitched layer, slow modulation |
| Triangle | Odd harmonics, falling roughly as 1/n² | Soft melodic or pad body |
| Symmetric square | Odd harmonics, falling roughly as 1/n | Hollow/reedy tone, articulated pulse |
| Saw | All integer harmonics, falling roughly as 1/n | Bright, rich sound for subtractive filtering |
| White noise | Roughly equal power per Hz | Bright hiss, air, excitation |
| Pink noise | Roughly equal power per octave | Balanced texture or body |
| Brown noise | Stronger low-frequency energy | Rumble or dark texture; control unwanted low end |

These ideal descriptions explain relative tendencies. Actual Orbit waveforms are band-limited/anti-aliased and noise shaping is implementation-specific; see [oscillators and noise](../audio/oscillators-and-noise.md). A square with non-50% duty contains a different partial distribution. Waveform selection is not a guaranteed equal-loudness switch.

### Design a warmer drone rather than just turning it up

Start with a sine at 110 Hz, amplitude 0.12; add a triangle at 220 Hz, amplitude 0.04; optionally a sine at 330 Hz, amplitude 0.02. Mix at unity amounts, low-pass near 1 kHz if useful, use a modest filtered-noise layer (amplitude 0.02–0.04), low-rate amplitude modulation and reverb as appropriate. Sparse upper harmonics can provide warmth/body and audibility on smaller speakers. A pure sine filtered far above its frequency will hardly change; add harmonics first if the desire is audible brightness modulation. Try a gentle bell EQ cut if a part is harsh, not automatic boosts everywhere.

Detuning two sines at 110 and 110.4 Hz makes approximately 0.4 beats per second (a 2.5 s beating cycle), not a second, automatically independent modulation envelope. At different registers the same detuning in cents gives different Hz differences. Detune by a few cents for an evolving coherent layer, then check mono sum because phase interaction can cause deep cancellations. More detuning can become rough/out of tune; choose based on intent. Chorus can provide movement without adding many independent oscillator banks.

Consider relationships beyond simple layering: feed an audio-rate signal into a suitable modulation parameter and recombine a shaped path with its source, or coordinate multiple spectral and amplitude controls over different timescales. Their interaction can produce emergent harmonics and a breathing timbre. The [breathing harmonic drone foundation](foundations/breathing-harmonic-drone.md) demonstrates deliberate additive-Hz detuning and nonlinear amplitude shaping in Orbit, including why its original sound works and why output headroom matters. Use the principle creatively rather than imposing it on every sound.

Use noise as a quiet contrasting texture or attack/excitation, not an obligatory ingredient. Inharmonic partials, resonators, and modulation sidebands can become compelling metallic sounds. At the time of writing the dedicated resonator/modal bank are planned; use only the available filters, oscillators and effects for actual delivered presets or document a future design explicitly.

In a polished musical voice, a pure sine on its own may sound hollow or like a test tone; support it with meaningful partials or shape it as a deliberate sub/impact/detail. A loud raw-noise texture, even with long reverb, can mask music. See [voice design](sound-quality-and-voice-design.md) for concrete noise transformations and richer tonal recipes; evaluate in context rather than assuming lower RMS or more effects is better.

### Articulation and modulation

The envelope determines a sound's entry, body and exit. AD is suitable for plucks/strikes; AR for swells; ADSR for gate-held voices; Loop can create repeated evolution. Real sounds rarely have the same brightness throughout their duration. A separate envelope or scaled copy may close a filter after a note attack, while amplitude fades more slowly. Avoid hard waveform cuts at arbitrary sample values unless clicks are deliberate. Short attack/release smoothing can remove clicks, but intentional percussive transients need not be over-smoothed.

Try different timescales: 5–20 ms attack and 0.3–1.5 s decay for a soft pluck; 2–8 s attack/release or slow fades for a pad; 20–90 s filter modulation for a texture. These are illustrative, not universally safe labels. Check the [envelope settings](../audio/envelopes.md) and [modulation contract](../core/parameters-and-modulation.md). For current additive amplitude modulation, bipolar LFO amplitude 1 into a base 0.20, amount 0.08 gives approximately 0.12–0.28. Zero base plus a unipolar 0–1 AD envelope with amount 0.12 gives silence between events and a 0.12 peak setting. Excessive depth hitting min/max clamps changes shape; choose base and depth accordingly. Audio-rate modulation can create extra spectral components, unlike slow LFO movement; it is a synthesis technique, not a smooth automation substitute.

## 6. Perceived loudness, gain staging and dynamic range

**Amplitude is not perceived loudness.** Peak measures the largest magnitude, RMS describes the root-mean-square amplitude over a window (its square is proportional to average power under the same load), and subjective loudness depends additionally on frequency distribution, duration, simultaneous masking, playback level and listener. Equal-loudness contours describe pure-tone listening conditions ([ISO 226:2023](https://www.iso.org/standard/83117.html)); they do not define a universal EQ or constant waveform compensation for music. A low sine may sound faint next to a harmonic-rich saw with the same peak setting. Raising the sub until it seems as loud as the saw may consume all available headroom.

| Metric | Meaning | Use in Orbit |
|---|---|---|
| Linear amplitude | Sample/parameter magnitude relative to unit full scale | Gain staging and modulation math |
| Peak dBFS | 20 × log10(max absolute sample), relative to full scale | Detect sample peaks near clipping |
| RMS dBFS | 20 × log10(sqrt(mean(sample²))), using unit amplitude reference | Compare energy in windows; not loudness |
| Crest factor | Peak-to-RMS ratio expressed in dB | Distinguish spiky and more continuous sounds |
| LUFS | Frequency-weighted program loudness estimate (BS.1770 family; integrated measurement uses gating) | Future/externally measured program comparison; not equal to current RMS |
| True peak (dBTP) | Estimated reconstructed peak between digital samples | Final output headroom; not reported by ordinary sample peak |

For a sine at peak 1, RMS ≈ 0.707, or −3.01 dBFS with the above RMS reference. An ideal bipolar square with peak 1 has RMS 1 = 0 dBFS. Actual band-limited signals differ. A linear amplitude 0.5 is −6.02 dB, 0.25 ≈ −12.04 dB and 0.1 = −20 dB. Doubling amplitude is ~+6 dB, not necessarily twice the perceived loudness. Do not confuse amplitude dB calculation with power (10 log10).

### Practical mix procedure

1. Start with a quiet foundation and enough room for the loudest later section.
2. Bring up the important layer until clearly audible; introduce each supporting voice below it.
3. Listen/inspect both isolated and in context, because masking means a sound can disappear only when other sounds are present.
4. Compare timbre/effect changes at approximately matched perceived loudness, adjusting the output gain rather than preferring whichever sound got louder. RMS matching is a useful initial approximation, not final loudness matching.
5. Check the master-feed scope at its loudest section and for accumulated delay/reverb tails. As an initial conservative target, try sample peaks around −12 to −6 dBFS (0.25–0.5 linear peak) while building; final delivery may use different targets. Do not claim this guarantees safe inter-sample peaks or hearing SPL. Start playback at a comfortable low device level.

If four coherent signals each peak at 0.2 and align, their sum can reach 0.8 (roughly −1.94 dBFS), not 0.2. Incoherent signals may sum less at a given moment but peaks can still align unpredictably over a long piece. A conservative bound for a weighted linear sum is the sum of individual absolute weighted peak bounds; processors such as resonant filters, EQ boosts and feedback effects may exceed simplistic input bounds. Use actual measured peaks and leave margin. A gain at the end cannot undo upstream nonlinear clipping if a stage introduces it. Check loudest combinations, not just the start of the loop.

For a simple bed, begin with bass amplitude 0.10–0.16, pad voices 0.04–0.08 each, melody 0.05–0.12 and texture 0.015–0.04, then adjust mixer weights or Gain nodes. These are not calibrated loudness ratios; waveform, filter and note register matter. Orbit's current Gain is linear 0–1 and attenuates; use the actual parameter definitions and routing for other devices. Do not invent a compressor, limiter or perceptual normalization feature in a patch. If a measured loudness target is requested, use an actual BS.1770 implementation or external render measurement and distinguish momentary/short-term/integrated loudness; see [ITU-R BS.1770-5](https://www.itu.int/rec/R-REC-BS.1770/en). A particular LUFS number is a delivery choice, not evidence of good music.

Preserve contrast. Ambient music can benefit from quieter details and dynamic swells; a constantly maximized signal can fatigue and make the climax smaller in perception. A louder mix is not necessarily a more expressive one.

For more on research-backed relative voice balance, activity-aware level measurements, masking and selective phrase/section automation, use [contextual layer balance](contextual-layer-balance.md). A weighted energy difference can flag excessive prominence but is not a direct measure of perceived audibility inside a mixture.

## 7. Spectral balance, masking and register

Masking occurs when a stronger sound makes another sound less detectable, often when their spectra overlap. A mix with several audible layers does not require every layer to occupy a non-overlapping band; harmonic overlap can be pleasant. Choose a clear hierarchy and solve unwanted competition mainly through voicing, timbre, arrangement and relative level before extreme corrective EQ.

| Approximate frequency region | Possible role / issue |
|---|---|
| 20–60 Hz | Deep sub weight; may be poorly reproduced by small speakers; unnecessary rumble consumes headroom |
| 60–150 Hz | Bass fundamentals and body; too many strong sources may obscure the foundation |
| 150–350 Hz | Warmth/body; buildup can sound muddy |
| 350 Hz–1 kHz | Midrange texture and note identity; excessive overlapping resonances may sound boxy |
| 1–4 kHz | Presence/definition; bright sustained energy can dominate |
| 4–10 kHz | Attack, air/noise, brightness; excessive energy may be tiring |
| >10 kHz | Upper air or hiss, depending on playback and hearing |

These coarse overlapping ranges are guides for listening and diagnosis, not hard crossovers or frequency assignments for all music.

For an ambient example, keep the bass relatively pure around A1/A2; use upper harmonics to help bass translate; place a warm pad primarily above the bass; keep the lead or accents in a distinct upper register; high-pass an atmospheric noise layer if its low-frequency rumble competes with the bass. A low-pass filter cannot create absent harmonics and an EQ cannot fix a wrong note. Orbit's parametric EQ is a single bell band; if shelving is unavailable use the supported bell/filter modes or document the limitation, not fictional controls. Try small cuts (e.g. 2–4 dB around a diagnosed competing region) before multiple large boosts. High-Q peaks can ring or dramatically raise output.

Use a scoped spectrum to see overlap/peaks and the waveform to diagnose clipping, amplitude behavior or abrupt edges. FFT resolution is sampleRate / FFT size; a size of 8192 at 48 kHz gives about 5.86 Hz per bin. It cannot reliably resolve tiny detuning differences in separate bins; use a longer observation window or inspect beating in time. The scope's reported peak frequency is an FFT-bin estimate. Smoothing may hide short events; match timebase/window to the sound being examined. See [signal observation](../audio/signal-observation.md) and [visualizations](../editor/visualizations.md). A visually balanced spectrum is not proof of a pleasant mix.

## 8. Depth, clarity and the illusion of space

Spatial depth is the impression that different sounds occupy different distances. Stereo width is a separate left/right attribute. A sound can be close and wide, or distant and centered. Orbit's current synthesis and core effects are mono; depth is possible using level, spectral and temporal cues, but do not promise stereo panning, stereo widening or a spatial renderer until those features actually exist.

| Cue | Foreground (starting point) | Background (starting point) |
|---|---|---|
| Direct level | Clear and present | Lower, behind the focal element |
| Tonal detail | More transient/high-frequency clarity | Softer attacks, reduced high detail when appropriate |
| Wet/dry ratio | More direct sound, some shared room | More reverberant relative to direct |
| Tail | Shorter or less prominent | Longer/diffuse if the space calls for it |
| Motion | Defined and readable | Subtle slow modulation or distant echoes |

These cues work together. Turning down a sound alone does not guarantee it feels farther away, and a loud reverberant sound may still be foreground. Physical distance cues involve more than volume ([University of California, Irvine, audio spatialization example](https://music.arts.uci.edu/dobrian/maxcookbook/node/764)).

### Practical three-plane mix

- Foreground: melodic triangle/sine pluck, reverb mix 0.12–0.20, decay 1–3 s, clear event onset.
- Midground: slow chord pad with chorus or softer filtering, reverb mix 0.25–0.40, decay 3–6 s.
- Background: high-passed/low-passed texture depending on intended band, quieter, reverb mix 0.45–0.65, decay 5–10 s and modulation over tens of seconds.

These are example settings within current controls, not realistic distance calibration. A shared reverb on an appropriate combined bus can provide a coherent room; use explicit dry and wet paths with careful summing if needed, avoiding accidental duplicate dry signal. If each voice has a very long reverb, then another long master reverb, tails may overlap and erase melody and harmonic changes. Test them together. Reverb damping shapes how high-frequency energy dies; Orbit's algorithm has fixed geometry and no separately editable pre-delay/room size as of this guide. Delay creates distinct repetition and motion; feedback controls how much returns; high feedback can build up. Chorus changes time/pitch of copies and may introduce comb filtering in mono. See [effects](../audio/effects.md) for actual supported controls, topology and behavior.

## 9. Arrangement: from a pleasant sound to a complete piece

Choose an emotional trajectory and give each sound a reason to exist. Establish a focal voice, then selectively introduce or withdraw other layers. A build can be created by a widening register range, increasing note density, brightening filtered harmonics or raising a supporting texture; it need not simply get louder. The quiet before a climax makes the contrast meaningful.

Example 10-minute structure (not a fixed genre template):

| Time | Action | Musical intent |
|---|---|---|
| 0:00–1:15 | Sparse bass pedal, distant texture | Establish space and tonal center |
| 1:15–3:00 | Add upper harmony and a recognizable motif | Establish identity |
| 3:00–4:30 | Move harmony; refrain from constant melody | Deepen tension |
| 4:30–6:30 | Introduce pulse/accent layer, increase brightness gradually | Development and momentum |
| 6:30–7:45 | One clear climax: broader register or melodic variation | Reward expectation |
| 7:45–9:00 | Withdraw pulse/lead, return to a recognizable harmonic color | Release and recall |
| 9:00–10:00 | Fade toward the original pedal or silence; allow tails | Intentional ending |

Use separately named composites or clearly organized lanes, explicit gain/weight controls, shared transport and independent timescales. The guide's timeline is an arrangement plan; not all scheduled section automation or long-form control nodes are necessarily implemented. A patch may require manual performance actions to realize it; disclose that rather than claiming a fully autonomous piece. For automated ambient patches using implemented features, see [generative ambient composition](generative-ambient-composition.md).

## 10. Common bad practices and useful alternatives

| Pitfall | Better first experiment |
|---|---|
| Choose all oscillator Hz values at random | Select a pitch center/interval vocabulary; intentionally detune or introduce dissonance |
| Tune an LFO carrier in Hz and expect that rate to be its pitch depth | Calculate the period and destination modulation depth independently |
| Add the same Hz change to all pitches | Use shared semitone modulation for preserved ratios |
| Assume every scale note works equally well over every chord | Check actual simultaneous intervals, voicing and intent |
| Add many full-amplitude saws to make a drone “rich” | Use a quieter fundamental and a few controlled harmonic layers |
| Give every voice the same slow modulation, rhythm and wetness | Assign distinct but related roles, rates and spatial depth |
| Treat a Hold step as a silent rest | Use actual amplitude and envelope/trigger routing |
| Maximize all amplitudes, boost EQ and add limiting | Balance roles with attenuation, headroom and measured peaks |
| Use long reverb to hide a boring or badly tuned sound | Improve source/voicing/articulation first; add space intentionally |
| Change all knobs to random values | Vary a small number of bounded parameters, listen and compare |
| Equate a clean scope with great music or every click with artistic intent | Use numerical diagnostics plus listening, and explicitly distinguish defects from sound design |
| Assume future features are available | Confirm node type, units, schema, capabilities and current status before building |

Rules may be intentionally broken: harsh clipping as a controlled artistic effect differs from accidental overload; repetitive drones can be compelling; stable dissonance may be an aesthetic center; dense sound may be a goal. State the artistic purpose before making exceptions. Never bypass engineering validation or imply an unverified patch sounds good.

## 11. Agent workflow and quality checks

For a new patch or piece, adapt this checklist to the task rather than requiring unnecessary analysis or a fixed musical style:

1. **Intent:** establish desired mood, genre, duration, focal element and whether this is a sound/effect or a whole piece.
2. **Vocabulary:** choose tonal center/scale/intervals or explicitly non-tonal tuning; calculate pitches; assign each voice its register, role and level.
3. **Time:** choose whether notes follow free seconds or BPM, phrase lengths, modulation times and whether events actually articulate notes. Document the expected repeating period or manual actions.
4. **Build:** use registered node types and actual parameter/port definitions. Reuse current composites, recipes, validated patch serialization and the application's routing invariants. Document future capabilities separately.
5. **Audition in stages:** listen to dry sources, each processed voice, then the mix. Compare at similar perceived level; change one variable at a time when diagnosing. Check whether an important layer is masked, a chord sounds congested or tails collide. Listening is indispensable for subjective quality. If an agent cannot listen, say so, provide a plausible recipe and request human audition rather than claiming that it sounds excellent.
6. **Measure:** confirm pitch and expected note intervals, ranges, timing, envelope shape, finite output, headroom, no unexpected discontinuities, and representative later sections (not only first few seconds). Use master-feed scope/FFT and offline/browser render checks when available; distinguish sample peak/RMS from LUFS or acoustic loudness. Check performance on complex patches separately via [performance diagnostics](../../performance.md).
7. **Refine:** apply the [production iteration](mixing-mastering-and-analysis.md#5-agent-workflow-render--analyze--refine--re-render) at an appropriate scale: compare the baseline and targeted revised render, when feasible, including important voices and master. Preserve musical strengths; respect human listening feedback and distinguish it from objective data.
8. **Deliver:** include expected musical behavior and settings, any manual performance steps, supported import format, assumptions, known limitations, actual verification and documentation report. Prefer small readable patches with names/layers that help modification; complexity is not a quality metric.

For an effect-only preset, a single representative dry input plus several boundary tests may be sufficient. For a whole-piece patch, verify a representative transition and the full intended duration where feasible, with deliberate silence, harmonic/timing relationships and no runaway overlap or accidental clipping. A stable autonomous performance that sounds intentional over ten minutes is a different success condition from a technically valid oscillator demonstration.

## 12. References and further reading

- ISO 226:2023, [normal equal-loudness-level contours](https://www.iso.org/standard/83117.html): physical sound pressure levels giving equal perceived loudness for pure tones under specified conditions.
- ITU-R BS.1770-5, [audio programme loudness and true-peak measurement](https://www.itu.int/rec/R-REC-BS.1770/en): standardized program measurement rather than simple waveform amplitude normalization.
- R. Plomp and W. J. M. Levelt (1965), [Tonal consonance and critical bandwidth](https://doi.org/10.1121/1.1909741): empirical research into partial spacing and sensory consonance.
- J. H. McDermott et al. (2016), [Indifference to dissonance in native Amazonians reveals cultural variation in music perception](https://doi.org/10.1038/nature18635): evidence against treating one learned harmonic preference as universal.
- Christopher Dobrian, University of California, Irvine, [Amplitude is inversely proportional to distance](https://music.arts.uci.edu/dobrian/maxcookbook/node/764): examples of distance cues beyond amplitude.

The composition recommendations and examples above are original practical guidance for Orbit; the cited research and standards support the physical/perceptual distinctions, not a prescriptive ranking of musical styles.
