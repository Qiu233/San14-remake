using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The exact class66 HdrLut Static source and its original BGRA8 volume.
/// Source validation preserves the native body and sampler. A device owner supplies
/// an actual ready texture and an opaque remake handle for registration.</summary>
public static class SourceResourceRegistrationLut
{
    public const string DefinitionArchive = "KIDSSystemResource";
    public const string TextureArchive = "RRPreview";
    public const uint DefinitionResourceId = 0x7DB38795;
    public const uint ObjectKey = 0x7D85DA95;
    public const uint TextureContextResourceId = 0x70996D1C;
    public const uint StaticClassId = 0xFF7DBFD4;
    public const uint TextureInterfaceClassId = 0xB672FAA2;
    public const string DefinitionSha256 = "89D3FA7CB1B6CE19B4CF8C8F8D5E383B2579187B692A27955E6364AA155FC5A5";
    public const string TextureSha256 = "78ADC9A91D910E221406CEEC004B51A0FA04AB3DA05629EABD4AB03CC2BDC9B8";
    public const int EncodedBodyOffset = 56;
    public const int EncodedBodyBytes = 32 * 32 * 32 * 4;
    public const uint NativeResourceFormat = 90;
    public const uint NativeBaseViewFormat = 87;
    public const uint NativeAlternateViewFormat = 91;
    private static readonly byte[] ExpectedHeader = Convert.FromHexString(
        "47543147303630303800020020000000010000000A00000000000000050000000400000012015505111101100C0000000000000000000000");

    public static StaticTextureBinding LoadGameDefault(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return ReadGameDefault(repository.LoadObjects(DefinitionArchive, DefinitionResourceId),
            repository.LoadRdbAsset(TextureArchive, TextureContextResourceId));
    }

    /// <summary>Read this exact source family without widening the shared G1T reader.</summary>
    public static StaticTextureBinding ReadGameDefault(LoadedObjects definitions, LoadedRdbAsset texture)
    {
        ArgumentNullException.ThrowIfNull(definitions); ArgumentNullException.ThrowIfNull(texture);
        ValidateAsset(definitions.Source, DefinitionArchive, DefinitionResourceId, DefinitionSha256);
        ValidateAsset(texture, TextureArchive, TextureContextResourceId, TextureSha256);
        DokDocument canonical = DokReader.Read(definitions.Source.Bytes);
        DokObject[] selected = canonical.Objects.Where(value => value.ObjectId == ObjectKey).ToArray();
        if (selected.Length != 1 || selected[0].Kind != DokObjectKind.Inline ||
            selected[0].ClassId != StaticClassId || selected[0].Properties.Count != 2)
            throw new InvalidDataException("HdrLut registration requires its exact inline Static declaration.");
        DokObject definition = selected[0];
        DokProperty context = Field(definition, 0x6C7321D2, 5);
        DokProperty imageReference = Field(definition, 0x00200571, 4);
        if (BinaryPrimitives.ReadUInt32LittleEndian(context.RawValue) != TextureContextResourceId ||
            BinaryPrimitives.ReadUInt32LittleEndian(imageReference.RawValue) != 0)
            throw new InvalidDataException("HdrLut registration differs from its declared context resource and TID0.");
        LoadedRdbAsset owned = texture with { Bytes = texture.Bytes.ToArray() };
        G1TextureContainer container = ReadExactVolume(owned.Bytes);
        var result = new StaticTextureBinding(new(canonical, definitions.Source), definition,
            context, imageReference, new(container, owned), 0);
        Validate(result);
        return result;
    }

    public static void Validate(StaticTextureBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ValidateAsset(binding.Definitions.Source, DefinitionArchive, DefinitionResourceId, DefinitionSha256);
        ValidateAsset(binding.Resource.Source, TextureArchive, TextureContextResourceId, TextureSha256);
        DokProperty context = Field(binding.Definition, 0x6C7321D2, 5);
        DokProperty imageReference = Field(binding.Definition, 0x00200571, 4);
        if (binding.Definition.ObjectId != ObjectKey || binding.Definition.ClassId != StaticClassId ||
            binding.Definition.Kind != DokObjectKind.Inline || binding.ImageIndex != 0 ||
            binding.Definition.Properties.Count != 2 ||
            BinaryPrimitives.ReadUInt32LittleEndian(context.RawValue) != TextureContextResourceId ||
            BinaryPrimitives.ReadUInt32LittleEndian(imageReference.RawValue) != 0 ||
            !ReferenceEquals(binding.ContextReference, context) ||
            !ReferenceEquals(binding.ImageReference, imageReference))
            throw new InvalidDataException("HdrLut binding has a different source object, context resource, or texture index.");
        G1TextureContainer container = binding.Resource.Container;
        G1TextureContainer canonical = ReadExactVolume(binding.Resource.Source.Bytes);
        G1TextureImage actual = binding.Image;
        G1TextureImage expected = canonical.Textures[0];
        if (container.Textures.Count != 1 || container.RawVersion != canonical.RawVersion ||
            container.Platform != canonical.Platform || container.SourceByteLength != canonical.SourceByteLength ||
            container.TableOffset != canonical.TableOffset || !container.RawOffsets.SequenceEqual(canonical.RawOffsets) ||
            !container.RawHeader.AsSpan().SequenceEqual(canonical.RawHeader) || actual.Index != 0 || actual.PixelFormat != 1 ||
            actual.TextureSystem != 2 || actual.Width != 32 || actual.Height != 32 || actual.Depth != 32 ||
            actual.LayerCount != 1 || actual.MipCount != 1 || actual.RawMipCount != 1 ||
            actual.RawFacesAndArrays != 0 || actual.RawContainerVersion != "0600" ||
            actual.SubresourceLayout != G1TextureSubresourceLayout.VolumeMipChain ||
            actual.EncodedDataRange != expected.EncodedDataRange || actual.ImageRange != expected.ImageRange ||
            !actual.RawHeader.AsSpan().SequenceEqual(expected.RawHeader) || actual.Mips.Count != 1 ||
            actual.Mips[0] != expected.Mips[0])
            throw new InvalidDataException("HdrLut binding must retain its complete 32x32x32 BGRA8 single-mip volume.");
    }

    public static ReadOnlyMemory<byte> GetNativeBody(StaticTextureBinding binding)
    {
        Validate(binding);
        return binding.Resource.Source.Bytes.AsMemory(EncodedBodyOffset, EncodedBodyBytes);
    }

    public static G1TextureSamplerRecipe ReadSampler(StaticTextureBinding binding)
    {
        Validate(binding);
        return G1TextureSamplerReader.ReadRetainedSource(binding.Image);
    }

    private static G1TextureContainer ReadExactVolume(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != EncodedBodyOffset + EncodedBodyBytes ||
            !bytes[..EncodedBodyOffset].SequenceEqual(ExpectedHeader))
            throw new InvalidDataException("HdrLut volume header/body differs from its exact original G1T source.");
        var body = new G1EncodedRange(EncodedBodyOffset, EncodedBodyBytes);
        var mip = new G1TextureMip(0, 32, 32, 32 * 32 * 4, body, 32);
        var image = new G1TextureImage(0, 1, 32, 32, 1, 1, 2, 1, 0,
            G1TextureSubresourceLayout.VolumeMipChain, new(36, bytes.Length - 36), body,
            bytes.Slice(36, 20).ToArray(), Array.AsReadOnly(new[] { mip }), 32, "0600");
        return new("0600", 10, bytes.Length, 32, [4], bytes[..36].ToArray(),
            Array.AsReadOnly(new[] { image }));
    }

    private static void ValidateAsset(LoadedRdbAsset source, string archive, uint fileId, string sha)
    {
        if (source.ResourceUri != GameResourceId.Rdb(archive, fileId).ToString() || source.FileId != fileId ||
            source.OverrideSource is not null || source.DecodedSha256 != sha ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != sha)
            throw new InvalidDataException("HdrLut registration requires its complete original definition and G1T identities.");
    }

    private static DokProperty Field(DokObject definition, uint hash, uint type)
    {
        DokProperty[] values = definition.Properties.Where(value => value.NameHash == hash).ToArray();
        if (values.Length != 1 || values[0].TypeCode != type || values[0].ElementCount != 1 ||
            values[0].ElementSize != 4 || values[0].RawValue is not { Length: 4 })
            throw new InvalidDataException("HdrLut Static declaration requires its two exact typed scalar fields.");
        return values[0];
    }
}
