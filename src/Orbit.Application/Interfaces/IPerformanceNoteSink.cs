namespace Orbit.Application.Interfaces;

public interface IPerformanceNoteSink
{
    Task<bool> SendPerformanceNoteAsync(string soundId, Guid generation, int index, int code);
}
