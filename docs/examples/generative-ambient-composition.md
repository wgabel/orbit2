# Generative ambient composition

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Instrument recipes](instrument-recipes.md) · [Worked drone](drone.md)

Guide introduced in [PR #22](https://github.com/wgabel/orbit2/pull/22) (documentation only; existing node implementation references are in the linked technical modules).

## 1. The structure of a musical piece

An ambient piece is more than a sustained sound with reverb. It can have a theme, harmony, rhythm, contrasting textures and a beginning, development and ending, even without a conventional drum beat. Generative composition delegates some of the decisions to the patch (e.g. when notes happen or how brightness changes), while the composer determines the musical vocabulary and the overall journey.

Think in layers with distinct musical roles and timescales. These are suggestions, not mandatory instruments: a sparse piece might use only three layers, and a single instrument can change roles during a performance.

| Layer | Musical purpose | Typical behavior |
|---|---|---|
| Foundation / bass | Establish tonal center, weight and continuity | Sustained root, slowly shifting bass notes |
| Harmony / pad | Create emotional color and harmonic movement | Slowly changing intervals, chords, inversions or swells |
| Melody / motif | Give the listener something recognizable to remember | Short phrases, repetitions, variations and pauses |
| Pulse / rhythm | Create movement and expectation without dominating | Soft plucks, gated textures, repeated echoes |
| Accents / events | Mark moments of interest, contrast or transition | Bells, impacts, a rare high note or a swell |
| Atmosphere / texture | Suggest space, depth and motion | Filtered noise, gentle beating, drifting timbre |
| Space / mix | Bring layers into a coherent sound world | Relative levels, shared reverb, delay and headroom |
| Arrangement / form | Give a long piece direction | Introduce and withdraw layers, build tension, climax, return and resolve |

Work on three timescales: individual attacks and releases (milliseconds to seconds), phrases and harmonic changes (seconds to minutes), and the arrangement (minutes to an hour). Silence, density and contrast are compositional tools. Changing everything all the time is as tiring as changing nothing.

## 2. Sound design versus composition

| Task | What is being designed? | What makes it successful? |
|---|---|---|
| Individual effect | A transformation of an input, e.g. delay, reverb, filter | A useful response to input, appropriate controls, continuity and safe levels |
| Individual instrument / sound | A playable or self-running voice, e.g. Impact — AD percussion, pluck, drone | Musical pitch, recognizable timbre and articulation; independent reusable controls |
| Entire musical piece | Several voices, their relationships, timing, musical material and long-term form | Coherent harmony, motifs, meaningful contrast, transitions and an intentional ending |

For a single instrument or effect, build and audition its internal signal graph first. For a whole piece, design from the top down: identify the mood, tonal material, roles and timeline, then choose or create the individual instruments. A beautifully designed impact is not an arrangement; a drone is a valid layer rather than automatically a complete composition. Reuse [instrument recipes](instrument-recipes.md) and [effect contracts](../audio/effects.md) as building blocks. Keep composition decisions in the piece rather than hard-coding them into a general-purpose DSP node.

### Organizing the patch

Use the root graph as the arrangement/mixing canvas and put self-contained voices in named [composite nodes](../editor/composite-nodes.md). Every voice's final audio connects to its own internal Output Rack Audio socket, which becomes the composite's Audio output in the parent. It does not need a Master output inside the composite. Expose useful modulation inputs when appropriate; ordinary Gain stages or mixer connection amounts at the root provide straightforward performance controls.

~~~mermaid
flowchart TD
    H[Harmonic bed] --> M[Root mixer]
    P[Melodic pluck] --> M
    B[Bell accents] --> M
    N[Atmosphere] --> M
    M --> R[Shared reverb]
    R --> G[Master gain]
    G --> O[Master output]
~~~

Make changes at the correct level: oscillator/envelope/filter edits change the voice; root mixer levels and the entrance/exit of voices change the arrangement. A flat graph also works for experimentation. Composite boundaries are organizational and do not create independent transports.

As the [patch annotation features](../editor/patch-annotations.md) become available, use a patch overview for musical intent and these kinds of performance instructions, named groups/aliases for voice roles, and a few note cards to explain non-obvious tuning or modulation. Keep technical identifiers valid and do not export hypothetical annotation fields before the schema supports them.

## 3. Worked piece: The Sleeping Mountains

An original, slowly evolving ambient sketch for a roughly one-hour performance using only synthesis. The recipe uses nodes available on main at the time of writing (through [PR #21](https://github.com/wgabel/orbit2/pull/21)). It is a construction guide, not an exported patch or an assertion of a listening-tested preset. Parameters are starting values; audition each layer and adjust by ear.

Set the shared tempo to **72 BPM**, treating four beats as one bar. Use **A minor pentatonic** (A, C, D, E, G) for the melodic and bass patterns. Open fifths and octaves in the bed introduce some extra harmony (e.g. B over E, still in A natural minor); choose roots and intervals by ear rather than assuming all transposed chord tones remain pentatonic. Different voices have different rates of movement, but remain related by pitch and mood.

### Layer A — shifting harmonic bed

Build a composite containing three Oscillators, a Note Sequencer, Mixer, low-pass Filter, slow sine LFO (another Oscillator), and a Gain. Connect the three audio signals to the Mixer, then Filter, Gain and Output Rack.

| Oscillator | Base frequency | Amplitude | Role |
|---|---:|---:|---|
| Sine | 110 Hz (A2) | 0.15 | Root |
| Triangle | 164.81 Hz (E3) | 0.08 | Fifth |
| Sine | 220 Hz (A3) | 0.08 | Octave |

Set all three to Free (Hz), Pitch tracking (free) = Semitones, Pitch offset = 0. Connect Note Sequencer Pitch to each Pitch socket with amount 1, preserving the oscillator frequency ratios. Set Reference note to A2 and use Edit pattern:

- Pattern mode: Loop; Step count: 5.
- Timing: Synchronized; Step division: 1 bar; Step multiplier: 8.
- Glide seconds: 5.
- Steps: A2, C3, D3, G2, E2.

At 72 BPM each root lasts about 26.7 seconds, with a 133.3-second cycle. Pitch glides make this a shifting open-interval bed rather than separate struck chords. The fifth and octave move together; for independent chord voices, use separate sequencers and deliberately chosen patterns.

Set the Filter to Low-pass, Cutoff 900 Hz, Resonance (Q) 0.707. Set the LFO to Sine, Free (Hz), Frequency 0.013 Hz, Amplitude 1; connect Signal to Cutoff with amount 600 Hz, producing roughly 300–1500 Hz over 77 seconds. Start final Gain at 1. If the filter sweep is too subtle on mostly sine content, try a brighter triangle layer at a low level.

### Layer B — repeating melodic pluck

Keep pitch and articulation separate: the sequence determines *which* pitch is held, and the envelope determines *when* and *for how long* the voice is audible.

~~~mermaid
flowchart TD
    S[Note Sequencer] -->|Pitch| O[Triangle oscillator]
    C[Clock] -->|Trigger| E[Envelope AD]
    E -->|Amplitude| O
    O --> D[Delay]
    D --> R[Reverb]
    R --> G[Gain / Output Rack]
~~~

| Node | Configuration |
|---|---|
| Note Sequencer | Reference A3; Loop; 7 steps; Synchronized, 2 beats, multiplier 1; glide 0 s |
| Oscillator | Triangle; Free (Hz); 220 Hz; Semitones tracking; Pitch offset 0; Amplitude **0** |
| Clock | Synchronized (BPM); Musical division **1/2 note** |
| Envelope | Mode AD; Trigger source **Trigger / clock**; Attack 10 ms; Decay 900 ms; attack curve 1, decay curve 3 |
| Delay | Synchronized (BPM); Dotted 1/4 note; Feedback 0.55; Wet mix 0.35 |
| Reverb | Decay 8 s; Damping 0.6; Wet mix 0.5 |

Enter `A3 C4 E4 G4 E4 D4 C4` in the pattern. Connect Pitch to oscillator Pitch at amount 1; Clock to Envelope's typed Trigger socket at unity (with Trigger / clock selected); Envelope output to oscillator Amplitude at amount 0.18. With base amplitude zero, the envelope creates a distinct attack and decay, not an extra amplitude offset on a continuous tone. Audio then flows through Delay, Reverb, final Gain (initial 1) and the rack.

At 72 BPM each step lasts 1.667 s; the full phrase is 14 beats or 11.67 s. The Clock and step timing share the transport. The Clock does not emit a startup/reset pulse: the first strike comes at the first crossed boundary, and the first slot may not be heard until wrap. Dotted-quarter echoes (1.25 s) provide a different rhythmic subdivision from the notes. Use a shorter decay or less feedback when the phrase becomes washed out.

**Current limitation:** Note Sequencer emits a continuous pitch offset only; Hold sustains pitch and is not a rest. A separate Clock still strikes on a Hold step, and a live keyboard key does not itself trigger the envelope. For a fixed audible gap, shorten decay, slow the rhythm, or fade/mute the voice. For independently articulated notes, future Note-mode Trigger/Gate support is needed. See [current sequencer behavior](../instruments/note-sequencer.md), [planned note articulation](../instruments/note-sequencer-design.md) and [envelopes and clocks](../audio/envelopes.md).

### Layer C — sparse bell accents

Use an independent Note Sequencer with Reference A4, Loop, Free seconds, 11 steps, Step seconds 23, Glide seconds 0. Enter `A4 E5 G4 C5 D5 A4 C5 E5 G5 D5 E5`. Set a Sine Oscillator to Free (Hz), 440 Hz, Semitones tracking, Pitch offset 0 and base Amplitude 0. Link the sequencer Pitch to the oscillator Pitch at unity.

- Clock: Free (Hz), Rate 0.037 Hz (about one strike every 27 seconds).
- Envelope: AD, Trigger / clock source, Attack 3 ms, Decay 1800 ms, attack curve 1, decay curve 3; Clock to Trigger at unity; Envelope to Amplitude at amount 0.09.
- Audio: oscillator → Reverb (Decay 12 s, Damping 0.6, Wet mix 0.7) → Gain (1) → Output Rack.

The 253-second pitch pattern and the independent strike interval gradually move against each other and the pluck. This is deliberately not a synchronized note-by-note melody: some pitch slots may be skipped, and a pitch change during the audible tail can retune the oscillator. For cleaner locked strikes, use the same synchronized step duration and Clock division as Layer B, and keep the bell sparse through low level, short decay or live muting instead. A quiet, rare accent should remain meaningful; more events are not necessarily better. A filtered-noise impact or editable Impact — AD percussion composite can substitute for a bell.

### Layer D — atmosphere / moving air

Connect Pink Noise (Amplitude 0.35) → Filter (Low-pass, Cutoff 700 Hz, Q 0.707) → Gain (base 0.06) → Output Rack. Use two Sine Oscillators as bipolar modulation sources with positive base values so the resulting controls stay nonnegative:

| LFO (Free Hz, amplitude 1) | Connection | Effective range |
|---|---|---|
| 0.017 Hz (59 s cycle) | Signal → Cutoff, amount 500 Hz | 200–1200 Hz |
| 0.023 Hz (43 s cycle) | Signal → Gain, amount 0.06 | 0–0.12 |

The filtered noise breathes in and out without adding a conflicting pitch. Keep it below the tonal layers. Slow LFOs are ordinary oscillators that modulate parameters, not audio sources to mix directly into the master. Additive modulation is `base + signal × amount`, clamped by the target, so a bipolar sine normally needs a positive base for symmetric amplitude movement. A unipolar AD envelope with base zero behaves differently. See [modulation math](../core/parameters-and-modulation.md).

### Root mix and space

Wire each composite Audio output into its own root Gain, then into one Mixer using unity audio connection amounts. The root Gains are the arrangement faders; start bed at 0.6, melody at 0.4, bells at 0.25 and atmosphere at 0.6. Mixer → shared Reverb (Decay 6 s, Damping 0.6, Wet mix 0.2) → Gain (0.5 initially) → Master output. These are conservative starting settings, not peak guarantees: the mixer adds signals, feedback and resonant filters can raise peaks, and reverb tails overlap. Current delay/reverb/filter instances are mono; do not infer stereo spatialization from the broad sound suggested by the recipe.

## 4. How to keep an hour interesting

### Local variation: overlapping cycles, not constant novelty

Different loop lengths cause the same familiar materials to meet at different points. For example, three synchronized patterns lasting 7, 11 and 13 beats have a joint cycle of 1001 beats, about 13 minutes 54 seconds at 72 BPM. This is a separate mathematical example, not the repeat length of the patch above. In the worked piece the bed repeats every 160 beats, the pluck every 14 beats (their pitch patterns realign after 1120 beats, about 15 minutes 33 seconds), and free-rate clocks and LFOs add further movement. These lengths alone do not guarantee musical quality or an exact non-repeating audio duration.

- Give each phrase a recognizable identity and leave audible space between notes.
- Keep simultaneous pitch patterns in a compatible scale or chord family; favor common tones in independent lines. Listen through their combinations, not just isolated loops.
- Vary timbre, relative loudness, density and echo tails more slowly than the melody. A subtle nested LFO can vary another LFO's rate without changing the composition's pitch vocabulary.
- Bounded pseudo-random noise exists, but directly routing it to pitch produces rapid jitter, not a thoughtful melody. Use crafted patterns and slow modulation until appropriate sample-and-hold, smooth random, probability and quantization tools are available.

### Long-range form: compose a performance

The patch can generate notes and evolving timbres without playing every key. The following hour-long timeline still needs a composer to shape section changes with the root Gains and continuous parameters. Start with all voices connected and turn off the quiet layers via their root Gains, rather than repeatedly removing/rebuilding nodes. Fade gradually; each new layer needs room.

| Time | Section | Performance plan |
|---|---|---|
| 00–08 min | Arrival | Fade in bed, then air; leave melody and bells at zero. |
| 08–18 min | Discovery | Slowly bring in the pluck and its echoes; maintain a clear motif. |
| 18–28 min | Expansion | Let occasional bells appear; keep them quieter than the motif. |
| 28–38 min | Climax | Brighten the bed/filter and raise selected layer levels within headroom; emphasize harmony or change a bass pattern. |
| 38–45 min | Retreat | Fade the pluck and reduce density; let the bed and accent tails breathe. |
| 45–55 min | Return | Reintroduce the motif and slightly richer brightness/echoes, recalling the earlier section. |
| 55–60 min | Resolution | Reduce layers one by one, settle on A, lower dry voices and let effect tails decay before fading master gain and stopping. |

An ending matters even in ambient music. Avoid just cutting playback at 60 minutes while the notes are still at full volume. Numeric parameter edits normally retain DSP state; topology changes can crossfade to a new voice and lose long tails. Build the intended routing before the performance, and favor gain and other continuous edits while playing. To record a new pitch motif, use Note Sequencer's [quantized live recording](../instruments/note-sequencer.md); it records pitch slots, not the complete performance timeline or note articulation.

## 5. What Orbit 2 can do now and what would help

Current tools are sufficient for layered, semi-generative ambient performance: independent pitch patterns (up to 64 steps, up to 600 seconds per free step), shared transport, free clocks, envelopes, nested modulation, effects, composite voices and mixing. A long pitch loop is not an arrangement sequencer. The hour-long example above is not a fully unattended structured performance; local variation is automated, major sections are performed manually.

Ideas for extending composition capability (not implemented by this guide, and not automatically scheduled milestones):

| Capability | Musical purpose |
|---|---|
| Note-mode Gate/Trigger, duration and rests | Articulate each actual note, including silence and held notes; see planned [Note Sequencer design](../instruments/note-sequencer-design.md). |
| Smooth random and sample-and-hold | Bounded, slowly changing or sampled values rather than raw noise jitter. |
| Probability gates and Euclidean rhythms | Controlled variation and occasional accents, with repeatable rhythmic structure. |
| Scale quantizer | Constrain generative pitch decisions to meaningful notes. |
| Macro controls | Shape several related parameters or voice levels with one musical gesture. |
| Scene sequencer and scene morphing | Schedule multi-minute sections, fades, density/harmonic changes and an ending, enabling an unattended composition with intentional form. |

For future composition work, design features against real musical workflows: a pitch source is not inherently a note event source, and a sound preset is not a song or scene. Prefer reusable primitives and compatible routing instead of special-case ambient-only nodes. Prioritize note articulation for playable motifs and arrangement/scene control for longer autonomous pieces; choose additional tools according to the requested musical goal. Consult the [roadmap](../../ROADMAP.md) before treating any proposal as an accepted implementation milestone.

## 6. Audition and iteration

- Audition each voice alone, then combine them. Verify both the intended note and its audible envelope; check a scope or meter for modulation and unexpected DC or clipped levels.
- Listen across several harmonic and melodic cycles. Confirm that changing alignment is pleasing and that bright sweeps, accents and delay/reverb tails do not overpower the motif.
- Rehearse every section transition, keep headroom and check for clicks, misplaced strikes or lost tails. Allow long-running playback to reveal unexpected repetition, timing drift or performance problems; consult the [performance guide](../../performance.md) if needed.
- Save the patch and document its intended tempo, role of each composite, performance controls and arrangement plan. Treat numerical settings as a starting point, not a formula for a masterpiece.
