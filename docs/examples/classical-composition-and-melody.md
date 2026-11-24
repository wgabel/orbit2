# Classical composition and melody — develop an idea into a musical piece

[Music creation index](README.md) · [Harmony, scales and temperament](harmony-scales-and-temperament.md) · [Compositional development](compositional-development.md) · [Musical quality and expectation](musical-quality-and-expectation.md)

**Purpose:** teach agents to compose elaborate, coherent material even with only one keyboard timbre, and transfer the lessons of historical tonal composition to electronic music. J. S. Bach (1685–1750) belongs primarily to the Baroque era, while W. A. Mozart (1756–1791) is a central Classical-era composer. Both belong to the broader Western classical tradition, but their textures, phrase syntax and instruments differ. Bach's keyboard works were written for historical instruments such as the harpsichord/clavichord; Mozart's mature keyboard writing belongs to an era of evolving fortepianos. Playing both on a modern piano illustrates the power of composition itself; it is not evidence that they composed for the same modern instrument. These models are options, not a requirement for all genres or a claim that an exercise below recreates either composer's style.

## 1. One instrument is not one musical voice

A keyboard is polyphonic: one physical instrument can carry overlapping bass, inner parts and soprano, or two independent melodic lines. The same timbre can communicate multiple identities through register, contour, articulation, repetition, accent and timing. A full texture does not require a different patch for every note, and adding new synths cannot rescue a weak musical idea by itself.

| Role | Musical job | Typical keyboard realization | Transfer to Orbit |
| --- | --- | --- | --- |
| Soprano/lead | Theme and phrase goals | Singing upper line, expressive accents | One tuned melodic voice, clear note/rest score |
| Bass | Establish inversion, pulse and tonal direction | Root, walking line, pedal, independent motive | Separate sequencer, darker voice and register |
| Inner voices | Fill harmony, link chord tones, resolve suspensions | Tenor/alto lines, broken chord, sparse responses | One or more separately sequenced pitches, subordinate levels |
| Countermelody | Complementary gesture or imitation | Second hand answers first; subject changes register | Similar timbre or distinct register, independent rhythm/pitch |

Listen to the main idea alone, then add each line. Every active part has a role, but it need not have equal prominence. The parts together create **vertical harmony** (what sounds simultaneously) and **horizontal coherence** (where each line goes). To compose a convincing piece, work on both.

## 2. What makes a melody memorable and expressive?

No equation proves that a melody is beautiful. Melodic expectation is nevertheless measurable in specific listening experiments. Pearce and Wiggins [C1] show how contextual statistical learning can account for many observed expectations. Pearce et al. [C2] compare computational predictions with listeners' behavioral and electrophysiological responses. The experiments and modeling in [C3] indicate that both learned patterns and Gestalt-like grouping principles can help predict continuation expectations. Clemente et al. [C4] studied how perceived balance, contour, symmetry and complexity relate to liking of novel melodies; this separates acoustic/score properties from what listeners actually perceive. These results are not a mandate to maximize predictability, surprise or symmetry.

### Build meaningful melodic structure

| Principle | Concrete action | Reason or evaluation |
| --- | --- | --- |
| Motif / cell | Start with 3–6 notes or a distinct rhythmic gesture, e.g. E4–G4–A4–G4. Repeat and vary it. | Give the piece an identifiable fragment. A motif can be rhythmic even when pitch changes. |
| Contour | Shape an arch, ascent, descent or two contrasting gestures. Reserve a higher note for a climax if appropriate. | A trajectory has more purpose than drawing every step independently. |
| Step and leap | Mix neighboring notes with larger intervals; after a large leap try movement back toward the skipped notes. | Proximity and contextual expectation are useful default tendencies, not strict rules. Leaps can establish arpeggiated harmony or dramatic contrast. |
| Anchors | Aim at a stable chord tone at a strong onset/phrase goal; vary what happens in between. | The tune relates to underlying harmony without only reciting chord notes. |
| Rhythmic identity | Use characteristic short-long patterns, held tones, syncopation, pickup and rests. | Identifies the motif even if transposed; provides breathing room. |
| Repetition and answer | Give the first phrase a recognizable question ending and the next a stronger answer; vary the last few events. | Repetition gives orientation, changed ending creates movement. |
| Register and range | Choose a singable or instrument-appropriate range; include high/low contrast purposefully. | A melody can be virtuosic, but unstructured huge jumps can be hard to follow. |
| Harmonic reinterpretation | Repeat a fragment over another bass/chord where its important pitches acquire new roles. | Familiar melody takes on a new color; avoid unintended long clashes. |

Keep **melodic rhythm** separate from **harmonic rhythm**. A single chord can support many notes and subdivisions, and a held note can continue across multiple chord changes. Changing which note starts on the downbeat, where an answer breathes, or how a motif leads into a cadence is often more consequential than increasing oscillators. Use actual note lengths and rests rather than uniformly triggering random in-scale notes at the fastest rate.

### MESH: a practical melody-development exercise

The educator in [MS3](music-learning-sources.md#ms3--how-to-write-your-first-piece-of-cinematic-music) describes **MESH** (Motive, Evolve, Shape, returning Home). It is a useful drafting method for pitched music, not a guarantee of memorability. Harmony-first is one possible starting point: sketch a short progression in an unadorned timbre and write the melodic idea against it. A melody-first approach is equally valid when an attractive gesture is already available.

- **Motive:** choose a short idea with an identifiable rhythm and contour rather than selecting unrelated scale notes. Start with only a few events; the rhythm, an interval or a distinctive leap should be recognizable even if transposed. Use stable chord tones as anchors and let non-chord tones provide deliberate motion. The video's suggestion that most notes belong to the current chord is a beginner heuristic, not a percentage requirement; metric placement, duration and dissonance resolution are often more informative.
- **Evolve:** repeat the cell so the listener has time to recognize it. Alter only the notes required to fit the next harmony, or modify the final event to produce an answer. Preserve rhythm and contour where useful; change the rhythmic identity deliberately if stronger contrast is the goal. Exact repetition may be the right choice on the first repeat.
- **Shape:** examine the full phrase rather than each event in isolation. Design a destination and choose the note-rate, leaps and holds that lead toward it. Reserving a higher note for the second half can help create a lyrical arc, but descending, centered and non-pitched shapes are valid. Do not mistake a higher MIDI pitch for an automatic increase in musical intensity.
- **Home:** decide whether the phrase should close, suspend, question or lead into another phrase. In a tonal closing phrase a stable tonic or chord tone is often suitable; an unfinished phrase can land on a less stable pitch or remain on a pedal. A loop may deliberately avoid complete closure until its final return.

**Original four-bar score sketch, A minor, 4/4.** Each row occupies one bar. q = quarter note, e = eighth, h = half; rest q = one quarter-note silence. These are new exercise notes, not the video's played examples. Use a plain lead voice and independent chord voices while judging the score.

| Bar / chord | Melody (ordered events) | Purpose |
| --- | --- | --- |
| 1 / Am | E4 q, G4 e, A4 e, E4 q, rest q | recognizable short-long cell and space |
| 2 / F | E4 q, F4 e, A4 e, C5 q, rest q | related rhythm; E approaches F, ending opens upward |
| 3 / G | G4 q, A4 e, B4 e, D5 q, rest q | familiar rhythm, higher phrase destination |
| 4 / Am | C5 q, B4 e, G4 e, A4 h | returns to the tonic; B/G are passing/color tones in context |

First audition the plain score and compare the fourth-bar ending with a sustained E4 or a rest leading to the next phrase. Then extend it with a slower answer or a fragment used in a contrasting section. The strong-beat and held notes should relate deliberately to the actual chord voicings; listen for whether B/G color in the last bar is appropriate. Do not blindly force all notes into a triad or preserve a dissonance that is accidental.

**Orbit translation:** for a monophonic line, select the current Note Sequencer note playback mode and use Pitch → Pitch plus Gate → Gate for a sustained instrument, or Trigger → Trigger for a struck/plucked voice, with the correct reference and documented velocity routing. Encode durations/rests using the supported step/articulation controls; verify note lengths and the timing grid against the [sequencer contract](../instruments/note-sequencer.md) rather than assuming MIDI-style overlapping notes. Chord parts need separately pitched voices or a verified suitable chord graph. Check actual sounded pitch, gates, timing, rests, phrase phase and harmony before choosing production effects. Compare motif variants at matched level. If no audio is auditioned, report this as a score proposal, not a listener-validated melody.

### Non-chord tones: musical detail without random discord

In this Western tonal vocabulary, a dissonant pitch can be meaningful when its approach, metric strength, duration and departure are clear. Classify actual vertical pitches including other melody/inner voices rather than assuming a note is dissonant because it is out of scale (chromatic chord tones can be stable locally).

| Type | Example over C or at a change | Function |
| --- | --- | --- |
| Passing tone | C4–D4–E4 over C | Connect two chord tones by step; D can be weaker or accented for effect. |
| Neighbor | E4–F4–E4 over C | Depart from and return to stable note. |
| Anticipation | Play C4 late over G before C chord arrives | Hints at next harmony before the bass arrives. |
| Suspension | Hold C4 from a C chord over G bass/G harmony and resolve to B3, then C4 on tonic | Preparation, dissonant hold, stepwise resolution; a 4–3 over G. |
| Appoggiatura | Leap to D5 on a strong beat over C; resolve down to C5 | Accented expressive friction, resolved by step in this example. |
| Escape tone | E4–F4–C4 over C | Step away then leap in opposite direction. |
| Pedal tone | Hold G2 while upper chords move G–C/G–G7 | Stable repeating/held pitch; resulting intervals change over bass. |

Do not enforce the table mechanically on blues, modal jazz, clusters or drones. In a classical-style exercise, preserve these relations across the full voices (e.g. check that a supposed suspension actually persists across the chord change and resolves). A `Hold` in Orbit's pitch sequencer holds only pitch, not the desired amplitude or articulation.

## 3. How Baroque and Classical musicians learned composition

Historical keyboard composition was often an acquired craft that included memorization, improvisation and ornamenting/realizing existing frameworks. Byros [C5] relates J. S. Bach's pedagogy to models and the act of developing an invention, discussing keyboard preludes and the Langloz partimento manuscript. Bach's 1723 title page for the Inventions and Sinfonias connects clear two- and three-part playing with invention and development of ideas. This gives a powerful lesson: find a generative motif, then explore its consequences.

In the broader eighteenth-century tradition:

- **Figured bass / thoroughbass**: a bass line with interval figures indicating what to play above it; a player realizes the harmony and texture.
- **Partimento**: a pedagogical bass (often unfigured, sometimes with other clues) from which the learner develops an idiomatic complete texture. It guides movement, cadence, harmonic grammar, and counterpoint, rather than supplying a finished melody.
- **Rule of the octave**: a repertoire of harmonizations for ascending/descending scale-degree basses in major/minor keys. It provides useful paths between rests/cadences; variants exist and it is not a universal automatic harmonizer.
- **Galant schemata**: recurring bass/soprano skeletons with stylistically typical upper parts, elaborations and placement in phrases. Gjerdingen [C6] describes a vocabulary of recognizable schemata. Rabinovitch and Slominski [C7] analyze Mozart's correction of a pupil's exercise and test schema/partimento-informed teaching with conservatory students; Rabinovitch and Carter-Enyi [C8] trace recurrent schemata in a sample of 27 galant expositions and distinguish skeletal pitches from surface elaboration. These findings support a repertoire-based workflow, not a single universally proven Mozart algorithm.

An illustrative skeleton in C inspired by a Prinner is soprano degrees `6–5–4–3` (A4 G4 F4 E4) against bass `4–3–2–1` (F3 E3 D3 C3). Harmonize it as F–C/E–Dm–C (one possible simple realization), then subdivide the right-hand notes while the outer-voice direction stays clear. This is an original exercise based on a common schema, not an exact historical realization. A canonical schema can be adapted, reharmonized and combined with others; confirm the specific historical version if claiming authentic imitation of a piece.

### Agent takeaway: skeleton → elaboration → form

1. Write an outer-voice skeleton: bass, significant melody tones and cadence goals. Use [harmony](harmony-scales-and-temperament.md#5-progressions-cadences-and-harmonic-time) to clarify where the phrase is going.
2. Fill inner voices with small directional movements. First test a reduction of only bass+soprano and then plain chords, without elaborate sound design.
3. Elaborate with passing tones, register changes, arpeggiation, varied rhythm and appropriate ornamentation. Preserve the important contour even if the intervening surface notes become busy.
4. Compose or improvise a different phrase from the same framework or related sequence. Arrange episodes, contrast, cadences and returns.
5. Add articulation and texture. Listen to the naked score as well as the full produced version.

This process is a practical synthesis of historical ideas, not the only way Mozart, Bach, or any other historical composer worked.

## 4. J. S. Bach: contrapuntal richness, arpeggiation and invention

**Counterpoint** is the craft of combining simultaneous, individually coherent lines. The lines need not be equally busy or equally loud. Prefer complementary rhythms and registral clarity; create vertical consonance or controlled, intentional dissonance. Huron's analysis of Bach keyboard polyphony [C9] connects interval choice with both consonance and avoidance of perceptual fusion. Traditional voice-leading conventions serve objectives such as audible independence; deliberate unison, thirds, parallel fifths or fused octave layers may be a different, legitimate effect.

### Techniques from an invention/fugue vocabulary

| Technique | Meaning | Original example based on C4 D4 E4 G4 |
| --- | --- | --- |
| Imitation | Second voice enters later with recognizably similar material | Upper C4 D4 E4 G4, later lower G3 A3 B3 D4 (transposition down a fourth) |
| Real answer | Transpose the subject with exact interval structure | G4 A4 B4 D5, exact fifth above |
| Tonal answer | Adjust selected intervals to preserve the tonic/dominant relationship | For a different subject starting C4–G4–A4, a possible answer G4–C5–D5 rather than G4–D5–E5; choose from harmonic context |
| Counter-subject | A recurrent companion line against the subject, not just a chord backing | Design contrasting rhythm and mostly thirds/sixths against the answer; leave space for its entries |
| Sequence | Repeat a pattern at successive scale levels | C4 D4 E4 G4 → D4 E4 F4 A4 → E4 F4 G4 B4 (diatonic, intervals adapt) |
| Inversion | Reverse direction of intervals around a chosen pitch | C4 Bb3 Ab3 F3 (chromatic inversion around C4; adjust tonal degrees when desired) |
| Augmentation / diminution | Lengthen/shorten durations in proportion | Quarter-note motif → half notes or eighth notes |
| Fragmentation | Use only the head/tail or interval/rhythmic kernel | C4 D4 E4; later E4 F4 G4 as a sequence |
| Stretto | Overlap successive subject entries before previous statement ends | Second voice enters while the first is still presenting the motif; check vertical result |
| Pedal and episode | Sustain tonal anchor or traverse new harmony using fragments between entries | G pedal with small sequential answers, later return subject in C |

A fugue normally opens with subject entries in multiple voices, often a dominant answer, then alternates later entries and episodes/development with tonal planning. There is no fixed universal count or mandatory order for subsequent entries. Research on the phrase relation of Bach's fugue subjects [C10] shows how entrances can interact with phrase boundaries and larger rhetoric. Subject occurrences alone are not a full structural analysis. Avoid calling every delayed echo a fugue: counterpoint requires intentional simultaneous notes, not just a delay feedback signal.

### Guided score: two-line imitation (original exercise, not a strict fugue)

At 100 BPM, 4/4, each column in a line is a quarter note. Give the lower and upper lines different dynamics and gentle note releases. `—` here means an actual sustained note in the written music, not a Pitch Hold with automatic sustain.

| Bar / harmony region | Upper (beats 1–4) | Lower (beats 1–4) | Commentary |
| --- | --- | --- | --- |
| 1 / C | C5 D5 E5 G5 | E3 E3 G3 C4 | Ascending subject, D5 passes on beat 2; upper voice is foreground |
| 2 / toward G | D5 C5 D5 F5 | G3 A3 B3 D4 | Lower line imitates the subject transposed; upper answers with different contour |
| 3 / C arrival | E5 — D5 C5 | C4 — B3 C4 | F5→E5 resolves; next B/D contraction moves to tonic |

This is a small tonal/imitative sketch, not a verified strict species-counterpoint exercise. At bar 3 play a **single long event** for E5/C4; if using a retriggering quarter clock, the dash must be implemented as a suitable sustain/mask or a separate lower-rate path rather than repeated attacks. Try a second realization in which the second line starts halfway through bar 1 (stretto); rewrite pitches that clash and compare the result, rather than automatically shifting an entire loop. Compare equal-register, same-timbre voices against octave-separated voices and choose whether to make them blend or stay independent.

### Repertoire study (read or listen to actual scores)

| Work | Focus | Transfer exercise |
| --- | --- | --- |
| Bach, Invention No. 1 in C, BWV 772 | Two independent parts and a short scale-like idea recurring in different positions, registers and transformations. Study entrances and related material. [Score source C11] | Author a two-bar subject and an answer that fits vertically; then use a fragment in a new episode. |
| Bach, Prelude in C, WTC I, BWV 846 | Recurring broken-chord surface above sustained lower tones; harmonic trajectory and changing bass/voice leading underneath. Final cadence differs from the recurring surface pattern. [Score C12; analysis C13] | Write eight chord voicings first, then map each to an original rapid pattern of their notes. Change harmony without needing new instruments. Interrupt the pattern at the cadence. |
| Bach, Fugue in C minor, WTC I, BWV 847 | Three parts and overlapping thematic/countersubject material. Compare entry points, modulations and episodes against the score. [C10, C12] | Write a subject and a separate, compatible counterphrase; transpose individually against an intended target key. |
| Bach, Goldberg Variations, BWV 988 | Variations organize diverse characters around a shared bass/harmonic framework rather than only repeating the opening aria's top melody. [C14] | Keep an 8-bar harmonic/outer-voice framework; create a lyrical version, rapid figuration, imitative version, minor variation and final reprise. |

Use public-domain compositions and verified editions for study; do not claim invented exercises are transcriptions. Historical scores can have editorial and interpretational differences. The point is to infer a controllable technique and write new material, not reproduce a famous work by rote unless requested.

## 5. Mozart: melodic speech, phrase function and contrast

Classical style often emphasizes a foreground melody with support (homophony), balanced phrases, contrast and cadence articulation, while still using counterpoint where helpful. Mozart's music is not uniformly homophonic, simple, or symmetrical. His themes often give an impression of musical conversation through question/answer, altered continuation, changes of accompaniment and a directed harmonic path. Caplin [C15] distinguishes formal functions: how a passage begins, continues and reaches closure can be as important as its note content.

| Form/function | Small useful model | Electronic transfer |
| --- | --- | --- |
| Period | Antecedent (often 4 bars) ends less conclusively, consequent (often 4 bars) gives stronger close | Verse/hook with first-question and second-answer endings, comparable identity |
| Sentence | 2-bar basic idea, 2-bar repetition/variation, 4-bar continuation/cadence | Make the continuation shorter cells, busier surface or faster harmonic rhythm (without requiring BPM automation) |
| Binary | Two main sections A–B, often a tonal departure followed by return/closure in tonal styles | Introduce contrasting texture and melody, then orient toward home |
| Rounded binary / ternary | Rounded binary embeds an A return in second section; ternary has distinct A–B–A sections | An altered hook return with identifiable middle episode |
| Rondo | Recurrent refrain interleaved with episodes (e.g. ABACA) | Memorable refrain between contrasting synth/bass explorations |
| Sonata-form principles | Exposition establishes main/contrasting-key material, development destabilizes/fragments, recapitulation resolves tonal conflict, possible coda | Design a longer narrative arc with substantive material and key changes; don't label every verse-chorus arrangement sonata form |
| Theme and variations | Retain recognizable theme/framework but transform figuration, mode, bass, texture or rhythm | Several genuinely different passages from one source rather than an unchanged loop with different gain |

These are simplified form models, not rigid bar requirements or an exhaustive theory of eighteenth-century form. Sections may expand, contract, overlap or use less normative cadences. A sonata movement is more than a three-section ABA scheme. Mozart's later finales can play with expected places of reprises and phrase proportions; Neumeyer [C16] analyzes dance and formal functions in Mozart's finales, useful for dance-music writers.

### Guided score: an eight-bar question and answer (original exercise)

Use 100 BPM, 4/4; each melody cell below gives the notes at four quarter-note beats (one beat per slot). The first 4 bars ask a question and the last 4 answer. You can render piano-like or synth keys, keeping a single overall timbre family. No particular instrument preset is required.

| Bar | Chord and bass | Melody beats 1, 2, 3, 4 | Middle/accompaniment notes | Purpose |
| ---: | --- | --- | --- | --- |
| 1 | C / C3 | E4 G4 A4 G4 | C4 E4 G4 | Establish motif; neighbor A returns to G |
| 2 | Am / A2 | E4 C5 B4 A4 | A3 C4 E4 | Rising leap, then B passes down to A |
| 3 | Dm / D3 | F4 A4 G4 F4 | A3 D4 F4 | Related contour over predominant |
| 4 | G / G2 | D4 B3 G4 — | G3 B3 D4 | Half cadence: hold G on beats 3–4 |
| 5 | C / C3 | E4 G4 A4 G4 | C4 E4 G4 | Recall opening |
| 6 | F / F2 | A4 C5 B4 A4 | A3 C4 F4 | New harmony; B is a passing pitch |
| 7 | G7 / G2 | G4 F4 D4 B4 | B3 D4 F4 | Dominant seventh; lead B approaches C |
| 8 | C / C3 | C5 — — — | G3 C4 E4 | Root-position tonic, soprano tonic at phrase end |

When adding the middle parts, voice or omit a doubled note where it obscures the melody. In bar 7, use a fuller G7 with F resolving to E4 (or E3) in the accompaniment; consider suppressing the lower B3 before the lead B4 to avoid doubling the leading tone unnecessarily in strict-style versions. This is a period-like pedagogical example with a half cadence and stronger authentic ending, not a strict four-part chorale or a Mozart transcription. For a richer version, keep structural targets and insert eighth-note neighbors or passing notes; compare to the original.

### Add keyboard movement without changing the underlying score

- **Alberti bass:** a broken-triad pattern low–high–middle–high, e.g. C3 G3 E3 G3 as eighth notes, twice per bar over C. On Am use A2 E3 C3 E3; on Dm use D3 A3 F3 A3; on G use G2 D3 B2 D3. For G7 add F3 in a chosen inner part; the plain three-note Alberti pattern does not by itself contain the seventh. Play these notes with their own envelopes, below the lead; avoid doubling the simultaneous held bass too strongly. This is one accompaniment style, not a synonym for all Mozart textures.
- **Other broken chords/arpeggios:** ascend, descend or alternate register; change the pattern at cadences instead of always looping it. Example over C: C3 G3 C4 E4 G4 E4 C4 G3; preserve bass/inversion and test overlap.
- **Chordal homophony:** instead of constant arpeggios, use soft sustained inner tones or offbeat stabs against the lead. Change density between phrase functions, not simply at a fixed loop count.
- **Countermelody / dialogue:** insert a short reply in a lower register after the motif's rest; later imitate an important interval, or contrast rhythm with the lead. Write and check actual simultaneous notes.
- **Ornaments/diminution:** turn one long E4 into E4–F4–E4–D4–E4 with suitably subdivided durations; preserve the intended phrase target. Trills, turns and appoggiaturas have stylistic performance conventions; author explicitly when exact style matters, not as uncontrolled vibrato.

Study Mozart's C-major Sonata K.545, first movement, alongside the [Neue Mozart-Ausgabe project](https://mozarteum.at/en/digital-interactive-mozart-edition-nma-online) or the [symbolic score collection C17]. Its opening melody stands out over an active broken-chord bass, while later material includes scale runs, tonal travel and contrasting figures. The apparent fullness arises from the simultaneous roles and elaboration of material, not from layers of unrelated instruments. The first movement's recapitulation begins in F major (subdominant) rather than simply restating the initial C-major opening; use this as a prompt to study how a recap can be altered to achieve overall tonic closure, not as a universal requirement. Also study K.545's finale for recurrent theme/contrasting episode and K.331's opening theme and variations for ways to maintain a core identity while changing rhythmic surface and character. [C15–C17]

## 6. Musical performance: make the notes breathe

Piano/fortepiano dynamics depend on striking behavior; a harpsichord has a different excitation mechanism and an individual key's level has much less direct dependence on touch, yet articulation, timing and registration still communicate phrasing. An identical synth waveform at identical loudness and length for every note can obscure the structure even if pitch is correct.

Repp's analysis of 115 recordings of Chopin's E-major Étude [C18] documents varied expressive timing strategies related to the structure of gestures (not Bach/Mozart-era performance evidence). Apply the general idea carefully: emphasize structural peaks, release before phrase endings, vary articulation between legato and staccato, and leave deliberate gaps. Random humanization is not a substitute for phrasing. A small deviation should serve a rhythm or phrase, and mechanical timing may be the intended character in chip or dance music.

For an electronic keyboard-style result, choose an envelope shape for each role. Preserve a stable pitch on sustained notes, soften bass attacks if they obscure chord transitions, use a quieter accompaniment, add restrained room/reflection and test dry first. Use a separate amplitude/brightness accent schedule where possible; do not make every note a full attack at maximum level. Piano realism is a separate acoustics problem (stretched partials, strike transients, decay, sympathetic resonance, pedal and true polyphony); a triangle plus a pluck envelope is only a stylized approximation. See [sound and instrument usage](../usage/sound-and-instruments.md) and [instrument recipes](instrument-recipes.md) for actual available instruments.

### Orchestrate the same score with available instruments

After reducing the piece to independent melodic/harmonic lines, consult the [current instrument selection table](instrument-recipes.md#choose-an-instrument-for-the-musical-role): gated Clarinet or Flute for a sustained counterline, triggered Physical Pluck/Marimba for broken-chord figures, Drawbar Organ for pedal color and optional Rotary Speaker movement. Compare each with the synthetic instrument/composite alternative at equivalent loudness; do not change the notes merely to make a candidate model seem better. For chords, route independent monophonic voices, and do not trust the experimental Physical Violin for strict counterpoint until octave/register tests and listening support it. These choices are orchestrations of an original score, not recreations of historic instrumental performance.

## 7. From an idea to a three-minute arrangement

Here is one adaptable structure at 96 BPM, 4/4, 72 bars (180 seconds). It is a composition exercise, not an automatic form template. All material should develop the same identifiable motif or thematic relationships.

| Bars | Time | Section and musical material |
| --- | --- | --- |
| 1–8 | 0–20 s | Establish C major, first eight-bar period. Expose motif over clear bass. |
| 9–16 | 20–40 s | Elaborate with broken-chord texture; same start, different phrase ending (e.g. half cadence). |
| 17–24 | 40–60 s | Transition via Am–D7–G; let bass and soprano lead toward a new center. |
| 25–32 | 60–80 s | Contrasting theme in G, broader note values, new rhythms, related interval cell. |
| 33–48 | 80–120 s | Development: fragment and imitate original idea, vary harmony/mode, alternate sparse and denser counterpoint; increase harmonic change near return. |
| 49–64 | 120–160 s | Transformed return in C: initial motif and contrast theme adapted to home; embellish, vary register and cadence. |
| 65–72 | 160–180 s | Cadential coda: reduce density, prolong V, resolve I, let final note/decay sound. |

Keep score-level contrast and rhythm meaningful, whether using piano-like sounds, chiptune, bass music or elaborate synth voices. The shift from one theme to another must be audible in pitches/timing/harmony, not only a new filter/gain setting. A minimalist or evolving drone has different formal goals and may use a much smaller vocabulary.

## 8. Technical translation: independently sequenced keyboard-like parts

The [Note Sequencer](../instruments/note-sequencer.md) now supports **Pitch only** and **Notes (Gate + Trigger)** modes ([PR #87](https://github.com/wgabel/orbit2/pull/87)); Notes mode has pattern-wide length/velocity but not independent per-note velocity, arbitrary note durations or polyphony. [Note Source](../instruments/pitch-control.md#phase-723--note-source-in-review) has keyboard pitch, gate, trigger and velocity but is monophonic. True score polyphony, automatic note durations, MIDI score import and tempo expression are not prerequisites for experiments; check current capabilities rather than assuming a feature from the musical analogy.

**Example realization of section 5, eight-bar period:**

| Lane | Timing | Data / routing |
| --- | --- | --- |
| Bass | 8 slots, 4 beats per slot | C3 A2 D3 G2 C3 F2 G2 C3 (MIDI 48 45 50 43 48 41 43 48), matching reference/base Hz, long or separate articulated bass |
| Lead | 32 slots, quarter-note grid | Enter the written melody; pitch Hold for the dash, plus an actual note-duration/articulation path |
| Broken-chord keyboard | 64 slots, eighth-note grid | Four-note pattern twice per bar, changing pitch targets according to actual harmony. Alternatively generate distinct inner voices with chord-rate lanes. |
| Inner tone/seventh | 8 slots or aligned faster score where needed | Actual voicing/voice leading, including F over G7 resolving to E; do not turn chord symbols into fixed whole-step transposition |
| Level/accents/rests | Same beat grid as relevant voice | Separate amplitude mask/envelope, not pitch changes |

Use [Orbit techniques T3](genres/orbit-techniques.md#t3--rhythm-grid-patterns-and-rests) (C4=0, C5=12 st, gain amount 1/12) for a bounded accent/rest mask and an actual clock or event path for articulation. Quantize to supported grid; for a shorter pluck at 100 BPM, eighth step = 300 ms, e.g. gentle attack 8–15 ms and decay under 280 ms to fit the grid, adjust to sound. A long note cannot always be represented by repeating a pitch Hold plus a periodic AD trigger; it will retrigger. Use a continuous pitched path with a shaped level mask, slower independent note clock/AD path, or live gate-held source as appropriate; document approximation if exact articulation is unavailable. A note sequencer will keep advancing while its scene fader is muted, so align phase to transport zero for entries. 64 slots per lane and 30 combined reachable action sources are current constraints; reserve capacity for masks and long-form gains. Avoid compiling a separate copy of every bar or attaching a lane for each chord if fewer independent lanes do the job. Consult the runtime source and scheduler tests before altering implementation.

**Score validation before DSP:** For each time slice of the score, resolve actual pitch (base note plus offset), sustain, gate or mask, register and resulting chord/interval against intended harmony. Check strong beats, dissonant preparations/resolutions, bass movement, repeated subject entries and octave/reference errors. If a chord changes halfway through a tail, include that overlap. For repeated sections confirm actual score content differs where claimed and that loops start at intended phase. Preserve intentional clashes; distinguish a missed or stuck event from a compositional choice.

**Musical evaluation:** Compare plain-score versions first, then full voice designs at matched levels. Listen for a memorable theme, clear phrase endings, harmony supporting (or deliberately challenging) it, independent/compatible lines, effective density changes and a purposeful return. Try A/B on the smallest relevant change (root-position versus voice-led chords, same melody over a new bass, one repeated phrase versus a varied continuation). Measure actual signals/levels/headroom separately; a successful graph, a good spectrum or low clipping does not prove that the musical composition works. If listening is unavailable, describe observed note structure and measured evidence without asserting listener judgment. Use the existing [production iteration](mixing-mastering-and-analysis.md#5-agent-workflow-render--analyze--refine--re-render) and [compositional development](compositional-development.md#4-musical-evaluation-and-refinement) rather than duplicating them.

## 9. Research, scores and further reading

The following sources combine empirical studies, analytical scholarship, primary scores and historical pedagogical context; the exercises in this document are original adaptations, not research results or validated patches.

- **[C1]** Pearce, M. T. & Wiggins, G. A. (2006). [Expectation in melody: the influence of context and learning](https://doi.org/10.1525/mp.2006.23.5.377). *Music Perception*, 23(5), 377–405. [Author manuscript](https://research.gold.ac.uk/1001/2/COM_2006-Pearce-Wiggins.pdf). Learned melodic expectations.
- **[C2]** Pearce, M. T. et al. (2010). [Unsupervised statistical learning underpins computational, behavioural, and neural manifestations of musical expectation](https://www.marcus-pearce.com/assets/papers/PearceEtAlNeuroImage2010.pdf). *NeuroImage*, 50(1), 302–313. Links modeled pitch expectations to empirical measures.
- **[C3]** Morgan et al. (2019). [Statistical learning and Gestalt-like principles predict melodic expectations](https://doi.org/10.1016/j.cognition.2018.12.015). *Cognition*, 189, 23–34. [Open manuscript](https://escholarship.org/uc/item/0zd498dt). Models learned patterns and grouping tendencies.
- **[C4]** Clemente, A. et al. (2024). [Perceptual representations mediate effects of stimulus properties on liking for music](https://pubmed.ncbi.nlm.nih.gov/38319962/). Melody-listening study with 96 nonmusicians examining perceptual mediation and liking.
- **[C5]** Byros, V. (2015). [Prelude on a partimento: invention in the compositional pedagogy of the German states in the time of J. S. Bach](https://mtosmt.org/issues/mto.15.21.3/mto.15.21.3.byros.php). *Music Theory Online*, 21(3). Historical models, Bach's own pedagogical text and realization examples.
- **[C6]** Gjerdingen, R. O. (2007). [Music in the Galant Style](https://academic.oup.com/book/49369). Oxford University Press. Scholarly monograph; schema prototypes and analyses including Mozart.
- **[C7]** Rabinovitch, G. & Slominski, J. (2015). [Towards a galant pedagogy: partimenti and schemata as tools in the pedagogy of eighteenth-century style improvisation](https://www.mtosmt.org/issues/mto.15.21.3/mto.15.21.3.rabinovitch.html). *Music Theory Online*, 21(3). Mozart's pupil exercise, partimento pedagogy and an empirical teaching project; audio/score examples.
- **[C8]** Rabinovitch, G. & Carter-Enyi, A. (2024). [Melodic organization and sequential ordering of galant schemata](https://doi.org/10.30535/mto.30.1.7). *Music Theory Online*, 30(1). [Open article with score examples](https://mtosmt.org/issues/mto.24.30.1/mto.24.30.1.rabinovitch_carter-enyi.html). Analysis of soprano skeletons and schema order.
- **[C9]** Huron, D. (1991). [Tonal consonance versus tonal fusion in polyphonic sonorities](https://doi.org/10.2307/40285526). *Music Perception*, 9(2), 135–154. See also [Tone and Voice](https://doi.org/10.1525/mp.2001.19.1.1) (2001) for perceptual interpretations of voice leading.
- **[C10]** [Subjects and phrase boundaries in two keyboard fugues by J. S. Bach](https://doi.org/10.1093/mts/mty029) (2019). *Music Theory Spectrum*, 41(1), 48–73. Analysis of phrase organization and subject placement. For computational subject analysis and annotated score data see [Algomus](https://www.algomus.fr/fugues/).
- **[C11]** Bach, J. S. [Inventions and Sinfonias, BWV 772–801](https://imslp.org/wiki/Inventions_and_Sinfonias%2C_BWV_772-801_%28Bach%2C_Johann_Sebastian%29), particularly [BWV 772](https://imslp.org/wiki/Invention_in_C_major%2C_BWV_772_%28Bach%2C_Johann_Sebastian%29). Primary compositions through score editions; [Bach Society recordings and context](https://www.bachvereniging.nl/en/bwv/bwv-772-786).
- **[C12]** Bach, J. S. (1722). [The Well-Tempered Clavier I, BWV 846–869](https://imslp.org/wiki/Well-Tempered_Clavier%2C_Book_1%2C_BWV_846-869_%28Bach%2C_Johann_Sebastian%29). Primary scores, including C-major prelude/fugue and C-minor fugue; [open modern engraving](https://upload.wikimedia.org/wikipedia/commons/d/d8/J.S._Bach_-_The_Well-Tempered_Clavier%2C_Book_1_-_OpenWTC.pdf).
- **[C13]** [Elision and the embellished final cadence in J. S. Bach's preludes](https://doi.org/10.1111/j.1468-2249.2008.00259.x) (2008). Analytical exploration of final cadences including BWV 846.
- **[C14]** [Interpretation of cyclic form in Bach's “Goldberg Variations” through performance history](https://www.gmth.de/zeitschrift/artikel/1119.aspx) (2021). Bass/harmonic pillars, cyclic structure and performance history.
- **[C15]** Caplin, W. E. (1998). *Classical Form: A Theory of Formal Functions for the Instrumental Music of Haydn, Mozart, and Beethoven*. Oxford University Press. Chapters on [sentence](https://academic.oup.com/book/49159/chapter-abstract/422032516) and [sonata form](https://academic.oup.com/book/49159/chapter-abstract/422032803). Analytic framework, not a prescriptive recipe for all styles.
- **[C16]** Neumeyer, D. (2006). [The contredanse, classical finales, and Caplin's formal functions](https://doi.org/10.30535/mto.12.4.1). *Music Theory Online*, 12(4). Mozart's dance-derived phrase/form play.
- **[C17]** Mozart, W. A. [Piano Sonata K.545](https://imslp.org/wiki/Piano_Sonata_K.545_%28Mozart%2C_Wolfgang_Amadeus%29). Primary repertoire; [digital edition of Mozart sonatas, Humdrum symbolic scores based on the Alte Mozart-Ausgabe](https://github.com/craigsapp/mozart-piano-sonatas); compare [NMA Online](https://dme.mozarteum.at/DME/nma/start.php?l=4) (critical edition).
- **[C18]** Repp, B. H. (1998). [A microcosm of musical expression. I. Quantitative analysis of pianists' timing in the initial measures of Chopin's Etude in E major](https://pubs.aip.org/asa/jasa/article-abstract/104/2/1085/555872/A-microcosm-of-musical-expression-I-Quantitative). *JASA*, 104(2), 1085–1100. A specific repertoire example of expressive timing, not a universal rubato rule.

For broader studies of pleasure, expectation, uncertainty and genre/experience see [musical quality and expectation](musical-quality-and-expectation.md#7-research-references-and-limits). When extending this material, identify the source, scope, original score/analysis, actual Orbit implementation evidence separately from experiments or hypotheses, and link from the [music creation index](README.md). Preserve useful earlier examples and user feedback.
