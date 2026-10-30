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

/// <summary>Imports an explicitly selected system/908AB6E2 Perspective Camera.
/// Bindings are retained literals from the paired native descriptor, loader and constructor audit;
/// serialized quaternion components are copied without normalization.</summary>
public static class MapCameraConstructionSource
{
    private sealed class LoadedCameraDeclarations
    {
        public readonly object Gate = new();
        public readonly Dictionary<uint, LoadedMapCameraConstructionSource> Cameras = new();
    }
    private static readonly ConditionalWeakTable<LoadedObjects, LoadedCameraDeclarations> LoadedDeclarations = new();
    public const uint SourceFileId = 0x908AB6E2;
    public const uint PerspectiveCameraClassId = 0x23E7C64E;
    public const string SourceResourceUri = "san14://rdb/system/908AB6E2";

    public static LoadedMapCameraConstructionSource ReadPerspective(LoadedObjects resource, uint objectId)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (objectId is not (0x38D04576u or 0x36C14145u or 0x1F3FB203u or 0x937EEA62u))
            throw new InvalidDataException("Source Camera construction requires one explicitly selected stock Perspective Camera.");
        var loaded = LoadedDeclarations.GetValue(resource, _ => new());
        lock (loaded.Gate)
        {
            if (loaded.Cameras.TryGetValue(objectId, out var found)) return found;
            var source = ReadLoadedPerspective(resource, objectId);
            loaded.Cameras.Add(objectId, source);
            return source;
        }
    }

    private static LoadedMapCameraConstructionSource ReadLoadedPerspective(LoadedObjects resource, uint objectId)
    {
        LoadedRdbAsset source = resource.Source;
        if (source is null || source.Bytes is null || source.FileId != SourceFileId ||
            source.ResourceUri != SourceResourceUri)
            throw new InvalidDataException("Source Camera construction requires the supported system camera library.");

        var declaration = ReadCameraDeclaration(resource.Document, objectId);
        string origin = SourceResourceUri + $"#object/{objectId:X8}@{source.DecodedSha256}";
        return new(resource, declaration.Camera, declaration.Defaults, declaration.InitialPrefix, origin);
    }

    internal static (DokObject Camera, CameraDeclarationDefaults Defaults, byte[] InitialPrefix) ReadCameraDeclaration(
        DokDocument document, uint objectId)
    {
        DokObject[] selected = document.Objects.Where(item => item.ObjectId == objectId).ToArray();
        if (selected.Length != 1 || selected[0].Kind != DokObjectKind.Inline ||
            selected[0].ClassId != PerspectiveCameraClassId || selected[0].Reference is not null)
            throw new InvalidDataException("Source Camera construction requires one inline Perspective Camera declaration.");
        DokObject camera = selected[0];
        return (camera, CameraDeclarationImport.Read(camera), CameraDeclarationEncoding.ImportInitialPrefix(camera));
    }
}
