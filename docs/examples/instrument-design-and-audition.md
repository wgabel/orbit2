# Instrument and sound design — repeatable audition

[Music creation index](README.md) · [Voice design](sound-quality-and-voice-design.md) · [Instrument recipes](instrument-recipes.md) · [Roadmap](../roadmap/overview.md)

**Status:** General-purpose sound design and listening workflow, with an editable audition reference patch using existing nodes. Additional test profiles, a reusable sound library, built-in fixture registration and future engine capabilities are planned. This is not a catalogue of approved presets.

Documentation and reference asset: [PR #54](https://github.com/wgabel/orbit2/pull/54) (no new runtime capabilities).

## Scope: any genre or appropriate sound

Use this workflow whenever an instrument, synthesized voice, percussion sound, evolving drone, texture, sound effect or processing chain can be meaningfully isolated and compared with repeatable inputs. It applies equally to classical-inspired, jazz, rock, cinematic, ambient, industrial, dance, experimental, retro/chiptune and pop music, among other styles. The brief determines the desired character: intentional noise, harshness, digital artifacts, static tones and basic waveforms are valid choices.

For a bass-specific example see [Bass Lab II](foundations/bass-lab-ii.md): identical four-bar solo/backed stimulus, matched K-weighted RMS, measured branches and five listener ratings. More layers helped some candidates but not all; retain the rejected design's evidence and distinguish preview approval from native and in-piece validation.

Reuse the comparison method, not necessarily the same melody. Choose a suitable stimulus, define the intended sound and role, design and level-match candidates, evaluate them in isolation and context, and save the exact successful revision with evidence. Long-form or interacting sounds may need a reproducible contextual test rather than a short monophonic melody. Genre and role tags are optional, multiple-valued discovery aids, not restrictions on reuse.

For acoustic/electromechanical realism, consult [Faust-first instrument emulation](../instruments/realistic-instrument-emulation.md) for instrument-specific excitation, continuous expression and model candidates. Keep the existing synthetic presets as baselines, choose suitable sustained/repeated/chord/strike profiles, and distinguish listener approval from native DSP correctness. License clearance and performance validation precede inclusion of new physical-model dependencies.

## Select and test the current physical instrument candidates

Before hand-building every foreground part from basic oscillator stacks, examine the [current instrument decision table](instrument-recipes.md#choose-an-instrument-for-the-musical-role). For a repeated pick/strike use Physical Pluck, Physical Electric Guitar or Physical Marimba; for a breath-shaped held melody compare Physical Clarinet/Flute and Playable composite approximations; for an organ layer test Drawbar Organ with and without Rotary Speaker. Physical Brass is an expressive horn candidate; Physical Violin remains **experimental** with known register-jump concerns. These Faust nodes are implemented but have not been certified as realistic by human listening. A synthetic sound, deliberate chiptune or resonator combination can still win the comparison.

**Controlled test protocol:** (1) choose an instrument whose playable range and Trigger-vs-Gate articulation suit the reference; (2) set A4/MIDI 69 reference and Pitch → Pitch at unity for the physical model, with Gate or Trigger to match its port, and optional Velocity → Expression/Strike strength at amount 1 with destination base 0; (3) use the same note sequence and meaningful release/rest windows, record preset/controls, sample rate and output gain; (4) level-match audible passages, alternate reference and candidate in isolation; (5) evaluate a representative ensemble passage as well as solo notes; (6) report attacks, timbre, stability, CPU/headroom and user listening feedback separately. For long decays or held breaths, use an adapted fixed stimulus instead of changing the reference test unfairly. A new model should replace an approved timbre **only** when the chosen role and audition justify it. See the [exact technical routing](../usage/sound-and-instruments.md#faust-instrument-nodes).

The reference TEST melody driver remains a valid *frozen legacy fixture*. Its four independently sequenced control lanes are deliberately retained for reproducibility, even though [PR #87](https://github.com/wgabel/orbit2/pull/87) now also provides Note Sequencer Notes mode with Gate/Trigger. Do not silently rewrite the reference patch during comparisons; create a separately identified variant for alternative sequencer modes.

## 1. Case study: Prism Rush and the unintended chiptune character

In *Prism Rush* (three minutes, 128 BPM, D major/B minor), the listener strongly approved the composition and intricate melodies, but described the individual tones as childish, 16-bit/chiptune, and unlike recent pop production. This is a sound-design problem to investigate while retaining the successful score, not a reason to simplify its counterpoint, remove its breaks or rewrite its harmony.

The exact delivered patch is `Prism-Rush.orbit2.json` (SHA-256 `a533a4a8acd004091d2d6755151c5e6febef53691bd07343d5fb9c3db53634d5`); the following are observations from its native 48 kHz mono render, not a listening comparison with a commercial recording. Its 181 nodes include 43 oscillators (14 sine, 14 triangle, 11 square/pulse, 4 saw, including modulation sources), 17 envelopes, 15 filters and only one mono chorus. The foreground verse, chorus, lift and reply each use a repeating grid-clock AD body (approximately 210 ms decay on eighth notes; 440 ms on chorus quarters) and shorter brightness/transient AD, with a separate pitch and level mask. The tones use a small vocabulary of basic oscillator combinations and largely identical short falling articulations. They do not have real per-note gate lengths, velocity-dependent timbral performance, sampled or physical-model instrument attacks, independently ringing modes or stereo placement. The oscillator code already uses band-limited square and saw approximations; the characterization is not evidence that playback is actually limited to 8/16-bit audio, nor a diagnosis of aliasing or corruption.

| Voice | Note grid at 128 BPM | Number of active analysis windows | Median late-to-early energy difference |
| --- | ---: | ---: | ---: |
| Verse | 234.375 ms | 60 | −15.0 dB |
| Chorus | 468.75 ms | 32 | −15.1 dB |
| Lift / glass keys | 234.375 ms | 55 | −16.5 dB |
| Reply | 234.375 ms | 30 | −14.9 dB |

Measurement: same aligned post-voice-weight stems of the final native render; verse 15–30 s, chorus 45–60 s, lift 30–45 s and reply 60–67.5 s. For each grid slot, compare mean-square energy at 25–80 ms vs the last 45 ms of the cropped note window ending 15 ms before the next slot. Include slots whose early energy exceeds −48 dB re unit mean-square energy. Effects/tails and actual playing patterns are included; these are descriptive within-slot ratios, not individual ADSR meter readings or a controlled listener study. The −2.7 dBTP MP3 peak and −24 LUFS integrated loudness also show that the render was quiet and not clipping; neither metric establishes commercial production quality.

The likely combined causes: high-register pluck-like articulation repeated across roles; limited diversity in source spectra and partial evolution; closely related synthetic percussion; lack of natural/organic transients and stereo differentiation; regular grid timing and unvarying articulation. The user's judgment is the listening evidence; relative contribution of each cause is a hypothesis to test. Merely adding oscillators, making everything darker, detuning every part, applying more reverb or boosting loudness can make the result worse. Simple waveforms and playful chip sounds are excellent when intended. Do not use this feedback as a universal ban on them.

## 2. What makes an instrument sound expressive and production-ready?

Timbre is the perceived identity of a sound, not its pitch or loudness alone. Attack duration, spectral centroid (roughly the brightness-related distribution of spectral energy) and spectral fine structure influence dissimilarity judgments for controlled synthetic tones [P1]. Spectrotemporal modulations contribute to recognition of sustained instruments [P2]. These experiments do not establish a universal numerical boundary between childish and mature music.

Design multiple coordinated components instead of giving every voice one oscillator bank and a common pluck envelope:

- **Excitation and body:** a short hammer, pick, strike or breath onset can excite a longer tuned body. Use a quiet shaped noise transient, independently decaying partials or resonators; do not leave unnecessary raw hiss. Fixed body/formant frequencies can coexist with pitch-tracked musical partials.
- **Time evolution:** separate fundamental, higher partials and noise envelopes. A sustained body can hold while brighter partials decay. Set meaningful attack/decay/sustain/release (ADSR), note lengths, repeated-note attacks and overlapping releases. Delayed gentle vibrato and pitch/brightness drift may add movement if appropriate to the role. An AD envelope is excellent for a deliberate pluck or kick but not the only note behavior.
- **Dynamics and performance:** design strike velocity to shape amplitude *and* brightness/excitation when suitable. Preserve note onsets, sustain, legato, rests, repetitions and phrase dynamics. Start with deliberate, small timing/velocity variations when actually available; do not disguise random instability as human expression.
- **Spectral identity:** distinguish a rounded bass fundamental from a saturated or filtered upper body, a mellow lead from a metallic transient, a breathing pad from a closed percussion sound. Select register, body resonances and bandwidth in relation to adjacent instruments. More partials alone do not guarantee a natural sound.
- **Mix and space:** give the important melody a recognizable direct signal, separate supporting parts in register and depth, shape transients, use short ambience selectively, preserve dry sub/kick, and check the full arrangement at matched loudness. True pan, stereo spread, saturation and controlled dynamics can help but do not fix identical tone/phrasing by themselves.

As one genre-specific illustration, modern commercial pop is diverse: some songs intentionally use basic synths. Dua Lipa's *Houdini* credits synth/programming along with guitar, bass guitar, drums and keyboards [P3]. FINNEAS describes blending acoustic guitars, pianos and virtual instruments and the importance of production serving the artist's expression in the *Birds of a Feather* production walkthrough and album interview [P4–P5]. These are examples of complementary sound sources and performance choices, not instructions to reproduce copyrighted songs or proof that every hit requires acoustic recordings. Other styles may favor extreme modulation, noise, historical instrument behavior, sparse spectra or deliberately synthetic character; choose the target first.

## 3. The instrument audition fixture — TEST Melody Driver v1

**[Download the editable Orbit 2 reference patch](instrument-audition/reference.orbit2.json)** (ordinary schema-1 patch JSON, not a standalone module-library format). Import it to start a dedicated audition workspace. It includes a deliberately plain reference instrument, a separately editable control-only composite drone and root mixer/master. This is the preferred initial test environment for future sound proposals, without requiring a custom Module Library or new sequencing work. The test drone's identity is its name `00 · TEST — Melody driver v1` and the `TEST` tag in the patch overview, not a new DSP node class.

**Why a control drone, not a pre-recorded melody?** Sending the actual reference Audio to another oscillator or a piano-like filter would not cause that new instrument to play the same notes. The test drone instead provides reusable musical performance signals; the instrument produces its own sound.

~~~mermaid
flowchart LR
    T[TEST melody driver] -->|Pitch / Gate / Trigger / Velocity| A[Reference instrument]
    T -->|Same performance| B[Candidate instrument]
    A -->|Matched trim| M[Root mixer]
    B -->|Matched trim| M
    M --> O[Master]
~~~

The supplied workspace has the reference branch; add and connect the candidate branch yourself. Only one branch is audible during isolated comparisons (mute the inactive instrument; do not mute the shared driver). Both use the same transport. There is no inner Master or new clock inside the instrument. Leave off any additional test-path coloration unless evaluating that effect explicitly.

### Frozen score and signal contract

- 120 BPM, 4/4, D major, four bars, 64 sixteenth-note slots, 125 ms per slot, 8 s loop. The first slot and final four slots are silent, allowing the edge detector to arm and a short release to finish. Slow instruments may require longer test rests or a separate versioned profile.
- Pitch reference A4/MIDI 69 = zero semitones. The score moves from B3 to F#5, includes a repeat of D4 after a rest, shorter separated notes, connected notes and a sustained D5. Do not change the score between variants of a test.
- Four independent Note Sequencer lanes, all anchored to the same 64-slot loop, emit pitch, gate level, onset level and strike velocity. These original independent lanes are deliberately preserved for cross-version reference consistency even though Note Sequencer Notes mode is now implemented. Four lanes are small relative to the current combined 30-lane capacity. Pitch Holds preserve pitch; the gate defines note duration. Velocity remains at the last note's strike through the release, instead of multiplying the release by zero. This is a deterministic test workaround, not a replacement for 7.2.8d.

| Output | Signal type | Meaning and connection |
| --- | --- | --- |
| Pitch (st) | Modulation | Semitone offset from MIDI 69; connect at unity to an instrument's reference-matched Pitch input. |
| Gate | Gate | Opens for the occupied score slots, remains high across intentional legato, closes for rests. A Signal to Gate adapter converts the 0/1 lane. |
| Trigger | Trigger | One typed edge pulse at each written onset (including changes during a held gate); a separate onset lane and Signal to Trigger adapter guarantee a new event after at least one low slot. |
| Velocity | Modulation | 0–1 strength, encoded by semitone level / 12. Held until next onset, including releases. |

The initial driver supports onsets at least two sixteenths apart because the Schmitt trigger must see a low slot between rises. For arbitrary adjacent sixteenth repetitions, use the **implemented** Note Sequencer Notes mode (7.2.8d, [PR #87](https://github.com/wgabel/orbit2/pull/87)) with separate Note onsets, Hold ties and explicit Rest instead of misusing Hold as a note event. Notes mode has pattern-wide velocity and length, **not** independently recorded per-note velocity or arbitrary note duration. A gate-driven ADSR responds to a new gate edge, not to every legato pitch change; a separate Trigger-driven AD transient can still respond at each onset. The supplied reference uses exactly that combination.

**Verification of the supplied fixture:** schema-1 deserialization/restoration through the actual application registry; compile with the Orbit Faust graph compiler; render two 8-second cycles at 48 kHz with the actual pitch-pattern scheduler and dynamic observations. All 30 expected typed trigger pulses occurred at their exact 6,000-frame grid positions; settled gate, pitch and normalized velocity matched the score including legato/repeated notes. Final 250 ms of each cycle was silent (< 10⁻⁶ sample magnitude); the master peak was approximately −18.0 dBFS. This verifies the control and demonstration voice, not approval of any proposed instrument or live device performance.

**Score table** (onset and duration are zero-based sixteenth slots; velocity numerator divided by 12):

| Bar | Onset: note / duration / velocity |
| --- | --- |
| 1 | 1:D4/3/9; 5:F#4/2/8; 8:A4/4/11; 12:G4/2/9 |
| 2 | 16:F#4/4/10; 20:E4/2/8; 23:D4/1/7; 25:D4/1/10; 27:A4/4/11 |
| 3 | 32:D5/8/10; 40:E5/4/10; 44:F#5/2/11 |
| 4 | 48:B3/4/8; 52:E4/4/9; 56:D4/4/11 |

### How to use it today

1. Download the JSON. In Orbit 2 save your current workspace if needed, stop playback, then **Patches → Import JSON**. Save a named copy such as `TEST · Warm lead A`; leave the original unchanged. The `TEST` tag and name make it discoverable through patch search, and the note cards explain the wiring. The tempo is already 120 BPM.
2. Reset transport and Play. You should hear the plain reference voice playing the four-bar loop. The reference is a triangle with an independently triggered quiet octave transient and dry ADSR body — intentionally straightforward, not proposed as a finished instrument.
3. Add a **Playable** instrument from Add node, or create a candidate composite with the matching input rack and one Audio output. For a sustained voice, make Pitch → Pitch, Gate → Gate, Velocity → Velocity; for struck/modal instruments use Pitch, Trigger and Velocity where exposed. Do not connect a continuous signal directly to a typed event port. For a hand-built voice, wire Gate to an ADSR, Trigger to an AD transient, and the continuous pitch to the semitone-tracked oscillator/resonator. Route amplitude through independent envelope and velocity gains as appropriate.
4. Check reference tuning. For an A4-referenced oscillator use 440 Hz base at zero pitch offset. Built-in playable instruments may use A2 (110 Hz) or A3 (220 Hz) reference, so the unchanged driver plays two or one octaves lower unless explicitly adapted. That is a useful bass profile, but not an equal-pitch A/B against the A4 reference. To compare in the same register, configure the candidate's base tuning or add a clearly documented separate pitch-offset adapter inside it. Confirm actual Hz/note before comparing. Do not connect Pitch to linear-Hz Frequency.
5. Connect each candidate's Audio to the root **A/B mixer** with conservative gain/connection amount. Start at low monitor volume. Use the composite header **M** (mute) to hear one at a time; mute the reference before listening to candidate, and reverse. Leave the driver and shared master unmuted. Do not assume muted graphs stop using CPU. After changing wiring, stop/reset/play for a fresh identical pass; allow tails to settle. If graph load limits intervene, use separate saved patch copies each with the same driver/mixer, not 15 simultaneously running instruments.
6. Match apparent loudness using the candidate's final output trim or mixer connection (not changes to internal nonlinear drive or excitation). Compare comparable windows or whole-cycle RMS/K-weighted energy as a starting point; preserve the instrument's dynamics and verify by ear. Check peak, silence/rest, tuning, repeated strikes and tails with scopes; compare two loops at safe, similar monitoring levels. Avoid choosing the louder version by default.
7. Save the accepted editable candidate and its settings in a separate named patch; leave unsuccessful experiments as clearly labelled candidates. Today reusable cross-patch modules are planned: use a saved complete test-patch copy or the documented grouping workflow until 7.4.3–7.4.5 ship. Then save the tested instrument composite to the Module Library; the test driver itself belongs in a Testing/Instrument audition category. Do not save the driver plus reference as the candidate instrument.
8. Finally test the winning instrument in a representative real arrangement at the intended register, including sparse and dense sections, on the same score. A pleasant solo sound may mask a lead or bass when layered. Preserve user's successful themes and form; compare only the sound change first. Physical realism is not required for a good synthesized sound.

### Extend the test without losing comparability

Keep `v1` stable. The supplied four-bar D-major melody is the first test profile, not a required scale, tempo, genre or interface for every sound. The following are examples of additional profiles to create as needed; only the original melody fixture is supplied today.

| Sound or behavior to evaluate | Suitable repeatable test |
| --- | --- |
| Lead, keys, plucked or bowed instrument | Melody v1, plus role-specific register, dynamics, articulation, scales and intervals as needed. |
| Sub, bass guitar, acid or distorted bass | Uniformly transposed low-register notes; repeated and long accents; stability, drive and interaction with kick. |
| Sustained pad, ambient drone, industrial texture | Long holds, fixed modulation and transport, isolated attack/release and a long steady-state window; allow intentional evolution. |
| Percussion, impacts, rhythmic noise | Fixed trigger/velocity pattern with rests, repeated strikes and mapped pitch only if relevant. |
| Chords, classical ensemble, jazz voicings | Fixed voices and chord changes using independent note controls or future polyphony; compare voice interaction and release overlap. |
| Effects, soundscapes, transitions and experimental sounds | Identical dry source or excitation/control sequence, fixed automation and duration; check tails, response, intended texture and level. |
| Context-dependent or generative sound | Fixed backing, initial state and seed where supported; compare repeated passes if inherently variable and document variation. |

Pin applicable tempo, score/stimulus, reference tuning, dynamics, duration, effects state, mix weights, control settings and render revision. Not every sound needs Pitch, Gate, Trigger and Velocity: expose only meaningful controls (or a fixed audio input for processors), while keeping comparisons equivalent. Never change the test between A and B unnoticed. The short melody alone is not sufficient for drums, long drones, effects or large polyphonic arrangements.

## 4. Initial candidate palette: 12 contrasting instruments

The following proposed roles originated in the Prism Rush feedback but are reusable across genres, not an exhaustive or mandatory palette or already approved sounds. Use current editable components first, compare alternate sound routes using an appropriate shared profile, and recommend instruments only after validation and user listening feedback. Choose complementary roles for each brief. Expand with any useful sound family (e.g. orchestral brass, modal mallets, evolving dissonant drones, aggressive basses, intentional chip voices, experimental textures or transition effects) using the same process.

| Candidate | Initial design direction (existing nodes) | Evaluate / further capability |
| --- | --- | --- |
| 01 Warm vocal-like lead | Tracked triangle or restrained saw core, quiet separately shaped harmonics, band-pass body/formant branches, controlled ADSR, expressive vibrato | Legato, vowel-like spectral evolution, no exposed test-tone attack; future formant model and stereo |
| 02 Smooth analog lead | Filtered pulse/saw with quiet detuned partner and separate slower brightness envelope, restrained chorus | Sustain, tone movement and tuning over range; future unison with profiling |
| 03 Felt-like piano | Short attack excitation plus independently decaying tuned resonators/partials, darker sustained body, velocity-brightness interaction | Inharmonicity, repeated strike, decay and register; future multisample or string model, do not claim exact piano |
| 04 Electric piano | Soft harmonic body with shorter metallic tine partial(s) or carefully bounded linear-Hz FM-like sidebands | Velocity sensitivity, chord overlap; future dedicated FM/PM operators |
| 05 Muted electric guitar | Brief pick noise in a narrow band, oscillator/resonant body, fast brightness decay and varying accents | Pick/body separation, convincing note stops; future string waveguide/multisamples |
| 06 Rounded electric bass | Dry lower fundamental, filtered mid-body, velocity-driven finger transient, longer held notes and short release | Bass readable at low fundamental, stable in mix, not a high-octave chip pulse |
| 07 Deep electronic sub | Single clean pitched sine or rounded low harmonic, gate-aware amplitude, dry path | Headroom, kick overlap, stable tuning; intentionally simple |
| 08 Warm string pad | Slow ADSR, restrained detuning, subtly evolving harmonics/PWM, shaped bow-like excitation, different time constants | Physical body and sustain, no constant high buzz; future stereo/unison |
| 09 Atmosphere / airy texture | Very quiet shaped band-limited noise plus independently moving resonant tones, controlled tail | No exposed wind/hiss; depth without masking |
| 10 Electronic kick | Tuned downward pitch transient with separate short click/body and stable low tail | Punch, tail length, peak/headroom and interaction with sub; sample hybrid optional |
| 11 Snare / clap | Separate short body and filtered transient; several decays and controlled noise bands | Not a game-like beep; variable strength, recorded drum layering later |
| 12 Hat / percussion | Short shaped metallic and filtered noise transients or struck modal resonances; distinct accents | Organic variation, transient clarity, avoid hiss dominance; future recorded hits |

Start with lead, electric keys, bass, pad and drums, rather than building twelve similar plucks. For sustained leads, a first experiment can use attack 20–60 ms, decay 120–300 ms, sustain 0.5–0.8 and release 80–250 ms; separate a brighter transient with much shorter decay, and use filtered body bands to change color across notes. For a synthetic plucked key, experiment with independent resonant mode frequencies/decays and an excitation burst rather than only a fixed harmonic oscillator stack. Values are starting points, not universally appropriate or listening-certified. Reference [synth recipes](synthesizer-patch-recipes.md), [resonators](../instruments/physical-modelling.md) and [envelopes](../audio/envelopes.md) for real controls. Linear-Hz frequency modulation is not dedicated phase modulation; mono chorus is not stereo width; no fake sample nodes or saturation controls.

## 5. Instrument acceptance and evidence records

Use a repeatable listening and engineering loop for each role: design → run an identical test profile → compare level-matched alternatives → diagnose → refine → test in context → retain or reject. Avoid hiding an unsuitable sound with a beautiful arrangement or loud master. Keep a plain reference and failed variants where useful. A compact record should include:

- Candidate name/version, actual graph/export, dependencies/PRs, role, intended character, optional genre tags, usable register where relevant, control interface, test score/profile revision and transport; envelope/transient/body and effects design choices.
- Import, validation, actual native render (if available), tuning, finite output, resting silence, articulation/retrigger and tail behavior, peak/headroom, relative metrics and comparisons. Record alignment, sample rate and test window selection. Check voice CPU/performance too before accumulating many simultaneous candidates.
- Listening judgment (e.g. too chip-like, warmer, cloudy, expressive, organic, weak transient, reference-like character), specific notes or sections, tested playback and whether solo or in mix. Use user feedback as evidence for that listener and role; never infer human approval from metrics.
- Status: `candidate / graph-validated / native-rendered / listener-approved for role / rejected or needs refinement` (technical evidence and subjective approval can coexist independently). Record exact variant, date, and feedback. Preserve prior revisions. Only promote a listener-approved reliable version into the recommended reusable palette for its stated role and context. Approval is not universal: a rejected variant for one brief may be ideal in another. Agents should select approved roles where relevant or identify new sounds as experiments.

For the specific Prism Rush case, compare original and sound-redesigned versions on the very same score, harmony, breaks, tempo and duration. Render stems and measure aligned role/backing ratios using [contextual layer balance](contextual-layer-balance.md); match overall playback level when comparing. Compare major chorus, quiet interlude and borrowed-minor bridge, and report what changed in instrument design versus mix. Revisit arrangement only if the user requests it or a later separate experiment justifies it.

## 6. Future engine support and dependencies

The first fixture and candidate sound experiments are useful now with existing nodes, so Phase 7.3 performance tooling is not a prerequisite for this small test patch. Keep the existing performance-first requirement for expensive DSP expansion, especially stereo/unison (7.5). Follow the [roadmap register](../roadmap/advanced.md#phase-76--instrument-and-sound-design-and-audition) for staged delivery. Address gaps by reusing existing contracts rather than inventing parallel sequencer or preset systems:

| Priority | Needed capability / existing track |
| --- | --- |
| Very high | **Remaining** per-note velocity and independently editable durations; Notes-mode Pitch/Gate/Trigger, pattern-wide velocity/length, rests and ties already implemented in 7.2.8d ([PR #87](https://github.com/wgabel/orbit2/pull/87)). Keep the frozen four-lane reference score when developing new profile variants. |
| Very high | Reusable independent instrument and test modules, catalogue and portable definitions (7.4.3–7.4.5); avoid bespoke storage. |
| High | Sample assets, sample players, and velocity-layered multisample instruments (Phase 8); recorded instruments are an option alongside synthesis, not mandatory. |
| High | True stereo/panning/unison (7.5 after 7.3 baseline); effects remain mono until that channel work ships. |
| High | Safe saturation and dynamics processors (7.6.6), with real detector-driven compressor/limiter distinct from programmed gain and explicit peak tests. |
| High | Further physical models and polyphony (7.2.7 / 7.2.6c), evaluate tuning, live cost and release tails. |
| Medium–high | Wavetable synthesis (8.6), dedicated FM/PM operator design (7.6.7), justified by measured and listener-validated gaps; don't just count waveforms. |

## 7. Research and production examples

- [P1] Caclin, A., McAdams, S., Smith, B. K. & Winsberg, S. (2005). *Acoustic correlates of timbre space dimensions: a confirmatory study using synthetic tones*. JASA 118, 471–482. [DOI 10.1121/1.1929229](https://doi.org/10.1121/1.1929229); [PubMed](https://pubmed.ncbi.nlm.nih.gov/16119366/).
- [P2] Thoret, E., Depalle, P. & McAdams, S. (2016). *Perceptually salient spectrotemporal modulations for recognition of sustained musical instruments*. [DOI 10.1121/1.4971204](https://doi.org/10.1121/1.4971204); [PubMed](https://pubmed.ncbi.nlm.nih.gov/28039992/). Instrument recognition, not quality rating or a pop recipe.
- [P3] Dua Lipa (2023), *Houdini*. [Release production/instrument credits](https://www.qobuz.com/us-en/album/houdini-dua-lipa/r7s9sxu3vjqfc).
- [P4] FINNEAS, *Birds of a Feather* writing/producing breakdown (2025). [Mix with the Masters release and course description](https://community.mwtm.com/t/new-video-release-finneas-writing-and-producing-birds-of-a-feather-by-billie-eilish/12550).
- [P5] Billie Eilish and FINNEAS (2024). [Direct interview on production and artistic intent, NPR/VPM](https://www.vpm.org/npr-news/2024-05-17/billie-eilish-finally-remembers-who-she-is).

Keep research evidence, actual patch measurements, hypotheses, and the user's feedback separate. The documentation maintenance workflow in [AGENTS.md](../agents/contributor-instructions.md) and the [music creation index](README.md) applies to this catalogue; add validated reference assets, versioned test records and actual PR provenance as experiments progress.
