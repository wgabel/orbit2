using Orbit.Domain.Ports;

namespace Orbit.Presentation.Canvas;

/// <summary>Reviewed, presentation-only help for built-in nodes. No DSP or graph rules live here.</summary>
public sealed record NodeHelpEntry(string Summary, IReadOnlyList<string> UseCases);

public static class NodeHelpCatalog
{
    private static readonly IReadOnlyDictionary<string, NodeHelpEntry> Entries =
        new Dictionary<string, NodeHelpEntry>(StringComparer.Ordinal)
        {
            ["generator.oscillator"] = new(
                "Produces a periodic tone (or a slow control wave when used as an LFO). Its output sounds continuously unless you control its amplitude.",
                ["Connect Signal to Gain, then Gain to Master for a basic voice.",
                 "Connect Note Sequencer Pitch to Pitch at amount 1 with semitone pitch tracking enabled; shape separate note attacks using an envelope on Amplitude."]),
            ["generator.noise"] = new(
                "Generates noise useful for texture, percussion and breath-like layers.",
                ["Send Noise through a Filter to soften or emphasize a frequency range.",
                 "Use an Envelope to vary its level and make short noisy attacks."]),
            ["generator.pitch-control"] = new(
                "Produces a semitone offset for transposition and fine tuning. It does not generate audio.",
                ["Connect to an oscillator Pitch input with semitone tracking enabled.",
                 "Send the same pitch offset to several voices to transpose them together."]),
            ["generator.note-source"] = new(
                "Receives notes from the performance keyboard; Pitch specifies the note while Gate and Trigger provide articulation.",
                ["Connect Pitch and Trigger to a plucked instrument to start individual strikes.",
                 "Connect Pitch and Gate to a sustained instrument; optionally route Velocity to its expression or strength."]),
            ["generator.note-sequencer"] = new(
                "Creates a timed sequence of semitone offsets. Pitch-only mode retunes an existing sound; Notes mode also emits note articulation.",
                ["For a continuous drone, connect Pitch to an oscillator Pitch input with semitone tracking enabled and amount 1.",
                 "In Notes mode, connect Pitch + Trigger to a plucked instrument, or Pitch + Gate to a sustained instrument."]),
            ["generator.envelope"] = new(
                "Shapes a control value over time. A trigger starts a one-shot shape; a gate can sustain and release it.",
                ["Use an AD envelope from a Trigger/Clock source to modulate a zero-base oscillator Amplitude.",
                 "Use ADSR with a held Gate to shape the loudness of sustained notes."]),
            ["generator.signal-to-trigger"] = new(
                "Detects threshold crossings in a continuous control signal and emits trigger pulses.",
                ["Convert a slowly changing oscillator signal to pulses that start an AD envelope.",
                 "Use threshold-crossing triggers to fire percussive instruments."]),
            ["generator.signal-to-gate"] = new(
                "Turns a continuous signal into a held Gate while it meets the configured threshold condition.",
                ["Convert an LFO into a gate for an envelope or sustained instrument.",
                 "Change the input shape to create different held-note durations."]),
            ["generator.manual-trigger"] = new(
                "Emits a trigger pulse when manually fired.",
                ["Connect Trigger to a Physical Pluck or Physical Marimba.",
                 "Connect Trigger to an envelope set to Trigger / clock for a single attack."]),
            ["generator.gate"] = new(
                "Creates a held control gate manually or in response to trigger/clock events.",
                ["Connect Gate to an ADSR envelope to make a sustained tone.",
                 "Use a Clock as the trigger source and set a duration for rhythmic gates."]),
            ["generator.clock"] = new(
                "Produces timing pulses at a free or tempo-synchronized rate.",
                ["Connect Clock to an envelope's Trigger input when Trigger / clock is selected.",
                 "Drive a Gate node in its Trigger / clock mode for repeated held pulses."]),
            ["generator.physical-pluck"] = new(
                "Models a plucked string: pitch sets the tuning and a Trigger starts each pluck.",
                ["Wire Note Source Pitch → Pitch and Trigger → Trigger.",
                 "Connect Velocity to Strike strength (with an appropriate base value) to vary attacks."]),
            ["generator.physical-marimba"] = new(
                "Models a struck wooden bar. Each Trigger starts a note that rings and decays.",
                ["Connect Note Source Pitch and Trigger to the corresponding inputs.",
                 "Modulate Strike strength and mallet hardness for varied note attacks."]),
            ["generator.physical-clarinet"] = new(
                "A sustained reed-driven physical instrument. Gate holds the note; Pitch changes its tuning.",
                ["Connect Note Source Pitch → Pitch and Gate → Gate.",
                 "Map Velocity to Expression, or modulate breath and vibrato for phrasing."]),
            ["generator.physical-flute"] = new(
                "A sustained air-jet flute model. It needs Gate to sustain notes.",
                ["Connect Note Source Pitch → Pitch and Gate → Gate.",
                 "Use Expression and vibrato controls to shape phrasing."]),
            ["generator.physical-violin"] = new(
                "Experimental bowed-string model; tuning may be unstable in some ranges.",
                ["Use Note Source Pitch and Gate to play held notes.",
                 "Adjust bow pressure and vibrato, listening for unexpected pitch jumps."]),
            ["generator.physical-brass"] = new(
                "Lip-reed brass model driven by held notes and expression.",
                ["Connect Note Source Pitch and Gate to the brass inputs.",
                 "Use Expression and Lip tension to change articulation and timbre."]),
            ["generator.physical-electric-guitar"] = new(
                "Triggered electric-string model; one Trigger produces a pluck, not an entire amplifier chain.",
                ["Connect Note Source Pitch → Pitch and Trigger → Trigger.",
                 "Try Pluck position and then route Audio through gain/effects for a fuller tone."]),
            ["generator.drawbar-organ"] = new(
                "A drawbar-based organ that sustains while Gate is held.",
                ["Connect Note Source Pitch and Gate to play notes.",
                 "Place a Rotary Speaker after Audio for slow or fast rotating-speaker effects."]),
            ["processor.gain"] = new(
                "Scales an incoming audio signal; the Gain modulation input changes its level over time.",
                ["Place Gain between a sound source and Master to control loudness.",
                 "Set base Gain to zero and control Gain with an envelope to articulate notes."]),
            ["processor.mixer"] = new(
                "Combines multiple audio signals into a single output.",
                ["Mix two oscillators tuned to different notes or harmonics.",
                 "Combine a dry signal and a processed signal and balance their levels."]),
            ["processor.filter"] = new(
                "Reduces or emphasizes parts of the frequency spectrum using a chosen filter mode.",
                ["Use a low-pass filter after a bright oscillator to soften it.",
                 "Modulate Cutoff slowly to add motion to a sustained sound."]),
            ["processor.parametric-eq"] = new(
                "Adjusts selected frequency bands to balance or shape a sound.",
                ["Reduce a resonant or harsh band in an instrument.",
                 "Emphasize a narrow frequency region for a deliberate timbral effect."]),
            ["processor.delay"] = new(
                "Repeats an audio signal after a configurable time.",
                ["Place Delay after a melodic voice to create echoes between notes.",
                 "Synchronize its timing to BPM and keep feedback moderate initially."]),
            ["processor.reverb"] = new(
                "Adds reverberation and a sense of acoustic space.",
                ["Place Reverb after a voice to add a trailing ambience.",
                 "Put Reverb after an envelope-controlled gain so the note tail can continue."]),
            ["processor.resonator"] = new(
                "Adds a ringing resonance at a tuned frequency.",
                ["Feed a short noisy impulse into Resonator for bell-like tones.",
                 "Combine several resonators tuned differently to create a richer texture."]),
            ["processor.chorus"] = new(
                "Layers small modulated delays to thicken or animate a sound.",
                ["Apply Chorus to an organ or pad to add gentle motion.",
                 "Use a subtle wet mix to avoid overpowering the original tone."]),
            ["processor.rotary-speaker"] = new(
                "Creates rotating-speaker amplitude and Doppler-like movement.",
                ["Route Drawbar Organ Audio through Rotary Speaker before Master.",
                 "Compare Speed 1 (slow), Speed 2 (fast), and a gradual transition."]),
            ["output.master"] = new(
                "Final audio destination for the patch.",
                ["Connect a conservative Gain output to Master.",
                 "Route a finished effects chain to Master to hear its result."]),
            ["rack.input"] = new(
                "Exposes signals arriving from the parent canvas to nodes inside this composite.",
                ["Route an exposed Pitch input into an internal oscillator Pitch input.",
                 "Route an exposed audio input into an internal processor."]),
            ["rack.output"] = new(
                "Exposes signals from inside the composite to its parent canvas.",
                ["Route an internal audio processor output to an exposed Audio port.",
                 "Expose a modulation output when the parent patch needs it."]),
            ["composite.graph"] = new(
                "A reusable group of nodes with explicit input and output ports; its internal graph remains editable.",
                ["Connect its exposed output to the next processing node or Master.",
                 "Open the composite to inspect or change its internal routing."])
        };

    public static NodeHelpEntry Get(string typeKey)
    {
        if (Entries.TryGetValue(typeKey, out var entry)) return entry;
        if (typeKey.StartsWith("preset.", StringComparison.Ordinal))
            return new(NodeAppearanceCatalog.Get(typeKey).Description,
                ["Connect the available exposed Audio output into a mixer or Master.",
                 "Open the composite to inspect its instruments, effects and signal routing."]);
        return new(NodeAppearanceCatalog.Get(typeKey).Description,
            ["Connect the node to compatible typed ports to use it in your patch.",
             "Check its parameter controls and active connections for the resulting behavior."]);
    }

    public static bool HasCuratedHelp(string typeKey) =>
        Entries.ContainsKey(typeKey) || typeKey.StartsWith("preset.", StringComparison.Ordinal);

    /// <summary>Semantic port explanation based on stable type/port keys, never on instance GUIDs.</summary>
    public static string PortHint(string typeKey, PortDefinition port)
    {
        if (port.Direction == PortDirection.Output)
            return port.Key switch
            {
                "pitch-out" => "Outputs a continuous semitone offset. It changes pitch but does not by itself start or stop notes.",
                "gate-out" => "Outputs a held gate while a note or gate source is active. Connect to a Gate input.",
                "trigger-out" => "Outputs a short trigger pulse on each onset. Connect to an instrument or envelope Trigger input.",
                "clock-out" => "Outputs periodic timing pulses. Connect to a Clock or compatible Trigger input.",
                "velocity-out" => "Outputs note intensity (0–1). Connect to a suitable Expression, Gain or Strike strength modulation input.",
                "envelope-out" => "Outputs the envelope's varying control level. Use it to modulate Amplitude, Gain, or another parameter.",
                "audio-out" or "signal-out" => "Outputs audio. Connect to an Audio input on a processor, mixer or Master.",
                "event-out" => port.SignalType == SignalType.Gate
                    ? "Outputs a held Gate derived from the incoming signal."
                    : "Outputs trigger pulses derived from the incoming signal.",
                _ => port.SignalType switch
                {
                    SignalType.Audio => "Outputs audio for a processor, mixer or Master Audio input.",
                    SignalType.Modulation => "Outputs a control signal used to modulate parameters on other nodes.",
                    SignalType.Gate => "Outputs a held note/gate state for a Gate input.",
                    SignalType.Trigger => "Outputs an onset pulse for a Trigger input.",
                    SignalType.Clock => "Outputs clock pulses for a Clock or compatible Trigger input.",
                    _ => "Outputs a typed signal to compatible inputs."
                }
            };

        if (typeKey == "generator.oscillator" && port.Key == "frequency-modulation-in")
            return "Adds a linear frequency offset in Hz, not musical semitones. Use Pitch for correct note intervals.";
        if (typeKey == "generator.oscillator" && port.Key == "pitch-modulation-in")
            return "Adds a semitone offset when Pitch tracking is set to Semitones. Note Pitch normally uses cable amount 1.";
        if (port.Key is "pitch-modulation-in" or "pitch-in")
            return "Adjusts the note's pitch in semitones. Use a Note Source or Note Sequencer Pitch output.";
        if (port.Key == "excitation-in")
            return "Feeds external audio excitation into the physical string model.";
        if (port.Key == "signal-trigger-in")
            return "Uses changes in a continuous signal to start the envelope when Continuous signal is selected.";
        if (port.Key == "audio-in")
            return "Receives audio to process, mix or play.";
        if (port.SignalType == SignalType.Trigger)
            return "Receives a short onset pulse to trigger a new event or note; not an audio signal.";
        if (port.SignalType == SignalType.Gate)
            return "Receives a held gate to start, sustain or release a note or envelope.";
        if (port.SignalType == SignalType.Clock)
            return "Receives timing pulses from a Clock output.";
        if (port.SignalType == SignalType.Modulation)
            return port.Key.EndsWith("-modulation-in", StringComparison.Ordinal)
                ? $"Modulates {port.Name} around its base value. Cable amount sets the contribution; the result is limited by the parameter's bounds."
                : "Receives a continuous control signal for modulation or threshold detection.";
        return port.SignalType == SignalType.Audio
            ? "Receives audio from a generator, mixer or effect."
            : "Receives a compatible typed signal.";
    }
}
