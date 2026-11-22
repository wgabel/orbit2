using System.Text.Json;
using System.Text.Json.Serialization;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Ports;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Timing;

namespace Orbit.Application.Persistence;

/// <summary>Versioned data-only format. Current registered definitions and domain validation are authoritative.</summary>
public sealed class PatchDocumentSerializer
{
    public PatchDocument Capture(IPatchSession session, double tempo, Guid activeGraphId, SavedCanvas[] layouts,
        SavedPatchOverview? overview = null) =>
        new(PatchDocument.CurrentVersion, session.PatchId, CaptureGraph(session.Snapshot), tempo, activeGraphId, layouts)
        { Overview = SavedPatchOverview.Validate(overview ?? SavedPatchOverview.Empty) };

    public string Serialize(PatchDocument document) =>
        JsonSerializer.Serialize(document with { Overview = SavedPatchOverview.Validate(document.Overview) }, PatchJsonContext.Default.PatchDocument);

    public PatchDocument Deserialize(string json)
    {
        var document = JsonSerializer.Deserialize(json, PatchJsonContext.Default.PatchDocument)
            ?? throw new JsonException("Missing patch document.");
        return document with { Overview = SavedPatchOverview.Validate(document.Overview) };
    }

    // Read only the overview and version. Do not reconstruct the graph or allocate runtime nodes.
    public SavedPatchOverview ReadOverview(string json)
    {
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        if (!root.TryGetProperty("schemaVersion", out var version) || version.GetInt32() != PatchDocument.CurrentVersion)
            throw new NotSupportedException("Unsupported patch schema.");
        return root.TryGetProperty("overview", out var metadata)
            ? SavedPatchOverview.Validate(metadata.Deserialize(PatchJsonContext.Default.SavedPatchOverview))
            : SavedPatchOverview.Empty;
    }

    public IPatchSession Restore(PatchDocument document, NodeRegistry registry)
    {
        if (document.SchemaVersion != PatchDocument.CurrentVersion)
            throw new NotSupportedException($"Unsupported patch schema {document.SchemaVersion}.");
        SavedPatchOverview.Validate(document.Overview);
        _ = new MusicalTransport(document.BeatsPerMinute);
        var root = RestoreGraph(document.RootGraph, registry);
        var graphs = new Dictionary<Guid, (Guid[] Nodes, Guid[] Connections)>();
        void Collect(SavedGraph graph, Guid[] racks)
        {
            graphs.Add(graph.Id, (graph.Nodes.Select(node => node.Id).Concat(racks).ToArray(),
                graph.Connections.Select(edge => edge.Id).ToArray()));
            foreach (var node in graph.Nodes)
                if (node.Composite is { } composite) Collect(composite.Graph, [composite.InputRackId, composite.OutputRackId]);
        }
        Collect(document.RootGraph, []);
        var canvasIds = new HashSet<Guid>();
        foreach (var canvas in document.CanvasLayouts)
        {
            SavedNote.Validate(canvas.Notes);
            if (!canvasIds.Add(canvas.GraphId) ||
                (canvas.Notes.Length > 0 && !graphs.ContainsKey(canvas.GraphId)))
                throw new ArgumentException("Invalid canvas note ownership or duplicate canvas.");
            var targets = graphs.GetValueOrDefault(canvas.GraphId);
            DescriptionLimits.Validate(canvas, targets.Nodes ?? [], targets.Connections ?? []);
            SavedVisualGroup.Validate(canvas.VisualGroups, targets.Nodes ?? [], canvas.Notes.Select(note => note.Id));
            if (canvas.VisualGroups.Length > 0 && !graphs.ContainsKey(canvas.GraphId))
                throw new ArgumentException("Visual groups belong to an unknown graph.");
        }
        return new PatchSession(registry, root, document.PatchId);
    }

    // Session history uses the same validated reconstruction path without JSON or storage.
    internal static SignalGraph RestoreSnapshot(GraphSnapshot snapshot, NodeRegistry registry) =>
        RestoreGraph(CaptureGraph(snapshot), registry);

    private static SignalGraph RestoreGraph(SavedGraph saved, NodeRegistry registry)
    {
        var root = new SignalGraph(saved.Id);
        RestoreGraph(saved, root, registry, new HashSet<Guid>());
        return root;
    }

    private static SavedGraph CaptureGraph(GraphSnapshot graph) => new(graph.Id,
        graph.Nodes.Where(node => node.TypeKey is not (InputRack.NodeType or OutputRack.NodeType)).Select(node =>
            new SavedNode(node.Id, node.TypeKey,
                node.Parameters.Select(parameter => new SavedParameter(parameter.Definition.Key, parameter.Id, parameter.BaseValue)).ToArray(),
                node.Inputs.Concat(node.Outputs).Select(port => new SavedPort(port.Key, port.Id)).ToArray(),
                node.Composite is { } composite ? new SavedComposite(node.Name, composite.InputRackId, composite.OutputRackId,
                    node.Inputs.Select(ToExposed).ToArray(),
                    composite.Graph.GetNode(composite.OutputRackId).Inputs.Select(ToExposed).ToArray(),
                    CaptureGraph(composite.Graph)) : null) { IsMuted = node.IsMuted, IsBypassed = node.IsBypassed }).ToArray(), graph.Connections.ToArray());

    private static ExposedPort ToExposed(PortDefinition port) => new(port.Id, port.Name, port.SignalType, port.ChannelCount, port.FanIn);

    private static void RestoreGraph(SavedGraph saved, SignalGraph graph, NodeRegistry registry, HashSet<Guid> graphIds)
    {
        if (!graphIds.Add(saved.Id)) throw new GraphValidationException("Duplicate graph identity.");
        foreach (var node in saved.Nodes)
        {
            if (node.Composite is { } composite)
            {
                if (node.TypeKey != CompositeNode.NodeType) throw new GraphValidationException("Invalid composite type.");
                var restored = new CompositeNode(node.Id, composite.Name, composite.Inputs, composite.Outputs,
                    composite.Graph.Id, composite.InputRackId, composite.OutputRackId);
                RestoreGraph(composite.Graph, restored.InternalGraph, registry, graphIds);
                graph.AddNode(restored);
            }
            else
            {
                if (node.TypeKey is CompositeNode.NodeType or InputRack.NodeType or OutputRack.NodeType ||
                    registry.Create(node.TypeKey, node.Id) is not SignalNode leaf)
                    throw new GraphValidationException("Invalid leaf node type.");
                var ports = node.Ports.ToDictionary(port => port.Key, port => port.Id);
                var parameters = node.Parameters.ToDictionary(parameter => parameter.Key, parameter => (parameter.Id, parameter.BaseValue));
                // Older schema-1 oscillators predate pulse width. Preserve every existing identity.
                if (leaf is OscillatorGenerator && ports.Count == 4 && parameters.Count == 6 &&
                    !ports.ContainsKey(OscillatorGenerator.PulseWidthPortKey) && !parameters.ContainsKey(OscillatorGenerator.PulseWidthKey))
                {
                    var pulse = leaf.Parameters.Single(p => p.Definition.Key == OscillatorGenerator.PulseWidthKey);
                    ports.Add(OscillatorGenerator.PulseWidthPortKey, pulse.ModulationPortId!.Value);
                    parameters.Add(OscillatorGenerator.PulseWidthKey, (pulse.Id, pulse.BaseValue));
                }
                // The complete pre-pitch schema gets an opt-in tracking mode and a dormant socket.
                if (leaf is OscillatorGenerator && ports.Count == 5 && parameters.Count == 7 &&
                    !ports.ContainsKey(OscillatorGenerator.PitchPortKey) &&
                    !parameters.ContainsKey(OscillatorGenerator.PitchTrackingKey) && !parameters.ContainsKey(OscillatorGenerator.PitchKey))
                {
                    var pitch = leaf.Parameters.Single(p => p.Definition.Key == OscillatorGenerator.PitchKey);
                    var tracking = leaf.Parameters.Single(p => p.Definition.Key == OscillatorGenerator.PitchTrackingKey);
                    ports.Add(OscillatorGenerator.PitchPortKey, pitch.ModulationPortId!.Value);
                    parameters.Add(OscillatorGenerator.PitchKey, (pitch.Id, pitch.BaseValue));
                    parameters.Add(OscillatorGenerator.PitchTrackingKey, (tracking.Id, tracking.BaseValue));
                }
                // Phase 7.1.1 AD nodes used only continuous triggering. Preserve those wires and IDs.
                if (leaf is EnvelopeGenerator && ports.Count == 6 && parameters.Count == 4 &&
                    !ports.ContainsKey(EnvelopeGenerator.EventTriggerPortKey) && !ports.ContainsKey(EnvelopeGenerator.GatePortKey) &&
                    !parameters.ContainsKey(EnvelopeGenerator.TriggerSourceKey))
                {
                    foreach (var key in new[] { EnvelopeGenerator.EventTriggerPortKey, EnvelopeGenerator.GatePortKey })
                        ports.Add(key, leaf.Inputs.Single(p => p.Key == key).Id);
                    var selector = leaf.Parameters.Single(p => p.Definition.Key == EnvelopeGenerator.TriggerSourceKey);
                    parameters.Add(EnvelopeGenerator.TriggerSourceKey, (selector.Id, selector.BaseValue));
                }
                // Phase 7.1.4 extends the same envelope type. Only a complete
                // legacy AD shape is migrated; all original IDs and wires survive.
                if (leaf is EnvelopeGenerator && ports.Count == 8 && parameters.Count == 5 &&
                    EnvelopeGenerator.LegacyAdParameterKeys.All(parameters.ContainsKey) &&
                    leaf.Inputs.Concat(leaf.Outputs).Where(p => !EnvelopeGenerator.ModeExtensionDefinitions.Any(d => p.Key == d.Key + "-modulation-in")).All(p => ports.ContainsKey(p.Key)))
                {
                    foreach (var parameter in leaf.Parameters.Where(p => !EnvelopeGenerator.LegacyAdParameterKeys.Contains(p.Definition.Key)))
                    {
                        parameters.Add(parameter.Definition.Key, (parameter.Id, parameter.BaseValue));
                        if (parameter.ModulationPortId is Guid portId)
                            ports.Add(leaf.Inputs.Single(p => p.Id == portId).Key, portId);
                    }
                }
                // The live-only sequencer had exactly two controls. New fixed-capacity slots default to Hold.
                if (leaf is NoteSequencerGenerator && parameters.Count == 2 && parameters.ContainsKey(NoteSequencerGenerator.ReferenceKey) && parameters.ContainsKey(NoteSequencerGenerator.InitialPitchKey))
                    foreach (var parameter in leaf.Parameters.Where(p => NoteSequencerGenerator.IsPatternParameter(p.Definition.Key)))
                        parameters.Add(parameter.Definition.Key, (parameter.Id, parameter.BaseValue));
                leaf.RestoreIdentity(ports, parameters);
                graph.AddNode(leaf);
            }
            if (node.IsMuted) graph.SetNodeMuted(node.Id, true);
            if (node.IsBypassed) graph.SetNodeBypassed(node.Id, true);
        }
        foreach (var connection in saved.Connections) graph.AddConnection(connection);
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PatchDocument))]
internal partial class PatchJsonContext : JsonSerializerContext { }
