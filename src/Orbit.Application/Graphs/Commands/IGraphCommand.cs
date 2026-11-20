namespace Orbit.Application.Graphs.Commands;

/// <summary>A requested graph edit. Only a patch session can execute it.</summary>
public interface IGraphCommand
{
    Guid GraphId { get; }
}
