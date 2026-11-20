using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Nodes.Outputs;
using Orbit.Domain.Ports;

namespace Orbit.Audio.Compilation;

/// <summary>Compiles an immutable flat graph into one mono Faust program.
/// It never loads a runtime or processes samples on the application thread.</summary>
public sealed class FaustGraphCompiler
{
    private readonly IReadOnlyDictionary<string, IFaustNodeEmitter> emitters;

    public FaustGraphCompiler(IEnumerable<IFaustNodeEmitter> emitters) =>
        this.emitters = emitters.ToDictionary(emitter => emitter.TypeKey, StringComparer.Ordinal);

    public static FaustGraphCompiler CreateBuiltIn() =>
        new(BuiltInFaustEmitters.Create());

    public FaustGraphProgram Compile(GraphSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var outputs = graph.Nodes.Where(node => node.TypeKey == MasterOutput.NodeType).ToArray();
        if (outputs.Length != 1)
            throw new FaustCompilationException("A patch must have exactly one master output.");

        var ordered = GraphDependencyOrder.Get(graph, outputs[0].Id);
        var incoming = graph.Connections.ToLookup(edge => edge.Destination);
        var signals = new Dictionary<PortAddress, string>();
        var controls = new List<FaustParameterControl>();
        var definitions = new List<string>();

        foreach (var node in ordered)
        {
            var context = new FaustNodeContext(node, incoming, signals, controls, definitions);
            if (node.Id == outputs[0].Id)
            {
                definitions.Add($"process = {context.Input("audio-in")};");
                continue;
            }

            if (!emitters.TryGetValue(node.TypeKey, out var emitter))
                throw new FaustCompilationException($"Unsupported connected node type: {node.TypeKey}.");
            if (node.Outputs.Count != 1 || node.Outputs[0].ChannelCount != 1 ||
                node.Inputs.Any(port => port.ChannelCount != 1))
                throw new FaustCompilationException("Only mono, single-output nodes are supported.");

            var name = $"n_{node.Id:N}";
            var expression = emitter.Emit(context);
            definitions.Add($"{name} = {expression};");
            signals.Add(new PortAddress(node.Id, node.Outputs[0].Id), name);
        }

        // Canonical order and invariant literals make source stable across base-only edits.
        var source = "import(\"stdfaust.lib\");\ndeclare name \"OrbitGraph\";\n"
            + string.Join("\n", definitions) + "\n";
        return new FaustGraphProgram(graph.Id, graph.Revision, source, controls.AsReadOnly());
    }
}
