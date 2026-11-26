using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Snapshots;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Presentation.Canvas;

/// <summary>Immutable editor context captured from the authored patch, with no
/// session mutation or runtime dependency. Only Apply creates a graph command.</summary>
public sealed class CompositeMacroDraft
{
    public sealed record Target(ParameterAddress Address, string NodeName,
        ParameterDefinition Definition, double ManualBase, bool IsActive, Guid? ExistingOwner);
    public sealed record PreviewRow(Guid MacroId, Target Target, double MappedBase)
    {
        public double EffectiveBase => Target.IsActive ? MappedBase : Target.ManualBase;
    }

    private readonly Dictionary<ParameterAddress, Target> targets;
    private readonly IReadOnlyList<PortDefinition> inputs;
    public Guid GraphId { get; }
    public Guid NodeId { get; }
    public long Revision { get; }
    public IReadOnlyList<CompositeMacroDefinition> OriginalMacros { get; }
    public IReadOnlyList<Target> Targets { get; }

    public CompositeMacroDraft(GraphSnapshot root, Guid graphId, Guid nodeId)
    {
        ArgumentNullException.ThrowIfNull(root);
        var wrapper = root.GetGraph(graphId).GetNode(nodeId);
        inputs = wrapper.Inputs;
        var composite = wrapper.Composite
            ?? throw new GraphValidationException("Macro drafts require a composite.");
        GraphId = graphId;
        NodeId = nodeId;
        Revision = root.Revision;
        OriginalMacros = Array.AsReadOnly(composite.Macros.ToArray());
        var owners = OriginalMacros.SelectMany(macro => macro.Mapping.Bindings.Select(binding => (binding.Target, macro.Id)))
            .ToDictionary(binding => binding.Target, binding => binding.Id);
        var options = composite.Graph.Nodes.SelectMany(node => node.Parameters
            .Where(parameter => CompositeMacroConfiguration.IsSupportedTarget(parameter.Definition))
            .Select(parameter =>
            {
                var address = new ParameterAddress(node.Id, parameter.Id);
                return new Target(address, node.Name, parameter.Definition, parameter.BaseValue,
                    node.IsParameterActive(parameter), owners.TryGetValue(address, out var owner) ? owner : null);
            })).ToArray();
        Targets = Array.AsReadOnly(options);
        targets = options.ToDictionary(target => target.Address);
    }

    public IReadOnlyList<PreviewRow> Preview(IReadOnlyList<CompositeMacroDefinition> proposed)
    {
        var copy = ValidateCopy(proposed);
        return Array.AsReadOnly(copy.SelectMany(macro => macro.ResolveBaseValues()
            .Select(value => new PreviewRow(macro.Id, targets[value.Target], value.Value))).ToArray());
    }

    public ReplaceCompositeMacrosCommand CreateCommand(IReadOnlyList<CompositeMacroDefinition> proposed) =>
        new(GraphId, NodeId, Array.AsReadOnly(ValidateCopy(proposed))) { ExpectedRevision = Revision };

    private CompositeMacroDefinition[] ValidateCopy(IReadOnlyList<CompositeMacroDefinition> proposed)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        var copy = proposed.ToArray();
        CompositeMacroConfiguration.Validate(copy, address => targets.TryGetValue(address, out var target)
            ? target.Definition : throw new GraphValidationException("Select a supported parameter in the direct child graph."), inputs);
        return copy;
    }
}
