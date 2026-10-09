using San14.Data.Formats;

namespace San14.Data;

public sealed record LoadedCameraSkylight(LoadedObjects Resource, DokObject Camera,
    SkylightParameters Parameters);

/// <summary>Read a caller-selected original Perspective Camera, retaining its complete DOK source and provenance.</summary>
public static class CameraSkylightSource
{
    public const uint PerspectiveCameraClassId = 0x23E7C64E;

    public static LoadedCameraSkylight ReadPerspective(LoadedObjects resource, uint objectId)
    {
        ArgumentNullException.ThrowIfNull(resource);
        DokObject[] matches = resource.Document.Objects.Where(item => item.ObjectId == objectId).ToArray();
        if (matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline ||
            matches[0].ClassId != PerspectiveCameraClassId)
            throw new InvalidDataException($"{resource.Source.ResourceUri} object {objectId:X8} requires one inline original Perspective Camera; references require explicit inheritance resolution.");
        try
        {
            SkylightParameters parameters = SkylightParameters.ConstructorDefaults()
                .ApplyProperties(matches[0].Properties);
            return new(resource, matches[0], parameters);
        }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"{resource.Source.ResourceUri} Camera {objectId:X8}: {error.Message}", error); }
    }
}
