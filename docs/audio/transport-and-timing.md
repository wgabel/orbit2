# Transport and audio timing

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [oscillators and noise](oscillators-and-noise.md) · [dsp engine](dsp-engine.md) · [note sequencer](../instruments/note-sequencer.md).

## 6. Audio time, oscillator phase and transport

There are three independent concepts of time that must not be confused.

| System | Meaning |
|---|---|
| AudioClock | Absolute elapsed audio frames and sample rate |
| PhaseAccumulator | Progress through an individual generator cycle |
| Transport | Musical beat position, tempo, start/stop and synchronization |

### AudioClock

At 48 kHz, each channel has 48,000 samples per second. All nodes must use the same audio context's frame timing.

~~~csharp
public interface IAudioClock
{
    long CurrentSample { get; }
    int SampleRate { get; }
    double CurrentTime { get; }
}
~~~

In the real-time runtime, Web Audio's AudioWorkletGlobalScope supplies currentFrame, currentTime and sampleRate. The C# interface can expose a snapshot to the UI; the DSP itself uses its actual rendering context. It is not a separate C# timer.

~~~
timeSeconds = sampleFrame / sampleRate
samplePeriod = 1 / sampleRate
~~~

| Frames at 48 kHz | Duration |
|---:|---:|
| 1 | 20.833 microseconds |
| 128 | 2.667 ms |
| 480 | 10 ms |
| 24,000 | 0.5 s |
| 48,000 | 1 s |

Request 48 kHz if desired, but use the AudioContext's actual supported sample rate rather than assuming it. The engine must also work at 44.1 kHz and other supported sample rates. Use the actual provided buffer size rather than hardcoding 128 frames.

### Phase accumulator

An oscillator stores a fractional phase between 0 and 1. For each sample:

~~~
output[n] = sin(2 * pi * phase[n])
phase[n+1] = wrap(phase[n] + frequency[n] / sampleRate)
~~~

Example: 440 Hz at 48 kHz advances by 440 / 48000 = approximately 0.0091667 cycles per sample; one cycle lasts approximately 109.09 samples.

Frequency can change on every sample. Advance from the previous phase rather than recalculating phase as currentTime * currentFrequency, which can create discontinuities when the frequency changes.

At phase 0 the sine output is 0; at phase 0.25 it is +1; at 0.5 it is 0; at 0.75 it is -1.

A phase reset should be an explicit trigger with a defined behavior, not an accidental consequence of editing a parameter or reopening a canvas.

### Musical transport

~~~csharp
public interface ITransport
{
    double BeatsPerMinute { get; }
    double BeatPosition { get; }
    bool IsPlaying { get; }
}
~~~

While the transport is running:

~~~
beatsPerSecond = BPM / 60
beatIncrementPerSample = BPM / (60 * sampleRate)
~~~

At constant 120 BPM and 48 kHz:

| Note duration | Seconds | Frames |
|---|---:|---:|
| Eighth note | 0.25 | 12,000 |
| Quarter note (one beat) | 0.5 | 24,000 |
| Half note | 1 | 48,000 |
| Whole note / 4/4 bar | 2 | 96,000 |

Free mode uses an absolute frequency in Hz. Synchronized mode uses a musical division:

~~~
frequencyHz = BPM / (60 * beatsPerCycle)
~~~

At 120 BPM, one cycle per beat is 2 Hz; one cycle per 4/4 bar is 0.5 Hz.

The oscillator's synchronized frequency is also projected in the editor: the workspace supplies its committed BPM through the shared canvas to `NodeFrame`; `ParameterReadout` derives the value from the active division in the immutable node snapshot using `MusicalTime`. The shared `ParameterChoiceEditor` renders the optional readout below the choice. It is a calculated rate (including while stopped), not measured telemetry, a graph parameter or a stored value. The free frequency and its dormant connections remain unchanged. The readout updates with tempo, division, preset application and workspace restoration without a separate subscription or DSP update.

~~~mermaid
flowchart TD
    T[Committed workspace BPM] --> R[ParameterReadout]
    N[Active division in node snapshot] --> R
    R --> M[Shared MusicalTime conversion]
    M --> U[Choice editor: read-only Hz]
~~~

Tempo changes affect musical time, not audio sample rate. Integrate beat position over time so that tempo changes do not imply an arbitrary phase jump. Define whether a synchronized modulator is locked to the shared transport position or free-running at a synced rate. Two LFOs with the same frequency are not necessarily in phase unless they share an initial phase/reference or reset event.

The audio clock can continue while musical transport is stopped, allowing effects tails and free-running signals. The transport's start/stop/reset semantics need to be deliberate.

**Implemented Phase 6.1:** `MusicalTransportService` owns one domain `MusicalTransport` and reads the existing `IAudioClock`. It serializes asynchronous observations and edits, publishes immutable `TransportSnapshot` results, and never creates an audio context. The composition root registers one service for the patch. Tempo defaults to 120 BPM and is constrained to 20–300 BPM. The shared `TransportControls` panel displays tempo, beat position, sample rate and derived note/bar rates. Start follows observed root playback, Stop freezes position, replay resumes, and reset anchors beat zero while preserving tempo and running state. Tempo changes preserve accumulated beats. Suspension or clock identity changes freeze and re-anchor the timeline. Canvas navigation and worklet crossfades do not reset transport; tempo edits do not mutate the signal graph.

`MusicalDivision` and `MusicalTime` define reusable quarter-note-relative cycle lengths and the conversion to Hertz. The application timeline is calculated from actual sampled audio frames, not from UI timer ticks.
