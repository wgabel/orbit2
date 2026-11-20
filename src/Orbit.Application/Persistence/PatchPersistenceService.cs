namespace Orbit.Application.Persistence;

/// <summary>Coalesces edits into one serialized write; never overlaps writes or saves runtime audio state.</summary>
public sealed class PatchPersistenceService(IPatchStorage storage, PatchDocumentSerializer serializer) : IDisposable
{
    private CancellationTokenSource? pending;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private bool disposed;

    public async Task<PatchDocument?> LoadAsync()
    {
        var json = await storage.LoadAsync();
        return json is null ? null : serializer.Deserialize(json);
    }

    public async Task ScheduleSaveAsync(Func<PatchDocument> capture)
    {
        if (disposed) return;
        pending?.Cancel();
        using var cancellation = new CancellationTokenSource();
        pending = cancellation;
        try
        {
            await Task.Delay(350, cancellation.Token);
            await writeGate.WaitAsync(cancellation.Token);
            try
            {
                cancellation.Token.ThrowIfCancellationRequested();
                await storage.SaveAsync(serializer.Serialize(capture()));
            }
            finally { writeGate.Release(); }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally { if (ReferenceEquals(pending, cancellation)) pending = null; }
    }

    public void Dispose()
    {
        disposed = true;
        pending?.Cancel();
    }
}
