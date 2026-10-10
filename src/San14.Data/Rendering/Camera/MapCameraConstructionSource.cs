using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>An imported camera declaration and its immutable initial settings.</summary>
public sealed class LoadedMapCameraConstructionSource
{
    private readonly byte[] initialPrefix;
    internal LoadedMapCameraConstructionSource(LoadedObjects resource, DokObject camera,
        CameraDeclarationDefaults declaration, byte[] initialPrefix, string constructorOrigin)
    {
        Resource = resource; Camera = camera; Declaration = declaration;
        this.initialPrefix = initialPrefix; ConstructorOrigin = constructorOrigin;
    }
    public LoadedObjects Resource { get; }
    public DokObject Camera { get; }
    public CameraDeclarationDefaults Declaration { get; }
    public CameraOpticsSettings OpticsDefaults => CameraOpticsImport.Defaults(this);
    public string ConstructorOrigin { get; }
    internal byte[] CreateInitialPrefix(string locale)
    {
        _ = MainMapCameraRules.Language(locale);
        return initialPrefix.ToArray();
    }
}

/// <summary>Read the explicitly selected stock system/908AB6E2 Perspective Camera.
/// Bindings are retained literals from the paired native descriptor, loader and constructor audit;
/// serialized quaternion components are copied without normalization.</summary>
public static class MapCameraConstructionSource
{
    private sealed class LoadedCameraDeclarations
    {
        public readonly object Gate=new();
        public readonly Dictionary<uint,LoadedMapCameraConstructionSource> Cameras=new();
    }
    private static readonly ConditionalWeakTable<LoadedObjects,LoadedCameraDeclarations> LoadedDeclarations = new();
    public const uint SourceFileId = 0x908AB6E2;
    public const uint PerspectiveCameraClassId = 0x23E7C64E;
    public const string SourceResourceUri = "san14://rdb/system/908AB6E2";
    public const string SourceSha256 = "3CB072E68CA9A2086D0230E3F9F86AD1B9B10E08CEA521A474CE1057A0E23B06";

    public static LoadedMapCameraConstructionSource ReadPerspective(LoadedObjects resource, uint objectId)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (objectId is not (0x38D04576u or 0x1F3FB203u or 0x937EEA62u))
            throw new InvalidDataException("Source Camera construction requires one explicitly selected stock Perspective Camera.");
        var loaded=LoadedDeclarations.GetValue(resource,_=>new());
        lock(loaded.Gate)
        {
            if(loaded.Cameras.TryGetValue(objectId,out var found))return found;
            var source=ReadLoadedPerspective(resource,objectId);
            loaded.Cameras.Add(objectId,source);
            // The reader's private canonical Resource is the same imported
            // generation. Accepted Camera prefix copies must not import that
            // DOK a second time; pose/lens/matrices remain current packet data.
            LoadedDeclarations.Add(source.Resource,loaded);
            return source;
        }
    }

    private static LoadedMapCameraConstructionSource ReadLoadedPerspective(LoadedObjects resource,uint objectId)
    {
        LoadedRdbAsset source = resource.Source;
        if (source is null || source.Bytes is null || source.FileId != SourceFileId ||
            source.ResourceUri != SourceResourceUri ||
            source.OverrideSource is not null || source.IndexSource is null || source.IndexSource.IsOverride)
            throw new InvalidDataException("Source Camera construction requires the original system908AB6E2 resource identity without overrides.");

        // Keep the decoded object and word values in the same resource generation
        // if the caller later edits its arrays.
        LoadedRdbAsset ownedSource = source with { Bytes = source.Bytes.ToArray() };
        DokDocument canonical = DokReader.Read(ownedSource.Bytes);
        var declaration = ReadCameraDeclaration(canonical, objectId);
        var ownedResource = new LoadedObjects(canonical, ownedSource);
        string origin = SourceResourceUri + $"#object/{objectId:X8}@{source.DecodedSha256}";
        return new(ownedResource, declaration.Camera, declaration.Defaults, declaration.InitialPrefix, origin);
    }

    internal static (DokObject Camera, CameraDeclarationDefaults Defaults, byte[] InitialPrefix) ReadCameraDeclaration(
        DokDocument document, uint objectId)
    {
        DokObject[] selected = document.Objects.Where(item => item.ObjectId == objectId).ToArray();
        if (selected.Length != 1 || selected[0].Kind != DokObjectKind.Inline ||
            selected[0].ClassId != PerspectiveCameraClassId || selected[0].Reference is not null ||
            selected[0].Properties.Count != 1111)
            throw new InvalidDataException("Source Camera construction requires its original inline1111-property Perspective Camera.");
        DokObject camera = selected[0];
        return (camera, CameraDeclarationImport.Read(camera), CameraDeclarationEncoding.ImportInitialPrefix(camera));
    }
}
