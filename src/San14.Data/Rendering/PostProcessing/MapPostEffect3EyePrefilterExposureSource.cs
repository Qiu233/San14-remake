namespace San14.Data;

/// <summary>Exact first Main View0 PS34 t1(0,0). A negative CB21+16
/// selects original unspecified61; other values retain the executed load
/// without output influence. Neither branch creates initialized history.</summary>
public sealed class MapPostEffect3EyePrefilterExposureSource
{
    private readonly MapPostEffect3TextureParameterState table;
    private readonly MapPostEffect3ResolvedTextureParameter pointer;
    private MapPostEffect3EyePrefilterExposureSource(LoadedMapPostEffect3EyeExposureInitializationSource initialization,
        MapPostEffect3EyeAdaptation2Frame frame,MapPostEffect3EyeBeforeBindings before,
        MapPostEffect3TextureParameterState table,MapPostEffect3BeginFunctorCounterReceipt begin,
        MapPostEffect3ApplyViewSlotSource view)
    {
        Initialization=initialization;Frame=frame;Before=before;this.table=table;
        ActualBegin=begin;ActualView=view;pointer=table.Require(61);
        Coverage=MapPostEffect3EyeGraphicsWriteCoverage.Prepare(table.Allocation,frame,before);
    }
    public LoadedMapPostEffect3EyeExposureInitializationSource Initialization{get;}
    public MapPostEffect3EyeAdaptation2Frame Frame{get;}
    public MapPostEffect3EyeBeforeBindings Before{get;}
    public MapPostEffect3EyeGraphicsWriteCoverage Coverage{get;}
    public MapPostEffect3BeginFunctorCounterReceipt ActualBegin{get;}
    public MapPostEffect3ApplyViewSlotSource ActualView{get;}
    public MapPostEffect3AllocationPlan Allocation=>table.Allocation;
    public long FrameNumber=>Frame.FrameNumber;
    public string ContextOwnerIdentity=>Frame.OwnerIdentity;
    public bool AdmitsFreshUnspecifiedValueDependency=>Coverage.RequiredShaderReads.Single(r=>r.SourceRegister==1).OutputDependsOnRead;
    public bool InitialPixelValuesAreKnown=>false;
    public bool EstablishesPriorPixelReadiness=>false;
    public bool ProvesNativeInitialBytesEqualBackendBytes=>false;
    public string SourceEvidence=>Initialization.SourceEvidence+";"+ActualBegin.SourceEvidence+";"+ActualView.SourceEvidence+
        ";"+Coverage.SourceEvidence+";original-first-Main-View0-PS34-t1-ld(0,0);"+
        (AdmitsFreshUnspecifiedValueDependency?"auto-negative-CB21+16-selects-source-unspecified-value":
            "actual-manual-CB21+16-output-independent-load-retained")+
        ";no-seed/no-exposure-readiness/no-native-backend-byte-equality";
    public void RequireCurrent()
    {
        Initialization.ValidateFor(Allocation);Frame.RequireCurrent();Coverage.RequireCurrent();
        ActualBegin.RequireCurrent();table.ValidateCurrent(pointer);
        var r=Coverage.RequiredShaderReads.SingleOrDefault(r=>r.SourceRegister==1);
        if(!ReferenceEquals(Frame.Allocation,Allocation)||FrameNumber!=table.FrameNumber||
            ContextOwnerIdentity!=table.ContextOwnerIdentity||Frame.State.SourceSha256!=table.SourceSha256||
            Before.Iteration.SourcePassIndex!=84||Before.Iteration.NativeCachedPassOrdinal!=0||Before.Iteration.NativeLoopIndex!=0||
            Coverage.PixelProgramSha256!="2427A9434AE69796287E1722E0B24D80BCDE596FD50948456C41CDBCE7C2EBA3"||
            r is null||r.SourceStageCode!=0||r.NativeBinding.ParameterIndex!=61||r.Kind!=MapPostEffect3ShaderReadFootprintKind.LiteralTexel||
            r.RelativeViewMipLevel!=0||r.LiteralX!=0||r.LiteralY!=0||r.LiteralZ!=0||!r.NativeReadInstructionExecutes||
            !Coverage.RequiredConstantBufferBytesSha256.Keys.Order().SequenceEqual(new[]{20,21})||
            ActualBegin.Counter!=1||ActualBegin.SelectedFunctorIndex!=1||ActualBegin.ModuleOwnerIdentity!=ContextOwnerIdentity||
            ActualBegin.ModuleSha256!=Frame.State.SourceSha256||ActualView.Index!=0||ActualView.FrameNumber!=FrameNumber||
            pointer.SlotRevision!=0||pointer.Reference.PhysicalAllocationParameterIndex!=61||
            pointer.Reference.InstanceIndex!=0||pointer.Reference.HasSourceSelectedMipView||
            pointer.Reference.ProducerOwnerIdentity!=ContextOwnerIdentity+"/texture61/side0")
            throw new InvalidDataException("Eye prefilter exposure needs its exact first source84 PS34/Main View0 pointer/CB consumer.");
    }
    public void RequireCurrentTextureTable(MapPostEffect3TextureParameterState actual)
    {if(!ReferenceEquals(table,actual))throw new InvalidDataException("Eye prefilter exposure belongs to another actual pointer table.");RequireCurrent();}
    public void RequireConsumer(int pass,uint register,string shader,IReadOnlyDictionary<int,string> cbs)
    {
        RequireCurrent();
        if(pass!=84||register!=1||shader!=Coverage.PixelProgramSha256||
            Coverage.RequiredConstantBufferBytesSha256.Any(e=>!cbs.TryGetValue(e.Key,out var v)||v!=e.Value))
            throw new InvalidDataException("Eye prefilter exposure cannot authorize another shader/register/CB20+21 consumer.");
    }
    public static MapPostEffect3EyePrefilterExposureSource Prepare(
        LoadedMapPostEffect3EyeExposureInitializationSource initialization,
        MapPostEffect3EyeAdaptation2Frame actualFrame,MapPostEffect3EyeBeforeBindings actualBefore,
        MapPostEffect3TextureParameterState actualTable,MapPostEffect3BeginFunctorCounterReceipt actualBegin,
        MapPostEffect3ApplyViewSlotSource actualView)
    {
        ArgumentNullException.ThrowIfNull(initialization);ArgumentNullException.ThrowIfNull(actualFrame);
        ArgumentNullException.ThrowIfNull(actualBefore);ArgumentNullException.ThrowIfNull(actualTable);
        ArgumentNullException.ThrowIfNull(actualBegin);ArgumentNullException.ThrowIfNull(actualView);
        var result=new MapPostEffect3EyePrefilterExposureSource(initialization,actualFrame,actualBefore,actualTable,actualBegin,actualView);
        result.RequireCurrent();return result;
    }
}
