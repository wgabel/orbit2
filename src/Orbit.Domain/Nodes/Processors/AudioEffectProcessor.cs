using Orbit.Domain.Parameters;
using Orbit.Domain.Ports;

namespace Orbit.Domain.Nodes.Processors;

/// <summary>An effect explicitly providing a single compatible dry audio path.
/// Routing processors need not inherit this capability.</summary>
public abstract class AudioEffectProcessor : SignalNode, IProcessor
{
    protected AudioEffectProcessor(Guid id, string typeKey, string name,
        IEnumerable<Parameter> parameters, IEnumerable<PortDefinition> inputs,
        IEnumerable<PortDefinition> outputs)
        : base(id, typeKey, name, parameters, inputs, outputs)
    {
        var dryInput = Inputs.Single(port => port.SignalType == SignalType.Audio);
        var output = Outputs.Single();
        if (output.SignalType != SignalType.Audio || output.ChannelCount != dryInput.ChannelCount)
            throw new ArgumentException("A bypassable effect requires a compatible audio input and output.");
        BypassInputKey = dryInput.Key;
    }

    public sealed override string BypassInputKey { get; }
}
