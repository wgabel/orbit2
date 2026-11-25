using Orbit.Application.Models;

namespace Orbit.Application.Graphs.Commands;

/// <summary>Apply a profile to a zero-based contiguous range of active note slots in one undoable edit.</summary>
public sealed record HumanizePitchPatternCommand(Guid GraphId, Guid NodeId, int FirstSlot, int SlotCount,
    PitchHumanization Profile) : IGraphCommand;
