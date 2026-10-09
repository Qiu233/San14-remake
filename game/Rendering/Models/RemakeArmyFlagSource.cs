using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using San14.Data;
using San14.Data.Formats;

public sealed record RemakeArmyFlagTextureSlot(int Index, uint ProxyObjectId,
    DokObject? StaticProxy, uint? StaticTextureFileId, uint? RawTextureId, byte[] RawTableRow)
{
    public bool RequiresRuntimeTexture => StaticProxy is null;
}

public sealed record RemakeArmyFlagDrawTexture(uint Register, uint SamplerRegister,
    string SourceTextureName, string SourceSamplerName, G1TextureEntry Entry,
    RemakeArmyFlagTextureSlot Slot);

/// <summary>Source flag geometry, declarations and texture references. Missing
/// runtime proxies retain their actual identity; they never become static images
/// or synthetic DOK objects. The original dynamic texture producer must resolve
/// them before a complete flag actor is admitted.</summary>
public sealed class RemakeArmyFlagSource
{
    private ArmyContainerMaterialGroupSource? materialGroups;
    public CeArmyModelCategory Category { get; }
    public LoadedArmyCatalog Catalog { get; }
    public ArmyModelDefinition Definition { get; }
    public LoadedModelMaterials Model { get; }
    public LoadedModelTransforms Transforms { get; }
    public DokObject TextureGroup { get; }
    public LoadedRdbAsset TextureTable { get; }
    public IReadOnlyList<RemakeArmyFlagTextureSlot> Slots { get; }
    public LoadedModelShaderBindings ShaderBindings { get; }
    public LoadedArmyAnimationAction Action0 { get; }
    public UnitWorldTransformState Constructor { get; }

    public ArmyContainerMaterialGroupSource ReadDeclaredMaterialGroups(GameResourceRepository repository)
        => materialGroups ??= ArmyContainerMaterialGroupSource.Load(repository, Catalog, Category.Category, Model.Geometry);

    private RemakeArmyFlagSource(CeArmyModelCategory category,
        LoadedArmyCatalog catalog, ArmyModelDefinition definition,
        LoadedModelMaterials model, LoadedModelTransforms transforms, DokObject group,
        LoadedRdbAsset table, IReadOnlyList<RemakeArmyFlagTextureSlot> slots,
        LoadedModelShaderBindings shaders, LoadedArmyAnimationAction action)
    {
        (Category, Catalog, Definition, Model, Transforms, TextureGroup, TextureTable, Slots,
            ShaderBindings, Action0) = (category, catalog, definition, model, transforms, group, table,
            slots, shaders, action);
        Constructor = UnitWorldTransform.ConstructDefinition(catalog.Catalog, definition.Definition);
    }

    public static RemakeArmyFlagSource Load(GameResourceRepository repository, int category)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (category is not (2 or 3)) throw Error("requires source banner category2 or flag category3");
        var known = CeArmyModels.KnownSourceCategories[category];
        var catalog = CeArmyModels.LoadCatalog(repository);
        var definition = catalog.Definitions.Single(row => row.Definition.ObjectId == known.DefinitionId);
        if (definition.ModelObject.ObjectId != known.ModelObjectId || definition.ModelFileId != known.ModelFileId)
            throw Error("catalog category/model identity changed");
        var model = repository.LoadModelMaterials("CharacterEditor", known.ModelFileId);
        Verify(model.Source);
        var canonical = G1ModelGeometryReader.Read(model.Source.Bytes, model.Source.Bytes.Length);
        model = new(canonical, G1ModelMaterialReader.Read(canonical), model.Source);
        var transforms = new LoadedModelTransforms(canonical,
            G1ModelTransformReader.Read(model.Source.Bytes, canonical), model.Source);
        DokObject group = catalog.Dictionary.Document.Objects.Single(row =>
            row.ObjectId == Scalar(definition.ModelObject, 0xF92C5190));
        if (group.Kind != DokObjectKind.Inline || group.ClassId != 0x3059B9C3)
            throw Error("requires the source inline texture group");
        var table = repository.LoadRdbAsset("CharacterEditor", Scalar(group, 0x7A1E1EF8));
        Verify(table);
        if (table.Bytes.Length == 0 || table.Bytes.Length % 8 != 0)
            throw Error("requires a complete source texture slot table");
        var slots = new List<RemakeArmyFlagTextureSlot>();
        for (int at = 0; at < table.Bytes.Length; at += 8)
        {
            int index = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(table.Bytes.AsSpan(at)));
            uint proxyId = BinaryPrimitives.ReadUInt32LittleEndian(table.Bytes.AsSpan(at + 4));
            if (index != slots.Count) throw Error("source texture slots are not ordinal rows");
            DokObject[] proxies = catalog.Dictionary.Document.Objects.Where(row => row.ObjectId == proxyId).ToArray();
            if (proxies.Length > 1) throw Error("source texture proxy identity is duplicated");
            DokObject? proxy = proxies.SingleOrDefault();
            if (proxy is not null && (proxy.Kind != DokObjectKind.Inline || proxy.ClassId != 0xFF7DBFD4))
                throw Error("a present texture proxy has an unsupported native class");
            slots.Add(new(index, proxyId, proxy, proxy is null ? null : Scalar(proxy, 0x6C7321D2),
                proxy is null ? null : Scalar(proxy, 0x00200571, 4), table.Bytes.AsSpan(at, 8).ToArray()));
        }
        var shaders = ModelShaderResources.LoadBindings(repository, catalog.Dictionary, known.ModelObjectId, model);
        var action = ArmyAnimationResources.LoadAction(repository, catalog, category, 0);
        if (action.AnimationData.Count != 1 || action.AnimationData[0].Source.FileId != 0xDC1FED0A)
            throw Error("flag action0 differs from its uniquely declared source clip");
        return new(known, catalog, definition, model, transforms, group, table, slots.AsReadOnly(), shaders, action);
    }

    public IReadOnlyList<RemakeArmyFlagDrawTexture> DrawTextures(int drawIndex,
        NamedModelShader provider, LoadedShaderContainer shader)
    {
        if (provider.Object.ObjectId != 0x0AC3D18D || provider.ShaderFileHash != 0xB90AEDD3 ||
            shader.Source.DecodedSha256 != ArmySkinVertexAdapter.ShaderSha256)
            throw Error("requires the exact source Army G-buffer provider");
        var metadata = G1ShaderResourceBindingReader.Read(shader.Source.Bytes, 1);
        var pair = metadata.Combination.Stages.Single(stage => stage.StageCode == 0).Entry ??
            throw Error("selected source flag shader has no pixel program");
        var rdef = shader.ReadResourceDefinitions(pair.ProgramIndex);
        var textures = rdef.Bindings.Where(row => row.InputType == 2).OrderBy(row => row.BindPoint).ToArray();
        var samplers = rdef.Bindings.Where(row => row.InputType == 3).OrderBy(row => row.BindPoint).ToArray();
        if (!textures.Select(row => row.Name.Text).SequenceEqual(new[] { "sStage0", "sRefRough", "sOcc", "sNMap" }) ||
            !textures.Select(row => row.BindPoint).SequenceEqual(new uint[] { 0, 1, 2, 3 }) ||
            !samplers.Select(row => row.BindPoint).SequenceEqual(new uint[] { 0, 1, 2, 3 }) || metadata.Entries.Count != 3)
            throw Error("selected source flag PS texture inventory changed");
        var record = G1ModelDrawTextureReader.Read(Model.Geometry).Bindings[drawIndex].TextureRecord;
        if (record.UnknownValue8 != 1 || record.UnknownValue12 != 1 || record.TextureEntries.Count != 4)
            throw Error("source flag texture registration header changed");
        var final = new Dictionary<int, G1TextureEntry> { [0] = record.TextureEntries[0], [1] = record.TextureEntries[1] };
        foreach (var entry in record.TextureEntries.Skip(1))
        {
            var primary = metadata.Entries.SingleOrDefault(row => row.PrimaryEncoded != 0 &&
                Encoded(row.PrimaryEncoded) == Encoded(entry.RawValue4));
            var secondary = metadata.Entries.SingleOrDefault(row => row.SecondarySamplerEncoded != 0 &&
                row.SecondarySamplerEncoded != row.PrimaryEncoded && Encoded(row.SecondarySamplerEncoded) == Encoded(entry.RawValue6));
            if (primary is null || secondary is null && entry.RawValue6 != entry.RawValue4)
                throw Error("flag texture entry lacks its exact source registration");
            final[primary.SlotIndex] = entry;
            if (secondary is not null && secondary.SlotIndex < 16) final[secondary.SlotIndex] = entry;
        }
        if (!final.Keys.Order().SequenceEqual(new[] { 0, 1, 2, 3 }))
            throw Error("source flag texture writes do not cover its PS registers");
        return final.OrderBy(row => row.Key).Select(row =>
        {
            if (row.Value.RawValue0 >= Slots.Count) throw Error("flag texture entry exceeds its source table");
            return new RemakeArmyFlagDrawTexture((uint)row.Key, samplers[row.Key].BindPoint,
                textures[row.Key].Name.Text, samplers[row.Key].Name.Text, row.Value, Slots[row.Value.RawValue0]);
        }).ToArray();
    }

    public ArmyAnimationSourceInput FreshAction() => new(Category.Category, 0, 0xDC1FED0A, 0);
    private static int Encoded(int value)
    {
        if (value < 0 || value > short.MaxValue) throw Error("unsupported signed texture registration");
        int index = (value & 7) + 8 * (((value >> 3) & 15) + 10 * (value >> 7));
        if (index >= 88) throw Error("texture registration exceeds its source namespace");
        return index;
    }
    private static uint Scalar(DokObject item, uint key, uint type = 5)
    {
        var fields = item.Properties.Where(field => field.NameHash == key).ToArray();
        if (fields.Length != 1 || fields[0].TypeCode != type || fields[0].ElementCount != 1 || fields[0].RawValue.Length != 4)
            throw Error($"object {item.ObjectId:X8} has an unsupported source field {key:X8}");
        return BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue);
    }
    private static void Verify(LoadedRdbAsset source)
    {
        if (Convert.ToHexString(SHA256.HashData(source.Bytes)) != source.DecodedSha256)
            throw Error("source bytes/hash disagree");
    }
    private static InvalidDataException Error(string message) => new($"Source-created Army flag {message}.");
}
