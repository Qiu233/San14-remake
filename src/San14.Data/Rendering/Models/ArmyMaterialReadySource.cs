using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Imported A8 declaration and its four full shader tables. The
/// selected state18 provider is intentionally not used as a table identity.
/// Texture object IDs are lookup inputs, not current resource availability.</summary>
public sealed class LoadedArmyMaterialReadySource
{
    internal LoadedArmyMaterialReadySource(LoadedObjects materials, DokObject material,
        LoadedRdbAsset bindings, NamedModelShader? normal, NamedModelShader? cloth,
        NamedModelShader? staticNormal, NamedModelShader? staticCloth,
        uint regularTextures, uint renderTargetTextures, uint alternateTextures)
    {
        Materials = materials; Material = material; ShaderBindingSource = bindings;
        NormalShader = normal; ClothShader = cloth; StaticNormalShader = staticNormal; StaticClothShader = staticCloth;
        RegularTextureGroupObjectId = regularTextures; RenderTargetTextureGroupObjectId = renderTargetTextures;
        AlternateTextureGroupObjectId = alternateTextures;
    }
    public LoadedObjects Materials { get; }
    public DokObject Material { get; }
    public LoadedRdbAsset ShaderBindingSource { get; }
    public NamedModelShader? NormalShader { get; }
    public NamedModelShader? ClothShader { get; }
    public NamedModelShader? StaticNormalShader { get; }
    public NamedModelShader? StaticClothShader { get; }
    public uint RegularTextureGroupObjectId { get; }
    public uint RenderTargetTextureGroupObjectId { get; }
    public uint AlternateTextureGroupObjectId { get; }
    public string SourceEvidence => Materials.Source.ResourceUri + "@" + Materials.Source.DecodedSha256 +
        ";A8=" + Material.ObjectId.ToString("X8") + ";D9B7B0-CPU8/20/30;D9D3C0-four-shader-Data;" +
        "D9D687-stage1-full-typed-records;current-registration-and-ready-fields-separate";
}

public static class ArmyMaterialReadySource
{
    /// <summary>Called once at actual material import. Source declarations
    /// are checked here; normal ready/mode/table requests use retained objects.</summary>
    public static LoadedArmyMaterialReadySource Load(GameResourceRepository repository,
        LoadedObjects actualMaterials, uint actualMaterialObjectId, LoadedObjects actualShaderObjects)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(actualMaterials);
        ArgumentNullException.ThrowIfNull(actualShaderObjects);
        if (actualMaterials.Source.ResourceUri != GameResourceId.Rdb("KIDSSystemResource", ArmyFlagMaterialVariations.MaterialDictionaryId).ToString() ||
            actualMaterials.Source.OverrideSource is not null ||
            actualMaterials.Source.DecodedSha256 != ArmyFlagMaterialVariations.MaterialDictionarySha256 ||
            Convert.ToHexString(SHA256.HashData(actualMaterials.Source.Bytes)) != ArmyFlagMaterialVariations.MaterialDictionarySha256)
            throw new InvalidDataException("Army material ready import requires its original A8 material dictionary.");
        actualMaterials = new(DokReader.Read(actualMaterials.Source.Bytes), actualMaterials.Source);
        var material = actualMaterials.Document.Objects.Single(o => o.ObjectId == actualMaterialObjectId);
        if (material.Kind != DokObjectKind.Inline || material.ClassId != 0xA8F14404)
            throw new NotSupportedException("Army material ready declaration requires its actual A8 inline resource creator.");
        uint Scalar(uint hash)
        {
            var p = material.Properties.Single(p => p.NameHash == hash);
            if (p.TypeCode != 5 || p.ElementCount != 1 || p.RawValue.Length != 4)
                throw new InvalidDataException("Army A8 ready field is not its actual DWORD declaration.");
            return BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue);
        }
        var bindings = repository.LoadRdbAsset("system", Scalar(0xAD260326));
        var bindingRows = ShaderBindingCsvReader.Read(bindings.Bytes);
        NamedModelShader? Shader(uint hash)
        {
            uint identifier = Scalar(hash);
            if (identifier == 0) return null; // The original loader omits a zero shader ID.
            var row = bindingRows.RequireIdentifier(identifier);
            var table = ModelShaderResources.ReadDeclaredShader(actualShaderObjects, row.ObjectNameHash);
            if (!table.HasVerifiedType16Creator)
                throw new NotSupportedException("Army material shader requires its original full typed16 creator.");
            return table;
        }
        return new(actualMaterials, material, bindings, Shader(0x3DDC205C), Shader(0x6CFA424D),
            Shader(0x6341056D), Shader(0xA03F1C67), Scalar(0xF92C5190), Scalar(0x87814C77), Scalar(0x80C25565));
    }

    public static uint ReadWorldPreserve2EC(LoadedArmyCatalog actualCatalog, int category)
    {
        ArgumentNullException.ThrowIfNull(actualCatalog);
        if (category is not (0 or 1 or 2 or 3)) throw new ArgumentOutOfRangeException(nameof(category));
        var known = CeArmyModels.KnownSourceCategories[category];
        var world = actualCatalog.Definitions.Single(d => d.Definition.ObjectId == known.DefinitionId).Definition;
        var p = world.Properties[46];
        if (p.Index != 46 || p.NameHash != 0x6AB12F24 || p.TypeCode != 5 || p.ElementCount != 1 || p.RawValue.Length != 4)
            throw new InvalidDataException("Army World2EC requires its actual ordinal46 DWORD publication.");
        return BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue);
    }
}

/// <summary>The source material's shader/texture subset, using actual owned
/// reference identities in their two separate domains. Parameter records
/// 88..B8/E8/F0 and callbacks remain with their actual producer. No whole
/// material ready or World primitive completion authority is issued.</summary>
public sealed class ArmyMaterialReadyState<TShaderRecord, TTextureRecord>
    where TShaderRecord : class where TTextureRecord : class
{
    public TShaderRecord? Normal58 { get; private set; }
    public TShaderRecord? Cloth60 { get; private set; }
    public TShaderRecord? StaticNormal68 { get; private set; }
    public TShaderRecord? StaticCloth70 { get; private set; }
    public TShaderRecord? CurrentNormal78 { get; private set; }
    public TShaderRecord? CurrentCloth80 { get; private set; }
    public TTextureRecord? RegularC0 { get; private set; }
    public TTextureRecord? RenderTargetD0 { get; private set; }
    public TTextureRecord? AlternateD8 { get; private set; }
    public TTextureRecord? CurrentTexturesE0 { get; private set; }

    /// <summary>Only after the actual stage1 dependency readiness gates.
    /// A present Data with a null typed record is different from absent Data.</summary>
    public void ApplyStage1ReadyFields(TShaderRecord? actualNormal58, TShaderRecord? actualCloth60,
        TShaderRecord? actualStaticNormal68, TShaderRecord? actualStaticCloth70,
        bool actualRegularDataPresent, TTextureRecord? actualRegularTypedRecord)
    {
        Normal58 = actualNormal58; Cloth60 = actualCloth60;
        StaticNormal68 = actualStaticNormal68; StaticCloth70 = actualStaticCloth70;
        if (actualRegularDataPresent)
        {
            RegularC0 = actualRegularTypedRecord;
            ApplyCurrentMode(0); // D9D7D6 is inside the CPU8 Data-present block.
        }
    }

    /// <summary>Only after the actual stage2 dependency readiness gates.
    /// CPU20 absent skips normal shader publication. A present Data whose
    /// typed getter returns null still publishes nonnull Normal58/Cloth60.</summary>
    public void ApplyStage2ReadyFields(bool actualRenderTargetDataPresent, TTextureRecord? actualRenderTargetTypedRecord,
        bool actualAlternateDataPresent, TTextureRecord? actualAlternateTypedRecord)
    {
        if (actualRenderTargetDataPresent)
        {
            RenderTargetD0 = actualRenderTargetTypedRecord;
            if (RenderTargetD0 is not null) CurrentTexturesE0 = RenderTargetD0;
            PublishNormalShaders();
        }
        if (actualAlternateDataPresent) AlternateD8 = actualAlternateTypedRecord;
    }

    /// <summary>D9D290 pointer setter semantics. Null source fields preserve
    /// previous destinations; out-of-domain mode is the original no-op.</summary>
    public void ApplyCurrentMode(int actualMode)
    {
        switch (actualMode)
        {
            case 0:
                if (RegularC0 is not null) CurrentTexturesE0 = RegularC0;
                if ((StaticNormal68 ?? Normal58) is { } normal) CurrentNormal78 = normal;
                if ((StaticCloth70 ?? Cloth60) is { } cloth) CurrentCloth80 = cloth;
                break;
            case 1:
                if (RenderTargetD0 is not null) CurrentTexturesE0 = RenderTargetD0;
                PublishNormalShaders(); break;
            case 2:
                if (AlternateD8 is not null) CurrentTexturesE0 = AlternateD8;
                PublishNormalShaders(); break;
        }
    }
    private void PublishNormalShaders()
    {
        if (Normal58 is not null) CurrentNormal78 = Normal58;
        if (Cloth60 is not null) CurrentCloth80 = Cloth60;
    }
    public bool ShouldRetainPriorWorldTable(uint actualWorld2EC)
        => actualWorld2EC != 0 && ReferenceEquals(CurrentTexturesE0, RegularC0);

    /// <summary>Called after the actual dynamic-replacement producer. Fresh
    /// World records use78; copied records use80 only for a real nonnullP50
    /// and nonnull80. The material overwrite itself may produce null.</summary>
    public TShaderRecord? SelectWorldTable(uint actualWorld2EC, TShaderRecord? actualPriorFullTable,
        bool actualCopiedPrimitive, bool actualPrimitive50Present)
        => ShouldRetainPriorWorldTable(actualWorld2EC) ? actualPriorFullTable :
            actualCopiedPrimitive && actualPrimitive50Present && CurrentCloth80 is not null ? CurrentCloth80 : CurrentNormal78;
}
