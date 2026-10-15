using San14.Data.Formats;

namespace San14.Data;

/// <summary>A captured main-light declaration and its imported controls.
/// Keeping the original document does not make it a current View row.</summary>
public sealed class LoadedMainLightBindingSource
{
    internal LoadedMainLightBindingSource(LoadedObjects resource, DokObject declaration)
    {
        Resource = resource; Declaration = declaration;
        Settings = MainLightSettings.Import(declaration);
        MainLightResourceReferences references = Settings.Resources;
        RequiredResourceKeys = Array.AsReadOnly(new uint[]
        {
            references.SpecularMap, 0x80F9DB7B, references.DiffuseMap, references.IndexedRamp,
            references.HairHighlight, references.OutlineColor, 0x7F97260C, 0x54F8C0DE,
            references.EyeTwinkle, references.HdrLut
        }.Where(key => key != 0).Distinct().ToArray());
    }

    public LoadedObjects Resource { get; }
    public DokObject Declaration { get; }
    public MainLightSettings Settings { get; }
    public uint GroupMask => Settings.GroupMask;
    public uint GroupMaskMain => Settings.GroupMaskMain;
    public IReadOnlyList<uint> RequiredResourceKeys { get; }
}

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
        if (resource.Source.ResourceUri != uri || resource.Source.FileId != ResourceId)
            throw new InvalidDataException("Game default main light requires the selected KIDSSystemResource/43AE6F41 library.");
        DokObject[] matches = resource.Document.Objects.Where(value => value.ObjectId == ObjectId).ToArray();
        if (matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline || matches[0].ClassId != ClassId)
            throw new InvalidDataException("Native Game default HDRI requires the unique inline9E08F950 class66ECB240 declaration.");
        DokObject declaration = matches[0];
        return new(resource, declaration);
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

}
