using San14.Data.AI.Original;

namespace San14.Data;

public sealed record LoadedOriginalAiScripts(OriginalAiScriptLibrary Definitions,
    CompiledOriginalAiScriptLibrary Compiled, SourceProvenance Source);
public sealed record LoadedOriginalAiPolicy(LoadedOriginalAiResources Behavior,
    LoadedOriginalAiScripts Scripts, OriginalAiBehaviorProgram Program);

public sealed partial class GameResourceRepository
{
    private sealed record OriginalAiScriptCache(string PhysicalPath, long Length,
        long LastWriteTicks, LoadedOriginalAiScripts Loaded);
    private readonly Dictionary<string, OriginalAiScriptCache> _originalAiScripts =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Behavior, string Script), LoadedOriginalAiPolicy> _originalAiPolicies = [];

    public LoadedOriginalAiScripts LoadOriginalAiScripts(string relativePath = "0000/param_s.bin")
    {
        SynchronizeResidentInvalidation();
        var id = GameResourceId.File(relativePath);
        _residentSources.OriginalAiScriptSources.TryAdd(id, 0);
        string uri = id.ToString();
        string path = _overrides.Find(id) ?? SourcePath(relativePath);
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException($"AI script resource is missing: {uri}", path);
        if (_originalAiScripts.TryGetValue(uri, out var cached) &&
            string.Equals(cached.PhysicalPath, path, StringComparison.OrdinalIgnoreCase) &&
            cached.Length == info.Length && cached.LastWriteTicks == info.LastWriteTimeUtc.Ticks)
            return cached.Loaded;
        var (bytes, source) = ReadSource(relativePath);
        try
        {
            var definitions = OriginalAiScriptFormat.Read(bytes);
            var loaded = new LoadedOriginalAiScripts(definitions, definitions.Compile(), source);
            _originalAiScripts[uri] = new(path, info.Length, info.LastWriteTimeUtc.Ticks, loaded);
            return loaded;
        }
        catch (InvalidDataException error)
        {
            _originalAiScripts.Remove(uri);
            throw new InvalidDataException($"Original AI script failed to load: {source.ResourceUri}. {error.Message}", error);
        }
    }

    /// <summary>Normal resources, typed script delegates and behavior-region
    /// cache share one immutable resource owner. This does not claim that all
    /// native tree nodes or original scheduler entries have been implemented.</summary>
    public LoadedOriginalAiPolicy LoadOriginalAiPolicy(string behaviorPath = "0000/bt_exce.bin",
        string scriptPath = "0000/param_s.bin")
    {
        var behavior = LoadOriginalAiResources(behaviorPath);
        var scripts = LoadOriginalAiScripts(scriptPath);
        var key = (GameResourceId.File(behaviorPath).ToString(), GameResourceId.File(scriptPath).ToString());
        if (_originalAiPolicies.TryGetValue(key, out var current) &&
            ReferenceEquals(current.Behavior, behavior) && ReferenceEquals(current.Scripts, scripts)) return current;
        var policy = new LoadedOriginalAiPolicy(behavior, scripts, new OriginalAiBehaviorProgram(behavior.Document, scripts.Compiled));
        _originalAiPolicies[key] = policy; return policy;
    }

    public void InvalidateOriginalAiPolicy()
    {
        var sources = _residentSources.OriginalAiScriptSources.Keys
            .Append(GameResourceId.File("0000/param_s.bin")).Distinct().ToArray();
        InvalidateOriginalAiResources();
        foreach (var source in sources) Invalidate(source);
    }
}
