namespace Orbit.Application.Persistence;

public sealed record SavedNodeDescription(Guid NodeId, string Alias, string Description)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(Alias) && Description.Length == 0;
}

public sealed record SavedConnectionDescription(Guid ConnectionId, string Label)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(Label);
}

public static class DescriptionLimits
{
    public const int NameLength = 120;
    public const int BodyLength = 16000;

    public static void Validate(SavedNodeDescription value)
    {
        if (value is null || value.NodeId == Guid.Empty || value.Alias is null ||
            value.Alias.Length > NameLength || value.Alias.Contains('\n') || value.Alias.Contains('\r') ||
            value.Description is null || value.Description.Length > BodyLength)
            throw new ArgumentException("Invalid node alias or description.");
    }

    public static void Validate(SavedConnectionDescription value)
    {
        if (value is null || value.ConnectionId == Guid.Empty || value.Label is null ||
            value.Label.Length > NameLength || value.Label.Contains('\n') || value.Label.Contains('\r'))
            throw new ArgumentException("Invalid connection label.");
    }

    public static void Validate(SavedCanvas canvas, IEnumerable<Guid> nodes, IEnumerable<Guid> connections)
    {
        ArgumentNullException.ThrowIfNull(canvas.NodeDescriptions);
        ArgumentNullException.ThrowIfNull(canvas.ConnectionDescriptions);
        var nodeIds = nodes.ToHashSet();
        var connectionIds = connections.ToHashSet();
        foreach (var value in canvas.NodeDescriptions)
        {
            Validate(value);
            if (!nodeIds.Remove(value.NodeId)) throw new ArgumentException("Unknown or duplicate node description target.");
        }
        foreach (var value in canvas.ConnectionDescriptions)
        {
            Validate(value);
            if (!connectionIds.Remove(value.ConnectionId)) throw new ArgumentException("Unknown or duplicate connection description target.");
        }
    }
}
