# Historical implementation record — effects

> Superseded 2026-11-23 README implementation notes, regrouped without changing their technical claims. These are **historical**, not current feature status. Go to the [archive index](legacy-readme.md), [roadmap](../../ROADMAP.md) and [feature contracts](../README.md) before implementing.

### Chorus (Phase 6.15)

Add **Chorus** to thicken an audio signal using two short modulated delay voices. Rate (0.05–5 Hz, default 0.6), depth (0–10 ms, default 3), base delay (5–40 ms, default 15) and wet mix (0–1, default 0.5) all support live modulation. Depth is the peak delay excursion around the base delay; it is bounded to keep both taps at least 1 ms from the present. With depth zero the effect is a fixed short delay; mix zero passes the original signal unchanged. With modulation the moving taps introduce pitch variations; the mono wet signal averages both voices. The internal phase continues through base edits. No feedback is used, so the effect does not accumulate echoes. Stereo chorus can follow when the graph supports multichannel DSP.

~~~mermaid
flowchart TD
    A[Audio input] --> T[Two fractional delay taps]
    L[Phase-offset modulation] --> T
    T --> M[Dry/wet mix]
    A --> M
    M --> O[Audio output]
~~~

### Reverb (Phase 6.14)

Add **Reverb** to create a dense, decaying mono acoustic tail. Decay (RT60, 0.1–12 seconds, default 2.5) controls the approximate time for low-frequency reflections to decay by 60 dB. Damping (0–1, default 0.4) progressively absorbs high frequencies; wet mix (0–1, default 0.35) blends the original and reverberated audio. All three parameters support live modulation. The algorithm combines eight damped feedback delays and four all-pass diffusers with sample-rate-scaled lengths. The room geometry is fixed in this first implementation; longer decay increases sustain, not the physical size of the simulated space. Mix 0 bypasses the audible tail without discarding its state. Keep headroom for resonant accumulation; structural graph replacement may reset the tail.

~~~mermaid
flowchart TD
    A[Audio input] --> C[Eight damped feedback delays]
    C --> D[Four all-pass diffusers]
    D --> M[Dry/wet mix]
    A --> M
    M --> O[Audio output]
~~~

### Delay (Phase 6.13)

Add **Delay** between any audio source and output. Free timing uses a modulatable delay time of 0.01–2 seconds (default 0.30). Synchronized timing uses a musical division from the shared transport BPM, up to one bar (12 seconds at 20 BPM); the free-time wire remains dormant until free mode returns. Feedback (0–0.9, default 0.35) and wet mix (0–1, default 0.35) are modulatable in either mode. Wet mix 0 preserves the dry input; 1 outputs only the delayed sound. Feedback repeats decay without an editable graph cycle. Delay time changes intentionally create pitch movement, as with a tape delay; base edits and tempo changes update the running worklet, while timing mode changes crossfade a graph replacement and may reset its echo tail.

~~~mermaid
flowchart TD
    A[Audio input] --> S[Sum]
    S --> D[Fractional delay]
    D --> F[Bounded feedback]
    F --> S
    D --> M[Dry/wet mix]
    A --> M
    M --> O[Audio output]
~~~

### Parametric EQ (Phase 6.12)

Add **Parametric EQ** to an audio path to boost or cut one frequency band. Frequency (20–20,000 Hz, default 1,000 Hz), gain (−18 to +18 dB, default 0 dB) and Q (0.2–10, default 1) each have an independent modulation socket. Gain of 0 dB is transparent; higher Q narrows the bell. The shared state-variable DSP core applies effective values at audio rate; base edits do not recompile. Chain multiple instances for multiband EQ. Allow headroom for boost and modulation; output is not clipped or normalized.

~~~mermaid
flowchart TD
    I[Audio input] --> E[Parametric EQ]
    L[Modulation signals] -->|Frequency, gain, Q| E
    E --> O[Audio output]
~~~

### Resonant filter processor (Phase 6.7)

Add **Filter** between a generator or mixer and the master output. Choose Low-pass, High-pass, Band-pass or Notch. Cutoff (20–20,000 Hz, default 1,000 Hz) and resonance Q (0.5–8, default 0.707) have independent modulation sockets. At Q 0.707, low-pass/high-pass are approximately Butterworth responses; higher Q emphasizes the cutoff region. Band-pass and notch use cutoff as their center frequency.

~~~mermaid
flowchart TD
    I[Audio input] --> F[State-variable filter]
    C[Effective cutoff] --> F
    Q[Effective resonance Q] --> F
    F --> M[Selected response]
    M --> O[Audio output]
~~~

`FilterFaustEmitter` uses a topology-preserving state-variable filter with two trapezoidal integrator states. The coefficients follow the effective parameters at sample rate; cutoff is additionally limited to 45% of the actual sample rate to avoid the Nyquist singularity. Mode selection is structural and crossfades; live cutoff and Q edits preserve the existing state. The raw filter response is not clipped or normalized, since resonance can amplify and modulation can add energy. Use a gain stage for headroom. The range estimator intentionally returns unknown for filter output rather than inventing a fixed bound.
