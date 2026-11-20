namespace Orbit.Application.Persistence;

/// <summary>One browser-local recovery document; implementations own storage mechanics.</summary>
public interface IPatchStorage
{
    Task<string?> LoadAsync();
    Task SaveAsync(string json);
}
