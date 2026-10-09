namespace San14.Data;

/// <summary>First Main source100/101 executes PS50/51 ld t2(1,0) before
/// writer104. Negative CB22+24 selects original unspecified texel1. This
/// consumer receipt neither initializes61 nor proves native/backend equality.</summary>
public sealed class MapPostEffect3Composite2AddBloomExposureSource
{
    private readonly MapPostEffect3TextureParameterState table;
    private readonly MapPostEffect3ResolvedTextureParameter pointer;
    private MapPostEffect3Composite2AddBloomExposureSource(LoadedMapPostEffect3EyeExposureInitializationSource initialization,
        MapPostEffect3Composite2BeforeBindings before,MapPostEffect3Composite2ReadFootprint proof,
        MapPostEffect3TextureParameterState table,MapPostEffect3BeginFunctorCounterReceipt begin,
        MapPostEffect3ApplyViewSlotSource view)
        =>(Initialization,Before,Proof,this.table,pointer,ActualBegin,ActualView)=(initialization,before,proof,table,table.Require(61),begin,view);
    public LoadedMapPostEffect3EyeExposureInitializationSource Initialization{get;}
    public MapPostEffect3Composite2BeforeBindings Before{get;}
    public MapPostEffect3Composite2ReadFootprint Proof{get;}
    public MapPostEffect3BeginFunctorCounterReceipt ActualBegin{get;}
    public MapPostEffect3ApplyViewSlotSource ActualView{get;}
    public MapPostEffect3AllocationPlan Allocation=>Before.Frame.Allocation;
    public long FrameNumber=>Before.Frame.FrameNumber;
    public string ContextOwnerIdentity=>Before.Frame.OwnerIdentity;
    public bool InitialPixelValuesAreKnown=>false;
    public bool EstablishesPriorPixelReadiness=>false;
    public bool ProvesNativeInitialBytesEqualBackendBytes=>false;
    public string SourceEvidence=>Initialization.SourceEvidence+";"+Before.SourceEvidence+";"+Proof.SourceEvidence+
        ";"+ActualBegin.SourceEvidence+";"+ActualView.SourceEvidence+
        ";source100/101-before104-exact-PS50/51-t2-ld(1,0).x/negative-CB22+24-selects-unspecified-original-initialData-null;no-seed/no-ready/no-native-byte-equality";
    public void RequireCurrent()
    {
        Initialization.ValidateFor(Allocation);Before.RequireCurrent();Proof.RequirePrepared(Before.Frame.Prepared);
        ActualBegin.RequireCurrent();table.ValidateCurrent(pointer);
        _=Before.Frame.ValidateInitializationOutput79(table);
        var read=Proof.ShaderReads.SingleOrDefault(r=>r.SourceRegister==2);
        string expected=Before.SourcePassIndex switch{100=>"289138938C03716F6C03CB061548F722F124D423BC88CA3770EB8C5F8A6B2411",
            101=>"0BEDA5871170FCE6FEEF933E5D5BBA9D1DC2A3640B835D4BE2AEF2C4A2252B3D",_=>""};
        if(!ReferenceEquals(table.Allocation,Allocation)||table.FrameNumber!=FrameNumber||table.ContextOwnerIdentity!=ContextOwnerIdentity||
            Proof.SourcePassIndex!=Before.SourcePassIndex||Proof.PixelProgramSha256!=expected||expected.Length==0||
            read is null||read.SourceStageCode!=0||read.NativeBinding.ParameterIndex!=61||
            read.Kind!=MapPostEffect3ShaderReadFootprintKind.LiteralTexel||read.RelativeViewMipLevel!=0||
            read.LiteralX!=1||read.LiteralY!=0||read.LiteralZ!=0||!read.NativeReadInstructionExecutes||!read.OutputDependsOnRead||
            ActualBegin.Counter!=1||ActualBegin.SelectedFunctorIndex!=1||ActualBegin.ModuleOwnerIdentity!=ContextOwnerIdentity||
            ActualBegin.ModuleSha256!=Allocation.Source.Sha256||ActualView.Index!=0||ActualView.FrameNumber!=FrameNumber||
            pointer.SlotRevision!=0||pointer.Reference.PhysicalAllocationParameterIndex!=61||pointer.Reference.InstanceIndex!=0||
            pointer.Reference.HasSourceSelectedMipView||pointer.Reference.ProducerOwnerIdentity!=ContextOwnerIdentity+"/texture61/side0")
            throw new InvalidDataException("Cold AddBloom exposure requires exact first Main source100/101 PS50/51 texel1/CB22/pointer consumer.");
    }
    public void RequireCurrentTextureTable(MapPostEffect3TextureParameterState actual)
    {if(!ReferenceEquals(table,actual))throw new InvalidDataException("Cold AddBloom exposure belongs to another actual pointer table.");RequireCurrent();}
    public void RequireConsumer(int pass,uint register,string shader,IReadOnlyDictionary<int,string> cbs)
    {
        RequireCurrent();
        if(pass!=Before.SourcePassIndex||register!=2||shader!=Proof.PixelProgramSha256||
            !cbs.TryGetValue(22,out string? hash)||hash!=Proof.RequiredConstantBufferBytesSha256[22])
            throw new InvalidDataException("Cold AddBloom exposure cannot authorize another shader/register/CB22 or texel0 consumer.");
    }
    public static MapPostEffect3Composite2AddBloomExposureSource Prepare(
        LoadedMapPostEffect3EyeExposureInitializationSource initialization,MapPostEffect3Composite2BeforeBindings before,
        MapPostEffect3Composite2ReadFootprint proof,MapPostEffect3TextureParameterState table,
        MapPostEffect3BeginFunctorCounterReceipt begin,MapPostEffect3ApplyViewSlotSource view)
    {
        ArgumentNullException.ThrowIfNull(initialization);ArgumentNullException.ThrowIfNull(before);ArgumentNullException.ThrowIfNull(proof);
        ArgumentNullException.ThrowIfNull(table);ArgumentNullException.ThrowIfNull(begin);ArgumentNullException.ThrowIfNull(view);
        var result=new MapPostEffect3Composite2AddBloomExposureSource(initialization,before,proof,table,begin,view);
        result.RequireCurrent();return result;
    }
}
