using System.Buffers.Binary;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ArmyFlagMaterialManagerRow(int Category, int ManagerKind,
    int ForceVariantCount, int FaceSlot);

public sealed record ArmyFlagMaterialMatrix(ArmyFlagMaterialManagerRow Manager,
    DokObject Setting, int RowCount, int ColumnCount, IReadOnlyList<string> RowNames,
    IReadOnlyList<uint> MaterialObjectIds)
{
    public uint MaterialAt(int row) => (uint)row < RowCount
        ? MaterialObjectIds[row * ColumnCount + Manager.FaceSlot]
        : throw new ArgumentOutOfRangeException(nameof(row));
}

public sealed record ArmyFlagMaterialStageTable(ushort EntryCount,
    ushort PrimaryCount, short SpecialEntryIndex, ushort RawValue14,
    IReadOnlyList<G1TextureEntry> Entries, byte[] RawHeader);

public sealed record ArmyFlagMaterialTextureSlot(int Index, uint ProxyObjectId,
    DokObject Proxy, uint? TextureFileId, uint? RawTextureId,
    RenderTextureDefinition? RenderTarget, byte[] RawTableRow)
{
    public bool RequiresProducerImage => RenderTarget is not null;
}

public sealed record ArmyFlagMaterialTextureUse(uint TextureRegister,
    uint SamplerRegister, string TextureName, string SamplerName,
    G1TextureEntry Entry, ArmyFlagMaterialTextureSlot Slot);

public sealed record ArmyFlagMaterialUserParameters(DokProperty? MaterialIdProperty,
    int? DeclaredMaterialId, DokProperty? OpacityThresholdProperty,
    float? DeclaredOpacityThreshold, DokProperty? AutomatonContextProperty,
    uint? AutomatonContextObjectId)
{
    public bool RuntimeAutomatonToShaderWritesVerified => false;
}

/// <summary>The material manager initializes row variant+1. The controller's
/// independent BCF970 gate reads row variant. A caller-owned variant is required;
/// a Core force ID, color preview or current original field is not inferred.</summary>
public sealed record LoadedArmyFlagMaterialVariation(
    ArmyFlagMaterialManagerRow ManagerRow, int OwnedForceVariant,
    LoadedArmyCatalog Catalog, DokObject Setting, int SourceVariationCount,
    int FaceWidth, IReadOnlyList<string> SourceVariationNames,
    uint ControllerGateMaterialObjectId, uint SelectedMaterialObjectId,
    LoadedObjects Materials, DokObject MaterialDefinition, DokObject TextureGroup,
    LoadedRdbAsset TextureTable, LoadedRdbAsset StageSource,
    ArmyFlagMaterialStageTable StageTable,
    IReadOnlyList<ArmyFlagMaterialTextureSlot> Slots,
    LoadedRdbAsset ShaderBindingSource, NamedModelShader ShaderOwner,
    NamedModelShader GbufferProvider)
{
    public ArmyFlagMaterialUserParameters UserParameters { get; init; } = new(null,null,null,null,null,null);
    public IReadOnlyDictionary<uint,uint> ReferencedRenderStateObjectIds { get; init; } = new Dictionary<uint,uint>();
    public IReadOnlyDictionary<uint,DokObject?> LocalRenderStateDeclarations { get; init; } = new Dictionary<uint,DokObject?>();
    public int SelectedSourceVariationRow => OwnedForceVariant + 1;
    public string SelectedSourceVariationName => SourceVariationNames[SelectedSourceVariationRow];
    public bool ControllerGateNonzero => ControllerGateMaterialObjectId != 0;
    public bool SourceDrawParametersRemainExplicit => true;
    public bool ForceVariantFidelityVerified => false;
    public bool CompleteMaterialDrawFidelityVerified => false;
}

public sealed record LoadedArmyFlagMaterialImage(ArmyFlagMaterialTextureUse Use,
    LoadedTextureContainer Container, G1TextureImage Image,
    uint NativeViewFormat, G1TextureSamplerRecipe Sampler);

/// <summary>Source-backed flag material manager declarations, GSTK texture
/// registrations and their actual static/RT texture graphs. This reads source
/// objects only. The UI producer, final RT state and eventual draw parameter
/// consumption remain separately owned inputs.</summary>
public static class ArmyFlagMaterialVariations
{
    public const uint MaterialDictionaryId = 0xD956E4A2;
    public const uint GbufferShaderId = 0xA72FC040;
    public static IReadOnlyList<ArmyFlagMaterialManagerRow> ManagerRows { get; } =
        Array.AsReadOnly(new[] { new ArmyFlagMaterialManagerRow(2,0,52,0), new(2,1,52,1), new(3,2,52,0) });

    public static int ReadManagerForDeclaredGroup(int actualCategory, uint actualWorldGroupOrdinal)
    {
        var row = ManagerRows.SingleOrDefault(r => r.Category == actualCategory && r.FaceSlot == actualWorldGroupOrdinal);
        return row?.ManagerKind ?? throw Error("actual declared World group has no ColorVariation manager");
    }

    /// <summary>The manager initializes its declared number of variants from
    /// rows starting at one. The matrix may contain additional editor rows.</summary>
    public static ArmyFlagMaterialMatrix ReadMatrix(LoadedArmyCatalog catalog, int managerKind)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if ((uint)managerKind >= ManagerRows.Count) throw new ArgumentOutOfRangeException(nameof(managerKind));
        var manager = ManagerRows[managerKind];
        var known = CeArmyModels.KnownSourceCategories[manager.Category];
        var definition = catalog.Definitions.SingleOrDefault(d => d.Definition.ObjectId == known.DefinitionId);
        if (catalog.Catalog.Source.ResourceUri != GameResourceId.Rdb("KIDSSystemResource", CeArmyModels.CatalogId).ToString() ||
            definition is null || definition.ModelObject.ObjectId != known.ModelObjectId || definition.ModelFileId != known.ModelFileId)
            throw Error("requires the category catalog/model identity");
        var setting = catalog.Catalog.Document.Objects.Single(o => o.Kind == DokObjectKind.Inline &&
            o.ClassId == 0xD186CEEB && Scalar(o, 0x68A6F779) == known.DefinitionId);
        int count = checked((int)Scalar(setting, 0xB3A1E5AA));
        int width = manager.Category == 2 ? 3 : 2;
        uint[] ids = Values(setting, 0x24C114F6);
        if (count <= manager.ForceVariantCount || ids.Length != checked(count * width))
            throw Error("color variation matrix cannot supply its declared manager rows");
        string[] names = new UTF8Encoding(false, true).GetString(Field(setting, 0x17DE9422, 1).RawValue).Split('\0');
        if (names.Length != count + 1 || names[^1].Length != 0)
            throw Error("color variation labels do not match the declared matrix rows");
        return new(manager, setting, count, width, Array.AsReadOnly(names[..^1]), Array.AsReadOnly(ids));
    }

    public static LoadedArmyFlagMaterialVariation Load(GameResourceRepository repository,
        LoadedArmyCatalog catalog, int managerKind, int explicitOwnedForceVariant)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return Load(repository, catalog, managerKind, explicitOwnedForceVariant,
            repository.LoadObjects("KIDSSystemResource", MaterialDictionaryId),
            repository.LoadObjects("system", 0x718CCB8B));
    }

    /// <summary>Prepare another requested force from the same imported material
    /// and shader declarations. The owning resource generation imports them once.</summary>
    public static LoadedArmyFlagMaterialVariation Load(GameResourceRepository repository,
        LoadedArmyCatalog catalog, int managerKind, int explicitOwnedForceVariant,
        LoadedObjects materials, LoadedObjects shaderObjects)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(materials); ArgumentNullException.ThrowIfNull(shaderObjects);
        if (materials.Source.ResourceUri != GameResourceId.Rdb("KIDSSystemResource", MaterialDictionaryId).ToString() ||
            shaderObjects.Source.ResourceUri != GameResourceId.Rdb("system", 0x718CCB8B).ToString())
            throw Error("requires its declared material and shader libraries");
        if ((uint)managerKind >= ManagerRows.Count || (uint)explicitOwnedForceVariant >= 52)
            throw Error("requires source manager kind 0..2 and an explicit owned force variant 0..51");
        var matrix = ReadMatrix(catalog, managerKind);
        var manager = matrix.Manager;
        uint gate = matrix.MaterialAt(explicitOwnedForceVariant);
        uint selected = matrix.MaterialAt(explicitOwnedForceVariant + 1);
        if (selected == 0) throw Error("selected manager material is source-null");
        var material = Object(materials,selected,0xA8F14404);
        var textures = ModelMaterialTextureDeclaration.Read(material);
        var group = Object(materials,textures.RegularGroup,0x3059B9C3);
        var table = repository.LoadRdbAsset("MaterialEditor",Scalar(group,0x7A1E1EF8));
        var slots = ReadTextureSlots(materials,table);
        var stageSource=repository.LoadRdbAsset("MaterialEditor",textures.StageTableResource);
        var stage=ReadStageTable(stageSource.Bytes);
        var bindingSource=repository.LoadRdbAsset("system",Scalar(material,0xAD260326));
        var binding=ModelShaderResources.LoadPreparedBindingCatalog(repository,bindingSource)
            .RequireIdentifier(Scalar(material,0x3DDC205C));
        var owner=ModelShaderResources.ReadDeclaredShader(shaderObjects,binding.ObjectNameHash);
        var provider=ModelShaderResources.ResolveType16Interface(owner,18).Provider ?? throw Error("source material G-buffer provider is null");
        var references = new Dictionary<uint,uint> { [0x87814C77]=textures.RenderTargetGroup,
            [0xC17A66DF]=textures.BlendState, [0x80C25565]=textures.AlternateGroup };
        return new(manager,explicitOwnedForceVariant,catalog,matrix.Setting,matrix.RowCount,matrix.ColumnCount,matrix.RowNames,gate,selected,
            materials,material,group,table,stageSource,stage,slots,bindingSource,owner,provider)
        { UserParameters=ReadUserParameters(material), ReferencedRenderStateObjectIds=references,
            LocalRenderStateDeclarations=references.ToDictionary(pair=>pair.Key,
                pair=>materials.Document.Objects.SingleOrDefault(obj=>obj.ObjectId==pair.Value)) };
    }

    public static ArmyFlagMaterialUserParameters ReadUserParameters(DokObject material)
    {
        ArgumentNullException.ThrowIfNull(material);
        if(material.Kind!=DokObjectKind.Inline||material.ClassId!=0xA8F14404)
            throw Error("user parameter declarations require the actual inline Material class");
        DokProperty? Optional(uint key,uint type)
        {var fields=material.Properties.Where(p=>p.NameHash==key).ToArray();if(fields.Length==0)return null;
         if(fields.Length!=1||fields[0].TypeCode!=type||fields[0].ElementCount!=1||fields[0].RawValue.Length!=4)
             throw Error($"material user parameter {key:X8} is not its declared typed scalar");return fields[0];}
        var id=Optional(0xEF80BB3A,4);var opacity=Optional(0x7E31E942,8);var context=Optional(0x9AAEA305,5);
        float? threshold=opacity is null?null:BinaryPrimitives.ReadSingleLittleEndian(opacity.RawValue);
        if(threshold is float value&&!float.IsFinite(value))throw Error("declared opacity threshold is nonfinite");
        return new(id,id is null?null:BinaryPrimitives.ReadInt32LittleEndian(id.RawValue),opacity,threshold,context,
            context is null?null:U(context.RawValue,0));
    }

    public static ArmyFlagMaterialStageTable ReadStageTable(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16 || !bytes[..4].SequenceEqual("GSTK"u8) || U(bytes,4) != 0)
            throw Error("requires the complete supported GSTK source header");
        ushort count=S(bytes,8),primary=S(bytes,10),raw14=S(bytes,14);
        short special=BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(12,2));
        if (count != 4 || primary != 1 || special != 1 || raw14 != 0 || bytes.Length != 16+count*12)
            throw Error("GSTK source flag registration extent/header differs");
        var entries=new List<G1TextureEntry>();
        for (int i=0;i<count;i++)
        {
            var row=bytes.Slice(16+i*12,12);
            entries.Add(new(i,S(row,0),S(row,2),S(row,4),S(row,6),S(row,8),S(row,10),row.ToArray()));
        }
        return new(count,primary,special,raw14,entries.AsReadOnly(),bytes[..16].ToArray());
    }

    public static LoadedArmyFlagMaterialImage LoadStaticImage(GameResourceRepository repository,
        ArmyFlagMaterialTextureUse use)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(use);
        if (use.Slot.RequiresProducerImage || use.Slot.TextureFileId is not uint file || use.Slot.RawTextureId is not uint tid)
            throw Error("render-target primary requires its actual producer-owned image");
        if(use.Slot.Proxy.Kind!=DokObjectKind.Inline||use.Slot.Proxy.ClassId!=0xFF7DBFD4||
            use.Slot.Proxy.ObjectId!=use.Slot.ProxyObjectId||Scalar(use.Slot.Proxy,0x6C7321D2)!=file||Scalar(use.Slot.Proxy,0x00200571,4)!=tid)
            throw Error("static image selection differs from its actual source proxy");
        var container=repository.LoadTextureContainer("MaterialEditor",file);
        int index=checked((int)(tid & 0x7FFFFFFF));
        if ((uint)index >= container.Container.Textures.Count) throw Error("source proxy image selector exceeds its G1T directory");
        var image=container.Container.Textures[index];
        return new(use,container,image,ModelStageTextures.ReadNativeViewFormat(image),G1TextureSamplerReader.ReadRetainedSource(image));
    }

    public static G1TextureSamplerRecipe ReadProducerSampler(LoadedDeferredSamplerNativeSource native,
        ArmyFlagMaterialTextureUse use)
    {
        ArgumentNullException.ThrowIfNull(native); ArgumentNullException.ThrowIfNull(use);
        var rt=use.Slot.RenderTarget ?? throw Error("producer sampler requires an original RT texture declaration");
        return RenderSamplerSourceAdapters.DecodeKey(native,RenderSamplerSourceAdapters.FinalRenderTextureKey(native,rt));
    }

    /// <summary>Shared source table import for ColorVariation and the separately
    /// declared pole material. Runtime primaries remain explicit RT proxies.</summary>
    public static IReadOnlyList<ArmyFlagMaterialTextureSlot> ReadTextureSlots(
        LoadedObjects materials, LoadedRdbAsset table)
    {
        ArgumentNullException.ThrowIfNull(materials); ArgumentNullException.ThrowIfNull(table);
        if (table.Bytes.Length == 0 || table.Bytes.Length % 8 != 0)
            throw Error("requires complete material texture table rows");
        var slots = new List<ArmyFlagMaterialTextureSlot>();
        for (int at=0;at<table.Bytes.Length;at+=8)
        {
            int index = checked((int)U(table.Bytes,at)); uint proxyId = U(table.Bytes,at+4);
            if (index != slots.Count) throw Error("material texture table rows are not ordinal");
            var proxy = Unique(materials,proxyId);
            if (proxy.Kind != DokObjectKind.Inline) throw Error("material texture reference inheritance remains unsupported");
            uint? file=null,tid=null; RenderTextureDefinition? rt=null;
            if (proxy.ClassId == 0xFF7DBFD4) { file=Scalar(proxy,0x6C7321D2); tid=Scalar(proxy,0x00200571,4); }
            else if (proxy.ClassId == RenderTextureResources.Texture2DClassId)
                rt=RenderTextureResources.Read(materials,proxyId);
            else throw Error($"material texture proxy {proxyId:X8} has an unsupported native class");
            slots.Add(new(index,proxyId,proxy,file,tid,rt,table.Bytes.AsSpan(at,8).ToArray()));
        }
        return slots.AsReadOnly();
    }

    /// <summary>Bind the retained material GSTK to the actual selected shader,
    /// which may be a World2EC-retained base table rather than NormalShader.
    /// This is a source-load operation; frame preparation reuses the result.</summary>
    public static IReadOnlyList<ArmyFlagMaterialTextureUse> ResolveTextureUsesForActualShader(
        LoadedArmyFlagMaterialVariation actualMaterial, LoadedShaderContainer actualShader, int actualCombination)
    {
        ArgumentNullException.ThrowIfNull(actualMaterial); ArgumentNullException.ThrowIfNull(actualShader);
        return ResolveTextureUses(actualMaterial.StageTable, actualMaterial.Slots, actualShader, actualCombination);
    }

    public static IReadOnlyList<ArmyFlagMaterialTextureUse> ResolveTextureUsesForActualShader(
        ArmyFlagMaterialStageTable stage, IReadOnlyList<ArmyFlagMaterialTextureSlot> slots,
        LoadedShaderContainer actualShader, int actualCombination)
    {
        ArgumentNullException.ThrowIfNull(stage); ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(actualShader);
        return ResolveTextureUses(stage,slots,actualShader,actualCombination);
    }

    private static IReadOnlyList<ArmyFlagMaterialTextureUse> ResolveTextureUses(
        ArmyFlagMaterialStageTable stage,IReadOnlyList<ArmyFlagMaterialTextureSlot> slots,LoadedShaderContainer shader,
        int combination)
    {
        var metadata=shader.ReadG1ResourceBindings(combination);
        var ps=metadata.Combination.Stages.Single(s=>s.StageCode==0).Entry ?? throw Error("replacement has no selected pixel program");
        var rdef=shader.ReadResourceDefinitions(ps.ProgramIndex);
        var textures=rdef.Bindings.Where(b=>b.InputType==2).OrderBy(b=>b.BindPoint).ToArray();
        var samplers=rdef.Bindings.Where(b=>b.InputType==3).OrderBy(b=>b.BindPoint).ToArray();
        if (!textures.Select(b=>b.Name.Text).SequenceEqual(new[]{"sStage0","sRefRough","sOcc","sNMap"}) ||
            !textures.Select(b=>b.BindPoint).SequenceEqual(new uint[]{0,1,2,3}) ||
            !samplers.Select(b=>b.BindPoint).SequenceEqual(new uint[]{0,1,2,3}) || metadata.Entries.Count!=3)
            throw Error("replacement PS source texture inventory differs");
        var final=new Dictionary<int,G1TextureEntry>{[0]=stage.Entries[0],[1]=stage.Entries[stage.SpecialEntryIndex]};
        foreach(var entry in stage.Entries.Skip(stage.PrimaryCount))
        {
            var primary=metadata.Entries.SingleOrDefault(e=>e.PrimaryEncoded!=0&&Encoded(e.PrimaryEncoded)==Encoded(entry.RawValue4));
            var secondary=metadata.Entries.SingleOrDefault(e=>e.SecondarySamplerEncoded!=0&&e.SecondarySamplerEncoded!=e.PrimaryEncoded&&Encoded(e.SecondarySamplerEncoded)==Encoded(entry.RawValue6));
            if(primary is null || secondary is null && entry.RawValue6!=0 && entry.RawValue6!=entry.RawValue4)
                throw Error("GSTK entry lacks its original shader registration");
            final[primary.SlotIndex]=entry;
            if(secondary is not null && secondary.SlotIndex<16) final[secondary.SlotIndex]=entry;
        }
        if(!final.Keys.Order().SequenceEqual(new[]{0,1,2,3}))throw Error("source registration does not cover all selected PS texture slots");
        return final.OrderBy(p=>p.Key).Select(p=>
        {
            if(p.Value.RawValue0>=slots.Count)throw Error("GSTK entry exceeds its material texture table");
            return new ArmyFlagMaterialTextureUse((uint)p.Key,samplers[p.Key].BindPoint,textures[p.Key].Name.Text,samplers[p.Key].Name.Text,p.Value,slots[p.Value.RawValue0]);
        }).ToArray();
    }
    private static int Encoded(int value)
    { if(value<0||value>short.MaxValue)throw Error("unsupported signed source texture registration");
      int index=(value&7)+8*(((value>>3)&15)+10*(value>>7));if(index>=88)throw Error("texture registration exceeds source namespace");return index; }
    private static DokObject Object(LoadedObjects source,uint id,uint cls)
    {var item=Unique(source,id);if(item.Kind!=DokObjectKind.Inline||item.ClassId!=cls)throw Error($"object {id:X8} is not its declared inline class {cls:X8}");return item;}
    private static DokObject Unique(LoadedObjects source,uint id)
    {var matches=source.Document.Objects.Where(o=>o.ObjectId==id).ToArray();return matches.Length==1?matches[0]:throw Error($"object {id:X8} is absent or ambiguous");}
    private static DokProperty Field(DokObject item,uint key,uint type)
    {var fields=item.Properties.Where(p=>p.NameHash==key).ToArray();if(fields.Length!=1||fields[0].TypeCode!=type)throw Error($"object {item.ObjectId:X8} field {key:X8} differs");return fields[0];}
    private static uint Scalar(DokObject item,uint key,uint type=5)
    {var field=Field(item,key,type);if(field.ElementCount!=1||field.RawValue.Length!=4)throw Error("requires a typed four-byte scalar");return U(field.RawValue,0);}
    private static uint[] Values(DokObject item,uint key)
    {var field=Field(item,key,5);if(field.RawValue.Length!=field.ElementCount*4L)throw Error("requires a complete source uint array");return Enumerable.Range(0,field.ElementCount).Select(i=>U(field.RawValue,i*4)).ToArray();}
    private static uint U(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(at,4));
    private static ushort S(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(at,2));
    private static InvalidDataException Error(string message)=>new("Original Army flag material "+message+".");
}
