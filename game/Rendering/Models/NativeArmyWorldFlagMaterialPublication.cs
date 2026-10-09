using San14.Data;

/// <summary>The reached parameter ready/callback owner. Texture-group Data
/// and shader-table readiness do not supply MaterialC8. Implementations own
/// the actual context Data lookup, parameter records and D60030 enqueue.</summary>
public interface IArmyWorldFlagMaterialParameterWriter
{
    void CompleteActualReadyParameters(NativeArmyWorldFlagMaterialData actualMaterialData);
    ArmyWorldFlagPrimitiveParameterInputs PublishActualPrimitiveInputs(
        NativeMapArmyPreparedFrame actualArmy,RemakeArmyFlagActor actualActor,int actualRawDraw,
        NativeArmyWorldFlagMaterialData actualMaterialData,
        bool actualPrior28And40Retained);
    void RequireCurrent();
    string SourceEvidence {get;}
}

public sealed record ArmyWorldFlagPrimitiveParameterInputs(uint ActualMaterialObjectId,
    ReadOnlyMemory<byte>? ActualPrimitiveMaterialRecord28,int? ActualMappedMaterialId,
    string SourceWriterEvidence);

/// <summary>The actual owned MaterialData in220 and passed to D60030. Its
/// identity is independent of Source.Material's declaration ID, MaterialCPU,
/// the context13 record and each shader/texture typed record.</summary>
public sealed class NativeArmyWorldFlagMaterialData
{
    internal NativeArmyWorldFlagMaterialData(LoadedArmyMaterialReadySource source,
        NativeArmyMaterialReadyOwner fields)=>(Source,Fields)=(source,fields);
    public LoadedArmyMaterialReadySource Source {get;}
    public NativeArmyMaterialReadyOwner Fields {get;}
    public void RequireRetained()=>Fields.RequireCurrent();
}

/// <summary>Current source-created World220/228, manager controller and full
/// typed16 publication for owned fresh flag primitives. The external parameter
/// writer is mandatory: this owner does not invent a missing type13 record,
/// execute an unimplemented context, or turn a declaration into current Data.</summary>
public sealed class NativeArmyWorldFlagMaterialPublication:
    IArmyWorldFlagMaterialTablePublication,IArmyWorldFlagMaterialResourceReceiver
{
    private sealed record Actor(RemakeArmyInitialization Initialization,RemakeArmyFlagSource Source,
        uint World2EC,NativeArmyWorldFlagMaterialBank<NativeArmyWorldFlagMaterialData> Bank);
    private readonly GameResourceRepository repository;
    private readonly NativeWorldShaderTableRegistry tables;
    private readonly NativeArmyMaterialTextureRegistry textures;
    private readonly IArmyWorldFlagMaterialParameterWriter parameters;
    private readonly LoadedObjects shaderObjects;
    private readonly ArmyWorldPreparedTableSource priorTables=new();
    private readonly Dictionary<string,Actor> actors=[];
    private readonly Dictionary<uint,NativeArmyWorldFlagMaterialData> materials=[];
    private readonly Dictionary<uint,NativeArmyFlagMaterialTextureGroupRecord> registeredGroups=[];
    private readonly Dictionary<(RemakeArmyFlagSource Source,int Draw),NativeWorldShaderTableState> baseTables=[];
    private NativeMapArmyPreparedFrame? current;
    private IReadOnlyDictionary<string,RemakeArmyFlagActor> currentActors=new Dictionary<string,RemakeArmyFlagActor>();
    private Action? requireCurrentArmy;
    private bool alive=true;
    private NativeArmyMaterialParameterWriter? ownedParameters;
    private NativeArmyMaterialTextureRegistry? ownedTextures;
    public NativeArmyWorldFlagMaterialPublication(GameResourceRepository repository,
        NativeWorldShaderTableRegistry actualSharedTables,NativeArmyMaterialTextureRegistry actualTextureDirectory,
        IArmyWorldFlagMaterialParameterWriter actualParameterWriter)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(actualSharedTables);
        ArgumentNullException.ThrowIfNull(actualTextureDirectory);ArgumentNullException.ThrowIfNull(actualParameterWriter);
        this.repository=repository;tables=actualSharedTables;textures=actualTextureDirectory;parameters=actualParameterWriter;
        shaderObjects=repository.LoadObjects("system",0x718CCB8B);
    }
    /// <summary>One source import for the owned renderer lifetime. The context
    /// directory is the real complete installed namespace; subsequent frames
    /// reuse it and the native parameter/name recipes without source reads.</summary>
    public static NativeArmyWorldFlagMaterialPublication CreateLoadedOwned(GameResourceRepository repository,
        NativeWorldShaderTableRegistry actualSharedTables,string actualSourceLocale)
    {
        var native=LoadedArmyMaterialParameterNativeSource.Load(repository,actualSourceLocale);
        var names=LoadedModelNativeParameterDirectorySource.Load(repository,actualSourceLocale);
        var contexts=LoadedArmyContextDataSourceDirectory.Load(repository,actualSourceLocale);
        var textures=new NativeArmyMaterialTextureRegistry();
        var parameters=new NativeArmyMaterialParameterWriter(repository,native,names,contexts);
        return new(repository,actualSharedTables,textures,parameters){ownedParameters=parameters,ownedTextures=textures};
    }

    /// <summary>The caller retains the returned actual frame context until
    /// rendering has completed. Binding a later frame does not release an
    /// earlier context whose accepted work is still in flight.</summary>
    public NativeArmyMaterialRenderResourceContext PrepareActualFrameWithOwnedResourceContext(
        NativeMapArmyPreparedFrame actualArmy,Action requireActualCurrentArmy)
    {
        var writer=ownedParameters??throw new InvalidOperationException("Borrowed parameter writer supplies its own actual render context.");
        var context=new NativeArmyMaterialRenderResourceContext(actualArmy);
        writer.BindActualRenderResourceContext(context);
        try{PrepareActualFrame(actualArmy,requireActualCurrentArmy);return context;}
        catch{context.Dispose();throw;}
    }
    public string SourceEvidence=>"actual-owned-World220/228;1C8850-manager/gate/variant+1/DC1120;"+
        "actual-registered-texture-Data8/20/30;D9D3C0-retained-fields;C243C0-fresh-prior;DB8BBB-current18/28/40-selection;"+parameters.SourceEvidence;

    public void PrepareActualFrame(NativeMapArmyPreparedFrame actualArmy,Action requireActualCurrentArmy)
    {
        ObjectDisposedException.ThrowIf(!alive,this);ArgumentNullException.ThrowIfNull(actualArmy);
        ArgumentNullException.ThrowIfNull(requireActualCurrentArmy);requireActualCurrentArmy();parameters.RequireCurrent();
        var next=actualArmy.Flags.Values.SelectMany(f=>f.Actors).ToDictionary(a=>a.InstanceKey,StringComparer.Ordinal);
        foreach(var actual in next.Values)
        {
            if(actors.TryGetValue(actual.InstanceKey,out var prior)&&ReferenceEquals(prior.Initialization,actual.Initialization)&&
                ReferenceEquals(prior.Source,actual.Source))continue;
            prior?.Bank.Retire();
            var groups=actual.Source.ReadDeclaredMaterialGroups(repository);
            actors[actual.InstanceKey]=new(actual.Initialization,actual.Source,
                ArmyMaterialReadySource.ReadWorldPreserve2EC(actual.Source.Catalog,actual.Category),
                NativeArmyWorldFlagMaterialBank<NativeArmyWorldFlagMaterialData>.CreateFreshClearedOwned(groups));
        }
        foreach(var key in actors.Keys.Where(k=>!next.ContainsKey(k)).ToArray())
        {actors[key].Bank.Retire();actors.Remove(key);}
        current=actualArmy;currentActors=next;requireCurrentArmy=requireActualCurrentArmy;
    }

    public void ReceiveActualReadyRegularTextureGroup(NativeMapArmyPreparedFrame actualArmy,
        RemakeArmyFlagActor actualActor,int actualRawDraw,LoadedArmyFlagMaterialVariation actualVariation,
        NativeArmyFlagMaterialTextureGroupRecord actualCompleteGroup)
    {
        RequireCurrent(actualArmy);var actor=ReadActor(actualActor);
        if(!ReferenceEquals(actualCompleteGroup.Source,actualVariation))
            throw new InvalidDataException("Army ready texture record belongs to another selected material resource.");
        uint group=actor.Bank.Source.ReadDeclaredDraw(actualRawDraw).DeclaredContainerGroupOrdinal??
            throw new InvalidDataException("Army controller has no current source group ordinal.");
        if(actualVariation.ManagerRow.FaceSlot!=group||actualVariation.ManagerRow.Category!=actualActor.Category||
            actualArmy.SourceMaterials?.Select(actualActor).ExplicitOwnedForceVariant!=actualVariation.OwnedForceVariant)
            throw new InvalidDataException("Army controller material does not match the actual frame force/group.");
        actualCompleteGroup.RequireCurrent();
        uint id=actualVariation.TextureGroup.ObjectId;
        if(!registeredGroups.TryGetValue(id,out var retained)||!ReferenceEquals(retained,actualCompleteGroup))
        {actualCompleteGroup.RegisterActualReadyData(textures);registeredGroups[id]=actualCompleteGroup;}
        uint materialId=actualVariation.SelectedMaterialObjectId;
        if(!materials.TryGetValue(materialId,out var material))
        {
            var source=ArmyMaterialReadySource.Load(repository,actualVariation.Materials,materialId,shaderObjects);
            var fields=NativeArmyMaterialReadyOwner.CreateLoaded(source,tables,textures);
            // These calls stand at real owned dependency completion. The
            // parameter owner must finish its actual CPU18/F8 route before a
            // controller publishes this Data to the current World bank.
            fields.ApplyReadyStage1Fields();fields.ApplyReadyStage2Fields();
            material=new(source,fields);
            parameters.CompleteActualReadyParameters(material);
            materials.Add(materialId,material);
        }
        material.Fields.RequireCurrent();parameters.RequireCurrent();
        actor.Bank.ApplyActualControllerAssignment(actualVariation.ManagerRow.ManagerKind,actualVariation.OwnedForceVariant,
            group,actualVariation,material);
    }

    /// <summary>The actual current World220 material's stage2 D0. An empty
    /// bank slot or a completed ready null remains null. The SDF consumer owns
    /// checking its separate 28B/+20 typed record shape. No source lookup,
    /// material callback or D600 resource append is performed by this getter.</summary>
    public INativeArmyMaterialTextureTypedRecord? ReadActualReadyRenderTargetD0(
        RemakeArmyFlagActor actualActor,int sourceDrawIndex)
    {
        var frame=current??throw new InvalidOperationException("Army material owner has no actual prepared frame.");
        RequireCurrent(frame);
        var material=ReadActor(actualActor).Bank.ReadActualRawDraw(sourceDrawIndex);
        return material?.Fields.ActualReadyRenderTargetD0;
    }

    public ArmyWorldFlagPrimitiveMaterialTable ReadActualFinalTable(RemakeArmyFlagActor actualActor,
        int sourceDrawIndex,LoadedArmyFlagMaterialVariation? actualVariation)
    {
        var frame=current??throw new InvalidOperationException("Army material owner has no actual prepared frame.");
        RequireCurrent(frame);var actor=ReadActor(actualActor);
        var group=actor.Bank.Source.ReadDeclaredDraw(sourceDrawIndex);
        var key=(actualActor.Source,sourceDrawIndex);
        if(!baseTables.TryGetValue(key,out var declaredBase))
            baseTables.Add(key,declaredBase=tables.RetainImportedTable(
                ModelShaderResources.ResolveDrawBaseSource(actualActor.Source.ShaderBindings,sourceDrawIndex,shaderObjects).Binding.Shader));
        var prior=priorTables.ReadActualFreshPriorTable(actualActor.Source.Catalog,actualActor.Category,
            actualActor.Source.Model.Geometry,sourceDrawIndex,declaredBase);
        var material=actor.Bank.ReadActualRawDraw(sourceDrawIndex);
        if(material is null)
        {
            if(actualVariation is not null)throw new NotSupportedException("Army cloth controller did not publish its actual material Data.");
            return new(actualActor,sourceDrawIndex,group.DeclaredContainerGroupOrdinal??uint.MaxValue,0,prior,
                actor.Bank.SourceEvidence+";actual220-null;fresh-Model-prior18/28/40/38-retained")
                {ActualParameterRoute=ArmyWorldPrimitiveParameterRoute.ActualPriorRetained};
        }
        if(actualVariation is not null&&actualVariation.SelectedMaterialObjectId!=material.Source.Material.ObjectId)
            throw new InvalidDataException("Army current bank has another controller-selected material.");
        material.Fields.RequireCurrent();
        bool preserve=actor.World2EC!=0&&ReferenceEquals(material.Fields.CurrentTexturesE0,material.Fields.RegularTexturesC0);
        var final=material.Fields.ReadWorldTableAfterActualPriorPublication(actor.World2EC,prior,false,false)??
            throw new NotSupportedException("Actual Army current material publishes a null full typed16 table.");
        var inputs=parameters.PublishActualPrimitiveInputs(frame,actualActor,sourceDrawIndex,material,preserve);
        parameters.RequireCurrent();
        if(inputs.ActualMaterialObjectId!=material.Source.Material.ObjectId||string.IsNullOrWhiteSpace(inputs.SourceWriterEvidence))
            throw new InvalidDataException("Army parameter callback publication belongs to another current Material Data.");
        return new(actualActor,sourceDrawIndex,group.DeclaredContainerGroupOrdinal??uint.MaxValue,
            material.Source.Material.ObjectId,final,actor.Bank.SourceEvidence+";"+material.Fields.SourceEvidence+";"+inputs.SourceWriterEvidence)
        {
            ActualCurrentTextures=material.Fields.CurrentTexturesE0 as NativeArmyFlagMaterialTextureGroupRecord??
                throw new NotSupportedException("Current Army E0 typed texture-group producer is not supported by this flag packet consumer."),
            ActualParameterRoute=preserve?ArmyWorldPrimitiveParameterRoute.ActualPriorRetained:ArmyWorldPrimitiveParameterRoute.ActualCurrentMaterial,
            ActualMaterialRecord64=preserve?null:inputs.ActualPrimitiveMaterialRecord28,
            ActualMaterialId=preserve?null:inputs.ActualMappedMaterialId
        };
    }
    private Actor ReadActor(RemakeArmyFlagActor actualActor)
    {
        if(!currentActors.TryGetValue(actualActor.InstanceKey,out var currentActor)||!ReferenceEquals(actualActor,currentActor))
            throw new InvalidDataException("Army material lookup belongs to another actual actor publication.");
        return actors[actualActor.InstanceKey];
    }
    public void RequireCurrent(NativeMapArmyPreparedFrame actualArmy)
    {
        ObjectDisposedException.ThrowIf(!alive,this);
        if(!ReferenceEquals(current,actualArmy))throw new InvalidDataException("Army material publication belongs to another current frame.");
        requireCurrentArmy!();parameters.RequireCurrent();
        var retained=new HashSet<NativeArmyWorldFlagMaterialData>(ReferenceEqualityComparer.Instance);
        foreach(string key in currentActors.Keys)
            foreach(var draw in actors[key].Bank.Source.Draws)
                if(actors[key].Bank.ReadActualRawDraw(draw.SourceRawDrawIndex) is {} material&&retained.Add(material))
                    material.RequireRetained();
    }
    public void Retire()
    {
        alive=false;current=null;currentActors=new Dictionary<string,RemakeArmyFlagActor>();
        foreach(var actor in actors.Values)actor.Bank.Retire();actors.Clear();
        foreach(var material in materials.Values)material.Fields.Invalidate("World flag material owner retired");materials.Clear();
        baseTables.Clear();registeredGroups.Clear();requireCurrentArmy=null;
        ownedParameters?.Dispose();ownedParameters=null;
        ownedTextures?.Invalidate("Owned Army material publication retired");ownedTextures=null;
    }
}
