namespace Orbit.Domain.Graphs;

/// <summary>A rejected graph operation that would violate a graph invariant.</summary>
public sealed class GraphValidationException : InvalidOperationException
{
    public GraphValidationException(string message) : base(message)
    {
    }
}
