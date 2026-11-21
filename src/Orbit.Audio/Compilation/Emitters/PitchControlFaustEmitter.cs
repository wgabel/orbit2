using Orbit.Audio.Analysis;
using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Parameters;
using Orbit.Domain.Timing;

namespace Orbit.Audio.Compilation.Emitters;

public sealed class PitchControlFaustEmitter : IFaustNodeEmitter
{
    public string TypeKey => PitchControlGenerator.NodeType;

    public ParameterRange? EstimateOutputRange(SignalRangeContext context) =>
        context.Parameter(PitchControlGenerator.SemitonesKey) is { } coarse && context.Parameter(PitchControlGenerator.CentsKey) is { } fine
            ? new(MusicalPitch.Semitones(coarse.Minimum, fine.Minimum), MusicalPitch.Semitones(coarse.Maximum, fine.Maximum)) : null;

    public string Emit(FaustNodeContext context) =>
        $"({context.Parameter(PitchControlGenerator.SemitonesKey)} + {context.Parameter(PitchControlGenerator.CentsKey)} / {FaustNumber.Format(MusicalPitch.CentsPerSemitone)})";
}
