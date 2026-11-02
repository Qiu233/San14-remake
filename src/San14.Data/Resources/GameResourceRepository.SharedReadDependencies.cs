namespace San14.Data;

public sealed partial class GameResourceRepository
{
    /// <summary>Publishes a shared static preparation's actual source reads to
    /// another exclusive scope, using the same source-generation checks as a
    /// resident cache hit. Does not reread or decode resource payloads.</summary>
    public void ObserveSharedResourceReads(IEnumerable<ResourceReadGeneration> dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        foreach (var dependency in dependencies) ObserveResidentRead(dependency);
    }
}
