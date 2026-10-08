using System.Buffers.Binary;
using San14.Data;

/// <summary>The actual managed render context's retained MaterialData list.
/// D60030 appends every reached reference, including repeated references.
/// This owns strong references until the caller releases the completed context;
/// the managed lifetime does not emulate unused native reference-count bits.</summary>
public sealed class OriginalNativeArmyMaterialRenderResourceContext:IDisposable
{
    private readonly List<OriginalNativeArmyWorldFlagMaterialData> materials=[];
    private bool alive=true;
    public OriginalNativeArmyMaterialRenderResourceContext(OriginalNativeMapArmyPreparedFrame actualArmy)
    {ArgumentNullException.ThrowIfNull(actualArmy);ActualArmy=actualArmy;}
    public OriginalNativeMapArmyPreparedFrame ActualArmy {get;}
    public int RetainedMaterialReferenceCount=>materials.Count;
    public void RetainActualWorldMaterialData(OriginalNativeMapArmyPreparedFrame actualArmy,
        OriginalNativeArmyWorldFlagMaterialData actualMaterialData)
    {
        ObjectDisposedException.ThrowIf(!alive,this);
        if(!ReferenceEquals(actualArmy,ActualArmy))
            throw new InvalidDataException("MaterialData retain belongs to another actual render context.");
        actualMaterialData.RequireRetained();materials.Add(actualMaterialData);
    }
    public void Dispose(){materials.Clear();alive=false;}
}

/// <summary>Actual parameter ready stores for the imported Army materials.
/// Source and native name directories load before normal requests. Current
/// CPU18 resolves through the complete owned source directory; original
/// B3B6F0 MISS plus empty F8 retains the constructor's explicitly null C8.
/// Nonempty context sources require their real producer and cannot be stubbed.</summary>
public sealed class OriginalNativeArmyMaterialParameterWriter:IOriginalArmyWorldFlagMaterialParameterWriter,IDisposable
{
    private sealed record Parameters(LoadedOriginalArmyMaterialParameterSource Source,
        OriginalArmyMaterialParameterCurrentFields<object> Fields,string Evidence);
    private readonly OriginalGameRepository repository;
    private readonly LoadedOriginalArmyMaterialParameterNativeSource native;
    private readonly LoadedOriginalModelNativeParameterDirectorySource names;
    private readonly LoadedOriginalArmyContextDataSourceDirectory contexts;
    private readonly Dictionary<uint,IReadOnlyList<OriginalArmyMaterialShaderParameterLookup>> shaderRows=[];
    private readonly Dictionary<OriginalNativeArmyWorldFlagMaterialData,Parameters> parameters=new(ReferenceEqualityComparer.Instance);
    private OriginalNativeArmyMaterialRenderResourceContext? currentContext;
    private bool alive=true;

    public OriginalNativeArmyMaterialParameterWriter(OriginalGameRepository repository,
        LoadedOriginalArmyMaterialParameterNativeSource actualParameterSource,
        LoadedOriginalModelNativeParameterDirectorySource actualNativeNames,
        LoadedOriginalArmyContextDataSourceDirectory actualContextDirectory)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(actualParameterSource);
        ArgumentNullException.ThrowIfNull(actualNativeNames);ArgumentNullException.ThrowIfNull(actualContextDirectory);
        this.repository=repository;native=actualParameterSource;names=actualNativeNames;contexts=actualContextDirectory;
        SourceEvidence=native.SourceEvidence+";"+names.SourceEvidence+";"+contexts.SourceEvidence+
            ";actual-ready-88/90/98/A0/B8/E8/F0;C8-source-constructor-preserved;"+
            "D60030-actual-MaterialData-retain;managed-context-strong-reference-lifetime";
    }
    public string SourceEvidence {get;}

    /// <summary>The caller owns this context and disposes it after its submitted
    /// rendering has completed. Rebinding does not release an in-flight context.</summary>
    public void BindActualRenderResourceContext(OriginalNativeArmyMaterialRenderResourceContext actualContext)
    {RequireCurrent();ArgumentNullException.ThrowIfNull(actualContext);currentContext=actualContext;}

    public void CompleteActualReadyParameters(OriginalNativeArmyWorldFlagMaterialData actualMaterialData)
    {
        RequireCurrent();actualMaterialData.RequireRetained();
        if(parameters.ContainsKey(actualMaterialData))return;
        var source=native.ReadMaterial(actualMaterialData.Source);
        var context=contexts.ReadActualContextDataLookup(source);
        // ReadCurrent rejects a real registered HIT whose producer is not yet
        // supported. The source filename fallback is independently selected.
        if(context.HasActualRegisteredDefinition)
            throw new NotSupportedException("Actual context Data requires its registered type13 producer.");
        if(source.HasFilenameContextSource)
            throw new NotSupportedException("Actual nonempty material F8 requires its filename context producer.");

        var fields=new OriginalArmyMaterialParameterCurrentFields<object>();
        var scalars=source.ReadImportedScalarParameters5();
        void Shader(OriginalNamedModelShader? shader,OriginalArmyMaterialStage1ParameterSlot slot)
        {
            if(shader is null)return; // The native stage1 dependency block is absent.
            if(!shaderRows.TryGetValue(shader.Object.ObjectId,out var rows))
                shaderRows.Add(shader.Object.ObjectId,rows=names.ReadFreshImportedFullTable(repository,shader));
            fields.PublishStage1ShaderParameterRecord(slot,source.BuildActualScalarAggregate15(scalars,rows));
        }
        var actualFields=actualMaterialData.Fields;
        Shader(source.Material.NormalShader,OriginalArmyMaterialStage1ParameterSlot.Normal88);
        Shader(source.Material.ClothShader,OriginalArmyMaterialStage1ParameterSlot.Cloth90);
        Shader(source.Material.StaticNormalShader,OriginalArmyMaterialStage1ParameterSlot.StaticNormal98);
        Shader(source.Material.StaticClothShader,OriginalArmyMaterialStage1ParameterSlot.StaticClothA0);
        if(actualFields.HasActualRegularData)
            fields.PublishStage1RegularParameterRecord12(source.ConstructInitialRecord12(OriginalArmyMaterialParameterPhase.RegularStage1));
        // CPU18 MISS and F8 null execute D9D7DB/D9D83C with no C8 store;
        // the loaded constructor explicitly wrote C8=0. No fake type13 record.
        if(actualFields.HasActualRenderTargetData)
            fields.PublishStage2RenderTargetParameterRecord12(source.ConstructInitialRecord12(OriginalArmyMaterialParameterPhase.RenderTargetStage2));
        parameters.Add(actualMaterialData,new(source,fields,
            context.SourceEvidence+";F8-source-null;D9B69B-C8-zero;D9D7DB/D9D83C-no-C8-store;"+
            "actual-parameter-ready-stores;type15-full-native-row-map"));
    }

    public OriginalArmyWorldFlagPrimitiveParameterInputs PublishActualPrimitiveInputs(
        OriginalNativeMapArmyPreparedFrame actualArmy,OriginalRemakeArmyFlagActor actualActor,int actualRawDraw,
        OriginalNativeArmyWorldFlagMaterialData actualMaterialData,bool actualPrior28And40Retained)
    {
        RequireCurrent();actualMaterialData.RequireRetained();
        var owned=ReadActualParameters(actualMaterialData);
        var context=currentContext??throw new InvalidOperationException("Army material publication requires its actual render resource context.");
        // CPU+C8 is independently published before the retain-base gate and is
        // genuinely null for this reached source. D60030 retains MaterialData.
        context.RetainActualWorldMaterialData(actualArmy,actualMaterialData);
        if(actualPrior28And40Retained)
            return new(actualMaterialData.Source.Material.ObjectId,null,null,owned.Evidence+";actual-prior28/40-retained;actual-MaterialData-enqueued");
        var material=owned.Fields.CurrentMaterialF0 as OriginalArmyMaterialParameterRecord12;
        var aggregate=owned.Fields.CurrentNormalA8 as OriginalArmyMaterialParameterAggregate15;
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

    public OriginalArmyMaterialParameterCurrentFields<object> ReadActualReadyParameterFields(
        OriginalNativeArmyWorldFlagMaterialData actualMaterialData)
    {RequireCurrent();return ReadActualParameters(actualMaterialData).Fields;}
    private Parameters ReadActualParameters(OriginalNativeArmyWorldFlagMaterialData actualMaterialData)
        =>parameters.TryGetValue(actualMaterialData,out var owned)?owned:
            throw new InvalidOperationException("Actual MaterialData has not completed its parameter ready stores.");
    public void RequireCurrent()=>ObjectDisposedException.ThrowIf(!alive,this);
    public void Dispose(){parameters.Clear();shaderRows.Clear();currentContext=null;alive=false;}
}
