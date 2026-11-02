using San14.Data.AI.Original;

namespace San14.Data;

public sealed record LoadedOriginalAiResources(OriginalAiBehaviorDocument Document,
    SourceProvenance Source);

public sealed partial class GameResourceRepository
{
    private sealed record OriginalAiResourceCache(string PhysicalPath, long Length,
        long LastWriteTicks, LoadedOriginalAiResources Loaded);

    private readonly Dictionary<string, OriginalAiResourceCache> _originalAiResources =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Loads normal installed/overridden strategy data once for its
    /// resource owner. Resolving a replacement invalidates this parsed cache;
    /// no executable, decompilation or offline execution cache is consulted.</summary>
    public LoadedOriginalAiResources LoadOriginalAiResources(
        string relativePath = "0000/bt_exce.bin")
    {
        SynchronizeResidentInvalidation();
        var id = GameResourceId.File(relativePath);
        _residentSources.OriginalAiBehaviorSources.TryAdd(id, 0);
        string resourceUri = id.ToString();
        string physicalPath = _overrides.Find(id) ?? SourcePath(relativePath);
        var info = new FileInfo(physicalPath);
        if (!info.Exists) throw new FileNotFoundException($"AI resource is missing: {resourceUri}", physicalPath);
        long length = info.Length;
        long ticks = info.LastWriteTimeUtc.Ticks;
        if (_originalAiResources.TryGetValue(resourceUri, out var cached) &&
            string.Equals(cached.PhysicalPath, physicalPath, StringComparison.OrdinalIgnoreCase) &&
            cached.Length == length && cached.LastWriteTicks == ticks)
            return cached.Loaded;
        var (bytes, source) = ReadSource(relativePath);
        try
        {
            var loaded = new LoadedOriginalAiResources(OriginalAiBehaviorReader.Read(bytes), source);
            _originalAiResources[resourceUri] = new(physicalPath, length, ticks, loaded);
            return loaded;
        }
        catch (InvalidDataException error)
        {
            // Never fall back to a previous tree or the installed version when
            // an override is malformed.
            _originalAiResources.Remove(resourceUri);
            throw new InvalidDataException($"Original AI resource failed to load: {source.ResourceUri} " +
                $"({source.PhysicalPath}). {error.Message}", error);
        }
    }

    /// <summary>Resource update owners call this after a replacement when they
    /// preserve file timestamps. Ordinary loads also detect changed path/size/time.</summary>
    public void InvalidateOriginalAiResources(string? relativePath = null)
    {
        // Invalidate the shared source as well as this reader's parsed view;
        // otherwise same-size/time replacement would reuse resident old bytes.
        GameResourceId[] sources = relativePath is null
            ? _residentSources.OriginalAiBehaviorSources.Keys
                .Append(GameResourceId.File("0000/bt_exce.bin")).Distinct().ToArray()
            : [GameResourceId.File(relativePath)];
        foreach (var source in sources) Invalidate(source);
    }
}
