# Parameters and modulation

[Architecture overview](../../ORBIT2-ARCHITECTURE.md) · [Roadmap](../../ROADMAP.md)

Related: [signals and ports](signals-and-ports.md) · [oscillators and noise](../audio/oscillators-and-noise.md) · [dsp engine](../audio/dsp-engine.md) · [design system](../editor/design-system.md).

## 4. Parameters and modulation

### Parameter definitions and instances

Every continuously adjustable numerical parameter should be an independent object rather than just a float property on an oscillator. The parameter has identity, units, range, a base value and semantics for applying incoming signals.

~~~csharp
public enum ParameterUnit
{
    Normalized, Hertz, Cents, Decibels, Seconds, Degrees,
    BeatsPerMinute, Ratio
}

public enum ParameterScale
{
    Linear, Logarithmic, PitchCents, Decibels
}

public record ParameterDefinition(
    string Key,
    string Name,
    double DefaultValue,
    double Minimum,
    double Maximum,
    ParameterUnit Unit,
    ParameterScale Scale,
    bool CanModulate
);

public sealed class Parameter
{
    public required Guid Id { get; init; }
    public required ParameterDefinition Definition { get; init; }

    // Changed through validated application commands.
    public double BaseValue { get; internal set; }

    // Stable input socket for incoming modulation, when supported.
    public Guid? ModulationPortId { get; init; }
}

public record ParameterAddress(
    Guid NodeId,
    Guid ParameterId
);
~~~

Parameter definitions can also describe control rate, display precision, smoothing time, bipolar/unipolar mapping, modulation limits and other metadata. Some discrete parameters, such as waveform selection, are configuration rather than continuously modulatable signals and should have a distinct contract where needed.

Examples:

| Component | Parameters |
|---|---|
| Oscillator | Frequency, amplitude, phase |
| Pulse generator | Frequency, pulse width, amplitude |
| Filter | Cutoff, resonance |
| Reverb | Mix, decay, size |
| Gain | Volume |
| LFO (same oscillator concept) | Frequency, amplitude, phase |
| Drone composite | Any explicitly exposed parameters, e.g. pitch, volume, cutoff |

BaseValue represents the user/preset setting. The runtime effective value can change at every sample independently of it. Never overwrite the stored base value with successive LFO samples.

### Modulation math

For additive linear modulation:

~~~
effective[n] = clamp(base + sum(source[i][n] * depth[i]), min, max)
~~~

For pitch modulation expressed in cents:

~~~
effectiveHz[n] = baseHz * 2^(sum(source[i][n] * depthCents[i]) / 1200)
~~~

For example:

- Oscillator B has a frequency of 440 Hz.
- Oscillator A produces a 0.5 Hz sine signal ranging from -1 to +1.
- The connection has a modulation depth of 100 Hz.
- B's frequency varies between 340 and 540 Hz, maintaining continuous phase.

~~~mermaid
flowchart LR
    A["Sine A: 0.5 Hz"] --> P["Frequency: base 440 Hz"]
    P --> B["Sine B: 340–540 Hz"]
    B --> O[Audio output]
~~~

For a musical vibrato, a modulation amount of +/-20 cents is more appropriate than +/-100 Hz. The exact mapping depends on ParameterScale, and parameter limits may apply after scaling.

For volume, decide whether the modulation is a linear amplitude offset, a multiplicative gain factor, or a dB offset. For cutoff, an exponential or logarithmic mapping often provides more musical behavior than a linear Hz offset. These decisions belong to the target's parameter definition.

### Multiple incoming modulation signals

| Source output | Target parameter | Example depth |
|---|---|---|
| LFO 1 | Oscillator 1 frequency | +/-20 cents |
| LFO 1 | Oscillator 2 frequency | +/-10 cents |
| LFO 2 | Drone volume | +/-0.2 linear gain |
| LFO 3 | Filter cutoff | +/-500 Hz |
| LFO 3 | LFO 1 frequency | +/-0.1 Hz |

A single source can feed several parameters; several sources can feed a single compatible parameter. The graph's modulation connections collectively form the modulation matrix; avoid maintaining a second independently editable list of the same routes.

A possible convenience view:

~~~csharp
public record ModulationRoute(
    PortAddress Source,
    ParameterAddress Target,
    double Amount
);
~~~

The route is derived from the canonical SignalConnection and the target parameter's modulation input.

### Audio-rate and control-rate

Slow modulation and audio-frequency modulation use the same conceptual signal machinery. Fine-grained continuous modulation must run on the audio engine, not through periodic C# events or JavaScript setInterval calls.

The DSP can optimize eligible control-rate or constant signals. Manual changes to base values should normally be smoothed to avoid abrupt jumps. Smoothing must not accidentally suppress intended audio-rate modulation; apply it deliberately to base changes and other appropriate control paths.

### Base-control smoothing

**Parameter-specific smoothing (in review, [PR #75](https://github.com/wgabel/orbit2/pull/75)):** The previous default `si.smoo` gives a roughly 68 ms 95% settling time, which can make large manual frequency jumps sound like brief pitch slides. `FaustControlSmoothing` provides sample-rate-independent policies for frequently edited controls; emitters explicitly select the semantic policy through `FaustNodeContext.Parameter`.

| Base control | Approx. 95% settling time |
|---|---:|
| Free oscillator frequency and manual semitone offset | 5 ms |
| Oscillator/noise amplitude, Gain gain and EQ gain | 10 ms |
| Oscillator phase offset | 10 ms |
| Filter cutoff and EQ centre frequency | 15 ms |
| Other continuous controls | Existing `si.smoo` |

The one-pole coefficient is `exp(-3 / (sampleRate * durationSeconds))`; approximately 5% of the step remains after the named duration. This is not a linear ramp, an ADSR attack or total UI-to-sound latency. The control keeps its current smoothed state when a new target arrives (even before the previous transition settles).

Only the base slider is smoothed. Modulation is added at audio rate afterward, with existing range and Nyquist constraints. The free oscillator integrates frequency continuously; changing a base value does not recompile, reset phase, or schedule a voice gain envelope. Discrete choices, explicit unsmoothed controls, structural crossfades, and intentional note-sequencer glide keep their existing behavior. No new patch field or UI slider is introduced.

Validation: the compiler checks the selected policies and stable source across edits. The browser Faust smoke test measures the pitch of a single continuously running triangle after several large up/down retunes and checks adjacent-sample changes, including block boundaries during rapid overlapping edits, at 44.1 and 48 kHz. This detects discontinuities and excessively slow settling; it is not a guarantee of perceived clicklessness for every waveform/frequency combination. Audition large jumps on the actual device before refining these defaults.
