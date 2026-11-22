# Compositional development — beyond changing layers

[Music creation index](README.md) · [Musical theory](music-theory-and-sound-design.md) · [Production iteration](mixing-mastering-and-analysis.md#5-agent-workflow-render--analyze--refine--re-render)

Documentation provenance: [PR #43](https://github.com/wgabel/orbit2/pull/43) (composition guidance and listener-feedback case; no new engine features).

**Principle: variation in instrumentation is not a substitute for variation in composition.** A compelling voice, a good motif and clean transitions can coexist with a monotonous piece. Changing only the gain or presence of repeating layers can create effective transitions, but may leave the underlying music unchanged. Design development at the timescales that matter for the musical brief.

## 1. Three levels of musical development

| Level | What develops | Examples | Evaluation question |
| --- | --- | --- | --- |
| Micro — sound and gesture | Individual events and continuous timbre | Interacting modulators, envelopes, filter movement, emerging harmonics, articulation | Does the voice breathe and respond, rather than sound like a test tone? |
| Meso — phrase and groove | Musical material over a few events, bars or phrases | Motif variation, call and response, rhythmic displacement, different note rates, harmonic movement, silence | Does the listener discover a new musical idea while recognizing the original identity? |
| Macro — form and emotional arc | Relationships between larger passages | Contrasting theme or chorus, transformed reprise, breakdown, drop, rising/falling tension, resolution | Does the next section feel like a different musical chapter, not just a different mix of the same loop? |

The levels overlap; they are analytical tools, not fixed durations. The [breathing harmonic drone](foundations/breathing-harmonic-drone.md) is a strong example of micro development through interacting nodes. Its lesson should not be confused with a requirement to change chords or add a chorus to every drone. Minimalism, drone, ambient and hypnotic dance music may purposefully develop one dimension very slowly; listen for intentional evolution on the relevant timescale. An effect, instrument or loop does not require song form.

## 2. Compose the changes before building the voices

For pitched pieces, use [harmony, scales and temperament](harmony-scales-and-temperament.md) to plan actual voice-led harmony, cadence goals and modulation; the [classical composition and melody guide](classical-composition-and-melody.md#3-how-baroque-and-classical-musicians-learned-composition) provides motif/skeleton development, counterpoint, phrase structure and original keyboard score exercises that can transfer to electronic genres. Start with a simple score reduction when appropriate; complexity of timbre should not substitute for development of the material.

- Establish a memorable motif or identity and an emotional trajectory. For tonal music choose a suitable harmonic palette; for non-tonal music consider recurring rhythmic, gestural or textural identities instead.
- For each important section, specify its musical content as well as the active layer names: theme/phrase, pitch contour and register, rhythm and note density, harmonic progression or pedal, dynamics, timbre, and the relationship to the preceding section. A short passage may not need every dimension to change.
- Keep recognizable connections between sections (e.g. a shared melodic interval, rhythm, bass pedal or timbral signature), while making the important departures substantial enough to hear. A distinct chorus can use a new melody, broader intervals, new chord movement or a contrasting groove; it should not merely be the verse with extra oscillators. A drop can change groove, bass phrasing, rhythmic gaps and melodic interaction, rather than only volume or distortion.
- Use subtraction deliberately: let important voices disappear before a new section, use silence and tails to create tension, let a simpler version of the theme return. Preserve effective subtractive transitions when rewriting the score.
- Separate global tempo from perceived speed. A change from eighth notes to sixteenth notes doubles their event rate at fixed BPM; halftime and double-time grooves change perceived pulse using accents. Use this when independent melody pacing or groove changes are desired without disrupting all synchronized layers. Actual tempo changes are another musical choice, not a requirement.

### Example: contrasting passages for an upbeat chiptune/polka and dark-bass hybrid

These are alternative musical ideas, not a prescribed genre structure or a tested revision. At 160 BPM in 4/4, eight bars take 12 seconds. At fixed transport tempo, change the pattern and articulation rather than relying exclusively on layer gain.

| Section (example) | Musical change | Connection to the rest |
| --- | --- | --- |
| Polka verse | Bouncy short D-minor motif, offbeat chord stabs and alternating bass | Introduce a memorable rhythmic cell |
| Chorus | New, higher, longer-note melody over a brighter relative-major passage (F major, then back toward D minor) | Retain the cell's opening interval or answering phrase |
| First drop | Halftime snare, syncopated bass calls and sparse chip answers | Quote just a fragment of the hook; leave actual rests |
| Breakdown | Slow the motif's note rate, change register and thin the harmony | Familiar contour with different emotional weight |
| Return / second drop | New ending to the verse, contrasting bass rhythm or double-time answer | Prepare a recognizably transformed return |
| Final chorus or coda | Reprise chorus with a deliberate cadence, countermelody or transformed harmony | Resolve the earlier tension; plan effect tails and ending |

For a continuous ambient piece the equivalent might be a slow shift in tonal center, a newly emerging melodic contour, long silence followed by a new call, or a sustained change in harmonic density. No verse/drop/chorus vocabulary is required.

## 3. Translate the score into Orbit 2

Consult current [Note Sequencer/pitch pattern contracts](../instruments/note-sequencer.md), [transport](../audio/transport-and-timing.md) and [musical techniques T3/T9](genres/orbit-techniques.md) before implementing. Use actual node definitions and verify limits; this guide introduces no new node.

- **Different phrases:** author distinct pitch patterns (or one longer pattern within its capacity) and appropriate articulation/level patterns. Select or combine separate voice branches with section gains; when both variants feed the same pitch destination, ensure inactive contributions are zero and check overlap and pitch bounds. A Hold step is not a rest; use actual amplitude/trigger/gate paths.
- **Different pacing:** a synchronized pattern at 160 BPM can use 0.5-beat (187.5 ms) or 0.25-beat (93.75 ms) steps. Keep triggers and level masks aligned to the intended grid. Longer held notes may require a separate gate/envelope or sustained voice path, not merely a slower pitch pattern that still retriggers on the old clock. Halftime snare/backbeat and faster melodic subdivisions can coexist. The transport has a shared editable BPM, not a per-section automatic tempo map; use manual tempo changes if desired and disclose the performance requirement. Free-second lanes do not follow BPM changes.
- **Different sections:** use explicit, bounded arrangement gain masks or manual transitions as appropriate. Arrange phrase banks to enter at the intended phase: muted loops still advance against transport, so unmuting does not guarantee a restart from their first step. Section boundaries and pattern lengths must agree or the phase offset must be deliberate. Count all reachable event lanes including inactive-but-connected branches against the current engine limit, and avoid needless duplication. Prefer reusable voices when practical.
- Check silence, releases and effects tails at section boundaries. Verify start/reset, timing of section entries, full-duration wrap or end, and the mix. Do not claim native scene scheduling or automatic tempo automation based on these workarounds. If the intended score exceeds current facilities, choose a different realization or a clearly described manual performance; document the gap separately.

## 4. Musical evaluation and refinement

Use the [production iteration](mixing-mastering-and-analysis.md#5-agent-workflow-render--analyze--refine--re-render) to preserve a baseline and the parts the listener liked. For pieces where development is part of the brief, evaluate meso and macro alongside micro and technical quality:

1. Map the **actual** sections to motifs, pitches/harmony, rhythmic subdivisions, event/rest patterns and active roles (from saved score, scheduler traces or suitable rendered observations). Compare adjacent sections and recurring sections. Mark changes caused solely by faders/filters separately from changes in underlying notes or rhythms. A planned section is not evidence that it was realized.
2. Audition contrasting excerpts and the transitions, then the overall arc where possible. At comparable playback levels, ask if the chorus/drop/return has its intended character and whether the track evolves or just gets fuller. Do not flatten intended dynamic contrast in the delivered mix. If listening is unavailable, distinguish structural evidence and objective measurements from predicted musical effect; request listener feedback.
3. Diagnose at the right level. For a well-mixed but monotonous track, first compare a new phrase, groove, harmonic change or larger form against the baseline. Preserve successful voices, hooks, layer interaction and transitions unless changing them serves the brief. If the issue is a hollow voice or poor balance instead, refine at that level.
4. Revalidate routing, note timing, section phase, headroom and role balance following changes. Re-render and compare the corresponding passages (or map revised section boundaries explicitly); seek listening feedback on the revised contrast. Peak, LUFS, layer count, long loop period or spectral change alone cannot establish engaging development. Do not use a fixed minimum number of changes, distinct motifs or sections as a universal pass criterion.

For a more detailed research-informed explanation of why purposeful repetition, unexpected departures, familiar returns and cultural context can matter, and how to compare versions, see [musical quality and expectation](musical-quality-and-expectation.md#6-apply-these-criteria-in-the-existing-production-iteration). This helps distinguish monotonous material from successful hypnotic repetition without prescribing a universal surprise or complexity target.

For delivery, summarize the form and why major passages differ, the refinement performed, and what remains unevaluated. Scale the detail to the brief; an intentionally static work should be judged against its intent, not a pop-song template.

## 5. Listener feedback case — Pixel Kraków — Black Cartridge

Original 144-second, 160 BPM D-minor 16-bit/chiptune, polka and dark-bass hybrid delivered 2026-11-22. Source revision: `Pixel-Krakow.orbit2.json`, SHA-256 `07bf62e8f829729fee040e9ecf62c822eb8a7c4c0a3c8c8ecbf399fd8fa82152` (delivered to the user, not a checked-in preset). The final native graph-compiler/Faust offline preview had seven musical roles, two bass drops, and gain/mask-based sectional arrangement. It had been refined for audio level and role balance; that alone did not validate the composition. The listener's playback route was not specified.

**Recorded human feedback:** the melody was very good, layering was nice, and some transitions created by removing parts were especially effective. The track nevertheless felt somewhat monotonous, with not much happening. Some sections would benefit from major changes in melody tempo or a chorus more clearly differentiated from the rest.

**Interpretation:** preserve the successful melodic identity, layering, sound palette and subtractive transitions. Investigate stronger phrase-level and form-level development rather than merely adding voices, increasing loudness or changing more synth parameters. The example in §2 proposes possible changes; it is not an auditioned improvement. In a revised version, compare actual section content and ask the listener if the contrast is convincing. This is listener feedback on the original composition, not blanket approval or rejection of the individual voice designs.

## 6. Maintain and expand

Record new generalizable arrangement lessons here or in a focused linked guide when warranted. Identify the actual score/patch revision, genre, relevant passages and listening conditions if known; distinguish listener observations from agent interpretations, experiments, measurements and untested proposals. Preserve successful and unsuccessful examples, and record follow-up evidence for a new revision without overwriting the original feedback. Link the actual documentation PR and any separate relevant implementation PRs. For distinct reproducible sound-design relationships use the [foundation catalogue](foundations/README.md) and its evidence conventions; not every compositional lesson needs a foundation asset. Update the music creation index and relevant workflow links so future agents find the lesson before composing, not only after feedback. Do not turn a useful example into a stylistic constraint or claim unavailable tools are prerequisites.
