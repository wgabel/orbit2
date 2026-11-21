using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;
using Orbit.Domain.Timing;

namespace Orbit.Presentation.Canvas;

/// <summary>A calculated value accompanying a parameter, independent of DSP telemetry.</summary>
public sealed record ParameterReadout(string Label, double Value, ParameterUnit Unit)
{
    public static ParameterReadout? ForChoice(GraphNodeSnapshot node, ParameterSnapshot parameter, double beatsPerMinute)
    {
        if (node.TypeKey != OscillatorGenerator.NodeType || parameter.Definition.Key != "division" ||
            !node.IsParameterActive(parameter)) return null;

        var division = (MusicalDivision)parameter.BaseValue;
        return new("Synchronized frequency", MusicalTime.GetFrequency(beatsPerMinute, MusicalTime.GetBeats(division)),
            ParameterUnit.Hertz);
    }
}
