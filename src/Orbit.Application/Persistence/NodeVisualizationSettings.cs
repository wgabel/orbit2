namespace Orbit.Application.Persistence;

/// <summary>Portable presentation preferences; never part of graph history or DSP state.</summary>
public sealed record NodeVisualizationSettings
{
    public bool Visible { get; init; }
    public Guid? PortId { get; init; }
    public string Mode { get; init; } = "waveform";
    public string Timebase { get; init; } = "0.02";
    public string Trigger { get; init; } = "rising";
    public string FftSize { get; init; } = "8192";
    public string Window { get; init; } = "hann";
    public string Smoothing { get; init; } = "0.15";

    public static NodeVisualizationSettings Default { get; } = new();
}

public sealed record SavedNodeVisualization(Guid NodeId, NodeVisualizationSettings Settings);
