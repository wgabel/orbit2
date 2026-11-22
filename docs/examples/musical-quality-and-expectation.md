# Musical quality, expectation and listener experience

[Music creation index](README.md) · [Music theory and sound design](music-theory-and-sound-design.md) · [Compositional development](compositional-development.md) · [Production analysis](mixing-mastering-and-analysis.md)

Documentation provenance: [PR #49](https://github.com/wgabel/orbit2/pull/49) (research-informed musical guidance and agent routing; documentation only).

**Purpose:** research-informed criteria for people and agents designing and evaluating music in Orbit 2. A valid patch, a pleasant timbre, and a compelling composition are three different achievements. This guide explains how musical structure, listener expectations and cultural/functional context interact. It complements, rather than replaces, the existing theory, genre, composition and production guides. It introduces no DSP features, patches, listening results or universal aesthetic score.

## 1. Three complementary perspectives

| Perspective | What it helps evaluate | What it cannot establish alone |
| --- | --- | --- |
| Structural and compositional craft | Recognizable identity, relationships between voices, contrast, pacing, tonal or non-tonal organization, rhythm, timbre and form | Whether every listener will enjoy the result |
| Cognitive and perceptual response | How established patterns create anticipation, how departures and confirmations affect engagement, how groove and familiarity affect experience | A universal amount of complexity, novelty or surprise that maximizes enjoyment |
| Cultural, aesthetic and functional context | Whether choices communicate a convincing mood, fit or deliberately challenge the audience's musical vocabulary, and serve the brief | That convention is mandatory, or that subjective preference excuses accidental technical faults |

Keep engineering quality separate: valid connections, reliable playback, deliberate levels and absence of unintended artifacts can be tested independently. A perfect waveform or a technically complex graph does not establish musical worth. Conversely, intentional distortion, unresolved harmony, repetitive form or rhythmic instability can be exactly what a piece needs.

## 2. Structural craft: unity, contrast and purposeful tension

A useful starting principle is to establish an intelligible relationship among musical events and then develop it. Unity can come from a motif, tuning, pedal tone, pulse, sound palette, repeating gesture, texture or performance rule; contrast can occur in any of those dimensions. The [theory guide](music-theory-and-sound-design.md#1-what-makes-music-work) covers the individual musical dimensions; the [development guide](compositional-development.md#1-three-levels-of-musical-development) describes how they change at different timescales.

- **Tension and release:** in tonal music, for example, a dominant chord can prepare a return to the tonic; a suspension may resolve by step. In electronic music, rising density, increasing roughness, a withheld beat, narrower register, rhythmic ambiguity or increasing brightness can prepare a contrasting event. Release can mean settling harmony, restoring a groove, opening the register, removing tension layers or even returning to silence. A resolution need not be immediate, tonal or complete. A sustained threatening drone may intentionally leave tension unresolved.
- **Integration of elements:** let voices have complementary or deliberately competing roles. Bass can anchor pitch/pulse; a lead can supply identity; percussion can clarify or challenge meter; a timbral reply can answer a phrase. Analyze actual simultaneous events and the resulting mix. A counter-rhythm or dissonance may be effective if its relationship is audible and serves the idea; incidental conflicts, masking and overloaded low registers can obscure it.
- **Complexity and accessibility:** an identifiable foreground can coexist with less obvious contrapuntal, rhythmic, timbral or harmonic detail. A compelling sparse piece is equally legitimate. Extra notes, oscillators, randomness and effects do not necessarily add meaningful detail. Repeated listening can reveal relationships that were initially unnoticed; do not assume complexity guarantees longevity or that simplicity implies generic music.

For tonal examples, the common cadence Am–Dm–E7–Am establishes a return to A minor; replacing the last Am with F can give a deceptive resolution in that language. Try the alternative with the same voicing, register and intensity first, then choose by listening in context. In a non-tonal texture the recurring behavior of a rough, bright gesture followed by a long dark tail may provide a recognizable expectation without a chord progression.

## 3. Cognition: anticipation, uncertainty and surprise

Listeners can learn relationships over years of exposure to musical traditions and also within a newly heard piece. Familiarity with a genre, previous repetitions, attention and musical experience can affect what they expect. Expectations need not be conscious or verbally described.

Distinguish two concepts:

- **Predictive uncertainty:** how unclear the next event is before it arrives. A stable pattern with one likely continuation has relatively low uncertainty; several plausible continuations imply more uncertainty.
- **Surprise (information content):** how unexpected the actual event is given that context. A rare continuation of an established motif can be surprising even though the listener's prediction had previously been quite confident.

In a study of Western popular-song harmonic syntax, Cheung et al. (2019) found greater pleasure ratings for both an unexpected chord in a comparatively predictable context and an expected chord following a comparatively uncertain context. This is not a linear rule that each extra surprise improves music. The study measured chord responses and modeled a particular musical vocabulary, not quality of complete compositions or universal musical preferences [1].

Gold et al. (2019) reported relationships between melodic pleasure and intermediate levels of modeled complexity in their experiments [2]. Witek et al. (2014) found that intermediate syncopation in the tested drum grooves elicited more pleasure and motivation to move than the low or high conditions [3]. These findings support trying manageable departures from a recognizable pattern; they are not prescribed note densities or syncopation ratios for all genres. Other work shows that repeated exposure can increase liking across different stimulus complexities [4, 5], and that the effect depends on listening conditions and stimuli.

Salimpoor et al. (2011) found dopamine involvement during anticipation and peak emotional enjoyment of selected music [6]. This is evidence that musical reward can involve anticipation and pleasurable experience; it does not establish that every violated expectation releases dopamine, that dopamine is the only mechanism, or that an Orbit analysis can measure reward chemistry. The goal is an intended musical experience, not maximizing a neurotransmitter.

### Practical expectation design

| Situation or listener goal | Compositional experiment | What to check |
| --- | --- | --- |
| Establish a new identity | Repeat a recognizable contour, rhythm or timbral gesture with a stable anchor | Can the listener identify or anticipate the idea? |
| Familiarity with a local surprise | Retain most of a phrase, alter its final interval, accent or chord | Does the change feel purposeful in relation to the original? |
| Uncertain or dense passage | Restore a recognizable bass rhythm, theme or color | Does the re-entry provide orientation, relief or satisfying contrast? |
| Physical groove | Keep a legible pulse while selectively shifting accents, or omitting an expected hit | Can the intended audience still feel the beat; does it invite movement? |
| Hypnotic or contemplative repetition | Preserve the pattern while allowing subtle evolution in beating, filter phase, spatial or harmonic texture, or leave it still where appropriate | Does the repetition support immersion instead of pulling attention away? |
| Experimental surprise or discomfort | Build a local vocabulary of unusual gestures, then repeat, deform or contradict it | Does the unpredictability sustain the intended atmosphere rather than simply obscure all relationships? |

An optional way to investigate a score is to estimate event probabilities conditional on preceding events using a suitable corpus or explicitly specified model. Information content in bits is −log2(p(event | context)); predictive uncertainty is Shannon entropy, the sum over possible continuations of −p × log2(p). A modeled probability of 1/2 yields 1 bit of surprise; 1/8 yields 3 bits. These are illustrative numbers, not probabilities for any real chord. A model needs a defined event representation (note, interval, onset, chord etc.), genre/training context, smoothing for unseen events and documented evaluation. Unconditional pitch entropy alone ignores sequence/context; a high entropy oscillator signal or spectral complexity is not equivalent to musical surprise. Orbit 2 is not assumed to implement a listener-model analyzer. No probability, entropy threshold or synthetic dopamine/quality score is a musical acceptance criterion.

## 4. Cultural context and functional success

Music can be evaluated against the intended listener experience and relevant musical cultures, not a single Western harmonic or popular-song standard. McDermott et al. (2016) found variation in consonance preferences across populations, including little preference for the tested consonant over dissonant chords among Tsimane' participants [7]. Mehr et al. (2019) documented diverse musical contexts across societies, including dance, infant care, healing and love; acoustic features were related to those contexts probabilistically [8]. This supports contextual evaluation without denying shared perceptual abilities or claiming that each culture has only one set of rules.

Also separate the emotion the music expresses from the emotion a listener experiences. Kawakami et al. (2013) found evidence for pleasurable feelings alongside perceived sadness [9]. A dark, grief-oriented or frightening piece can succeed without sounding cheerful or soothing. Mixed, personal and changing responses are legitimate.

| Brief | Useful questions | Potential mistake |
| --- | --- | --- |
| Chiptune/polka with dark bass | Is the playful rhythmic/melodic identity memorable? Does the heavier passage reshape its energy while retaining a relationship? | Assuming more distorted layers automatically produce a better drop |
| Dubstep / experimental space bass | Does rhythmic negative space frame the bass calls? Are timbral surprises distinct and the pulse intentional? | Smoothing every unusual rhythm or dissonance into conventional pop |
| Ambient / meditative drone | Does the sound invite sustained immersion? Is its evolution subtle or expansive as intended? | Requiring frequent harmonic changes or a chorus |
| Noise / industrial / horror texture | Is harshness controlled and dramatically purposeful? Do contrasting gestures or textures communicate the intended unease? | Equating a pleasing consonant spectrum with success, or treating accidental clipping as art |
| Background / concentration music | Does it support attention without unwanted interruptions or intrusive foreground gestures? | Maximizing novelty and perceived excitement |

Genre conventions are a useful reference for building and assessing a piece, not prerequisites for originality. For a hybrid, identify which conventions serve each part of the brief; negotiate intentional incompatibilities rather than averaging them away. Compare an unfamiliar piece with relevant reference characteristics and the listener's feedback, not a narrowly chosen favorite genre. The artist's explicit intention and the intended audience's reaction can disagree; report both.

## 5. Worked example: establish, surprise, orient, transform

This is an original score-planning experiment, not an auditioned patch or verified listener reaction. Consider a 144 BPM, 4/4 chiptune/leftfield-bass hybrid. Four bars take about 6.67 seconds. The short notes and timings can be drafted with pitch/trigger sequences; use actual current [sequencer](../instruments/note-sequencer.md) and [transport](../audio/transport-and-timing.md) contracts before graph construction.

| Passage | Material | Intended expectation |
| --- | --- | --- |
| A (bars 1–4) | D-minor hook, D4–F4–A4–G4 contour, on/offbeat chip articulation, repeating bass pulse | Establish an identifiable opening |
| A' (bars 5–8) | Repeat the contour but change its answer, introduce an offbeat accent and a short gap before the last event | Familiarity plus a local departure |
| B (bars 9–12) | Sparse halftime bass calls using the hook's opening minor third, with rests and contrasting timbral answers | Substantial change in energy, pacing and material |
| C (bars 13–16) | Withhold the expected bass hit; let a more ambiguous texture briefly dominate, then restore the hook's rhythm | Create uncertainty and reorientation |
| A'' (bars 17–20) | Return the recognizable idea in a new register with longer note values and a counterphrase | Recall and transformed closure |

This is one possible contrast study, not a recipe for every track. Compare with a reference version that repeats A through all passages with changing layer weight. Preserve hook and mix and change only the selected arrangement dimensions first. Check actual pitches/gates and phase alignment; a section mask does not automatically restart a sequencer. Listen to the lead and backing separately and in context, especially B and the return. Ask which version has clearer identity, a distinct contrasting passage and a satisfying return; a listener may prefer the simpler version. Neither answer is predetermined.

## 6. Apply these criteria in the existing production iteration

Use the [composition development workflow](compositional-development.md#4-musical-evaluation-and-refinement) and [render–analyze–refine procedure](mixing-mastering-and-analysis.md#5-agent-workflow-render--analyze--refine--re-render) rather than establishing a competing workflow.

1. **Define success.** Specify listener/use case, aesthetic intent, genre or hybrid, duration, focal musical idea and desired arc. Identify one or more qualities to preserve. Decide what *not* to optimize (e.g. avoid disruptive surprises in a meditation loop).
2. **Map expectations.** At relevant micro/meso/macro timescales, name anchors, repeats, likely continuations, deliberate departures, points of uncertainty, tensions and possible releases. Explain which musical dimension changes and why. Mark hypothesized listener expectations as hypotheses.
3. **Inspect the realized music.** Check actual note/gate/rhythm/harmony or modulation events, not only planned section names. A motif's recurrence, note/rest density, phrase contour, rhythmic accent pattern, section change and relative role balance can help diagnose whether the intended relationships exist. None of those is a quality score. Use the appropriate technical and audio checks, including headroom, alignment and unintended artifacts.
4. **Compare alternatives.** Preserve the baseline patch and render, compare equivalent passages at comparable monitoring levels, and change a small number of relevant musical dimensions. For a motif issue, vary the score; for masking, vary the mix; for a weak drop, test actual anticipation and contrast. Include prior context when evaluating a surprise or return. If possible compare unlabeled versions with varied order and ask about both the first and later listens. Keep intended dynamic differences within each version.
5. **Evaluate specific responses.** Ask listeners what they remember, where attention or energy changes, what they anticipate, whether transitions are effective, if the intended mood/groove comes across, and which moments become tiring, confusing or especially rewarding. Record their own language, listening context, timestamps, variant and preferences. A successful diagnosis may expose incompatible preferences; do not average away useful differences.
6. **Report and refine.** Clearly distinguish structural inspection, objective measurement, actual listener feedback, and untested interpretation. If no audition is possible, report what is observable and offer a focused listening experiment; never assert perceived engagement or measured neural reward. Preserve successful hooks, voices and transitions during fixes. Repeat only as needed for the brief and available tooling.

Example evaluation record (template, not evidence):

| Criterion | Evidence to collect | Outcome or remaining question |
| --- | --- | --- |
| Identity and cohesion | actual recurring note/gesture and role relations; listener recall | What ties the piece together? |
| Expectation and contrast | corresponding phrases and section boundaries; feedback on departure/return | Does the change have meaning in context? |
| Tension and release | musical preparation, actual arrival/rest/re-entry, listener reaction | Is the trajectory intentional, even if unresolved? |
| Groove and pacing | accent/rest grid, perceived pulse or listener movement | Does the rhythmic feel serve this brief? |
| Emotional/functional fit | brief, listening situation, reported experience | Does the result convey/support the intended experience? |
| Sound and production craft | native render if feasible, analysis and listening compared with baseline | Are intended gestures perceptible and accidental faults controlled? |

Do not combine these questions into one universal numerical rating, mandatory variation rate, or prescriptive checklist for all music. A human listener's feedback is direct evidence about that listener and version, not a guarantee for every future version. Mature artistry involves deciding when to follow or challenge these tendencies; the research helps guide experiments.

## 7. Research references and limits

The papers below are original studies, not a comprehensive musicology literature review. The musical applications and examples above are Orbit-specific reasoning; these studies did not test Orbit 2 or the particular example arrangements. The two compatible research propositions are that musical expectations can influence some experiences of reward and that aesthetic judgments depend on learned context and artistic function. Neither is a complete theory of greatness.

1. V. K. M. Cheung et al. (2019), [Uncertainty and Surprise Jointly Predict Musical Pleasure and Amygdala, Hippocampus, and Auditory Cortex Activity](https://doi.org/10.1016/j.cub.2019.09.067), *Current Biology* — harmonic expectations and pleasure in popular-song chord stimuli.
2. B. P. Gold et al. (2019), [Predictability and Uncertainty in the Pleasure of Music: A Reward for Learning?](https://doi.org/10.1523/JNEUROSCI.0428-19.2019), *Journal of Neuroscience* — modeled melodic expectations and ratings.
3. M. A. G. Witek et al. (2014), [Syncopation, Body-Movement and Pleasure in Groove Music](https://doi.org/10.1371/journal.pone.0094446), *PLOS ONE* — rhythmic syncopation, enjoyment and desire to move.
4. K. K. Szpunar, E. G. Schellenberg and P. Pliner (2004), [Liking and memory for musical stimuli as a function of exposure](https://pubmed.ncbi.nlm.nih.gov/14979811/), *Journal of Experimental Psychology: Learning, Memory, and Cognition* — repeated exposure, attention and liking.
5. G. Madison and G. Schiölde (2017), [Repeated Listening Increases the Liking for Music Regardless of Its Complexity](https://doi.org/10.3389/fnins.2017.00147), *Frontiers in Neuroscience* — repeated naturalistic listening across complexity categories.
6. V. N. Salimpoor et al. (2011), [Anatomically distinct dopamine release during anticipation and experience of peak emotion to music](https://doi.org/10.1038/nn.2726), *Nature Neuroscience* — reward-related neural responses to pleasurable music.
7. J. H. McDermott et al. (2016), [Indifference to dissonance in native Amazonians reveals cultural variation in music perception](https://doi.org/10.1038/nature18635), *Nature* — cultural variation in consonance preference.
8. S. A. Mehr et al. (2019), [Universality and diversity in human song](https://doi.org/10.1126/science.aax0868), *Science* — cross-cultural contexts and acoustic correlates.
9. A. Kawakami et al. (2013), [Sad music induces pleasant emotion](https://doi.org/10.3389/fpsyg.2013.00311), *Frontiers in Psychology* — emotion perceived in music versus emotion felt by listeners.

Maintain this guide by adding complete citations and distinguishing experimental findings from compositional interpretations. Record new score/patch experiments and listener observations in the closest relevant guide, with the actual revision and limitations, including contrary findings. Update the music creation index when new areas merit focused guidance; for reusable graph relationships, follow the [foundation catalogue](foundations/README.md#maintaining-and-expanding-the-catalogue). Avoid turning any study's sample, genre, measured response or average effect into a compulsory quality threshold.
