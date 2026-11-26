using System.Security.Cryptography;
using System.Text;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Ports;
using Orbit.Domain.Parameters;

namespace Orbit.Audio.Compilation;

/// <summary>Backend-only lowering of a patch into leaf nodes and transparent
/// one-socket routes. Editable rack nodes and graph-local identities stay intact.</summary>
internal static class GraphFlattener
{
    internal sealed record NodeOrigin(Guid GraphId, Guid NodeId);
    // The boundary route already applies the actual cable coefficients. Keep
    // implicit base dependencies separate from ordinary additive input edges.
    internal sealed record MacroSource(Guid GraphId, Guid CompositeId, Guid MacroId,
        double DefaultValue, double BaseValue, PortAddress Signal);
    internal sealed record MacroTarget(MacroSource Source, CompositeMacroBinding Binding);
    internal sealed record Result(GraphSnapshot Graph, IReadOnlyDictionary<Guid, NodeOrigin> Origins,
        IReadOnlyDictionary<(Guid GraphId, PortAddress Address), PortAddress> Endpoints,
        IReadOnlyDictionary<ParameterAddress, MacroTarget> MacroTargets)
    {
        public IEnumerable<(Guid Source, Guid Destination)> MacroDependencies =>
            MacroTargets.Select(pair => (pair.Value.Source.Signal.NodeId, pair.Key.NodeId)).Distinct();
    }

    public static Result Flatten(GraphSnapshot root, bool includeMacroDependencies = false)
    {
        ArgumentNullException.ThrowIfNull(root);
        root = root.WithEffectiveMacroBaseValues();
        var nodes = new List<GraphNodeSnapshot>();
        var edges = new List<SignalConnection>();
        var origins = new Dictionary<Guid, NodeOrigin>();
        var endpoints = new Dictionary<(Guid, PortAddress), PortAddress>();
        var macroTargets = new Dictionary<ParameterAddress, MacroTarget>();
        var graphIds = new HashSet<Guid>();
        var nodeIds = new HashSet<Guid>();
        var edgeIds = new HashSet<Guid>();

        void Add(GraphNodeSnapshot node)
        {
            if (!nodeIds.Add(node.Id))
                throw new FaustCompilationException("Flattened node identities collide.");
            nodes.Add(node);
        }

        void Map(Guid graphId, Guid nodeId, Guid portId, PortAddress flattened)
        {
            if (!endpoints.TryAdd((graphId, new PortAddress(nodeId, portId)), flattened))
                throw new FaustCompilationException("Ambiguous boundary or node socket.");
        }

        PortAddress Resolve(Guid graphId, PortAddress address) =>
            endpoints.TryGetValue((graphId, address), out var result) ? result
                : throw new FaustCompilationException($"Missing socket {address} in graph {graphId}.");

        void Route(GraphSnapshot parent, GraphNodeSnapshot composite, GraphSnapshot child,
            Guid rackId, PortDefinition outer, PortDefinition inner)
        {
            if (outer.Id != inner.Id || outer.Direction == inner.Direction ||
                outer.SignalType != inner.SignalType || outer.ChannelCount != inner.ChannelCount)
                throw new FaustCompilationException("Composite socket does not match its rack.");

            var id = StableId("boundary", parent.Id, composite.Id, outer.Id);
            var receiving = outer.Direction == PortDirection.Input ? outer : inner;
            var input = receiving with { Id = StableId("in", id), Key = "signal-in" };
            var output = receiving with { Id = StableId("out", id), Key = "signal-out",
                Direction = PortDirection.Output, FanIn = FanInPolicy.Single };
            var outputBoundary = outer.Direction == PortDirection.Output;
            Add(new GraphNodeSnapshot(id, BoundaryFaustEmitter.NodeType, outer.Name, [], [input], [output])
            {
                SupportsMute = outputBoundary && composite.SupportsMute,
                IsMuted = outputBoundary && composite.IsMuted
            });
            if (outputBoundary) origins.Add(id, new NodeOrigin(parent.Id, composite.Id));
            var inAddress = new PortAddress(id, input.Id);
            var outAddress = new PortAddress(id, output.Id);
            Map(parent.Id, composite.Id, outer.Id, outer.Direction == PortDirection.Input ? inAddress : outAddress);
            Map(child.Id, rackId, inner.Id, inner.Direction == PortDirection.Input ? inAddress : outAddress);
        }

        void Visit(GraphSnapshot graph, CompositeSnapshot? owner)
        {
            if (!graphIds.Add(graph.Id))
                throw new FaustCompilationException("A child graph is shared or recursive.");

            foreach (var node in graph.Nodes)
            {
                if (owner is not null && (node.Id == owner.InputRackId || node.Id == owner.OutputRackId))
                    continue; // Already mapped by the owning composite.
                if (node.TypeKey is InputRack.NodeType or OutputRack.NodeType ||
                    (owner is not null && node.TypeKey == MasterOutput.NodeType))
                    throw new FaustCompilationException("Unexpected rack or master output in child graph.");

                if (node.Composite is { } composite)
                {
                    var child = composite.Graph;
                    var inputRack = child.Nodes.SingleOrDefault(rack => rack.Id == composite.InputRackId);
                    var outputRack = child.Nodes.SingleOrDefault(rack => rack.Id == composite.OutputRackId);
                    if (inputRack?.TypeKey != InputRack.NodeType || outputRack?.TypeKey != OutputRack.NodeType ||
                        inputRack.Inputs.Count != 0 || outputRack.Outputs.Count != 0 ||
                        inputRack.Outputs.Count != node.Inputs.Count || outputRack.Inputs.Count != node.Outputs.Count)
                        throw new FaustCompilationException("Composite has invalid boundary racks.");
                    foreach (var port in node.Inputs)
                        Route(graph, node, child, inputRack.Id, port, Match(inputRack.Outputs, port.Id));
                    foreach (var port in node.Outputs)
                        Route(graph, node, child, outputRack.Id, port, Match(outputRack.Inputs, port.Id));
                    Visit(child, composite);
                    if (includeMacroDependencies)
                    {
                        foreach (var macro in composite.Macros)
                        {
                            if (macro.ModulationInputId is not Guid input ||
                                !graph.Connections.Any(edge => edge.Destination == new PortAddress(node.Id, input)))
                                continue;
                            var signal = Resolve(child.Id, new PortAddress(composite.InputRackId, input));
                            var source = new MacroSource(graph.Id, node.Id, macro.Id,
                                macro.DefaultValue, macro.BaseValue, signal);
                            foreach (var binding in macro.Mapping.Bindings)
                            {
                                var target = child.GetNode(binding.Target.NodeId);
                                var parameter = target.Parameters.Single(p => p.Id == binding.Target.ParameterId);
                                if (!target.IsParameterActive(parameter)) continue;
                                var address = new ParameterAddress(StableId("node", child.Id, target.Id), parameter.Id);
                                if (!macroTargets.TryAdd(address, new MacroTarget(source, binding)))
                                    throw new FaustCompilationException("Ambiguous flattened macro target.");
                            }
                        }
                    }
                    continue;
                }

                // Graph-local IDs can repeat across independent child canvases.
                var id = owner is null ? node.Id : StableId("node", graph.Id, node.Id);
                Add(node with { Id = id });
                origins.Add(id, new NodeOrigin(graph.Id, node.Id));
                foreach (var port in node.Inputs.Concat(node.Outputs))
                    Map(graph.Id, node.Id, port.Id, new PortAddress(id, port.Id));
            }

            foreach (var edge in graph.Connections)
            {
                var id = owner is null ? edge.Id : StableId("edge", graph.Id, edge.Id);
                if (!edgeIds.Add(id))
                    throw new FaustCompilationException("Flattened connection identities collide.");
                var source = Resolve(graph.Id, edge.Source);
                var destination = Resolve(graph.Id, edge.Destination);
                // Dormant modulation stays in the editable snapshot, but cannot
                // contribute dependencies, controls or bounds to the active DSP.
                if (graph.GetNode(edge.Destination.NodeId).IsInputActive(edge.Destination.PortId))
                    edges.Add(edge with { Id = id, Source = source, Destination = destination });
            }
        }

        Visit(root, null);
        return new Result(new GraphSnapshot(root.Id, root.Revision, nodes.AsReadOnly(), edges.AsReadOnly()), origins, endpoints, macroTargets);
    }

    private static PortDefinition Match(IReadOnlyList<PortDefinition> ports, Guid id) =>
        ports.SingleOrDefault(port => port.Id == id)
        ?? throw new FaustCompilationException("Composite socket is missing its mirrored rack socket.");

    // Stable across graph revisions and independent of display names or traversal order.
    private static Guid StableId(string role, params Guid[] ids) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(role + ":" +
            string.Join(":", ids.Select(id => id.ToString("N"))))).AsSpan(0, 16));
}
