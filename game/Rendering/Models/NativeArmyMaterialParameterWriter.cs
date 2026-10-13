using System.Buffers.Binary;
using San14.Data;
using San14.Data.Formats;

/// <summary>The actual managed render context's retained MaterialData list.
/// D60030 appends every reached reference, including repeated references.
/// This owns strong references until the caller releases the completed context;
/// the managed lifetime does not emulate unused native reference-count bits.</summary>
public sealed class NativeArmyMaterialRenderResourceContext:IDisposable
{
    private readonly List<NativeArmyWorldFlagMaterialData> materials=[];
    private bool alive=true;
    public NativeArmyMaterialRenderResourceContext(NativeMapArmyPreparedFrame actualArmy)
    {ArgumentNullException.ThrowIfNull(actualArmy);ActualArmy=actualArmy;}
    public NativeMapArmyPreparedFrame ActualArmy {get;}
    public int RetainedMaterialReferenceCount=>materials.Count;
    public void RetainActualWorldMaterialData(NativeMapArmyPreparedFrame actualArmy,
        NativeArmyWorldFlagMaterialData actualMaterialData)
    {
        ObjectDisposedException.ThrowIf(!alive,this);
        if(!ReferenceEquals(actualArmy,ActualArmy))
            throw new InvalidDataException("MaterialData retain belongs to another actual render context.");
        actualMaterialData.RequireRetained();materials.Add(actualMaterialData);
    }
    public void Dispose(){materials.Clear();alive=false;}
}

/// <summary>Actual parameter ready stores for the imported Army materials.
/// Source parameter declarations and shader names load before normal requests.
/// Texture-stage parameters come from their actual retained GSTK resource.
/// Named blend-state Data has a separate dependency and readiness owner.</summary>
public sealed class NativeArmyMaterialParameterWriter:IArmyWorldFlagMaterialParameterWriter,IDisposable
{
    private sealed record Parameters(LoadedArmyMaterialParameterSource Source,
        ArmyMaterialParameterCurrentFields<object> Fields,string Evidence);
    private readonly GameResourceRepository repository;
    private readonly LoadedArmyMaterialParameterNativeSource native;
    private readonly LoadedModelNativeParameterDirectorySource names;
    private readonly Dictionary<DokObject,IReadOnlyList<ArmyMaterialShaderParameterLookup>> shaderRows=new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<NativeArmyWorldFlagMaterialData,Parameters> parameters=new(ReferenceEqualityComparer.Instance);
    private NativeArmyMaterialRenderResourceContext? currentContext;
    private bool alive=true;

    public NativeArmyMaterialParameterWriter(GameResourceRepository repository,
        LoadedArmyMaterialParameterNativeSource actualParameterSource,
        LoadedModelNativeParameterDirectorySource actualNativeNames)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(actualParameterSource);
        ArgumentNullException.ThrowIfNull(actualNativeNames);
        this.repository=repository;native=actualParameterSource;names=actualNativeNames;
        SourceEvidence=native.SourceEvidence+";"+names.SourceEvidence+
            ";actual-ready-88/90/98/A0/B8/C8/E8/F0;GSTK-texture-stage-parameters;"+
            "D60030-actual-MaterialData-retain;managed-context-strong-reference-lifetime";
    }
    public string SourceEvidence {get;}

    /// <summary>The caller owns this context and disposes it after its submitted
    /// rendering has completed. Rebinding does not release an in-flight context.</summary>
    public void BindActualRenderResourceContext(NativeArmyMaterialRenderResourceContext actualContext)
    {RequireCurrent();ArgumentNullException.ThrowIfNull(actualContext);currentContext=actualContext;}

    public void CompleteActualReadyParameters(NativeArmyWorldFlagMaterialData actualMaterialData)
    {
        RequireCurrent();actualMaterialData.RequireRetained();
        if(parameters.ContainsKey(actualMaterialData))return;
        var source=native.ReadMaterial(actualMaterialData.Source);
        var textureStage=source.LoadTextureStageParameters(repository);
        if(textureStage is null&&source.HasFilenameContextSource)
            throw new NotSupportedException("Actual nonempty material F8 requires its filename context producer.");

        var fields=new ArmyMaterialParameterCurrentFields<object>();
        var scalars=source.ReadImportedScalarParameters5();
        void Shader(NamedModelShader? shader,ArmyMaterialStage1ParameterSlot slot)
        {
            if(shader is null)return; // The native stage1 dependency block is absent.
            if(!shaderRows.TryGetValue(shader.Object,out var rows))
                shaderRows.Add(shader.Object,rows=names.ReadFreshImportedFullTable(repository,shader));
            fields.PublishStage1ShaderParameterRecord(slot,source.BuildActualScalarAggregate15(scalars,rows));
        }
        var actualFields=actualMaterialData.Fields;
        Shader(source.Material.NormalShader,ArmyMaterialStage1ParameterSlot.Normal88);
        Shader(source.Material.ClothShader,ArmyMaterialStage1ParameterSlot.Cloth90);
        Shader(source.Material.StaticNormalShader,ArmyMaterialStage1ParameterSlot.StaticNormal98);
        Shader(source.Material.StaticClothShader,ArmyMaterialStage1ParameterSlot.StaticClothA0);
        if(actualFields.HasActualRegularData)
            fields.PublishStage1RegularParameterRecord12(source.ConstructInitialRecord12(ArmyMaterialParameterPhase.RegularStage1));
        if(textureStage is not null)fields.PublishActualContextParameterRecord13(textureStage);
        if(actualFields.HasActualRenderTargetData)
            fields.PublishStage2RenderTargetParameterRecord12(source.ConstructInitialRecord12(ArmyMaterialParameterPhase.RenderTargetStage2));
        parameters.Add(actualMaterialData,new(source,fields,
            (textureStage is null?"absent-stage-resource;empty-filename;constructor-C8-retained;":
                "actual-GSTK-resource;type13-C8-produced;filename-fallback-not-selected;")+
            "actual-parameter-ready-stores;type15-full-native-row-map"));
    }

    public ArmyWorldFlagPrimitiveParameterInputs PublishActualPrimitiveInputs(
        NativeMapArmyPreparedFrame actualArmy,RemakeArmyFlagActor actualActor,int actualRawDraw,
        NativeArmyWorldFlagMaterialData actualMaterialData,bool actualPrior28And40Retained)
    {
        RequireCurrent();actualMaterialData.RequireRetained();
        var owned=ReadActualParameters(actualMaterialData);
        var context=currentContext??throw new InvalidOperationException("Army material publication requires its actual render resource context.");
        // C8 is owned by the texture-stage parameter producer above. This
        // adapter returns material/scalar inputs; D60030 retains MaterialData.
        context.RetainActualWorldMaterialData(actualArmy,actualMaterialData);
        if(actualPrior28And40Retained)
            return new(actualMaterialData.Source.Material.ObjectId,null,null,owned.Evidence+";actual-prior28/40-retained;actual-MaterialData-enqueued");
        var material=owned.Fields.CurrentMaterialF0 as ArmyMaterialParameterRecord12;
        var aggregate=owned.Fields.CurrentNormalA8 as ArmyMaterialParameterAggregate15;
        int? materialId=null;
        if(aggregate is not null)
        {
            var scalar=aggregate.ScalarRecords.SingleOrDefault(x=>x.NativeSuffixName=="nMtrID");
            if(scalar is not null)
            {
                if(scalar.ElementCount!=1||scalar.Value.Length!=4)
                    throw new NotSupportedException("Current nMtrID is not the source int scalar consumed by this primitive.");
                materialId=BinaryPrimitives.ReadInt32LittleEndian(scalar.Value.Span);
            }
        }
        return new(actualMaterialData.Source.Material.ObjectId,material?.MaterialRecord64,materialId,
            owned.Evidence+";actual-F0/normalA8-to-Primitive28/40;actual-MaterialData-enqueued");
    }

    public ArmyMaterialParameterCurrentFields<object> ReadActualReadyParameterFields(
        NativeArmyWorldFlagMaterialData actualMaterialData)
    {RequireCurrent();return ReadActualParameters(actualMaterialData).Fields;}
    private Parameters ReadActualParameters(NativeArmyWorldFlagMaterialData actualMaterialData)
        =>parameters.TryGetValue(actualMaterialData,out var owned)?owned:
            throw new InvalidOperationException("Actual MaterialData has not completed its parameter ready stores.");
    public void RequireCurrent()=>ObjectDisposedException.ThrowIf(!alive,this);
    public void Dispose(){parameters.Clear();shaderRows.Clear();currentContext=null;alive=false;}
}
