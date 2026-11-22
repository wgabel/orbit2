using Orbit.Domain.Parameters;

namespace Orbit.Application.Models;

/// <summary>Editable settings for the first fixed modulation graph, in Hz.</summary>
public sealed record ModulationDemoSettings(
    double BaseFrequencyHertz = 440,
    double LfoFrequencyHertz = 0.5,
    double DepthHertz = 100)
{
    // Narrow, comfortable demo ranges; the eventual editor uses node definitions.
    public const double MinimumBaseFrequency = 100;
    public const double MaximumBaseFrequency = 1000;
    public const double MinimumLfoFrequency = 0.05;
    public const double MaximumLfoFrequency = 5;
    public const double MaximumDepth = 200;

    // These definitions drive both validation and reusable presentation controls.
    public static ParameterDefinition BaseFrequencyDefinition { get; } =
        new("baseFrequency", "Base frequency", 440, MinimumBaseFrequency, MaximumBaseFrequency,
            ParameterUnit.Hertz, ParameterScale.Linear, true, Smoothing: ParameterSmoothing.Pitch);
    public static ParameterDefinition LfoFrequencyDefinition { get; } =
        new("lfoRate", "LFO frequency", 0.5, MinimumLfoFrequency, MaximumLfoFrequency,
            ParameterUnit.Hertz, ParameterScale.Linear, true, Smoothing: ParameterSmoothing.Pitch);
    public static ParameterDefinition DepthDefinition { get; } =
        new("depthHz", "Modulation depth", 100, 0, MaximumDepth,
            ParameterUnit.Hertz, ParameterScale.Linear, false, Smoothing: ParameterSmoothing.Shape);

    // The fixed sine LFO spans [-1, 1]; the DSP enforces a 20 Hz floor.
    public ParameterRange FrequencyModulationRange => new(
        Math.Max(20, BaseFrequencyHertz - DepthHertz),
        BaseFrequencyHertz + DepthHertz);

    public void Validate()
    {
        ValidateRange(BaseFrequencyHertz, BaseFrequencyDefinition, nameof(BaseFrequencyHertz));
        ValidateRange(LfoFrequencyHertz, LfoFrequencyDefinition, nameof(LfoFrequencyHertz));
        ValidateRange(DepthHertz, DepthDefinition, nameof(DepthHertz));
    }

    private static void ValidateRange(double value, ParameterDefinition definition, string name)
    {
        if (!definition.IsValidBaseValue(value))
            throw new ArgumentOutOfRangeException(name, value,
                $"Expected {definition.Minimum} to {definition.Maximum}.");
    }
}
