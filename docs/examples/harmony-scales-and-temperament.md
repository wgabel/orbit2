# Harmony, scales and temperament — from a frequency to a convincing progression

[Music creation index](README.md) · [Theory overview](music-theory-and-sound-design.md) · [Classical composition and melody](classical-composition-and-melody.md) · [Pitch control](../instruments/pitch-control.md)

**Purpose:** deepen the theory needed for writing intentional pitched music in Orbit 2. The examples are original study material, mathematical calculations, and practical starting points, not native-rendered prescriptions or a universal ranking of music. The focus is Western tonal and modal idioms (including their electronic descendants); another tradition may organize pitches very differently. See [musical quality and expectation](musical-quality-and-expectation.md) for the broader research context.

## 1. Why notes sound good together: acoustics and context

A pitched instrument often produces a fundamental plus upper partials. For a harmonic timbre, partials of 100 Hz occur near 100, 200, 300, 400 Hz and so on. Add a 150 Hz instrument: 300, 600, 900 Hz and further partials coincide. The simple 3:2 frequency relationship helps explain why this fifth can fuse into a stable-sounding object. A 100 and 101 Hz sine pair instead produces approximately one beat per second (a periodic amplitude fluctuation); other differences and spectral combinations can produce roughness rather than an easily counted beat.

Three related but distinct contributors to simultaneous consonance are identified by Harrison and Pearce [H2], who reviewed research, reanalyzed four perceptual datasets and modeled three Western corpora:

| Contributor | Meaning | Musical implications |
| --- | --- | --- |
| Interference/roughness | Close spectral components interact within auditory filters; their effect depends on frequency, level and spacing [H1]. | A dense cluster of saws in the bass may be coarse; spread it or darken its spectrum. A soft cluster in the treble may be a useful color. |
| Harmonicity/periodicity | Spectra may jointly resemble one harmonic series or share a clear periodicity. | Octaves and fifths can feel unified; simple-ratio tuning can reduce beats in a static harmonic drone. Fusion can obscure independent voices. |
| Familiarity | Listeners learn characteristic combinations from their musical exposure. | An interval or chord can be welcome even when physically rough, especially when its function and style are familiar. |

McDermott et al. [H3] found that Tsimane' participants in their sample did not exhibit the same preference for consonant chords as Western listeners while showing sensitivity to roughness. Sensory roughness and musical preference are not interchangeable. Do not turn these experiments into a requirement to maximize consonance. Dissonant events can provide motion, expressive color, or the desired destination.

Sethares [H4] models the interaction between spectra and tuning systems: pleasant intervals can change when the spectrum is inharmonic. A metallic/modal instrument, aggressively frequency-modulated bass or bell may not favor precisely the same intervals as harmonic strings. An FFT, interval count or roughness model alone cannot evaluate a chord's stylistic function or quality.

### Practical diagnostics

1. Check the actual fundamentals, octave register, note intervals and intentional fine detunes, including tails from previous chords.
2. Listen to a chord with three simple harmonic spectra, then the designed voices at comparable levels. If only the latter is muddy, try register, filter and balance changes before rewriting the composition.
3. For harmony that changes frequently, use relatively clean tunings; let deliberate detuning or microtonality serve a musical purpose. Avoid accidentally combining exact ratios and equal-tempered doubles.
4. If each chord sounds fine alone but the succession feels directionless, examine harmonic function, bass and voice leading (sections 4–6). If mutually independent lines disappear into one sound, examine their shared onsets, octaves/fifths, timbre and contour instead of simply increasing gain.

## 2. Scales: pitch vocabulary with a center

Scale degrees describe relationship to a tonic (`1`, `2`, `b3`, etc.), not absolute frequency or a prescribed melody. Key establishes a hierarchy of pitch and harmonic roles over time; a scale is the collection of allowed or characteristic pitches. C major and A natural minor share seven pitch classes but can establish different homes, cadences and tendencies. Modes sharing a collection likewise differ in center, emphasis and characteristic harmonies. Krumhansl and Kessler's probe-tone experiments [H5] support context-dependent tonal hierarchies in listeners familiar with Western tonality, not a universal ordering applicable to every genre.

| Scale | Degrees relative to root | Semitones | Example from C |
| --- | --- | --- | --- |
| Ionian/major | 1 2 3 4 5 6 7 | 0 2 4 5 7 9 11 | C D E F G A B |
| Dorian | 1 2 b3 4 5 6 b7 | 0 2 3 5 7 9 10 | C D Eb F G A Bb |
| Phrygian | 1 b2 b3 4 5 b6 b7 | 0 1 3 5 7 8 10 | C Db Eb F G Ab Bb |
| Lydian | 1 2 3 #4 5 6 7 | 0 2 4 6 7 9 11 | C D E F# G A B |
| Mixolydian | 1 2 3 4 5 6 b7 | 0 2 4 5 7 9 10 | C D E F G A Bb |
| Aeolian/natural minor | 1 2 b3 4 5 b6 b7 | 0 2 3 5 7 8 10 | C D Eb F G Ab Bb |
| Locrian | 1 b2 b3 4 b5 b6 b7 | 0 1 3 5 6 8 10 | C Db Eb F Gb Ab Bb |
| Harmonic minor | 1 2 b3 4 5 b6 7 | 0 2 3 5 7 8 11 | C D Eb F G Ab B |
| Melodic minor (classical ascending form) | 1 2 b3 4 5 6 7 | 0 2 3 5 7 9 11 | C D Eb F G A B |
| Major pentatonic | 1 2 3 5 6 | 0 2 4 7 9 | C D E G A |
| Minor pentatonic | 1 b3 4 5 b7 | 0 3 5 7 10 | C Eb F G Bb |
| Blues (minor-pentatonic variant) | 1 b3 4 b5 5 b7 | 0 3 5 6 7 10 | C Eb F Gb G Bb |
| Whole tone | 1 2 3 #4 #5 b7 | 0 2 4 6 8 10 | C D E F# G# Bb |
| Chromatic (12-TET) | all semitones | 0–11 | C C# D Eb E F F# G Ab A Bb B |

In historical minor-key harmony, degrees 6 and 7 are flexible: raising 7 creates a leading tone to tonic and a major V/dominant. Raising 6 can smooth the augmented-second gap from b6 to 7; the conventional descending classical melodic-minor scale reverts to natural minor, but real passages are governed by voice leading and harmonic context, not an automatic direction switch. Jazz usage of melodic minor often retains the raised forms in both directions. The blues idiom also includes pitch bends and variable intonation; six equal-tempered pitch classes are only a simplified vocabulary.

**Use a mode as a musical decision.** To distinguish D Dorian from C major, anchor D in bass/melody and bring out B natural (Dorian's raised sixth relative to D minor). A Dm–G–Dm vamp offers this color. A C–F–G–C cadence may establish C major even though it uses the same notes. For Lydian try C and a high F# as a deliberate color on an otherwise C-centered pad; for Mixolydian try C–Bb with a C pedal. Do not assign mandatory emotions to modes or assume every scale member is stable against every chord.

## 3. The wolf interval and the historical tuning problem

### Ratios, cents and commas

For a ratio `r`, its interval in cents is `1200 * log2(r)`. Equal temperament divides each octave into twelve 100-cent steps; its ratio per semitone is `2^(1/12)`. Some simple just intervals and equal-tempered comparisons (cents rounded):

| Interval | Just ratio | Just cents | 12-TET cents | 12-TET minus just |
| --- | --- | ---: | ---: | ---: |
| Octave | 2:1 | 1200.000 | 1200 | 0 |
| Fifth | 3:2 | 701.955 | 700 | -1.955 |
| Fourth | 4:3 | 498.045 | 500 | +1.955 |
| Major third | 5:4 | 386.314 | 400 | +13.686 |
| Minor third | 6:5 | 315.641 | 300 | -15.641 |
| Major sixth | 5:3 | 884.359 | 900 | +15.641 |
| Minor seventh (one just form) | 9:5 | 1017.596 | 1000 | -17.596 |

The last interval can have other just ratios in different harmonic contexts (e.g. the harmonic seventh 7:4 ≈ 968.826 cents). Just intonation is not one uniquely determined mapping of all notes in all keys.

Stack twelve pure fifths and compare them with seven octaves: `(3/2)^12 / 2^7 = 531441/524288 ≈ 1.013643`, the **Pythagorean comma** (~23.460 cents). The circle does not quite close. On a twelve-key-per-octave keyboard made with eleven pure fifths, the remaining closing interval has to be about 678.495 cents rather than 701.955. The resulting poor fifth (often a diminished sixth in its notation) is a wolf interval. By changing which pitch is assigned the error, the wolf can be moved but not eliminated while keeping all eleven fifths pure.

There is another mismatch: four pure fifths reduced by two octaves give the major third `81/64` (~407.820 cents), rather than the desired `5/4` (~386.314 cents). Their ratio `81/80`, about 21.506 cents, is the **syntonic comma**. These separate discrepancies motivate different temperaments.

### How temperaments traded one problem for another

| Method | Construction/character | Benefit and trade-off |
| --- | --- | --- |
| Pythagorean tuning | Fifths near 3:2; one large closing discrepancy on a fixed 12-tone cycle. | Excellent fifths in favored regions; wide thirds and a wolf close the cycle. |
| Just intonation | Use small integer ratios appropriate to the active harmony, e.g. 1:5/4:3/2 for major. | Pure local triads with harmonic voices; fixed keys cannot supply every ratio for changing chords. Flexible pitches may need context-specific adjustments and comma-drift management. |
| Quarter-comma meantone | Narrow ordinary fifth (~696.578 cents) by 1/4 of syntonic comma; four such fifths give a pure major third. | Excellent major thirds in usable keys; a 12-note version with eleven regular fifths has a wide closing wolf (~737.637 cents). Enharmonic sharp/flat choices matter. Other meantone variants distribute different amounts. |
| Circulating well temperaments | Distribute discrepancies unequally among fifths so distant keys become usable, with differing thirds/key colors. | Wider modulation without a single intolerable wolf, but key-specific tuning character. Many historical schemes; no single universal recipe. |
| 12-tone equal temperament | Narrow each pure fifth by 1/12 Pythagorean comma (about 1.955 cents). Octave is exact, fifth is 700 cents. | Every transposition preserves the same interval sizes and the 12-class cycle closes. Thirds remain imperfect relative to just tuning; no single exaggerated wolf. |

Thus the wolf wasn't simply a broken note fixed by inventing a new scale. It came from trying to represent incompatible pure interval relationships on a finite fixed-pitch system. Different tuning approaches shifted or distributed the error. Equal temperament is a useful compromise, not perfectly just intonation.

**Bach's Well-Tempered Clavier:** each book contains a prelude/fugue pair in each of the 24 major and minor keys. This demonstrates the musical importance of usable tonality across the keyboard. It does not prove J. S. Bach invented, used or advocated modern exact equal temperament. Historical proposals and corpus analyses disagree about his preferred specific temperament [H6, H7]; present the precise intended scheme as uncertain. His keyboard works were written for historical keyboard instruments including harpsichord/clavichord rather than today's concert grand (and today can be played on piano).

**Two other problems with the word wolf:** (1) a cello/viola wolf *note* is an unstable warbling caused by coupled bowed-string/body resonance. It can be mitigated through mechanical damping/tuned absorbers, setup/playing techniques or experimental active damping [H8, H9]; retuning the circle of fifths is not the fix. (2) real piano strings have stiffness and inharmonic partials; skilled piano tuning often stretches octaves (the Railsback curve) to balance their beating [H10]. This is not the historical wolf interval either. A synthetic harmonic oscillator normally does not require piano octave stretch.

### Orbit tuning experiment (calculated, not a validated preset)

For A3 = 220 Hz, a just major triad is 220 / 275 / 330 Hz; 12-TET is 220 / 277.183 / 329.628 Hz. Use matched-amplitude harmonic oscillators (e.g. quieter saw/triangle rather than only sine) and compare on the same mix and filter. The just third's fourth partial (1100 Hz) aligns with the root's fifth; the 12-TET third's fourth is ~1108.73 Hz, giving a beat difference of ~8.73 Hz between those partials. The fifth's second partial is 660 Hz just or ~659.26 Hz equal-tempered versus the root's third (660 Hz). Actual audibility depends on the waveform/partial gains and hearing. Play the triad alone, then modulate it as a fixed bank and try a separately voiced progression. Level-match, compare spectra and listen.

To reproduce the two historical closing intervals, use A3 plus a second oscillator at `220 * 2^(cents/1200)`, compare 678.495, 700, 701.955 and 737.637 cents with the same spectrum and amplitude. Audible harshness, pitch and preference are observations to record, not predetermined. In Orbit's 12-TET semitone-based controls, cents correction = target cents minus integer semitone position × 100; e.g. just major third at +4 st plus -13.686 cents. A fixed just oscillator bank can transpose preserving ratios when its offsets are shared. An entire key-dependent temperament, enharmonic spelling, adaptive just tuning or MIDI tuning tables are **not** implemented by the Note Sequencer. Experiment using explicit base Hz, static fractional pitch offset/Pitch Control, or separate fixed-tuning voice branches and verify current port bounds. Do not send arbitrary cents as integer MIDI note slots or change pitch law globally.

## 4. Constructing and choosing chords

### Build diatonic harmony from a scale

Stack alternate scale degrees (thirds); the intervals determine the chord quality. For C major:

| Degree | Triad | Seventh chord | Typical tonal use in this key |
| --- | --- | --- | --- |
| I | C E G | C E G B (Cmaj7) | Tonic/home |
| ii | D F A | D F A C (Dm7) | Predominant, prepares V |
| iii | E G B | E G B D (Em7) | Mediant, weaker tonic-related color/contextual |
| IV | F A C | F A C E (Fmaj7) | Predominant, sometimes plagal/modal |
| V | G B D | G B D F (G7) | Dominant, expectation of I |
| vi | A C E | A C E G (Am7) | Relative minor, deceptive resolution/contextual tonic substitute |
| vii° | B D F | B D F A (Bm7b5) | Leading-tone dominant-function harmony |

Use Roman numerals for scale-relative harmony: uppercase major, lowercase minor, `°` diminished, `ø7` half-diminished, `+` augmented. Slash denotes stated bass (C/E = C major, E in bass), or an applied function in Roman numerals (V/V means dominant of the dominant, *not* a slash bass). Voicing specifies exact octaves and order. In A minor, E–G#–B–D (E7) makes V7 with a leading tone G# even though G# is not in natural minor. Natural-minor v (Em) may instead serve modal/repetitive styles.

| Color | Semitones from root | Example on C | Intent |
| --- | --- | --- | --- |
| Major / minor | 0 4 7 / 0 3 7 | C E G / C Eb G | Distinct third, clear foundation |
| Diminished / augmented | 0 3 6 / 0 4 8 | C Eb Gb / C E G# | Symmetry and tension; resolve in context |
| sus2 / sus4 | 0 2 7 / 0 5 7 | C D G / C F G | Third replaced; open or suspended color |
| add9 / 6 | 0 4 7 14 / 0 4 7 9 | C E G D / C E G A | Expansion without mandatory seventh |
| Dominant 7 / major 7 / minor 7 | 0 4 7 10 / 0 4 7 11 / 0 3 7 10 | C7 / Cmaj7 / Cm7 | Distinct harmonic functions or colors |
| Half / fully diminished 7 | 0 3 6 10 / 0 3 6 9 | C Eb Gb Bb / C Eb Gb Bbb | Leading-tone or predominant function, enharmonic reinterpretation |
| Dominant 9 | 0 4 7 10 14 | C E G Bb D | Additional color; seventh distinguishes from add9 |

More notes are not inherently richer. Color depends on voicing, duration, function and timbre. A chord with a 13th need not sound every intervening extension; clarify essential characteristic tones and intended bass. Enharmonic equivalents sound alike in equal temperament but spelling can communicate where the part is going: B# and C, or Bbb and A, have different notation roles. Plan chord members independently before turning them into MIDI numbers.

## 5. Progressions, cadences and harmonic time

Tonal harmony often organizes the movement **tonic → predominant → dominant → tonic**. Not every chord or musical style follows these categories, and prolonged tonic/dominant functions can span several chords and inversions. In C major, I–ii–V7–I (C–Dm–G7–C) makes a concise trajectory. B tends upward to C and F (G7's seventh) tends down to E, an audible resolution of the tritone. The listener's knowledge of the key and phrase matters, not just the last pair of chords.

| Desired phrase ending | Example in C | Explanation |
| --- | --- | --- |
| Perfect authentic cadence (PAC) | Root-position G or G7 → root-position C, soprano ends C | Strong classical close at a phrase boundary. |
| Imperfect authentic cadence (IAC) | G → C, melody ends on E/G, or another non-PAC authentic configuration | Arrival with a different degree of closure. |
| Half cadence (HC) | Dm → G, stop on V | Open question; further continuation expected. |
| Deceptive cadence | G7 → Am | Expected destination is redirected. B can resolve to C, F to E, but bass moves to A. |
| Plagal motion | F → C | Softer alternate arrival/after-cadence according to context; not a replacement for every authentic cadence. |

These are stylized examples: a cadence is a *phrase-ending event*, not just any two chords with those names. Metric placement, durations, melodic ending and surrounding harmonic syntax make the difference.

**Harmonic rhythm** = rate of chord change, as distinct from note or percussion rate. Hold a harmony while the melody elaborates it; accelerate from one chord per bar to two per bar near a cadence, or prolong V on a pedal before a satisfying arrival. A rapid arpeggio can imply stillness if the same harmony is repeating. Changing chord rate gives development without altering BPM.

### Move outside one key intentionally

| Technique | Concrete example | What carries the transition |
| --- | --- | --- |
| Secondary dominant/tonicization | C–D7–G–C (V/V → V → I in C) | F# in D7 leads to G. Briefly emphasizes G without necessarily establishing a new key. |
| Pivot modulation to G | C–Am–D7–G, then a further G-key ii–V–I (Am–D7–G) | Am belongs to C and G; F# and cadences establish G. A single D7–G alone may be tonicization. |
| Relative minor | C major → Am through E7–Am; return via G7–C | Same basic collection with changed center; G# establishes minor dominant. |
| Modal mixture | C–F–Fm–C | Borrow Fm from parallel C minor. A→Ab→G in one upper voice makes a chromatic line. |
| Deceptive detour | C–Dm–G7–Am, then F–G7–C | Withhold primary resolution; make later arrival meaningful. |
| Sequence | C–Am–Dm–G7–C or a longer descending-fifths sequence (Em–Am–Dm–G–C) | Repeated interval relationships and moving bass. A circle-of-fifths route is one useful pattern, not mandatory. |
| Pedal | C bass held while upper harmonies move C–F–G–C | Stable anchor against changing tensions. Names of upper chords do not describe the complete sounding sonority. |

To create a less predictable progression, preserve some familiar structure and alter one meaningful point (harmonic rhythm, secondary dominant, cadential destination, modal borrowing, bass inversion). Do not randomly pick more complex chords every beat. Conversely, deliberate static/modal music may need no functional cadence at all.

## 6. Voice leading: turn a chord list into music

Voice leading connects individual lines between sonorities. Use common tones, nearby changes, directed dissonance resolution, contrary/oblique motion and purposeful bass movement instead of resetting every oscillator to the next root-position shape. A common tone can remain connected while the other voices change; different rhythms and contours can maintain independent lines.

| Chord | Bass | Tenor | Alto | Soprano | Observations |
| --- | --- | --- | --- | --- | --- |
| C | C3 | G3 | C4 | E4 | Stable tonic |
| F/A | A2 | F3 | C4 | F4 | Inversion keeps C4; little upper motion |
| G7 | G2 | D3 | B3 | F4 | F4 is retained as seventh; B3 leads up |
| C | C2 | G3 | C4 | E4 | B3→C4, F4→E4; bass descends while tenor rises |

This four-voice educational voicing deliberately keeps the upper parts close and expands lower spacing; it is not a complete chorale or an exact score transcription. A final tonic soprano rather than E would produce a different cadence, so this particular phrase does not meet PAC conditions. Compare against four root-position closed chords at equal levels. Try changing only the highest note to compare how a chord succession projects an independent melody.

In traditional independent-part writing, avoid gratuitous parallel perfect fifths/octaves because they can fuse lines; use thirds/sixths, contrary/oblique motion, and controlled dissonance as appropriate. Huron's empirical/theoretical work [H11, H12] relates perceptual grouping to historical voice-leading practices, including Bach's avoidance of some strongly fused intervals. These are style/goal-specific craft considerations, not a ban on parallel power chords, layered octave leads or modern planing.

**Register matters:** C2–E2–G2–B2 on loud rich waveforms may be crowded. Try C2 + G2, then E3 and B4, or a single bass note and leave other pitches to the pad and melodic voices. Space low parts widely, use close upper color tones judiciously, and adjust waveform/filter/level as necessary. If the bass changes to E, a whole different inversion is heard even if the upper oscillators are unchanged. Do not duplicate the lead's strongest register in every accompaniment.

## 7. Agent implementation checklist

Before building: choose a center/scale, tuning assumptions, chords/inversions, bass and upper-part pitch paths, phrase endings and intentional deviations; write an actual time-indexed score. For melodic and polyphonic writing continue with [classical composition and melody](classical-composition-and-melody.md). For ambient/static pieces use only the relevant ideas and maintain the user's stylistic intent.

- Use `f = 440 * 2^((midi-69)/12)` for ordinary 12-TET pitch, and `f = baseHz * 2^(st/12)` for relative offsets. A sequencer reference equal to the carrier base note means actual target note = base + relative pitch. Each independent pitch lane needs its own targets (a shared Pitch signal simply transposes a fixed bank).
- Start by auditioning a plain harmonic reduction: bass + two or three independent upper tones, equal-tempered tuning, few effects. Compare altered voicings/progressions on the same rhythm before adding pads, bell/lead or bass variations.
- In the current [Note Sequencer](../instruments/note-sequencer.md), one lane has up to 64 slots. Pitch targets are integer MIDI notes, relative output clamped to ±60 semitones. Free-Hz semitone tracking and additional fractional Pitch offsets provide manual fine tuning. Step grid can be slower for chords than melody; group all against transport reset. Use short glide or none for pitch-discrete attacks, longer ramps deliberately. A Hold retains previous pitch and is not silence; gate/trigger or gain patterns implement articulation and rests. Check concurrent lanes against the shared 30-source limit.
- For analysis, inspect actual simultaneous note sets and voice paths, not just chord symbols. Verify bass inversions, scale/cross-scale intentionality, voice crossing, dissonance timing and resolution, phrase transitions, and unwanted beats with overlapping tails. Use native render where feasible, listen at comparable levels and report any listening limitations. See [production iteration](mixing-mastering-and-analysis.md#5-agent-workflow-render--analyze--refine--re-render).
- Record evidence and reusable musical discoveries in the appropriate guide/foundation, including score or patch revision, what was measured versus heard, and applicability. This guide is musical guidance, not a temperament engine or scale-quantizer feature proposal.

## 8. Research and historical references

The numeric interval examples are calculated from ratios; other examples are original explanatory exercises. Research findings are paraphrased and should be interpreted in the scope of the stimuli/corpora and listeners studied.

- **[H1]** Plomp, R. & Levelt, W. J. M. (1965). [Tonal consonance and critical bandwidth](https://doi.org/10.1121/1.1909741). *Journal of the Acoustical Society of America*, 38, 548–560. Classical experimental work on adjacent-partial interaction and frequency-dependent auditory bandwidth.
- **[H2]** Harrison, P. M. C. & Pearce, M. T. (2020). [Simultaneous consonance in music perception and composition](https://doi.org/10.1037/rev0000169). *Psychological Review*, 127(2), 216–244. [Open full text](https://pmc.ncbi.nlm.nih.gov/articles/PMC7032667/). Composite model rather than a single consonance mechanism.
- **[H3]** McDermott, J. H. et al. (2016). [Indifference to dissonance in native Amazonians reveals cultural variation in music perception](https://doi.org/10.1038/nature18635). *Nature*, 535, 547–550. Limits of universal consonance-preference assumptions.
- **[H4]** Sethares, W. A. (1993). [Local consonance and the relationship between timbre and scale](https://doi.org/10.1121/1.408175). *JASA*, 94, 1218–1228. [Author's paper and demonstrations](https://sethares.engr.wisc.edu/papers/consance.html).
- **[H5]** Krumhansl, C. L. & Kessler, E. J. (1982). [Tracing the dynamic changes in perceived tonal organization in a spatial representation of musical keys](https://doi.org/10.1037/0033-295X.89.4.334). *Psychological Review*, 89(4), 334–368. Probe-tone experiments and tonal organization.
- **[H6]** Amiot, E. (2009). [Discrete Fourier Transform and Bach's Good Temperament](https://www.mtosmt.org/issues/mto.09.15.2/mto.09.15.2.amiot.html). *Music Theory Online*, 15(2). Temperament geometry and evaluation of a proposed Bach tuning, not identification of a definitive original scheme.
- **[H7]** Blake, A. & Albrecht, J. (2023, issue date; online 2024). [Tempering the clavier: a corpus-based examination of Bach's cognition of intonation in the Well-Tempered Clavier](https://doi.org/10.1080/09298215.2024.2375552). *Journal of New Music Research*, 52(4). Historical uncertainty and corpus-based comparison.
- **[H8]** Inácio, O., Antunes, J. & Wright, M. C. M. (2008). [Computational modelling of string–body interaction for the violin family and simulation of wolf notes](https://doi.org/10.1016/j.jsv.2007.07.079). *Journal of Sound and Vibration*, 310, 260–286. Experimental and simulated cello body/string interaction.
- **[H9]** Neubauer, P. et al. (2018). [An active-system approach for eliminating the wolf note on a cello](https://doi.org/10.1121/1.5037467). *JASA*, 143, 2965. Experimental active damping and limitations of passive devices.
- **[H10]** [Explaining the Railsback stretch in terms of the inharmonicity of piano tones and sensory dissonance](https://doi.org/10.1121/1.4931439) (2015). *JASA*. Piano-string stiffness and stretched tuning; see also [Effect of inharmonicity on pitch perception and subjective tuning of piano tones](https://pubmed.ncbi.nlm.nih.gov/36050167/) (2022).
- **[H11]** Huron, D. (1991). [Tonal consonance versus tonal fusion in polyphonic sonorities](https://doi.org/10.2307/40285526). *Music Perception*, 9, 135–154. Corpus of Bach's keyboard polyphony; independence of lines as well as consonance.
- **[H12]** Huron, D. (2001). [Tone and voice: a derivation of the rules of voice-leading from perceptual principles](https://doi.org/10.1525/mp.2001.19.1.1). *Music Perception*, 19(1), 1–64. Perceptual motivations for craft rules.
