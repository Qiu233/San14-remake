using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record SourceResourceRegistrationFixedIdentity(uint ObjectKey,
    uint DefinitionResourceId, string DefinitionSha256, uint TextureContextResourceId,
    string TextureSha256, int LayerCount);

/// <summary>The three literal Static resources used by the class66 loader.
/// Resolves complete original declarations and G1Ts. These are source owners;
/// a device registration supplies actual readiness and opaque remake handles.</summary>
public static class SourceResourceRegistrationFixed
{
    public const string Archive = "system";
    public const uint StaticClassId = 0xFF7DBFD4;
    public const uint TextureInterfaceClassId = 0xB672FAA2;
    public static IReadOnlyList<SourceResourceRegistrationFixedIdentity> Identities { get; } =
        Array.AsReadOnly(new SourceResourceRegistrationFixedIdentity[]
        {
            new(0x80F9DB7B, 0x93FB3280,
                "4082FCB18D13F992407B2168F0105B9C86F53D095E2BA630802AD0BCDF19FF12",
                0xFB9EBD24, "D3740EDBC260794E229C4DF6AC5330735AEA4E1BDF19082BA9B40B0815D025F8", 1),
            new(0x7F97260C, 0x718CCB8B,
                "67115E16E1B21065B4F04E25109F8182AB027B7DBD38B98979CB02BCFD1291B9",
                0x6A95FB87, "7EC326DB9A2228F0A6BBE24180568967856F9ACFF2D673DE14AF970BA263454A", 3),
            new(0x54F8C0DE, 0x718CCB8B,
                "67115E16E1B21065B4F04E25109F8182AB027B7DBD38B98979CB02BCFD1291B9",
                0x90E33DF5, "C7DBE1A1CF81E9B09BBC9716BFF836D2DF51CDF6B711D7792AF584169A352D7A", 4)
        });

    public static IReadOnlyList<StaticTextureBinding> LoadGameDefault(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var documents = new Dictionary<uint, LoadedObjects>();
        var bindings = new List<StaticTextureBinding>(Identities.Count);
        foreach (SourceResourceRegistrationFixedIdentity identity in Identities)
        {
            if (!documents.TryGetValue(identity.DefinitionResourceId, out LoadedObjects? definitions))
                documents.Add(identity.DefinitionResourceId,
                    definitions = repository.LoadObjects(Archive, identity.DefinitionResourceId));
            ValidateDefinitions(definitions, identity);
            StaticTextureBinding binding = StaticTextureResources.Load(repository,
                definitions, Archive, [identity.ObjectKey]).Single();
            Validate(binding, identity);
            bindings.Add(binding);
        }
        return bindings.AsReadOnly();
    }

    /// <summary>All three exact resources have one mip and four bytes per
    /// physical layer. The native array uploader's ascending layer reads have
    /// a unique contiguous order; no unresolved multi-mip array layout is used.</summary>
    public static byte[] GetNativeLayerBytes(StaticTextureBinding binding, int layer)
    {
        ArgumentNullException.ThrowIfNull(binding);
        SourceResourceRegistrationFixedIdentity identity = Identities.SingleOrDefault(value =>
            value.ObjectKey == binding.Definition.ObjectId)
            ?? throw new InvalidDataException("Fixed source layer requires an exact default source key.");
        Validate(binding, identity);
        if ((uint)layer >= identity.LayerCount)
            throw new ArgumentOutOfRangeException(nameof(layer));
        return binding.Resource.Source.Bytes.AsSpan(binding.Image.EncodedDataRange.Offset + layer * 4, 4).ToArray();
    }

    /// <summary>Check a retained binding against its exact native fixed-key source.
    /// Array layers remain physical layers in the selected image; TID stays0.</summary>
    public static void Validate(StaticTextureBinding binding,
        SourceResourceRegistrationFixedIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(identity);
        if (!Identities.Contains(identity))
            throw new InvalidDataException("Fixed source registration requires one of the three exact native identities.");
        ValidateDefinitions(binding.Definitions, identity);
        DokObject definition = binding.Definition;
        if (definition.ObjectId != identity.ObjectKey || definition.Kind != DokObjectKind.Inline ||
            definition.ClassId != StaticClassId || definition.Properties.Count != 2 ||
            Scalar(definition, 0x6C7321D2, 5) != identity.TextureContextResourceId ||
            Scalar(definition, 0x00200571, 4) != 0 || binding.ImageIndex != 0 ||
            !ReferenceEquals(binding.ContextReference, definition.Properties.Single(value => value.NameHash == 0x6C7321D2)) ||
            !ReferenceEquals(binding.ImageReference, definition.Properties.Single(value => value.NameHash == 0x00200571)))
            throw new InvalidDataException("Fixed source registration differs from its exact Static context/TID declaration.");
        LoadedRdbAsset source = binding.Resource.Source;
        if (source.ResourceUri != GameResourceId.Rdb(Archive, identity.TextureContextResourceId).ToString() ||
            source.FileId != identity.TextureContextResourceId || source.OverrideSource is not null ||
            source.DecodedSha256 != identity.TextureSha256 ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != identity.TextureSha256)
            throw new InvalidDataException("Fixed source texture differs from its complete original G1T identity.");
        G1TextureContainer canonical = G1TextureContainerReader.Read(source.Bytes);
        if (canonical.Textures.Count != 1)
            throw new InvalidDataException("Fixed source texture requires its original single-image directory.");
        G1TextureImage image = canonical.Textures[0];
        if (image.PixelFormat != 1 || image.Width != 1 || image.Height != 1 || image.Depth != 1 ||
            image.MipCount != 1 || image.LayerCount != identity.LayerCount ||
            image.TextureSystem != (identity.LayerCount == 1 ? 0 : 3) ||
            image.EncodedDataRange.Length != identity.LayerCount * 4 ||
            binding.Image.Index != image.Index || binding.Image.PixelFormat != image.PixelFormat ||
            binding.Image.Width != image.Width || binding.Image.Height != image.Height || binding.Image.Depth != image.Depth ||
            binding.Image.MipCount != image.MipCount || binding.Image.RawMipCount != image.RawMipCount ||
            binding.Image.TextureSystem != image.TextureSystem || binding.Image.RawContainerVersion != image.RawContainerVersion ||
            binding.Image.RawFacesAndArrays != image.RawFacesAndArrays || binding.Image.SubresourceLayout != image.SubresourceLayout ||
            binding.Image.LayerCount != image.LayerCount || binding.Image.RawHeader is null ||
            !binding.Image.RawHeader.AsSpan().SequenceEqual(image.RawHeader) ||
            binding.Image.ImageRange != image.ImageRange || binding.Image.EncodedDataRange != image.EncodedDataRange ||
            !binding.Image.Mips.SequenceEqual(image.Mips))
            throw new InvalidDataException("Fixed source texture must preserve its complete 1x1 single-mip physical layers.");
    }

    private static void ValidateDefinitions(LoadedObjects definitions,
        SourceResourceRegistrationFixedIdentity identity)
    {
        if (definitions.Source.ResourceUri != GameResourceId.Rdb(Archive, identity.DefinitionResourceId).ToString() ||
            definitions.Source.FileId != identity.DefinitionResourceId || definitions.Source.OverrideSource is not null ||
            definitions.Source.DecodedSha256 != identity.DefinitionSha256 ||
            Convert.ToHexString(SHA256.HashData(definitions.Source.Bytes)) != identity.DefinitionSha256)
            throw new InvalidDataException("Fixed Static registration requires its exact complete original definition document.");
        DokObject[] matches = definitions.Document.Objects.Where(value => value.ObjectId == identity.ObjectKey).ToArray();
        if (matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline || matches[0].ClassId != StaticClassId)
            throw new InvalidDataException("Fixed Static registration definition is missing, ambiguous or has a different class.");
    }

    private static uint Scalar(DokObject definition, uint hash, ushort type)
    {
        DokProperty[] fields = definition.Properties.Where(value => value.NameHash == hash).ToArray();
        if (fields.Length != 1 || fields[0].TypeCode != type || fields[0].ElementCount != 1 ||
            fields[0].ElementSize != 4 || fields[0].RawValue is not { Length: 4 })
            throw new InvalidDataException("Fixed Static registration requires the exact typed scalar fields.");
        return BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue);
    }
}
