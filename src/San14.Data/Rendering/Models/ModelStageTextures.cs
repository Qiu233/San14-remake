using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Runtime branches stay explicit; only the verified ordinary PB2 path is implemented.</summary>
public sealed record ModelStageTextureRuntime(bool AlternatePrimaryMode,
    bool SkipEncodedEntries, ushort TextureSlotOffset);

public enum ModelStageTextureOperation { Primary, Special, EncodedPrimary, EncodedSecondary }

/// <summary>Ordered original write, including writes to native sentinel slot 16.</summary>
public sealed record ModelStageTextureEffect(int Order, ModelStageTextureOperation Operation,
    int SourceRecordIndex, int SourceEntryIndex, int SourceTextureSlot, int DestinationSlot,
    ushort SourceEncoded, int? MetadataEntryIndex, string? MetadataName);

public sealed record ModelStageTextureBinding(uint TextureRegister, uint SamplerRegister,
    string TextureName, string SamplerName, int SourceRecordIndex, int SourceEntryIndex,
    int SourceTextureSlot, uint ProxyObjectId, uint TextureFileId, uint RawTextureId,
    int ImageIndex, ushort UvSet, ushort RawPrimaryEncoded, ushort RawSecondaryEncoded,
    ushort RawValue8, ushort RawValue10);

public sealed record ModelStageTexturePlan(string ModelUri, string ModelSha256,
    string TextureTableUri, string TextureTableSha256, string ShaderUri, string ShaderSha256,
    uint ProviderObjectId, int DrawIndex, int ExplicitCombinationIndex, int PixelProgramIndex,
    int SourceRecordIndex, ushort PrimaryCount, short SpecialEntryIndex,
    IReadOnlyList<ModelStageTextureEffect> Effects,
    IReadOnlyList<ModelStageTextureBinding> Bindings);

public sealed record ModelStageTextureImage(ModelStageTextureBinding Binding,
    LoadedTextureContainer Container, G1TextureImage Image, G1TextureSamplerRecipe Sampler);

/// <summary>
/// Original section 10002 -> type19 -> PB2 metadata registrations -> PS slots.
/// Roles come from numeric source registrations and actual selected PS RDEF.
/// Source names never determine a role. Runtime variant selection remains external.
/// </summary>
public static class ModelStageTextures
{
    /// <summary>Native system0 modern-header selector, independent of texture role and proxy TID.</summary>
    public static uint ReadNativeViewFormat(G1TextureImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        // The original 0500 single-mip planar loader has no extended color
        // field and uses the ordinary view. Its sampler reader follows the
        // same legacy format branch rather than inventing an extra header.
        if (image.RawContainerVersion == "0500" && image.TextureSystem == 0 &&
            image.LayerCount == 1 && image.MipCount == 1 &&
            image.RawHeader.Length == 8 && (image.RawHeader[7] >> 4) == 0)
            return ModelTextureFormatMath.Resolve(image.PixelFormat, 0).ShaderViewFormat;
        if (image.TextureSystem != 0 || image.LayerCount != 1 ||
            image.SubresourceLayout != G1TextureSubresourceLayout.SingleLayerMipChain ||
            image.RawHeader.Length < 20 || (image.RawHeader[7] >> 4) != 1)
            throw Error("automatic source view requires a proved system0 image with one explicit extra header");
        // 951A3B/951A3F/951A52: extra+0B (image+19) low bit becomes
        // descriptor+36 bit15, selecting the original ordinary/alternate SRV.
        return ModelTextureFormatMath.Resolve(image.PixelFormat, (byte)(image.RawHeader[19] & 1)).ShaderViewFormat;
    }

    public static ModelStageTexturePlan Resolve(LoadedModelTextureResources model, int drawIndex,
        NamedModelShader provider, LoadedShaderContainer shader, int explicitCombinationIndex,
        ModelStageTextureRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(shader); ArgumentNullException.ThrowIfNull(runtime);
        if (runtime.AlternatePrimaryMode || runtime.SkipEncodedEntries || runtime.TextureSlotOffset != 0)
            throw Error("alternate primary mode, skipped encoded entries and texture slot offsets are unimplemented");
        bool army = provider.Object.ObjectId == 0x0AC3D18D;
        if (provider.Object.ClassId != 0x18EB1E56)
            throw Error("requires the verified PB2 provider creator class");
        if (army && (provider.ShaderFileHash != 0xB90AEDD3 ||
            shader.Source.DecodedSha256 != ArmySkinVertexAdapter.ShaderSha256))
            throw Error("Army state18 requires its exact original B90AEDD3 source profile");
        if (Scalar(provider.Object, ModelShaderResources.G1SPropertyHash) != provider.ShaderFileHash ||
            provider.ShaderFileHash != shader.Source.FileId ||
            shader.Source.ResourceUri != GameResourceId.Rdb("system", provider.ShaderFileHash).ToString())
            throw Error("provider G1S reference does not identify the supplied shader source");
        ValidateHash(shader.Source); ValidateHash(model.Model.Source); ValidateHash(model.TextureTable);
        ValidateLinks(model);
        if (explicitCombinationIndex is not (0 or 1))
            throw Error("only explicit PB2 combinations 0 and 1 are supported");
        G1ShaderResourceBindings metadata = G1ShaderResourceBindingReader.Read(shader.Source.Bytes, explicitCombinationIndex);
        G2ShaderTextureBases descriptor = G2ShaderTextureBaseReader.Read(shader.Source.Bytes);
        if (descriptor.ModeValue != 0 || descriptor.DescriptorFlags != 0x28 || descriptor.DescriptorTextureCount != 1)
            throw Error("unsupported PB2 source texture constructor descriptor or metadata header");
        G2ShaderCombinationStage pixel = metadata.Combination.Stages.Single(s => s.StageCode == 0);
        if (pixel.Entry is null) throw Error("selected combination has no original pixel program");
        G2ShaderContainer canonicalShader = G2ShaderContainerReader.Read(shader.Source.Bytes);
        DxbcResourceDefinitions rdef = DxbcResourceDefinitionReader.Read(
            canonicalShader.GetProgramBytes(shader.Source.Bytes, pixel.Entry.ProgramIndex));
        DxbcRdefBinding[] textures = rdef.Bindings.Where(b => b.InputType == 2).OrderBy(b => b.BindPoint).ToArray();
        DxbcRdefBinding[] samplers = rdef.Bindings.Where(b => b.InputType == 3).OrderBy(b => b.BindPoint).ToArray();
        int textureCount=textures.Length;
        string[] expectedNames=textureCount switch{4=>["sStage0","sRefRough","sOcc","sNMap"],
            3=>["sStage0","sRefRough","sNMap"],_=>[]};
        uint[] registers=Enumerable.Range(0,textureCount).Select(i=>(uint)i).ToArray();
        if (expectedNames.Length==0 || samplers.Length != textureCount ||
            metadata.RawHeaderWord2!=(uint)(textureCount-2) ||
            !textures.Select(b => b.Name.Text).SequenceEqual(expectedNames) ||
            !textures.Select(b => b.BindPoint).SequenceEqual(registers) ||
            !samplers.Select(b => b.BindPoint).SequenceEqual(registers) ||
            textures.Any(b => b.BindCount != 1 || b.Dimension != 4) || samplers.Any(b => b.BindCount != 1))
            throw Error("selected PB2 pixel program must declare the exact supported three/four individual 2D texture and sampler profile");
        short[] slots=Enumerable.Range(1,textureCount-1).Select(i=>(short)i).ToArray();
        if (metadata.Entries.Count != textureCount-1 || metadata.Entries.Any(e => e.SlotIndex < 1 || e.SlotIndex >= textureCount || e.RawAuxiliary != 0) ||
            !metadata.Entries.Select(e => e.SlotIndex).Order().SequenceEqual(slots))
            throw Error("unsupported PB2 resource registration profile");
        foreach (G1ShaderResourceBinding entry in metadata.Entries)
            if (entry.Name != textures[entry.SlotIndex].Name.Text)
                throw Error($"metadata entry {entry.Index} does not identify selected PS texture register {entry.SlotIndex}");

        // Parse the hash-checked model bytes again; unrelated caller-supplied geometry
        // or material records cannot substitute for this draw's original references.
        G1ModelGeometry geometry = G1ModelGeometryReader.Read(model.Model.Source.Bytes, model.Model.Source.Bytes.LongLength);
        G1ModelDrawTextures draws = G1ModelDrawTextureReader.Read(geometry);
        if ((uint)drawIndex >= draws.Bindings.Count) throw new ArgumentOutOfRangeException(nameof(drawIndex));
        G1TextureRecord record = draws.Bindings[drawIndex].TextureRecord;
        if (record.TextureEntries.Count > 16 || record.UnknownValue8 > ushort.MaxValue || record.UnknownValue12 > ushort.MaxValue)
            throw Error("texture record exceeds verified native counts or header width");
        ushort primaryCount = (ushort)record.UnknownValue8;
        short specialIndex = unchecked((short)record.UnknownValue12);
        if (primaryCount != 1 || record.TextureEntries.Count != textureCount || specialIndex != 1)
            throw Error("unsupported PB2 primary count, entry count or special texture reference");
        var primary = new Dictionary<int, G1ShaderResourceBinding>();
        var secondary = new Dictionary<int, G1ShaderResourceBinding>();
        foreach (G1ShaderResourceBinding entry in metadata.Entries)
        {
            if (entry.PrimaryEncoded != 0 && !primary.TryAdd(EncodedIndex(entry.PrimaryEncoded), entry))
                throw Error("duplicate primary encoded resource registration");
            if (entry.SecondarySamplerEncoded != 0 && entry.SecondarySamplerEncoded != entry.PrimaryEncoded &&
                !secondary.TryAdd(EncodedIndex(entry.SecondarySamplerEncoded), entry))
                throw Error("duplicate secondary encoded resource registration");
        }
        var effects = new List<ModelStageTextureEffect>();
        var final = new Dictionary<int, G1TextureEntry>();
        void Write(G1TextureEntry entry, int destination, ModelStageTextureOperation operation,
            ushort encoded = 0, G1ShaderResourceBinding? registration = null)
        {
            if (entry.RawValue0 >= model.Slots.Count) throw Error($"entry {entry.Index} texture slot exceeds source table");
            effects.Add(new(effects.Count, operation, record.Index, entry.Index, entry.RawValue0,
                destination, encoded, registration?.Index, registration?.Name));
            if (destination is >= 0 and < 16) final[destination] = entry;
            else if (destination != 16) throw Error("resource registration destination exceeds native slot array");
        }
        // 97F630: primary ordinal then descriptor flags bit 0x20's special reference.
        Write(record.TextureEntries[0], 0, ModelStageTextureOperation.Primary);
        Write(record.TextureEntries[specialIndex], 1, ModelStageTextureOperation.Special);
        // 97ED70: every extra row writes its primary registration and then its
        // secondary registration, including the initialized sentinel destination.
        for (int i = primaryCount; i < record.TextureEntries.Count; i++)
        {
            G1TextureEntry entry = record.TextureEntries[i];
            primary.TryGetValue(EncodedIndex(entry.RawValue4), out G1ShaderResourceBinding? p);
            secondary.TryGetValue(EncodedIndex(entry.RawValue6), out G1ShaderResourceBinding? s);
            if (p is null || (s is null && entry.RawValue6 != entry.RawValue4))
                throw Error($"entry {entry.Index} encoded texture roles lack exact selected shader registrations");
            Write(entry, p?.SlotIndex ?? 16, ModelStageTextureOperation.EncodedPrimary, entry.RawValue4, p);
            Write(entry, s?.SlotIndex ?? 16, ModelStageTextureOperation.EncodedSecondary, entry.RawValue6, s);
        }
        if (!final.Keys.Order().SequenceEqual(Enumerable.Range(0,textureCount)))
            throw Error("source registrations do not resolve all selected PS textures exactly");
        var bindings = new List<ModelStageTextureBinding>(textureCount);
        for (int target = 0; target < textureCount; target++)
        {
            G1TextureEntry entry = final[target]; ModelTextureSlot slot = model.Slots[entry.RawValue0];
            bindings.Add(new((uint)target, samplers[target].BindPoint, textures[target].Name.Text, samplers[target].Name.Text,
                record.Index, entry.Index, slot.Index, slot.ProxyObjectId, slot.TextureFileId, slot.RawValue00200571,
                checked((int)(slot.RawValue00200571 & 0x7FFFFFFF)), entry.RawValue2, entry.RawValue4, entry.RawValue6,
                entry.RawValue8, entry.RawValue10));
        }
        return new(model.Model.Source.ResourceUri, model.Model.Source.DecodedSha256,
            model.TextureTable.ResourceUri, model.TextureTable.DecodedSha256, shader.Source.ResourceUri, shader.Source.DecodedSha256,
            provider.Object.ObjectId, drawIndex, explicitCombinationIndex, pixel.Entry.ProgramIndex,
            record.Index, primaryCount, specialIndex, effects.AsReadOnly(), bindings.AsReadOnly());
    }

    /// <summary>Uses the native proxy TID selection and retains the complete source mip chain.</summary>
    public static ModelStageTextureImage LoadImage(GameResourceRepository repository,
        LoadedModelTextureResources model, ModelStageTexturePlan plan, uint textureRegister)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(plan);
        if (model.Model.Source.ResourceUri != plan.ModelUri || model.Model.Source.DecodedSha256 != plan.ModelSha256 ||
            model.TextureTable.ResourceUri != plan.TextureTableUri || model.TextureTable.DecodedSha256 != plan.TextureTableSha256)
            throw Error("texture plan and model/table source identities differ");
        ValidateHash(model.Model.Source); ValidateHash(model.TextureTable); ValidateLinks(model);
        ModelStageTextureBinding binding = plan.Bindings.Single(b => b.TextureRegister == textureRegister);
        if ((uint)binding.SourceTextureSlot >= model.Slots.Count) throw Error("resolved slot no longer belongs to model source");
        ModelTextureSlot slot = model.Slots[binding.SourceTextureSlot];
        if (slot.ProxyObjectId != binding.ProxyObjectId || slot.TextureFileId != binding.TextureFileId || slot.RawValue00200571 != binding.RawTextureId)
            throw Error("resolved slot proxy identity changed");
        LoadedTextureContainer container = ModelTextureResources.LoadTextureContainer(repository, model, slot.Index);
        ValidateHash(container.Source);
        container = container with { Container = G1TextureContainerReader.Read(container.Source.Bytes) };
        if ((uint)binding.ImageIndex >= container.Container.Textures.Count)
            throw Error($"proxy TID {binding.RawTextureId:X8} exceeds exact container {container.Source.ResourceUri}");
        G1TextureImage image = container.Container.Textures[binding.ImageIndex];
        return new(binding, container, image, G1TextureSamplerReader.ReadRetainedSource(image));
    }

    private static int EncodedIndex(int encoded)
    {
        // Original registration takes signed shorts; negative and overflowing
        // source namespaces remain unsupported instead of indexing native memory.
        if (encoded < 0 || encoded > short.MaxValue) throw Error($"unsupported encoded texture role {encoded}");
        int result = (encoded & 7) + 8 * (((encoded >> 3) & 15) + 10 * (encoded >> 7));
        if (result >= 88) throw Error($"encoded texture role {encoded} exceeds original registration table");
        return result;
    }

    private static void ValidateLinks(LoadedModelTextureResources model)
    {
        if (Scalar(model.ModelObject, 0x8AB68B3F) != model.Model.Source.FileId ||
            Scalar(model.ModelObject, 0xF92C5190) != model.TextureGroupObject.ObjectId ||
            Scalar(model.TextureGroupObject, 0x7A1E1EF8) != model.TextureTable.FileId ||
            model.TextureTable.Bytes.Length != model.Slots.Count * 8)
            throw Error("model, texture group and external slot table references differ");
        foreach (ModelTextureSlot slot in model.Slots)
        {
            if ((uint)slot.Index >= model.Slots.Count) throw Error("invalid source slot index");
            ReadOnlySpan<byte> row = model.TextureTable.Bytes.AsSpan(slot.Index * 8, 8);
            if (BinaryPrimitives.ReadUInt32LittleEndian(row) != slot.Index ||
                BinaryPrimitives.ReadUInt32LittleEndian(row[4..]) != slot.ProxyObjectId ||
                !row.SequenceEqual(slot.RawTableRow) || slot.Proxy.ClassId != 0xFF7DBFD4 || slot.Proxy.ObjectId != slot.ProxyObjectId ||
                Scalar(slot.Proxy, 0x6C7321D2) != slot.TextureFileId || Scalar(slot.Proxy, 0x00200571, 4) != slot.RawValue00200571)
                throw Error($"texture slot {slot.Index} table/proxy identity differs");
        }
    }

    private static uint Scalar(DokObject item, uint hash, uint type = 5)
    {
        DokProperty[] fields = item.Properties.Where(p => p.NameHash == hash).ToArray();
        if (fields.Length != 1 || fields[0].TypeCode != type || fields[0].ElementCount != 1 || fields[0].RawValue.Length != 4)
            throw Error($"object {item.ObjectId:X8} requires source scalar {hash:X8}");
        return BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue);
    }

    private static void ValidateHash(LoadedRdbAsset source)
    {
        if (source.DecodedSha256.Length != 64 ||
            !Convert.ToHexString(SHA256.HashData(source.Bytes)).Equals(source.DecodedSha256, StringComparison.OrdinalIgnoreCase))
            throw Error($"source hash mismatch for {source.ResourceUri}");
    }
    private static InvalidDataException Error(string detail) => new($"Original model stage textures: {detail}.");
}
