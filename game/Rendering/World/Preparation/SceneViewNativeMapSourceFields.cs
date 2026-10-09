using System.Buffers.Binary;
using San14.Data;

/// <summary>Finite fresh Main scalar owner outside its accepted3C0 prefix.
/// The saved-property loader and C42070 copy, rather than a lighting fixture,
/// supply the built-in ambient map-occlusion rate. Imported Camera edits need
/// a different current owner; this owner never invents a missing field.</summary>
public sealed class SceneViewNativeMapSourceFields
{
    private readonly LoadedObjects resource;
    private readonly uint objectId;
    private readonly string locale;
    private readonly uint ambientRateWord;
    private bool retained = true;

    private SceneViewNativeMapSourceFields(MapCameraConstructionPacket camera)
    {
        var canonical = MapCameraConstructionSource.ReadPerspective(camera.Source.Resource,
            MainMapCameraAdapter.MainCameraObjectId);
        if (camera.Source.Camera.ObjectId != canonical.Camera.ObjectId ||
            camera.ExecutableLocale is not ("SC" or "TC"))
            throw new InvalidDataException("Native map scalar owner requires its exact source Main Camera.");
        var property = canonical.Camera.Properties[977];
        // SC C54311/C54318 binds indexed property977 to CPU+170;
        // C570EB sets its4-byte extent. TC uses the paired loader.
        if (property.Index != 977 || property.NameHash != 0x08367252 ||
            property.TypeCode != 8 || property.ElementCount != 1 ||
            property.ElementSize != 4 || property.RawValue.Length != 4)
            throw new InvalidDataException("Main SkylightAmbientDORate differs from its actual property977 loader binding.");
        ambientRateWord = BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
        if (!float.IsFinite(BitConverter.UInt32BitsToSingle(ambientRateWord)))
            throw new InvalidDataException("Source Main map-occlusion rate is nonfinite.");
        resource = canonical.Resource;
        objectId = canonical.Camera.ObjectId; locale = camera.ExecutableLocale;
        SourceProvenance = canonical.Resource.Source.ResourceUri + "@" + canonical.Resource.Source.DecodedSha256 +
            ";Main38D/property977-08367252-float1/CPU170;" +
            "SC-C424BE/C424C4-TC-C40E0E/C40E14-source-copy/1330+3CC;" +
            "fresh-owned-retained-no-external-AO-assignment";
    }
    public string SourceProvenance { get; }
    public static SceneViewNativeMapSourceFields CreateFreshRetainedMain(
        MapCameraConstructionPacket initialCamera) => new(initialCamera);

    public float PrepareBuiltinAmbientCameraRate(MapCameraConstructionPacket currentCamera)
    {
        ArgumentNullException.ThrowIfNull(currentCamera);
        if (!retained || currentCamera.FrameNumber < 1 ||
            currentCamera.ExecutableLocale != locale || currentCamera.Source.Camera.ObjectId != objectId ||
            !ReferenceEquals(currentCamera.Source.Resource, resource))
            throw new InvalidDataException("Current Main scalar owner was replaced, edited, or imported.");
        return BitConverter.UInt32BitsToSingle(ambientRateWord);
    }
    public void MarkImportedOrEdited(string actualWriter)
    { ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter); retained = false; }
}
