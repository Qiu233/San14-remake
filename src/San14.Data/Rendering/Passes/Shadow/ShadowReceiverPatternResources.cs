using System.Text;

namespace San14.Data;

/// <summary>A resolved current light binding. Null means the actual loader
/// returned no resource; a nonnull key identifies its registered source.</summary>
public sealed record PatternLightTextureSource(uint LightObjectId, uint? TextureObjectId);

/// <summary>The selected camera's physical offline Pattern texture. Dynamic
/// palette wrappers stay with the atmosphere owner and are borrowed at draw.</summary>
public sealed class ShadowReceiverPatternResources
{
    private readonly PatternCameraResourceDeclaration declaration;
    private readonly LoadedPatternCameraResourceSource loader;
    private MapSeasonCameraPackage acceptedPackage;
    private IReadOnlyList<PatternLightTextureSource> acceptedLights;
    private readonly IReadOnlyDictionary<uint, SkyTextureSource?> localTextures;

    private ShadowReceiverPatternResources(LoadedPatternCameraResourceSource loader, MapSeasonCameraPackage package, PatternCameraResourceDeclaration declaration,
        SkyTextureSource offlinePattern, SkyTextureSource? mergedPattern,
        IReadOnlyList<PatternLightTextureSource> lights, Dictionary<uint, SkyTextureSource?> locals)
    {
        this.declaration = declaration;
        this.loader = loader;
        acceptedPackage = package;
        acceptedLights = lights;
        OfflinePattern = offlinePattern;
        MergedPattern = mergedPattern;
        localTextures = new System.Collections.ObjectModel.ReadOnlyDictionary<uint, SkyTextureSource?>(locals);
    }

    public SkyTextureSource OfflinePattern { get; }
    public SkyTextureSource? MergedPattern { get; }
    public IReadOnlyDictionary<uint, SkyTextureSource?> LocalTextures => localTextures;

    public static ShadowReceiverPatternResources Load(GameResourceRepository repository,
        MapSeasonCameraPackage package, string locale, IReadOnlyList<PatternLightTextureSource>? lights = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(package);
        var source = PatternCameraResourceSource.Load(repository, locale);
        var declaration = source.ReadDeclaration(package.Camera, package.SourceEvidence);
        uint key = declaration.OfflinePatternKey == 0 ? source.OfflineFallbackKey : declaration.OfflinePatternKey;
        // These are the actual libraries registered by the current renderer.
        // An unknown producer is not a completed registry lookup returning null.
        var libraries = new[] { package.Objects, repository.LoadObjects("system", 0x93FB3280),
            repository.LoadObjects("system", 0x718CCB8B) };
        var textures = new Dictionary<uint, SkyTextureSource>();
        SkyTextureSource Resolve(uint objectId)
        {
            if (textures.TryGetValue(objectId, out var retained)) return retained;
            var matches = libraries.DistinctBy(value => value.Source.ResourceUri)
                .Where(value => value.Document.Objects.Any(item => item.ObjectId == objectId)).ToArray();
            if (matches.Length != 1)
                throw new NotSupportedException("Pattern sampling requires one supported registered resource owner.");
            var binding = StaticTextureResources.Load(repository, matches[0], "system", [objectId]).Single();
            var texture = SkyTextureSource.Import(binding.Resource, binding.ImageIndex,
                ModelStageTextures.ReadNativeViewFormat(binding.Image));
            if (texture.SourceDimension != 4)
                throw new NotSupportedException("Pattern sampling requires a two-dimensional texture.");
            textures.Add(objectId, texture);
            return texture;
        }
        var selectedLights = lights ?? Array.Empty<PatternLightTextureSource>();
        var locals = selectedLights.ToDictionary(light => light.LightObjectId,
            light => light.TextureObjectId is uint id ? Resolve(id) : null);
        return new(source, package, declaration, Resolve(key),
            declaration.MergedPatternKey == 0 ? null : Resolve(declaration.MergedPatternKey), selectedLights, locals);
    }

    public void RequireCompatible(GameResourceRepository repository, MapSeasonCameraPackage package,
        IReadOnlyList<PatternLightTextureSource>? lights = null)
    {
        var selectedLights = lights ?? Array.Empty<PatternLightTextureSource>();
        if (ReferenceEquals(acceptedPackage, package) && ReferenceEquals(acceptedLights, selectedLights)) return;
        // A selected package can change without changing its retained texture
        // bindings. Loading a different physical generation needs installation.
        var next = loader.ReadDeclaration(package.Camera, package.SourceEvidence);
        if (next.SkylightStaKey != declaration.SkylightStaKey ||
            next.MergedPatternKey != declaration.MergedPatternKey ||
            next.OfflinePatternKey != declaration.OfflinePatternKey)
            throw new NotSupportedException("The receiver Pattern resource generation requires installation before this request.");
        var imported = Load(repository, package, loader.Locale, selectedLights);
        static bool Same(SkyTextureSource? first, SkyTextureSource? second) => first is null || second is null
            ? first is null && second is null
            : first.Identity == second.Identity && first.SamplerDescriptorSha256 == second.SamplerDescriptorSha256;
        if (!Same(imported.OfflinePattern, OfflinePattern) || !Same(imported.MergedPattern, MergedPattern) ||
            imported.LocalTextures.Count != LocalTextures.Count || imported.LocalTextures.Any(pair =>
                !LocalTextures.TryGetValue(pair.Key, out var previous) || !Same(pair.Value, previous)))
            throw new NotSupportedException("The Pattern texture generation requires installation before this request.");
        acceptedPackage = package;
        acceptedLights = selectedLights;
    }

    public void WriteNativeInputs(string inputRoot)
    {
        string root = Path.Combine(inputRoot, "normal-pattern-receiver-input");
        void WriteTexture(string path, SkyTextureSource texture)
        {
            string directory = Path.Combine(root, path);
            Directory.CreateDirectory(directory);
            var files = new Dictionary<string, byte[]>();
            string row = SkyTextureInputEncoding.Add(files, "", "Lit", 0, "pattern", texture);
            foreach (var pair in files) File.WriteAllBytes(Path.Combine(directory, pair.Key), pair.Value);
            File.WriteAllText(Path.Combine(directory, "texture.tsv"), row + "\n", new UTF8Encoding(false));
        }
        WriteTexture("fallback", OfflinePattern);
        File.WriteAllBytes(Path.Combine(root, "merged-presence.bin"), BitConverter.GetBytes(MergedPattern is not null ? 1u : 0u));
        if (MergedPattern is not null) WriteTexture("merged", MergedPattern);
        var rows = new List<string>();
        foreach (var light in acceptedLights)
        {
            string path = "local/" + light.LightObjectId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var texture = LocalTextures[light.LightObjectId];
            rows.Add(FormattableString.Invariant($"{light.LightObjectId} {(texture is not null ? 1 : 0)} {path}"));
            if (texture is not null) WriteTexture(path, texture);
        }
        File.WriteAllLines(Path.Combine(root, "local-textures.tsv"), rows, new UTF8Encoding(false));
    }
}
