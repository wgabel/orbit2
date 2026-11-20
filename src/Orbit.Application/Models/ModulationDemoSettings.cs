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

    public void Validate()
    {
        ValidateRange(BaseFrequencyHertz, MinimumBaseFrequency, MaximumBaseFrequency,
            nameof(BaseFrequencyHertz));
        ValidateRange(LfoFrequencyHertz, MinimumLfoFrequency, MaximumLfoFrequency,
            nameof(LfoFrequencyHertz));
        ValidateRange(DepthHertz, 0, MaximumDepth, nameof(DepthHertz));
    }

    private static void ValidateRange(double value, double minimum, double maximum, string name)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(name, value, $"Expected {minimum} to {maximum}.");
    }
}
