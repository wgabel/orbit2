using System.Text.Json;
using System.Text.Json.Serialization;
using Orbit.Application.Graphs;
using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.AgentKit.Exporter;

/// <summary>Projects detached native factory instances into stable, data-only metadata.
/// Does not serialize patch state, evaluate DSP or depend on presentation.</summary>
public static class CatalogExporter
{
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Export(NodeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var nodes = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var key in registry.RegisteredTypes)
            nodes.Add(key, Describe(key, registry.Describe(key), registry.Create(key)));
        return JsonSerializer.Serialize(new { catalogFormatVersion = FormatVersion, nodes }, Options);
    }

    private static object Describe(string key, string label, ISignalNode node)
    {
        var composite = node is ICompositeNode;
        var kind = composite ? (key == node.TypeKey ? "composite" : "composite-preset") : "leaf";
        var category = node switch
        {
            ICompositeNode => "composite",
            MasterOutput => "output",
            IGenerator => "generator",
            IProcessor => "processor",
            _ => "other"
        };

        // Composite boundary keys contain generated instance GUIDs. Do not expose
        // those as stable keys; nested portable factory data is a later slice.
        var ports = composite ? Array.Empty<PortDefinition>() :
            node.Inputs.Concat(node.Outputs).OrderBy(port => port.Key, StringComparer.Ordinal).ToArray();
        var parameters = composite ? Array.Empty<Parameter>() :
            node.Parameters.OrderBy(parameter => parameter.Definition.Key, StringComparer.Ordinal).ToArray();
        return new
        {
            creationKey = key,
            persistedTypeKey = node.TypeKey,
            label,
            name = node.Name,
            kind,
            category,
            metadataStatus = composite ? "deferred-composite" : "leaf-definition",
            unsupportedReason = composite ? "Portable nested graph and boundary identity export is deferred to 7.7-S9." : null,
            node.SupportsMute,
            bypassInputKey = composite ? null : node.BypassInputKey,
            factoryIsMuted = node.IsMuted,
            factoryIsBypassed = node.IsBypassed,
            parameters = parameters.Select(parameter => DescribeParameter(parameter, ports)).ToArray(),
            ports = ports.Select(port => new
            {
                port.Key,
                port.Name,
                port.Direction,
                port.SignalType,
                port.ChannelCount,
                port.FanIn,
                activeWhen = DescribeActivation(port.ActiveWhen)
            }).ToArray()
        };
    }

    private static object DescribeParameter(Parameter parameter, IReadOnlyList<PortDefinition> ports)
    {
        var definition = parameter.Definition;
        return new
        {
            definition.Key,
            definition.Name,
            valueType = "number",
            factoryBaseValue = parameter.BaseValue,
            definition.DefaultValue,
            definition.Minimum,
            definition.Maximum,
            definition.Unit,
            definition.Scale,
            definition.CanModulate,
            definition.Smoothing,
            definition.RequiresRecompile,
            definition.Choices,
            displayAnchors = definition.DisplayScale?.Anchors,
            activeWhen = DescribeActivation(definition.ActiveWhen),
            modulationPortKey = parameter.ModulationPortId is Guid portId
                ? ports.Single(port => port.Id == portId).Key : null
        };
    }

    // Native Clauses() provides the actual OR-of-AND semantics. Project only
    // selectors and values, never recursively serialize the expression objects.
    private static object? DescribeActivation(ParameterActivation? activation) =>
        activation?.Clauses().Select(clause => clause.Select(condition => new
        {
            condition.SelectorKey,
            condition.EqualsValue
        }).ToArray()).ToArray();
}
