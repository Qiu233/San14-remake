using System.Globalization;
using System.Text;

namespace San14.Data;

/// <summary>An immutable declared route import. Original program and encoded
/// textures are prepared before Title. Camera, draft, vertices and current
/// constant values never enter this source owner.</summary>
public sealed class MapRoutePreparedMedia
{
    private static readonly UTF8Encoding Utf8 = new(false);
    internal MapRoutePreparedMedia(string logicalKey, MapRouteDrawResources source, string root)
    {
        LogicalKey = logicalKey;
        Identity = Guid.NewGuid().ToString("N");
        Source = source;
        MapRouteShaderNativeInputs.WriteNativeInputs(source, root);
        Directory = Path.GetFullPath(Path.Combine(root, "route-program-input"));
        File.WriteAllLines(Path.Combine(Directory, "route-gpu-media.txt"),
            ["SAN14_ROUTE_GPU_MEDIA 1", LogicalKey, Identity], Utf8);
    }

    public string LogicalKey { get; }
    public string Identity { get; }
    public string Directory { get; }
    public MapRouteDrawResources Source { get; }
}

/// <summary>The normal Scene's complete four-season route declaration closure.
/// TC and SC borrow this same language-independent media. Namespace matching
/// uses retained descriptor identities only at installation boundaries.</summary>
public sealed class MapRouteStaticResources
{
    private static readonly UTF8Encoding Utf8 = new(false);
    private readonly MapRoutePreparedMedia[] media;
    private MapRouteStaticResources(MapRoutePreparedMedia[] media)
    { this.media = media; Media = Array.AsReadOnly(media); }

    public IReadOnlyList<MapRoutePreparedMedia> Media { get; }

    public static MapRouteStaticResources Load(GameResourceRepository repository,
        LoadedSceneResources scene, string generationDirectory, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(scene);
        var prepared = new List<MapRoutePreparedMedia>();
        // These are actual four source season selections; no simulation clock
        // or live World is constructed to resolve a resource namespace.
        for (int season = 0; season < 4; season++)
        {
            token.ThrowIfCancellationRequested();
            var action = MapSeasonResourceConsumer.LoadActionDatabase(repository,
                MapSeasonResourceConsumer.LoadActionSource(repository, season * 3 + 1));
            using var context = RenderResourceContext.LoadCurrentMap(repository, scene, action, out var seasonBatch);
            using (seasonBatch)
            {
                if (prepared.Any(entry => entry.Source.MatchesContext(context))) continue;
                var source = MapRouteDrawResources.Load(repository, context);
                string key = scene.Scene.Source.ResourceUri + "/route/" + season.ToString(CultureInfo.InvariantCulture);
                string directory = Path.Combine(generationDirectory, season.ToString(CultureInfo.InvariantCulture));
                prepared.Add(new(key, source, directory));
            }
        }
        token.ThrowIfCancellationRequested();
        return new(prepared.ToArray());
    }

    public MapRoutePreparedMedia Require(RenderResourceContext actual)
    {
        foreach (var entry in media)
            if (entry.Source.MatchesContext(actual)) return entry;
        throw new InvalidDataException("The actual route namespace was not included in startup media preparation.");
    }

    public void WriteOpeningReference(string inputRoot)
    {
        System.IO.Directory.CreateDirectory(inputRoot);
        var lines = new List<string> { "SAN14_ROUTE_GPU_MEDIA_REFERENCES 1", media.Length.ToString(CultureInfo.InvariantCulture) };
        foreach (var entry in media)
        {
            lines.Add(entry.LogicalKey);
            lines.Add(entry.Identity);
            lines.Add(entry.Directory);
        }
        File.WriteAllLines(Path.Combine(inputRoot, "route-gpu-media.ref"), lines, Utf8);
    }
}
