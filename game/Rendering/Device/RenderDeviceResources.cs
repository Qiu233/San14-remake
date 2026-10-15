using Godot;

/// <summary>Owns handles allocated in dependency order on the render thread.
/// Retirement removes each handle only after its release succeeds.</summary>
internal sealed class RenderDeviceResources(string allocationFailureMessage) : IAsyncDisposable
{
    private readonly List<Rid> remaining = new();
    private readonly object lifetimeGate = new();
    private Task? disposal;

    public int Count => remaining.Count;
    public bool Contains(Rid resource) => remaining.Contains(resource);

    public Rid Own(Rid resource)
    {
        if (!resource.IsValid) throw new InvalidDataException(allocationFailureMessage);
        remaining.Add(resource);
        return resource;
    }

    public void Release(RenderingDevice rd)
    {
        while (remaining.Count > 0)
        {
            rd.FreeRid(remaining[^1]);
            remaining.RemoveAt(remaining.Count - 1);
        }
    }

    public void ReleaseAfterConstructionFailure(RenderingDevice rd, Exception constructionFailure, string identity)
    {
        try { Release(rd); }
        catch (Exception retirementFailure)
        {
            var partial = new NativeMapBorrowerRetirementPackage(identity + "/" + Guid.NewGuid().ToString("N"), this);
            partial.RetainFailure();
            throw new NativeMapBorrowerConstructionException(constructionFailure, retirementFailure, partial);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (lifetimeGate)
        {
            if (disposal is null || disposal.IsFaulted || disposal.IsCanceled)
                disposal = TerrainDeviceTexture.OnRenderThreadAsync(rd =>
                {
                    Release(rd);
                    return true;
                });
            return new(disposal);
        }
    }
}
