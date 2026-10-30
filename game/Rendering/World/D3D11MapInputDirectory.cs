/// <summary>Only the generated bank of one map opening. The caller transfers
/// it to the map on adoption and disposes it after the native session closes.</summary>
internal sealed class D3D11MapInputDirectory : IDisposable
{
    private bool disposed;
    internal string Root { get; }
    internal string Source { get; }

    internal D3D11MapInputDirectory(string requestedSource)
    {
        string requested = Path.GetFullPath(requestedSource);
        Root = Path.Combine(Path.GetDirectoryName(requested)!, "openings", Guid.NewGuid().ToString("N"));
        Source = Path.Combine(Root, Path.GetFileName(requested));
        Directory.CreateDirectory(Root);
    }

    public void Dispose()
    {
        if (disposed) return;
        // Root is the exact directory this owner created, never a caller's
        // general cache path or a path selected by enumerating cache entries.
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        disposed = true;
    }
}
