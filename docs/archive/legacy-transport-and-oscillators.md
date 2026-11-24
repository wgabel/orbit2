# Historical implementation record — transport and oscillators

> Superseded 2026-11-23 README implementation notes, regrouped without changing their technical claims. These are **historical**, not current feature status. Go to the [archive index](legacy-readme.md), [roadmap](../roadmap/overview.md) and [feature contracts](../README.md) before implementing.

## Shared timing and audio-rate modulation (Phase 2)

The original Phase 2 playback proof built a fixed modulation graph through ModulationDemoPatch and PatchSession. Its precompiled DSP and offline tests remain as regression fixtures; the standalone demo UI and service have now been retired in favor of the editable graph.

~~~mermaid
flowchart LR
    LFO["Sine LFO · 0.5 Hz"] --> P["Frequency · 440 ± 100 Hz"]
    P --> OSC["Sine carrier"]
    OSC --> G["Voice gain and envelope"]
    G --> O["Master output"]
~~~

- ModulatedSine.dsp computes the LFO, effective frequency and carrier at the real audio sample rate. It uses the same reusable sine function for both oscillators. Faust smooths base settings before applying sample-rate modulation and clamps the effective frequency to a safe range.
- The same voice lifecycle is used by dynamic patch playback: it sustains until stopped, schedules a release using Web Audio time and disposes the voice.
- The default values produce a frequency changing from approximately 340 Hz to 540 Hz every two seconds. Depth 0 produces the base frequency without vibrato.
- AudioClock observes the single existing AudioContext's actual sample rate and elapsed context time. Its snapshot returns the estimated frame position at the rendering quantum boundary, not an independent clock. It does not start or resume the context.
- MusicalTransport converts sample-frame differences into beats and tempo-synchronized frequency. Tempo changes preserve beat position, stopping freezes it, and resuming continues from that position. The patch-wide transport is now wired to the browser clock (Phase 6.1).

### Shared musical transport (Phase 6.1)

The workspace has one tempo control (20–300 BPM, default 120), current beat number, real sample rate when available, and reset. The transport starts when patch playback is observed to be running, freezes on Stop, resumes at the previous beat when playing again, and resets to zero without changing tempo. Editing tempo before playing requires no audio context. Editing tempo during playback does not commit a graph revision or restart audio. Enter a value and choose **Apply tempo**.

~~~mermaid
flowchart TD
    A["Shared AudioContext"] --> C["IAudioClock"]
    C --> T["MusicalTransportService"]
    T --> M["MusicalTransport"]
    M --> S["TransportSnapshot"]
    S --> U["TransportControls"]
    U --> T
~~~

`MusicalTransportService` is registered once in the composition root and serializes clock observations, tempo changes and reset. `GraphPlaybackControls` observes the root playback lifetime and supplies the transport panel with immutable snapshots; it does not integrate beats itself. All nested canvases use that same transport; opening a drone or replacing its DSP never resets musical time. A suspended or replaced audio context freezes/re-anchors the timeline instead of jumping to another beat.

`MusicalTime` maps `MusicalDivision` to quarter-note beat lengths and converts them to Hz: for example at 120 BPM a quarter-note cycle is 2 Hz and a 4/4 bar is 0.5 Hz. Dotted and triplet divisions are defined too.

### Tempo-synchronized oscillator (Phase 6.2 / 6.4)

Add an **Oscillator** to any canvas and choose **Synchronized (BPM)** under Timing mode. Select a waveform and musical division (for example, 1/4 note), connect Signal to a frequency, amplitude or other modulation socket, and adjust the connection amount in the receiving parameter's units. At 120 BPM, a quarter note gives two cycles per second; a bar gives one cycle every two seconds. Change BPM while playing or reset transport to hear the synchronized modulation respond. Phase offset (degrees) and amplitude remain independently modulatable. Division is a validated discrete live choice, not a modulation socket.

~~~mermaid
flowchart TD
    T["MusicalTransportService"] --> A["Transport anchor"]
    A --> W["Transport AudioWorklet"]
    W --> F["Faust graph: shared beat input"]
    F --> R["Root tempo LFO"]
    F --> D["Nested tempo LFO"]
    R --> P["Modulated parameters"]
    D --> P
~~~

The application publishes transport anchors only when transport state changes. The browser owns one lazy `TransportSignalSource` per audio context; its worklet produces one beat sample per audio frame. When a reachable emitter requests the transport beat from `FaustNodeContext`, `FaustGraphCompiler` adds one beat input to the compiled graph. Every synchronized oscillator calculates phase in cycles as `beat / beatsPerCycle + phaseDegrees / 360`, then evaluates its chosen waveform. The 12-beat wrap keeps Float32 precision while preserving phase for all current divisions. Old and new graphs during a crossfade share the same source. Free-running oscillators use Hertz and need no transport input.

Steady-state phase is calculated at the actual audio sample rate (including 44.1 and 48 kHz). Start/stop/reset/tempo commands anchor to the observed audio frame and arrive at an audio-worklet message boundary; scheduled sample-exact command execution and rhythmic clock/trigger nodes remain future work.

### Reusable oscillator (Phase 6.3)

Select **Oscillator** from Add node (the default waveform is sine), then choose Sine, Triangle, Square or Sawtooth in the node itself. The starter patch and drone still default to sine. Every shape has the same output port and independently modulatable frequency (0.01–20,000 Hz), amplitude (0–1) and phase offset (0–360°); changing waveform leaves all node, parameter, port and connection identities untouched. The choice editor is the same reusable component used for musical division.

~~~mermaid
flowchart TD
    N["OscillatorGenerator"] --> P["Parameter definitions"]
    P --> U["Shared parameter editors"]
    U --> C["SetParameterValueCommand"]
    C --> G["Committed graph"]
    G --> F["OscillatorFaustEmitter"]
    F --> W["Selected waveform DSP"]
    W --> S["Signal output"]
~~~

`OscillatorWaveform` defines stable discrete choices; `ParameterDefinition.RequiresRecompile` distinguishes DSP-shaping choices from live controls. `GraphTopology` includes structural values, so selecting a different waveform recompiles the program and crossfades the replacement; ordinary frequency/amplitude/phase changes update the existing worklet. The compiler emits only the selected shape—not four parallel oscillators. A free-running oscillator integrates frequency into one phase signal at the actual sample rate, wraps phase plus the offset, evaluates the chosen waveform and clamps its normalized result before amplitude scaling. Saw and square use polynomial step corrections (PolyBLEP), and triangle uses a slope correction (PolyBLAMP) to reduce aliasing. This avoids a fixed audio-rate frequency floor for slow modulation. Equal peak amplitude is not equal perceived loudness; loudness matching is a separate feature.

### Unified timing modes (Phase 6.4)

The separate tempo sine node has been removed. All four oscillator waveforms work in both timing modes.

| Timing mode | Active rate parameter | Frequency modulation input |
|---|---|---|
| Free (Hz) | Frequency | Active |
| Synchronized (BPM) | Musical division | Dormant (cables preserved) |

`ParameterDefinition.ActiveWhen` references an unconditional local structural choice. The snapshot exposes `IsParameterActive` and `IsInputActive`, which drive the shared editor and backend. The inactive editor is hidden, but its stored value and socket survive. Dormant frequency connections remain visible as muted dashed wires, may be edited, and automatically reactivate when returning to free mode. `GraphFlattener` excludes dormant receiving routes from the backend graph before dependency ordering and range analysis. This prevents unused upstream DSP/control allocation; the original committed graph stays untouched. The graph still validates all connections, including dormant ones, so reactivation cannot create a cycle.

~~~mermaid
flowchart TD
    M["Timing mode"] --> H["Free: instantaneous Hz"]
    M --> B["Sync: transport beat / division"]
    H --> P["FaustOscillatorTiming"]
    B --> P
    P --> W["Shared phase-to-waveform DSP"]
    W --> S["Signal output"]
~~~

Changing timing mode is a structural edit and crossfades to the new program. Changing musical division is a discrete live control; it intentionally reinterprets the current beat relative to the selected cycle length (it can change phase immediately). Tempo edits preserve beat position without recompiling. Free and synchronized settings are remembered independently. Noise remains a separate generator family.

In synchronized mode the oscillator shows a read-only **Synchronized frequency** in Hz below Musical division. It follows the committed patch tempo and division even when playback is stopped and inside nested graphs. It does not change the stored free frequency or create a new modulation input.

### Noise generator (Phase 6.5)

Choose **Noise** in Add node, then select White (broadband), Pink (approximately equal power per octave), or Brown (bass-weighted). Amplitude defaults to 0.2 and supports audio-rate modulation from 0–1. Connect Signal to audio or a modulation input in any canvas. Unlike a periodic oscillator, noise has no frequency, phase or transport controls.

~~~mermaid
flowchart TD
    R[Node-specific random stream] --> C[Selected spectral shaping]
    C --> B[Bound to -1 through +1]
    B --> A[Modulatable amplitude]
    A --> S[Signal output]
~~~

`NoiseGenerator` uses the shared choice and slider editors. `NoiseFaustEmitter` selects the spectrum at compile time; changing color retains the connections and crossfades the replacement. Amplitude changes update the existing DSP. `FaustNoiseSpectra` uses a deterministic node-specific integer recurrence, octave-spaced low-pass responses for approximate pink noise and a leaky integrator for brown noise (not an unbounded random walk). Filter coefficients use the actual sample rate. The output is bounded before amplitude scaling, so existing modulation range prediction applies. Noise colors are not loudness matched. A structural replacement restarts the pseudorandom sequence.



There is no per-sample C# or JavaScript UI event loop. The audio engine controls waveform generation; browser timers observe playback for resource cleanup, and the UI reads passive meter snapshots at display rate. The earlier Phase 2 fixed-program prototype staged graph edits until restart; the current dynamic graph editor updates base values live and crossfades replacement DSP programs on structural edits.
