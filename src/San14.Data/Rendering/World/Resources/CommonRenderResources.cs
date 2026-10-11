using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public sealed class CommonRenderSettings
{
    public CommonRenderSettings(uint flags8, uint flags0C, uint flags10,
        byte viewAux3F2, byte modelGroupSelector400, SkyAlphaBlendMode skyAlphaBlend, string sourceIdentity)
    {
        Flags8 = flags8; Flags0C = flags0C; Flags10 = flags10;
        ViewAux3F2 = viewAux3F2; ModelGroupSelector400 = modelGroupSelector400;
        SourceIdentity = sourceIdentity;
        InitialAuxiliary = new(flags8, (CommonLightingOptions)flags0C, 0,
            viewAux3F2, modelGroupSelector400, (byte)(flags10 >> 16),
            SuppressOceanDrawing: (flags10 & 1u) != 0, SkyAlphaBlend: skyAlphaBlend);
    }
    public uint Flags8 { get; }
    public uint Flags0C { get; }
    public uint Flags10 { get; }
    public byte ViewAux3F2 { get; }
    public byte ModelGroupSelector400 { get; }
    public string SourceIdentity { get; }
    public CommonAuxiliaryState InitialAuxiliary { get; }
}

/// <summary>Common resource declarations and initial typed settings.
/// The raw resource is authoritative; parsed caller metadata is not a second input.</summary>
public static class CommonRenderResources
{
    public const uint SourceFileId = 0x6D011726;
    public const uint SourceObjectId = 0x899F101B;
    private static readonly ConditionalWeakTable<LoadedObjects, DokDocument> Documents = new();

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
    {
        uint viewGroup = ReadScalar(source, 0xA0BFFAC2, 4);
        uint modelGroup = ReadScalar(source, 0x92E9E9E7, 4);
        if (viewGroup > byte.MaxValue || modelGroup > byte.MaxValue)
            throw new InvalidDataException("Common group selectors require byte values.");
        return new(ReadScalar(source, 0x74C9D1B9, 4), ReadScalar(source, 0xF668216E, 5),
            ReadScalar(source, 0xAABBEBFF, 5), (byte)viewGroup, (byte)modelGroup,
            CommonRenderEncoding.DecodeSkyAlphaBlend(ReadScalar(source, 0xABA611F7, 4)),
            source.Source.ResourceUri + $"#object/{SourceObjectId:X8}@{source.Source.DecodedSha256};CommonViewSetting");
    }

}
