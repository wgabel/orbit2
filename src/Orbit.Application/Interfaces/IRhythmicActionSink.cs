using Orbit.Application.Models;

namespace Orbit.Application.Interfaces;

public interface IRhythmicActionSink
{
    Task<bool> SendRhythmicActionAsync(string soundId, Guid generation, int index, RhythmicAction action);
}
