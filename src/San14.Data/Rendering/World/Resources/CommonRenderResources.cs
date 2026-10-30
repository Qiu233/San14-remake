using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public sealed class CommonRenderSettings
{
    public CommonRenderSettings(uint flags8, uint flags0C, uint flags10,
        byte viewAux3F2, byte modelGroupSelector400, SkyAlphaBlendMode skyAlphaBlend, string sourceIdentity,
        uint? directionalLocationCheckType = null, int? directionalForceSetIndex = null, uint? flags14 = null,
        CommonClipPlaneConfiguration? clipPlanes = null)
    {
        Flags8 = flags8; Flags0C = flags0C; Flags10 = flags10; Flags14 = flags14;
        ViewAux3F2 = viewAux3F2; ModelGroupSelector400 = modelGroupSelector400;
        SourceIdentity = sourceIdentity;
        DirectionalLocationCheckType = directionalLocationCheckType;
        DirectionalForceSetIndex = directionalForceSetIndex;
        InitialAuxiliary = new(flags8, (CommonLightingOptions)flags0C, 0,
            viewAux3F2, modelGroupSelector400, (byte)(flags10 >> 16), ClipPlaneMask: clipPlanes?.Mask,
            SuppressOceanDrawing: (flags10 & 1u) != 0, SkyAlphaBlend: skyAlphaBlend)
            { Flags14 = flags14, ClipPlanes = clipPlanes?.Coordinates };
    }
    public uint Flags8 { get; }
    public uint Flags0C { get; }
    public uint Flags10 { get; }
    /// <summary>FlagEX2 after its actual loader. Null means this selected
    /// settings owner has not supplied the independent word.</summary>
    public uint? Flags14 { get; }
    public byte ViewAux3F2 { get; }
    public byte ModelGroupSelector400 { get; }
    public string SourceIdentity { get; }
    public uint? DirectionalLocationCheckType { get; }
    // This saved selector also supplies the initial shadow angle mode. Current
    // auxiliary state has a separate runtime writer and remains independent.
    public int? DirectionalForceSetIndex { get; }
    public CommonAuxiliaryState InitialAuxiliary { get; }
}

/// <summary>Common resource declarations and initial typed settings.
/// The raw resource is authoritative; parsed caller metadata is not a second input.</summary>
public static class CommonRenderResources
{
    public const uint SourceFileId = 0x6D011726;
    public const uint SourceObjectId = 0x899F101B;
    private static readonly ConditionalWeakTable<LoadedObjects, DokDocument> Documents = new();
    private static readonly ConditionalWeakTable<LoadedObjects, CommonRenderSettings> Settings = new();

    public static uint ReadScalar(LoadedObjects source, uint propertyName, uint type,
        uint objectId = SourceObjectId)
    {
        var document = Documents.GetValue(source, value => DokReader.Read(value.Source.Bytes));
        var declaration = document.Objects.Single(value => value.ObjectId == objectId);
        if (declaration.Kind != DokObjectKind.Inline || declaration.ClassId != 0xEF3D849D)
            throw new InvalidDataException("Common settings require a CommonViewSetting declaration.");
        var property = declaration.Properties.Single(value => value.NameHash == propertyName);
        if (property.TypeCode != type || property.ElementCount != 1 || property.RawValue.Length != 4)
            throw new InvalidDataException("Common setting requires its declared scalar type.");
        return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
    }

    public static CommonRenderSettings ImportSettings(LoadedObjects source)
        => Settings.GetValue(source, ReadSettings);

    private static uint? ReadOptionalScalar(LoadedObjects source, uint propertyName, uint type)
    {
        var document = Documents.GetValue(source, value => DokReader.Read(value.Source.Bytes));
        var declaration = document.Objects.Single(value => value.ObjectId == SourceObjectId);
        if (!declaration.Properties.Any(value => value.NameHash == propertyName)) return null;
        return ReadScalar(source, propertyName, type);
    }

    private static CommonRenderSettings ReadSettings(LoadedObjects source)
    {
        uint viewGroup = ReadScalar(source, 0xA0BFFAC2, 4);
        uint modelGroup = ReadScalar(source, 0x92E9E9E7, 4);
        if (viewGroup > byte.MaxValue || modelGroup > byte.MaxValue)
            throw new InvalidDataException("Common group selectors require byte values.");
        return new(ReadScalar(source, 0x74C9D1B9, 4), ReadScalar(source, 0xF668216E, 5),
            ReadScalar(source, 0xAABBEBFF, 5), (byte)viewGroup, (byte)modelGroup,
            CommonRenderEncoding.DecodeSkyAlphaBlend(ReadScalar(source, 0xABA611F7, 4)),
            source.Source.ResourceUri + $"#object/{SourceObjectId:X8}@{source.Source.DecodedSha256};CommonViewSetting",
            ReadOptionalScalar(source, 0x6AEA4989, 4),
            ReadOptionalScalar(source, 0x00D3B791, 4) is uint force ? unchecked((int)force) : null,
            // FlagEX2 defaults to zero in the loader, which forces bit0.
            // It is independent of FlagEX/Flags10 and the constructor 0xF.
            (ReadOptionalScalar(source, 0xF4B6AF8D, 5) ?? 0u) | 1u,
            CommonClipPlaneSource.ReadInitialSettings(source));
    }

}
