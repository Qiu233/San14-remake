namespace San14.Data;

/// <summary>Identity of one actual source read, observed before and after its
/// bytes were obtained. Only the opt-in preparation reader publishes it.</summary>
public sealed record ResourceReadGeneration(GameResourceId Id, string FallbackPath, string SelectedPath,
    long Length, long LastWriteUtcTicks)
{
    /// <summary>Explicit source invalidation identity, including replacements
    /// that intentionally preserve the original file's length and timestamp.</summary>
    public long Version { get; init; }
}

/// <summary>A preparation must be discarded when its resource read owners
/// change. This is not permission to substitute another resource or defaults.</summary>
public sealed class ResourceReadGenerationChangedException(GameResourceId resource)
    : IOException("Resource source changed during preparation: " + resource)
{
    public GameResourceId Resource { get; } = resource;
}

public sealed partial class GameResourceRepository
{
    private readonly Action<ResourceReadGeneration>? _readGeneration;

    private ResourceReadGeneration BeginResourceRead(GameResourceId id, string fallback, string selected)
    {
        string fallbackPath = Path.GetFullPath(fallback), selectedPath = Path.GetFullPath(selected);
        if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(_overrides.Find(id) ?? fallbackPath), selectedPath))
            throw new ResourceReadGenerationChangedException(id);
        var file = new FileInfo(selectedPath);
        return new(id, fallbackPath, selectedPath, file.Length, file.LastWriteTimeUtc.Ticks)
        { Version = _residentSources.Version(id) };
    }

    private void VerifyResourceRead(ResourceReadGeneration generation)
    {
        VerifyResourceVersion(generation);
        string selected = Path.GetFullPath(_overrides.Find(generation.Id) ?? generation.FallbackPath);
        if (!StringComparer.OrdinalIgnoreCase.Equals(selected, generation.SelectedPath))
            throw new ResourceReadGenerationChangedException(generation.Id);
        try
        {
            var file = new FileInfo(selected);
            if (file.Length != generation.Length || file.LastWriteTimeUtc.Ticks != generation.LastWriteUtcTicks)
                throw new ResourceReadGenerationChangedException(generation.Id);
        }
        catch (FileNotFoundException) { throw new ResourceReadGenerationChangedException(generation.Id); }
        catch (DirectoryNotFoundException) { throw new ResourceReadGenerationChangedException(generation.Id); }
    }

    private void VerifyResourceVersion(ResourceReadGeneration generation)
    {
        if (_residentSources.Version(generation.Id) != generation.Version)
            throw new ResourceReadGenerationChangedException(generation.Id);
    }

    private void CompleteResourceRead(ResourceReadGeneration generation)
    {
        if (HasResourceChangeNotifications) VerifyResourceVersion(generation);
        else VerifyResourceRead(generation);
        _readGeneration!(generation);
    }
}
