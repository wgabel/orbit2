# Synthesizer patch recipes — reference sound palette

[Music creation index](README.md) · [Sound quality and voice design](sound-quality-and-voice-design.md) · [Instrument examples](instrument-recipes.md) · [Orbit 2 musical techniques](genres/orbit-techniques.md) · [Foundation catalogue](foundations/README.md)

Documentation provenance: [PR #50](https://github.com/wgabel/orbit2/pull/50) (documentation only).

This is the user-supplied collection of classic synthesizer recipes. Consult it when planning the sound palette for new music and when designing or refining individual voices. The recipes are cross-synthesizer design references, not a claim that the original named instruments have been recreated, that these exact settings have been tested in Orbit 2, or that every feature mentioned is implemented. The full original text is preserved below, including its terminology and suggested settings.

## Recipe selection

| Recipe | Possible role or character |
| --- | --- |
| 1. Juno Chorus lush pad | Warm, nostalgic harmonic bed |
| 2. Moog / Berlin sub-heavy punch bass | Tight articulated bass foundation |
| 3. Blade Runner CS-80 brass lead | Expressive lead and cinematic swell |
| 4. Deadmau5 / progressive analog pluck | Arpeggios, rhythmic chords and melodic ostinatos |
| 5. Hoover / Reese detuned rave bass | Aggressive, moving bass and contrasting midrange |
| 6. Board of Canada lo-fi warped keys | Nostalgic texture, keys and drift |
| 7. DX7 / classic FM glassy electric piano / bell | Metallic transient, pitched percussion and keys |
| 8. Acid 303 squelchy bassline | Resonant rhythmic lead or bass |

## Use the recipes when creating music in Orbit 2

1. **Before building a piece:** read this catalogue and assign sound roles to the brief and musical plan. For each important synthesized voice, consider whether one of these recipes, a combination, a [foundation](foundations/README.md), an existing instrument or a new design best serves the musical role. Record the chosen recipe number/name and meaningful adaptations in the patch design or delivery notes, or explain why a different voice design fits better. A recipe is a starting point, not a mandatory palette or a song structure.
2. **Translate the technique, not just the numbers.** Check actual registered nodes, parameter units/ranges, signal types, channel layout, event and modulation connections, and the current editor/runtime contracts. Oscillator pitch ratios and cents, amplitude/filter envelopes, waveform/PWM support and cutoff modulation have concrete meanings; a percentage in another synthesizer does not necessarily match Orbit's connection amount, filter Q or parameter scale. Use separate pitch/timbral modulation and gain stages as appropriate.
3. **Handle capabilities honestly.** A recipe may call for stereo unison, stereo/ping-pong effects, an exact 18/24 dB filter slope, keyboard tracking, performance expression, legato/glide/accent events, bitcrushing or dedicated FM/phase-modulation operators. Verify each before claiming it exists; where absent, choose a compatible approximation, omit the optional component, or explicitly describe a manual control. For example, independently detuned oscillators can represent unison-like timbral beating; mono chorus is not stereo width. Audio-rate frequency modulation is not automatically identical to a DX7 operator algorithm. Keep sub and processed mid-bass separate where the available graph and musical role warrant it.
4. **Validate each adaptation in context.** Follow [voice quality](sound-quality-and-voice-design.md#4-build-a-reusable-auditioned-palette) and the [production iteration](mixing-mastering-and-analysis.md#5-agent-workflow-render--analyze--refine--re-render). Use conservative levels especially on high-resonance and driven bass recipes, check tuning, modulation range, note articulation, releases/tails, relevant register, peaks and role balance. Compare alternative settings at similar monitoring levels. Distinguish actual native render, measurements, human listening feedback and expectations. Do not describe an adaptation as a proven Orbit preset merely because the reference recipe is familiar.
5. **Maintain evidence.** Preserve the supplied reference text. If an Orbit-specific adaptation becomes worthwhile and reproducible, document its actual graph, node settings, revision, compatibility and evidence separately in the closest voice guide or [foundation catalogue](foundations/README.md#maintaining-and-expanding-the-catalogue). Link it here if useful. Keep failed experiments and limitations attached to their own variants.

For modern pop and avoiding unintended chiptune character, use the [12 candidate role designs and fixed-melody audition workflow](modern-pop-instrument-design.md). A classic synthesizer recipe is only a starting design: select distinct articulation and body/transient relationships across voices, compare on the [TEST Melody Driver](instrument-audition/reference.orbit2.json), and record actual listening evidence before recommending a variant.

Technical starting points: [oscillators and noise](../audio/oscillators-and-noise.md), [effects](../audio/effects.md), [envelopes](../audio/envelopes.md), [parameters and modulation](../core/parameters-and-modulation.md), [pitch control](../instruments/pitch-control.md), [Note Sequencer](../instruments/note-sequencer.md) and [instrument usage](../usage/sound-and-instruments.md).

## Original user-supplied recipe text

Source: `synthesizer-patch-recipes.txt` (verbatim, SHA-256 `38bb3c4d1d6386c60f9d375ebfce703dc899fd331cb3bb1b5f42413eec8bb56f`).

```text
================================================================================
SYNTHESIZER SOUND DESIGN: ESSENTIAL & PROVEN PATCH RECIPES
================================================================================

These classic patch recipes translate across almost any subtractive, wavetable,
or FM synthesizer (such as Vital, Serum, Diva, Prophet, Minimoog, or Dexed).

--------------------------------------------------------------------------------
1. THE "JUNO CHORUS" LUSH PAD
--------------------------------------------------------------------------------
Vibe: Warm, nostalgic, wide chords.
* Oscillator 1: Square/Pulse wave.
* Sub-Oscillator: Square wave, pitched 1 octave down, mixed at ~40%.
* Modulation (PWM): Route a slow LFO (0.3–0.6 Hz, triangle/sine) to modulate
  the Pulse Width of Oscillator 1.
* Filter: 24dB Low-Pass filter. Cutoff around 1.5–2 kHz, low resonance (~15%).
* Amp Envelope: Slow Attack (400–800 ms), High Sustain (85%), Long Release
  (1.5–2 seconds).
* Effects: Lush Stereo Chorus (essential for classic width) + large hall/plate
  reverb.

--------------------------------------------------------------------------------
2. THE "MOOG / BERLIN" SUB-HEAVY PUNCH BASS
--------------------------------------------------------------------------------
Vibe: Massive low-end weight with a tight, percussive snap.
* Oscillator 1: Sawtooth wave.
* Oscillator 2: Triangle or Sine wave, pitched 1 octave down (clean sub).
* Voicing: Monophonic, Legato enabled, subtle Glide/Portamento (30–50 ms).
* Filter: 24dB Low-Pass filter. Cutoff low (150–250 Hz), moderate resonance
  (25–30%).
* Filter Envelope (Env 2): Zero Attack, Short Decay (200–350 ms), Zero Sustain.
  Set envelope amount to ~50% so the filter snaps open on every note.
* Amp Envelope: Zero Attack, Medium Decay (400 ms), Medium Sustain (50%), Short
  Release (80 ms).
* Pro Tip: Add subtle analog saturation/drive after the filter to bring out
  upper harmonics on smaller speakers.

--------------------------------------------------------------------------------
3. THE "BLADE RUNNER" CS-80 BRASS LEAD
--------------------------------------------------------------------------------
Vibe: Emotional, vocal-like swell with expressive pitch drift.
* Oscillator 1 & 2: Sawtooth waves, detuned against each other (±10–15 cents).
* Filter: 12dB or 24dB Low-Pass filter. Cutoff around 800 Hz, noticeable
  resonance (35–40%).
* Filter Envelope (Env 2): Medium Attack (150–250 ms) for a brassy "bloom,"
  Medium Decay (600 ms), High Sustain (70%).
* Amp Envelope: Slightly faster Attack than filter (80 ms), Full Sustain (100%),
  Medium Release (500 ms).
* Modulation & Performance:
  - Map Aftertouch or Mod Wheel to open Filter Cutoff and increase a fast LFO
    (5–6 Hz) routed to Pitch for expressive vibrato.
  - Add a tiny pitch envelope (starting slightly flat and rising to pitch over
    40 ms) to mimic a brass player's embouchure.
* Effects: Long, dark hall reverb + subtle tape delay.

--------------------------------------------------------------------------------
4. THE "DEADMAU5 / PROGRESSIVE" ANALOG PLUCK
--------------------------------------------------------------------------------
Vibe: Hypnotic, evolving rhythmic energy ideal for arpeggios and chords.
* Oscillator 1 & 2: Sawtooth waves, slightly detuned with 4–7 voices of Unison
  (keep stereo width moderate, around 40–50%).
* Filter: 24dB Low-Pass filter. Cutoff closed down (80–120 Hz), low resonance
  (~10%).
* Filter & Amp Envelopes: Link both to a tight pluck shape: Zero Attack, Short
  Decay (250–400 ms), Zero Sustain, Short Release (150 ms). Set Filter Envelope
  amount to ~60%.
* The Secret Sauce: Assign a slow, free-running LFO (or manually automate) to
  slowly raise and lower the Filter Cutoff and Filter Decay over 8–16 bars.
* Effects: Dotted-eighth note ping-pong delay + bright reverb.

--------------------------------------------------------------------------------
5. THE "HOOVER / REESE" DETUNED RAVE BASS
--------------------------------------------------------------------------------
Vibe: Dark, swirling, aggressive mid-range movement (Drum & Bass, Techno, Dubstep).
* Oscillator 1 & 2: Sawtooth waves, heavily detuned against each other (±25–40
  cents) OR use 1 oscillator with 5–7 Unison voices and high detune.
* Sub-Oscillator: Clean Sine wave pitched 1 octave down (essential so the low
  end stays solid while the mids phase and swirl).
* Voicing: Monophonic with heavy Glide/Portamento (80–150 ms).
* Filter: 12dB or 24dB Low-Pass filter (or Band-Pass for vocal grit). Cutoff
  around 400–900 Hz, moderate resonance (20–30%).
* Amp Envelope: Fast Attack (10 ms), Full Sustain (100%), Short Release (100 ms).
* Effects: Tube distortion/overdrive to glue the detuned oscillators together,
  followed by subtle stereo chorus (keep frequencies below 120 Hz in mono).

--------------------------------------------------------------------------------
6. THE "BOARD OF CANADA" LO-FI WARPED KEYS
--------------------------------------------------------------------------------
Vibe: Nostalgic, dusty, slightly out-of-tune tape aesthetic (Ambient, Chillwave).
* Oscillator 1 & 2: Triangle waves (or a Triangle + soft Sawtooth mixed low).
* Pitch Drift (The Key Ingredient):
  - Route a slow random/sample-and-hold LFO (smoothed) to Pitch at a very low
    depth (±3 to 8 cents) to simulate warped cassette tape flutter.
  - Route a second, faster subtle LFO (~4 Hz, sine) to Pitch at tiny depth for
    worn motor wow.
* Noise Oscillator: Mix in white or pink noise at a very low level (~5–10%) to
  simulate tape hiss.
* Filter: 12dB Low-Pass filter. Cutoff around 1.2 kHz, low resonance (~10%), with
  50% Keyboard Tracking enabled.
* Amp Envelope: Soft Attack (30–60 ms), Medium Decay (800 ms), Medium Sustain
  (60%), Medium-Long Release (600 ms).
* Effects: Bitcrusher/sample-rate reduction (very subtle), tape saturation, and
  mono spring or plate reverb.

--------------------------------------------------------------------------------
7. THE "DX7 / CLASSIC FM" GLASSY ELECTRIC PIANO / BELL
--------------------------------------------------------------------------------
Vibe: Percussive, crystalline metallic attack with a warm sustained body.
(Works on FM synths like Dexed, Operator, or Vital/Serum using FM modulation).
* Carrier Oscillator: Pure Sine wave (Ratio 1.00).
* Modulator Oscillator: Pure Sine wave pitched up (Ratio 14.00 for classic glassy
  tine sparkle, or Ratio 3.50 for wooden marimba/bell tones).
* FM Amount Envelope: Zero Attack, Very Fast Decay (120–250 ms), Zero Sustain.
  Route this envelope to control how much the Modulator affects the Carrier—this
  creates a bright metallic "tine" hit only at the very start of the note.
* Amp Envelope (Carrier): Zero Attack, Long Decay (1.5–2.5 seconds), Low
  Sustain (20%), Medium Release (400 ms).
* Performance: Map Keyboard Velocity heavily to the FM Modulation Amount so
  soft playing sounds warm and dark, while hard playing rings brightly.
* Effects: Stereo Chorus + Lush Hall Reverb.

--------------------------------------------------------------------------------
8. THE "ACID 303" SQUELCHY BASSLINE
--------------------------------------------------------------------------------
Vibe: Hypnotic, liquid, resonant leads and basslines (Acid House, Techno).
* Oscillator: Single Sawtooth OR single Square wave (no unison, keep it raw).
* Voicing: Monophonic, Legato enabled, Glide/Portamento set to ~60 ms (triggers
  only when notes overlap).
* Filter: 18dB or 24dB Low-Pass filter.
  - Cutoff: Low-to-mid (200–500 Hz, meant to be tweaked live).
  - Resonance: Very High (70–85%—just below self-oscillation).
* Filter Envelope (Env 2): Zero Attack, Short-to-Medium Decay (250–450 ms),
  Zero Sustain. High envelope modulation amount (~70%).
* Amp Envelope: Zero Attack, Medium Decay (500 ms), Medium Sustain (60%), Very
  Short Release (20 ms).
* Accents & Effects: Program "Accent" steps in your sequencer (higher velocity)
  that simultaneously boost volume, filter cutoff, and resonance, then run the
  output into heavy overdrive/distortion.

================================================================================
```
