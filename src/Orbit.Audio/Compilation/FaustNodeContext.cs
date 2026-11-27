using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Audio.Compilation;

/// <summary>Provides resolved, weighted input signals and clamped modulatable controls
/// to a single node emitter. Every reference is a safe backend-generated identifier.</summary>
public sealed class FaustNodeContext
{
    private readonly ILookup<PortAddress, SignalConnection> incoming;
    private readonly IReadOnlyDictionary<PortAddress, string> signals;
    private readonly List<FaustParameterControl> controls;
    private readonly List<string> definitions;
    private readonly FaustMacroExpressions? macros;

    internal FaustNodeContext(GraphNodeSnapshot node, ILookup<PortAddress, SignalConnection> incoming,
        IReadOnlyDictionary<PortAddress, string> signals, List<FaustParameterControl> controls,
        List<string> definitions, FaustMacroExpressions? macros = null)
    {
        Node = node;
        this.incoming = incoming;
        this.signals = signals;
        this.controls = controls;
        this.definitions = definitions;
        this.macros = macros;
    }

    public GraphNodeSnapshot Node { get; }

    public bool RequiresTransportBeat { get; private set; }
    public bool RequiresTransportTempo { get; private set; }
    public bool RequiresTransportReset { get; private set; }
    public bool? ManualRhythmicInput { get; private set; }
    public bool ManualPitchInput { get; private set; }
    public bool ManualNoteInput { get; private set; }
    public string PerformanceNoteInput()
    {
        ManualNoteInput = true;
        return $"action_{Node.Id:N}";
    }
    public string? PitchMeterName { get; private set; }
    public string PitchMeter() => PitchMeterName = $"pitch_{Node.Id:N}";
    public string PerformancePitchInput()
    {
        ManualPitchInput = true;
        return $"action_{Node.Id:N}";
    }
    public string? EnvelopeMeterName { get; private set; }

    public string EnvelopeMeter() => EnvelopeMeterName = $"e_{Node.Id:N}";

    public string TransportReset
    {
        get { RequiresTransportBeat = RequiresTransportReset = true; return "orbit_reset"; }
    }

    public string RhythmicActionInput(bool allowClose)
    {
        ManualRhythmicInput = allowClose;
        return $"action_{Node.Id:N}";
    }

    /// <summary>Request the shared beat input only if this emitted node uses transport.</summary>
    public string TransportBeat
    {
        get
        {
            RequiresTransportBeat = true;
            return "orbit_beat";
        }
    }

    /// <summary>Host-owned tempo control, independent of the wrapped beat phase.</summary>
    public string TransportTempo
    {
        get
        {
            RequiresTransportTempo = true;
            return "orbit_tempo";
        }
    }

    public string Input(string key)
    {
        var port = Node.Inputs.SingleOrDefault(port => port.Key == key)
            ?? throw new FaustCompilationException($"Node {Node.TypeKey} has no input '{key}'.");
        return InputSignal(port.Id);
    }

    public string Parameter(string key, string? signalMaximum = null, string? baseMultiplier = null, bool initializeAtTarget = false)
    {
        var parameter = Node.Parameters.SingleOrDefault(parameter => parameter.Definition.Key == key)
            ?? throw new FaustCompilationException($"Node {Node.TypeKey} has no parameter '{key}'.");
        var definition = parameter.Definition;
        if (!Node.IsParameterActive(parameter))
            throw new FaustCompilationException($"Parameter '{key}' is inactive in this mode.");
        if (definition.RequiresRecompile)
            throw new FaustCompilationException($"Parameter '{key}' must be read as a compiled constant.");
        if (definition.Scale != ParameterScale.Linear)
            throw new FaustCompilationException($"Parameter '{key}' has an unsupported modulation scale.");
        if (!definition.IsValidBaseValue(parameter.BaseValue))
            throw new FaustCompilationException($"Parameter '{key}' has an invalid base value.");

        // IDs, rather than user-visible names, are stable across renames and revisions.
        var name = $"p_{Node.Id:N}_{parameter.Id:N}";
        var minimum = FaustNumber.Format(definition.Minimum);
        var maximum = FaustNumber.Format(definition.Maximum);
        var initial = FaustNumber.Format(definition.DefaultValue);
        var meter = baseMultiplier is not null || parameter.ModulationPortId is Guid modulationPort &&
            incoming[new PortAddress(Node.Id, modulationPort)].Any()
            ? $"m_{Node.Id:N}_{parameter.Id:N}" : null;
        var mappedBase = macros?.Resolve(Node, parameter);
        if (mappedBase is not null)
        {
            // Dynamic targets have no independent base writer, but retain a
            // passive meter for the final effective value after native modulation.
            meter = macros!.TargetMeterName(Node, parameter);
        }
        else if (!controls.Any(control => control.Name == name))
        {
            controls.Add(new FaustParameterControl(new ParameterAddress(Node.Id, parameter.Id), name, parameter.BaseValue, meter));
            var step = definition.Choices is null ? "0.001" : "1.0";
            var slider = $"hslider(\"{name}\", {initial}, {minimum}, {maximum}, {step})";
            // Smooth the base slider only; audio-rate modulation is added below.
            // Time-sensitive controls may start at their initialized target.
            var smoothing = FaustControlSmoothing.Filter(definition.Smoothing);
            if (definition.Choices is not null && smoothing is not null)
                throw new FaustCompilationException($"Discrete parameter '{key}' cannot be smoothed.");
            var expression = smoothing is null ? slider : initializeAtTarget
                ? $"first + ((value - first) : {smoothing}) with {{ value = {slider}; first = ((select2(1 - (1 : mem), _, value)) ~ _); }}"
                : slider + " : " + smoothing;
            definitions.Add($"{name} = {expression};");
        }

        var modulation = parameter.ModulationPortId is Guid portId ? InputSignal(portId) : "0.0";
        var baseValue = mappedBase ?? name;
        var baseExpression = baseMultiplier is null ? baseValue : $"({baseValue} * ({baseMultiplier}))";
        var effective = $"max({minimum}, min({maximum}, {baseExpression} + {modulation}))";
        if (signalMaximum is not null)
            effective = $"min({signalMaximum}, {effective})";
        // A passive bargraph passes its input unchanged and reports the actual
        // clamped sample value. Connected modulation and a transformed base
        // (e.g. pitch-tracked Hz) need a live meter.
        return meter is null ? effective : $"({effective} : hbargraph(\"{meter}\", {minimum}, {maximum}))";
    }

    /// <summary>Apply the same policy as a base parameter to its derived alternative
    /// (e.g. tempo-derived delay time). The base parameter can be inactive.</summary>
    public string SmoothDerivedControl(string key, string signal)
    {
        var definition = Node.Parameters.Single(p => p.Definition.Key == key).Definition;
        var smoothing = FaustControlSmoothing.Filter(definition.Smoothing);
        return smoothing is null ? signal : $"({signal} : {smoothing})";
    }

    /// <summary>Read a validated structural choice without allocating a Faust control.</summary>
    public double ConstantParameter(string key)
    {
        var parameter = Node.Parameters.SingleOrDefault(parameter => parameter.Definition.Key == key)
            ?? throw new FaustCompilationException($"Node {Node.TypeKey} has no parameter '{key}'.");
        if (!Node.IsParameterActive(parameter) || !parameter.Definition.RequiresRecompile ||
            !parameter.Definition.IsValidBaseValue(parameter.BaseValue))
            throw new FaustCompilationException($"Parameter '{key}' is not a valid compiled constant.");
        return parameter.BaseValue;
    }

    internal string InputSignal(Guid portId)
    {
        if (Node.Inputs.Single(port => port.Id == portId).SignalType.IsEvent())
        {
            var edges = incoming[new PortAddress(Node.Id, portId)].ToArray();
            if (edges.Length == 0) return "0.0";
            if (edges.Length != 1 || edges[0].Amount != 1 || !signals.TryGetValue(edges[0].Source, out var signal))
                throw new FaustCompilationException("Rhythmic input must have one unity source.");
            return signal;
        }
        var terms = incoming[new PortAddress(Node.Id, portId)].OrderBy(edge => edge.Id).Select(edge =>
        {
            if (!signals.TryGetValue(edge.Source, out var signal))
                throw new FaustCompilationException($"Missing compiled signal for connection {edge.Id}.");
            return $"({signal} * {FaustNumber.Format(edge.Amount)})";
        }).ToArray();
        return terms.Length == 0 ? "0.0" : "(" + string.Join(" + ", terms) + ")";
    }
}
