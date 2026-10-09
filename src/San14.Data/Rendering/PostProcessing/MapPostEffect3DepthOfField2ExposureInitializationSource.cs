namespace San14.Data;

/// <summary>The exact first Main View0 PS10/11 t2(0,0) value dependency on
/// original owned61 initialData=null. No initial value or ready history is
/// established. This receipt cannot authorize CS52 or another consumer.</summary>
public sealed class MapPostEffect3DepthOfField2FreshUndefinedExposureSource
{
    private readonly MapPostEffect3TextureParameterState table;
    private readonly MapPostEffect3ResolvedTextureParameter pointer;
    internal MapPostEffect3DepthOfField2FreshUndefinedExposureSource(
        LoadedMapPostEffect3EyeExposureInitializationSource initialization,
        MapPostEffect3DepthOfField2GraphicsSource consumer,
        MapPostEffect3TextureParameterState table,
        MapPostEffect3BeginFunctorCounterReceipt begin,MapPostEffect3ApplyViewSlotSource view)
    {
        Initialization=initialization;Consumer=consumer;this.table=table;
        ActualBegin=begin;ActualApplyView=view;pointer=table.Require(61);
    }
    public LoadedMapPostEffect3EyeExposureInitializationSource Initialization{get;}
    public MapPostEffect3DepthOfField2GraphicsSource Consumer{get;}
    public MapPostEffect3AllocationPlan Allocation=>table.Allocation;
    public MapPostEffect3BeginFunctorCounterReceipt ActualBegin{get;}
    public MapPostEffect3ApplyViewSlotSource ActualApplyView{get;}
    public long FrameNumber=>Consumer.FrameNumber;
    public string ContextOwnerIdentity=>Consumer.ContextOwnerIdentity;
    public int SourcePassIndex=>Consumer.SourcePassIndex;
    public string ProgramSha256=>Consumer.PixelProgramSha256;
    public string ConstantBufferBytesSha256=>Consumer.RequiredConstantBufferBytesSha256[18];
    public int ParameterIndex=>61;
    public uint SourceRegister=>2;
    public uint NativeMipLevel=>0;
    public uint NativeViewFormat=>41;
    public int ReadTexelX=>0;
    public int ReadTexelY=>0;
    public bool TexelValueAffectsOutput=>true;
    public bool InitialPixelValuesAreKnown=>false;
    public bool EstablishesPriorPixelReadiness=>false;
    public bool EstablishesGpuCompletion=>false;
    public bool ProvesNativeInitialBytesEqualBackendBytes=>false;
    public string SourceEvidence=>Initialization.SourceEvidence+";"+ActualBegin.SourceEvidence+";"+
        ActualApplyView.SourceEvidence+";"+Consumer.SourceEvidence+
        ";exact-first-Main-View0-PS10/11-auto-negative-CB18+12-t2-ld(0,0)-value-dependency;"+
        "source-unspecified-initialData-null/no-seed/no-pixel-readiness/no-native-backend-byte-equality";
    public void RequireCurrent()
    {
        Initialization.ValidateFor(Allocation);Consumer.RequireCurrentTextureTable(table);
        ActualBegin.RequireCurrent();table.ValidateCurrent(pointer);
        string expectedShader=SourcePassIndex switch
        {
            55=>"8FAF7FE2EB81CFF5906BAF6A69E78DCFA1621642B92473DC1E210FED5B8BD731",
            56=>"443BCD4B03CD6FAAB34AE24F3F82E18B2D656291D0F25862B0413E6C0430A3BA",
            _=>throw new InvalidDataException("Fresh undefined DOF exposure admits only exact source55/56 PS10/11.")
        };
        if(ProgramSha256!=expectedShader||!Consumer.ExposureTexelValueAffectsOutput||
            Consumer.AllowsUninitializedExposureLiteralRead||
            !Consumer.EffectivePixelTextureReads.Any(r=>r.ParameterIndex==61&&r.RegisterIndex==2&&
                r.Role==MapPostEffect3BindingRole.ShaderResourceRead&&(r.StageMask&4)!=0)||
            ActualBegin.Counter!=1||ActualBegin.SelectedFunctorIndex!=1||
            ActualBegin.ModuleOwnerIdentity!=ContextOwnerIdentity||ActualBegin.ModuleSha256!=Consumer.SourceSha256||
            ActualApplyView.Index!=0||ActualApplyView.FrameNumber!=FrameNumber||
            table.FrameNumber!=FrameNumber||table.ContextOwnerIdentity!=ContextOwnerIdentity||
            table.SourceSha256!=Consumer.SourceSha256||pointer.SlotRevision!=0||
            pointer.Reference.PhysicalAllocationParameterIndex!=61||pointer.Reference.InstanceIndex!=0||
            pointer.Reference.HasSourceSelectedMipView||
            pointer.Reference.ProducerOwnerIdentity!=ContextOwnerIdentity+"/texture61/side0")
            throw new InvalidDataException("Undefined DOF exposure is not its same-table first Main View0 auto source consumer.");
    }
    public void RequireCurrentTextureTable(MapPostEffect3TextureParameterState actual)
    {
        if(!ReferenceEquals(actual,table))throw new InvalidDataException("Fresh DOF exposure needs its exact source pointer table.");
        RequireCurrent();
    }
    public void RequireConsumer(MapPostEffect3DepthOfField2GraphicsSource actual,
        int pass,uint register,string programSha,IReadOnlyDictionary<int,string> actualConstantBufferSha256)
    {
        RequireCurrent();
        if(!ReferenceEquals(actual,Consumer)||pass!=SourcePassIndex||register!=SourceRegister||programSha!=ProgramSha256||
            !actualConstantBufferSha256.TryGetValue(18,out string? cb)||cb!=ConstantBufferBytesSha256)
            throw new InvalidDataException("Undefined DOF exposure cannot authorize another shader/register/CB18 consumer.");
    }
}

public static class MapPostEffect3DepthOfField2ExposureInitializationSource
{
    public static MapPostEffect3DepthOfField2FreshUndefinedExposureSource Prepare(
        LoadedMapPostEffect3EyeExposureInitializationSource initialization,
        MapPostEffect3DepthOfField2GraphicsSource actualConsumer,
        MapPostEffect3TextureParameterState actualTable,
        MapPostEffect3BeginFunctorCounterReceipt actualBegin,MapPostEffect3ApplyViewSlotSource actualView)
    {
        ArgumentNullException.ThrowIfNull(initialization);ArgumentNullException.ThrowIfNull(actualConsumer);
        ArgumentNullException.ThrowIfNull(actualTable);ArgumentNullException.ThrowIfNull(actualBegin);ArgumentNullException.ThrowIfNull(actualView);
        var source=new MapPostEffect3DepthOfField2FreshUndefinedExposureSource(
            initialization,actualConsumer,actualTable,actualBegin,actualView);
        source.RequireCurrent();return source;
    }
}
