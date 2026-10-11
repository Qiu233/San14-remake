using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The exact HDRI declaration created by the native Game initializer
/// from KIDSSystemResource/43AE6F41, object9E08F950. The complete declaration
/// and its source remain available; this does not certify a current View row.</summary>
public sealed record LoadedMainLightBindingSource(LoadedObjects Resource,
    DokObject Declaration, uint GroupMask, uint GroupMaskMain);

public sealed record LoadedMainLightBindingCameraSource(int ViewSlot,
    uint ViewParamObjectId, LoadedPreparedCameraLightingSource Camera);

/// <summary>Loads the concrete native Game default HDRI source. Game+F0 retains
/// its created Data record and passes it to the View Cameras' V450 setters.
/// Current source ownership and effective prepared LightData require observation.</summary>
public static class MainLightBindingSource
{
    public const string Archive = "KIDSSystemResource";
    public const uint ResourceId = 0x43AE6F41;
    public const uint ObjectId = 0x9E08F950;
    public const uint ClassId = 0x66ECB240;
    public const string DecodedSha256 = "75025658F0551D2EFC51FFFEE4B3C6BBE8B4F69CC02BD0D7705C6023213C7E61";
    public const uint ViewObjectId = 0xE1B6A687;
    public const uint CameraResourceId = 0x908AB6E2;

    public static LoadedMainLightBindingSource LoadGameDefault(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return ReadGameDefault(repository.LoadObjects(Archive, ResourceId));
    }

    public static LoadedMainLightBindingSource ReadGameDefault(LoadedObjects resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        string uri = GameResourceId.Rdb(Archive, ResourceId).ToString();
        if (resource.Source.ResourceUri != uri || resource.Source.FileId != ResourceId ||
            !resource.Source.DecodedSha256.Equals(DecodedSha256, StringComparison.OrdinalIgnoreCase) ||
            resource.Source.OverrideSource is not null)
            throw new InvalidDataException("Native Game default HDRI requires the exact original43AE6F41 resource and decoded SHA256.");
        DokObject[] matches = resource.Document.Objects.Where(value => value.ObjectId == ObjectId).ToArray();
        if (matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline || matches[0].ClassId != ClassId)
            throw new InvalidDataException("Native Game default HDRI requires the unique inline9E08F950 class66ECB240 declaration.");
        DokObject declaration = matches[0];
        return new(resource, declaration, Mask(declaration, 0xC0C5A5B5), Mask(declaration, 0x54E95FFC));
    }

    /// <summary>Loads one of the two actual source Cameras targeted by the
    /// native Game HDRI setters: C+48/slot3 or C+58/slot4. Runtime slot identity
    /// and node166's context base are separate observations.</summary>
    public static LoadedMainLightBindingCameraSource LoadSetterCamera(
        GameResourceRepository repository, int viewSlot)
    {
        ArgumentNullException.ThrowIfNull(repository);
        (uint objectId, uint viewParam) = viewSlot switch
        {
            3 => (0x1F3FB203u, 0xBACC070Eu),
            4 => (0x937EEA62u, 0x83CCFE4Fu),
            _ => throw new ArgumentOutOfRangeException(nameof(viewSlot), "The native Game HDRI setter targets only View source slots3 and4.")
        };
        LoadedObjects resource = repository.LoadObjects("system", CameraResourceId);
        if (resource.Source.FileId != CameraResourceId ||
            resource.Source.ResourceUri != GameResourceId.Rdb("system", CameraResourceId).ToString())
            throw new InvalidDataException("The HDRI setter Camera requires the supported camera library.");
        return new(viewSlot, viewParam, PreparedCameraLightingSource.ReadPerspective(resource, objectId));
    }

    /// <summary>CE31D0's primary source mask, with the actual ViewAux+3F2 byte.
    /// Only values1..8 override the producer. The default declaration has80.</summary>
    public static uint PrimaryGroupMask(LoadedMainLightBindingSource source, byte viewAuxOverride)
    {
        ArgumentNullException.ThrowIfNull(source);
        return viewAuxOverride is >= 1 and <= 8 ? 1u << (viewAuxOverride - 1) : source.GroupMaskMain;
    }

    /// <summary>Node166 uses source group8. A nonzero actual ViewAux+3F2 byte
    /// changes the consumer shift modulo32, including values outside1..8.</summary>
    public static uint Node166ExclusionMask(byte viewAuxOverride) =>
        ~(1u << (((viewAuxOverride == 0 ? 8 : viewAuxOverride) - 1) & 31));

    private static uint Mask(DokObject declaration, uint hash)
    {
        DokProperty[] matches = declaration.Properties.Where(value => value.NameHash == hash).ToArray();
        if (matches.Length != 1 || matches[0].TypeCode != 5 || matches[0].ElementCount != 1 || matches[0].RawValue.Length != 4)
            throw new InvalidDataException($"Native Game default HDRI requires one type5/count1 group field{hash:X8}.");
        return BinaryPrimitives.ReadUInt32LittleEndian(matches[0].RawValue);
    }
}
