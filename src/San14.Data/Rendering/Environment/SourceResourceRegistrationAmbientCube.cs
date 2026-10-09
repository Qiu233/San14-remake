using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The exact Static ambient-specular resource referenced by the Game
/// default class66 source. This is an original file Cube, with seven retained
/// BC6H mip levels. It is independent of the generated Sky cube owners.</summary>
public static class SourceResourceRegistrationAmbientCube
{
    public const uint ObjectKey = 0x5A53AD58;
    public const uint DefinitionResourceId = 0x7DB38795;
    public const string DefinitionArchive = "KIDSSystemResource";
    public const string DefinitionSha256 = "89D3FA7CB1B6CE19B4CF8C8F8D5E383B2579187B692A27955E6364AA155FC5A5";
    public const uint TextureContextResourceId = 0xDF292339;
    public const string TextureArchive = "RRPreview";
    public const string TextureSha256 = "0DE62FDCD990277879D5F066277621005BF80B63B5140F94C694791142CF5705";
    public const uint StaticClassId = 0xFF7DBFD4;
    public const uint TextureInterfaceClassId = 0xB672FAA2;
    public const uint NativeViewFormat = 95;
    public const int FaceCount = 6;
    public const int MipCount = 7;

    public static StaticTextureBinding LoadGameDefault(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return ReadGameDefault(repository.LoadObjects(DefinitionArchive, DefinitionResourceId),
            repository.LoadRdbAsset(TextureArchive, TextureContextResourceId));
    }

    public static StaticTextureBinding ReadGameDefault(LoadedObjects definitions, LoadedRdbAsset texture)
    {
        ArgumentNullException.ThrowIfNull(definitions); ArgumentNullException.ThrowIfNull(texture);
        ValidateAsset(definitions.Source, DefinitionArchive, DefinitionResourceId, DefinitionSha256);
        ValidateAsset(texture, TextureArchive, TextureContextResourceId, TextureSha256);
        // Reparse complete original bytes so a supplied mutable metadata record
        // cannot select a different Static object or texture context.
        LoadedRdbAsset ownedDefinitions = definitions.Source with { Bytes = definitions.Source.Bytes.ToArray() };
        LoadedRdbAsset ownedTexture = texture with { Bytes = texture.Bytes.ToArray() };
        DokDocument canonical = DokReader.Read(ownedDefinitions.Bytes);
        DokObject[] matches = canonical.Objects.Where(value => value.ObjectId == ObjectKey).ToArray();
        if (matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline ||
            matches[0].ClassId != StaticClassId || matches[0].Properties.Count != 2)
            throw new InvalidDataException("Ambient Cube requires its exact inline Static source declaration.");
        DokProperty context = Field(matches[0], 0x6C7321D2, 5, TextureContextResourceId);
        DokProperty image = Field(matches[0], 0x00200571, 4, 0);
        return new(new(canonical, ownedDefinitions), matches[0], context, image,
            new(ParseExactCube(ownedTexture.Bytes), ownedTexture), 0);
    }

    public static void Validate(StaticTextureBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.ImageIndex != 0)
            throw new InvalidDataException("Ambient Cube requires the original Static TID0.");
        StaticTextureBinding canonical = ReadGameDefault(binding.Definitions, binding.Resource.Source);
        G1TextureImage a = binding.Image, b = canonical.Image;
        if (binding.Definition.ObjectId != ObjectKey || binding.Definition.ClassId != StaticClassId ||
            binding.ImageIndex != 0 || a.TextureSystem != 1 || a.LayerCount != FaceCount ||
            a.Width != b.Width || a.Height != b.Height || a.Depth != 1 || a.PixelFormat != b.PixelFormat ||
            a.MipCount != MipCount || a.RawMipCount != MipCount || a.RawFacesAndArrays != 0 ||
            a.SubresourceLayout != G1TextureSubresourceLayout.MipMajorArray ||
            a.ImageRange != b.ImageRange || a.EncodedDataRange != b.EncodedDataRange ||
            !a.RawHeader.AsSpan().SequenceEqual(b.RawHeader) || !a.Mips.SequenceEqual(b.Mips))
            throw new InvalidDataException("Ambient Cube metadata differs from its original six-face BC6H mip chain.");
        Field(binding.Definition, 0x6C7321D2, 5, TextureContextResourceId);
        Field(binding.Definition, 0x00200571, 4, 0);
    }

    /// <summary>9503E0 reads ascending mip, then faces0..5; the corresponding
    /// D3D subresource index is mip+face*mipCount. No color decode or mip trimming.</summary>
    public static byte[] GetNativeFaceMip(StaticTextureBinding binding, int mip, int face)
    {
        Validate(binding);
        return binding.Image.GetEncodedMip(binding.Resource.Source.Bytes, mip, face).ToArray();
    }

    /// <summary>System1's caller supplies its zero-bias/minLOD extension template
    /// while retaining the file's actual base-header filter and address flags.</summary>
    public static byte[] GetSamplerDescriptor(StaticTextureBinding binding)
    {
        return new G1TextureSamplerRecipe(0x00211000, GetSamplerKey(binding),
            0x15, 1, 1, 3, 0, 1, 4, 0, 0x7F7FFFFF).EncodeD3D11Descriptor();
    }

    /// <summary>The exact source producer key before the ambient consumer's
    /// address override. Each call returns independent bytes.</summary>
    public static byte[] GetSamplerKey(StaticTextureBinding binding)
    {
        Validate(binding);
        return Convert.FromHexString("1094300000000000");
    }

    private static G1TextureContainer ParseExactCube(byte[] bytes)
    {
        if (bytes.Length != 2097068 || !bytes.AsSpan(0, 8).SequenceEqual("GT1G0600"u8) ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8, 4)) != bytes.Length ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12, 4)) != 32 ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16, 4)) != 1 ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(20, 4)) != 10 ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(32, 4)) != 4 ||
            !bytes.AsSpan(36, 8).SequenceEqual(Convert.FromHexString("715E990000102100")))
            throw new InvalidDataException("Ambient Cube requires its exact original0600 single-image system1 header.");
        var levels = new G1TextureMip[MipCount]; int total = 0;
        for (int mip = 0; mip < MipCount; mip++)
        {
            int side = 512 >> mip, count = checked(((side + 3) / 4) * ((side + 3) / 4) * 16);
            levels[mip] = new(mip, side, side, count, null);
            total = checked(total + count * FaceCount);
        }
        if (total != bytes.Length - 44)
            throw new InvalidDataException("Ambient Cube native mip/face reads do not cover the original encoded body.");
        var image = new G1TextureImage(0, 0x5E, 512, 512, MipCount, MipCount, 1, FaceCount, 0,
            G1TextureSubresourceLayout.MipMajorArray, new(36, bytes.Length - 36), new(44, total),
            bytes.AsSpan(36, 8).ToArray(), Array.AsReadOnly(levels), 1, "0600");
        return new("0600", 10, bytes.Length, 32, [4], bytes.AsSpan(0, 36).ToArray(), [image]);
    }

    private static DokProperty Field(DokObject definition, uint hash, ushort type, uint expected)
    {
        DokProperty[] matches = definition.Properties.Where(value => value.NameHash == hash).ToArray();
        if (matches.Length != 1 || matches[0].TypeCode != type || matches[0].ElementCount != 1 ||
            matches[0].ElementSize != 4 || matches[0].RawValue.Length != 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(matches[0].RawValue) != expected)
            throw new InvalidDataException($"Ambient Cube Static field{hash:X8} differs from its original typed value.");
        return matches[0];
    }

    private static void ValidateAsset(LoadedRdbAsset source, string archive, uint id, string sha)
    {
        if (source.ResourceUri != GameResourceId.Rdb(archive, id).ToString() || source.FileId != id ||
            source.OverrideSource is not null || source.DecodedSha256 != sha ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != sha)
            throw new InvalidDataException("Ambient Cube requires the exact complete original definition and texture bytes.");
    }
}
