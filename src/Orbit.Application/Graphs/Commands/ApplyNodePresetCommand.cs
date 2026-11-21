using Orbit.Application.Presets;

namespace Orbit.Application.Graphs.Commands;

/// <summary>Apply an independent preset configuration as one graph edit.</summary>
public sealed record ApplyNodePresetCommand(Guid GraphId, Guid NodeId, NodePreset Preset) : IGraphCommand;
