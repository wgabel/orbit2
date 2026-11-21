using Orbit.Application.Graphs.Snapshots;
using Orbit.Application.Persistence;
using Orbit.Application.Models;
using Orbit.Domain.Ports;
using Orbit.Domain.Nodes.Generators;

namespace Orbit.Presentation.Canvas;

/// <summary>Shared port-based discovery and validation for compact and expanded plots.</summary>
public static class NodeVisualizationCapabilities
{
    public static IEnumerable<PortDefinition> EligiblePorts(GraphNodeSnapshot node) =>
        node.Outputs.Concat(node.Inputs.Where(port => node.IsInputActive(port.Id)))
            .Where(port => port.ChannelCount == 1);

    public static PortDefinition? ResolvePort(GraphNodeSnapshot node, Guid? preferredPortId) =>
        EligiblePorts(node).FirstOrDefault(port => port.Id == preferredPortId) ?? EligiblePorts(node).FirstOrDefault();

    public static bool SupportsCycle(GraphNodeSnapshot node, Guid? preferredPortId) =>
        node.TypeKey == OscillatorGenerator.NodeType && ResolvePort(node, preferredPortId)?.Direction == PortDirection.Output;

    public static bool SupportsResponse(GraphNodeSnapshot node) => ProcessorResponseCapabilities.Supports(node.TypeKey);

    // Persisted preferences are optional. Unsupported values fall back to a safe default.
    // Keep a dormant port preference so it can resume when the port becomes active again.
    public static NodeVisualizationSettings Normalize(GraphNodeSnapshot node, NodeVisualizationSettings? settings)
    {
        settings ??= NodeVisualizationSettings.Default;
        return settings with
        {
            PortId = node.Outputs.Concat(node.Inputs).Any(port => port.Id == settings.PortId &&
                port.ChannelCount == 1) ? settings.PortId : null,
            Mode = settings.Mode == "response" && SupportsResponse(node) ? "response" : Choice(settings.Mode, "waveform", "spectrum", "level"),
            Timebase = settings.Timebase == "cycle" && SupportsCycle(node, settings.PortId) ? "cycle" :
                Choice(settings.Timebase, "0.02", "0.005", "0.05", "1", "5", "10"),
            Trigger = Choice(settings.Trigger, "rising", "falling", "free"),
            FftSize = Choice(settings.FftSize, "8192", "1024", "2048", "4096"),
            Window = Choice(settings.Window, "hann", "rectangular"),
            Smoothing = Choice(settings.Smoothing, "0.15", "0", "0.5")
        };
    }

    private static string Choice(string? value, string fallback, params string[] others) =>
        value == fallback || others.Contains(value) ? value! : fallback;
}
