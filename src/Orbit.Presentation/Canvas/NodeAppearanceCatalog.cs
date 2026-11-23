namespace Orbit.Presentation.Canvas;

public enum NodeCategory { Audio, Modulation, Effects, Sequencing, Processing, Composites, Routing }

public sealed record NodeAppearance(NodeCategory Category, string IconPath, string Description, string Keywords = "")
{
    public string CssClass => $"node-category-{Category.ToString().ToLowerInvariant()}";
}

/// <summary>Presentation metadata only. Creation remains the registry's responsibility.</summary>
public static class NodeAppearanceCatalog
{
    private static readonly Dictionary<string, NodeAppearance> Types = new(StringComparer.Ordinal)
    {
        ["generator.oscillator"] = new(NodeCategory.Audio, "M2 12 C5 12 5 4 8 4 S11 20 14 20 S17 12 22 12", "Periodic waveform generator", "tone sine wave LFO"),
        ["generator.noise"] = new(NodeCategory.Audio, "M2 12 5 5 8 19 11 8 14 16 17 3 20 15 22 12", "Random signal generator", "white pink noise"),
        ["generator.physical-pluck"] = new(NodeCategory.Audio, "M3 19L21 3 M4 13L11 20 M13 4L20 11", "Triggered resonating string from a physical waveguide", "guitar harp string instrument"),
        ["generator.physical-marimba"] = new(NodeCategory.Audio, "M3 7V20H6V7Z M9 5V18H12V5Z M15 3V15H18V3Z", "Triggered modal wooden bar with mallet and strike position controls", "marimba xylophone percussion instrument"),
        ["generator.drawbar-organ"] = new(NodeCategory.Audio, "M3 21V8H7V21 M10 21V3H14V21 M17 21V11H21V21", "Nine-drawbar tonewheel-style organ", "hammond organ instrument"),
        ["generator.pitch-control"] = new(NodeCategory.Modulation, "M5 19V5 M5 5 2 8 M5 5 8 8 M11 16H22 M11 8H22 M11 12H18", "Pitch and transposition control", "frequency semitone"),
        ["generator.envelope"] = new(NodeCategory.Modulation, "M2 20 7 3 11 10 17 10 22 20", "Shape attack, decay, sustain and release", "ADSR amplitude"),
        ["generator.signal-to-trigger"] = new(NodeCategory.Sequencing, "M2 14H9V4H11V14H22 M16 8 20 12 16 16", "Convert a signal to trigger events", "edge threshold"),
        ["generator.signal-to-gate"] = new(NodeCategory.Sequencing, "M2 18H7V6H17V18H22 M10 12H14", "Convert a signal to gate events", "threshold"),
        ["generator.manual-trigger"] = new(NodeCategory.Sequencing, "M12 2V8 M3 6 7 10 M21 6 17 10 M4 20H20V16H4Z", "Fire a trigger manually", "button pulse"),
        ["generator.gate"] = new(NodeCategory.Sequencing, "M2 18H6V6H18V18H22", "Generate gate signals", "pulse on off"),
        ["generator.clock"] = new(NodeCategory.Sequencing, "M12 2A10 10 0 1 0 12 22A10 10 0 1 0 12 2 M12 6V12L16 15", "Tempo-synchronized timing events", "beat BPM"),
        ["generator.note-sequencer"] = new(NodeCategory.Sequencing, "M2 5H22 M2 12H22 M2 19H22 M5 3V7 M13 10V14 M19 17V21", "Sequence pitched notes", "pattern melody steps"),
        ["generator.note-source"] = new(NodeCategory.Sequencing, "M4 3H20V21H4Z M9 3V13 M15 3V13 M4 15H20", "Play notes from the performance keyboard", "MIDI keyboard"),
        ["processor.gain"] = new(NodeCategory.Processing, "M3 18 21 6 M3 18H21V6 M12 10V20", "Adjust signal amplitude", "volume level"),
        ["processor.mixer"] = new(NodeCategory.Processing, "M5 3V21 M12 3V21 M19 3V21 M2 8H8 M9 16H15 M16 11H22", "Combine signals", "mix balance"),
        ["processor.filter"] = new(NodeCategory.Effects, "M2 5H22L15 13V20L9 17V13Z", "Shape frequency content", "lowpass highpass cutoff"),
        ["processor.parametric-eq"] = new(NodeCategory.Effects, "M2 17 C6 17 6 5 11 5S16 17 22 12 M2 21H22", "Equalize frequency bands", "EQ frequency"),
        ["processor.delay"] = new(NodeCategory.Effects, "M3 5V19 M9 8V16 M15 10V14 M21 11V13", "Repeat a signal over time", "echo feedback"),
        ["processor.reverb"] = new(NodeCategory.Effects, "M2 12H5 M7 5V19 M11 8V16 M15 10V14 M19 11V13 M22 12H23", "Add space and reverberation", "room decay"),
        ["processor.resonator"] = new(NodeCategory.Effects, "M2 12 5 4 8 20 11 8 14 16 17 10 20 14 22 12", "Resonate at tuned frequencies", "modal ring"),
        ["processor.chorus"] = new(NodeCategory.Effects, "M2 9Q7 1 12 9T22 9 M2 15Q7 7 12 15T22 15", "Layer modulated copies of a signal", "detune width"),
        ["processor.rotary-speaker"] = new(NodeCategory.Effects, "M12 3A9 9 0 1 1 3 12 M3 7V12H8 M12 7V17 M8 12H16", "Dual rotor amplitude and Doppler modulation", "leslie rotary organ speaker"),
        ["output.master"] = new(NodeCategory.Routing, "M3 9H7L13 4V20L7 15H3Z M16 8Q21 12 16 16 M19 5Q26 12 19 19", "Final audio output", "speaker master"),
        ["rack.input"] = new(NodeCategory.Routing, "M12 3H21V21H12 M2 12H17 M13 8 17 12 13 16", "Composite input boundary"),
        ["rack.output"] = new(NodeCategory.Routing, "M3 3H12V21H3 M7 12H22 M18 8 22 12 18 16", "Composite output boundary"),
        ["canvas.note"] = new(NodeCategory.Routing, "M4 3H20V16L15 21H4Z M8 8H16 M8 12H16 M15 21V16H20", "Add an annotation to the canvas", "text comment documentation"),
        ["composite.graph"] = new(NodeCategory.Composites, "M3 3H11V11H3Z M13 13H21V21H13Z M13 5H19V11 M5 13V19H11", "Reusable graph of connected nodes", "instrument modular rack")
    };

    private static readonly Dictionary<string, (string Path, string Description)> Presets = new(StringComparer.Ordinal)
    {
        ["organ"] = ("M3 21V8H7V21 M10 21V3H14V21 M17 21V11H21V21", "Sustained organ tones"),
        ["flute"] = ("M3 17 17 3 21 7 7 21Z M9 15H10 M13 11H14 M17 7H18", "Breathy flute tones"),
        ["brass"] = ("M3 9H12L21 3V21L12 15H3Z M7 9V5", "Brassy resonant tones"),
        ["bowed-strings"] = ("M12 2V13 M8 7Q3 12 8 17Q3 22 12 22Q21 22 16 17Q21 12 16 7 M2 20 22 4", "Bowed string textures"),
        ["didgeridoo"] = ("M5 3H9L18 21H10Z M15 5Q21 8 19 13", "Low buzzing drone"),
        ["handpan"] = ("M2 15Q12 2 22 15Q12 23 2 15Z M10 12H14 M6 15H7 M17 15H18", "Resonant pitched percussion"),
        ["bell"] = ("M5 17H19L17 14V8Q12 1 7 8V14Z M10 20H14", "Metallic bell partials"),
        ["gong"] = ("M2 3H22 M5 3V21 M19 3V21 M12 7A6 6 0 1 0 12 19A6 6 0 1 0 12 7 M12 12V14", "Sustained metallic gong"),
        ["wood"] = ("M3 14 5 6 21 10 19 18Z M7 11 17 14 M4 21 20 3", "Dry wooden percussion"),
        ["layered-fifth"] = ("M2 8Q7 1 12 8T22 8 M2 16Q7 9 12 16T22 16 M12 3V21", "Layered drone at a fifth"),
        ["ad-percussion"] = ("M2 20 7 3 9 20 M12 20 16 9 18 20 M20 20 22 15", "Attack-decay percussion")
    };

    public static NodeAppearance Get(string typeKey, NodeVisualRole? role = null)
    {
        if (Types.TryGetValue(typeKey, out var appearance))
            return appearance.Category == NodeCategory.Audio && role == NodeVisualRole.Modulator
                ? appearance with { Category = NodeCategory.Modulation } : appearance;
        if (typeKey.StartsWith("preset.", StringComparison.Ordinal))
        {
            var variant = typeKey[(typeKey.LastIndexOf('.') + 1)..];
            var playable = variant.StartsWith("playable-", StringComparison.Ordinal);
            var family = playable ? variant[9..] : variant;
            if (Presets.TryGetValue(family, out var preset))
                return new(NodeCategory.Composites, preset.Path,
                    playable ? $"Keyboard-playable {preset.Description.ToLowerInvariant()}" : preset.Description,
                    "instrument preset template");
        }
        if (typeKey.StartsWith("preset.", StringComparison.Ordinal) || typeKey.StartsWith("composite.", StringComparison.Ordinal))
            return Types["composite.graph"] with { Description = "Ready-to-use composite patch", Keywords = "instrument preset template" };
        return new(NodeCategory.Routing, "M3 3H21V21H3Z M8 12H16 M12 8V16", "Registered node");
    }

    public static string Label(NodeCategory category) => category switch
    {
        NodeCategory.Audio => "Audio generators",
        NodeCategory.Modulation => "Modulation",
        NodeCategory.Effects => "Effects",
        NodeCategory.Sequencing => "Sequencing & timing",
        NodeCategory.Processing => "Processing & mixing",
        NodeCategory.Composites => "Composites & instruments",
        _ => "Input / output & canvas"
    };
}

public sealed record NodeLibraryEntry(string Key, string Name, NodeAppearance Appearance)
{
    public bool Matches(string query, NodeCategory? category = null) =>
        (category is null || Appearance.Category == category) &&
        query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(word => $"{Name} {Key} {Appearance.Description} {Appearance.Keywords} {NodeAppearanceCatalog.Label(Appearance.Category)}"
                .Contains(word, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Editor preferences, independent of patch documents and history.</summary>
public sealed record NodeLibraryPreferences(string[] Favorites, string[] Recent)
{
    public static NodeLibraryPreferences Empty => new([], []);
    public NodeLibraryPreferences Normalize(IReadOnlySet<string> known) => new(
        (Favorites ?? []).Where(known.Contains).Distinct(StringComparer.Ordinal).Take(128).ToArray(),
        (Recent ?? []).Where(known.Contains).Distinct(StringComparer.Ordinal).Take(6).ToArray());
    public NodeLibraryPreferences ToggleFavorite(string key) => this with
    {
        Favorites = Favorites.Contains(key) ? Favorites.Where(value => value != key).ToArray()
            : Favorites.Append(key).Take(128).ToArray()
    };
    public NodeLibraryPreferences RecordUse(string key) => this with
    {
        Recent = new[] { key }.Concat(Recent.Where(value => value != key)).Take(6).ToArray()
    };
}
