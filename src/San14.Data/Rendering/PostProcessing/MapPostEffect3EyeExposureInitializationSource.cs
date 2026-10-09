using System.Security.Cryptography;

namespace San14.Data;

/// <summary>The original fresh owned61 creator supplies no initial pixels.
/// This grants the exact first CS52 texel0 load of unspecified device contents;
/// it does not grant readable initialized history or a known initial value.</summary>
public sealed class MapPostEffect3FreshUndefinedEyeExposureSource
{
    private readonly MapPostEffect3EyeComputeWriteCoverage coverage;
    private readonly MapPostEffect3TextureParameterState table;
    private readonly MapPostEffect3ResolvedTextureParameter pointer;
    private readonly MapPostEffect3BeginFunctorCounterReceipt begin;
    internal MapPostEffect3FreshUndefinedEyeExposureSource(
        LoadedMapPostEffect3EyeExposureInitializationSource source,
        MapPostEffect3AllocationPlan allocation,MapPostEffect3EyeComputeWriteCoverage coverage,
        MapPostEffect3TextureParameterState table,MapPostEffect3BeginFunctorCounterReceipt begin,
        MapPostEffect3ApplyViewSlotSource view)
    {
        Source=source;Allocation=allocation;this.coverage=coverage;this.table=table;
        this.begin=begin;ActualApplyView=view;pointer=table.Require(61);
    }
    public LoadedMapPostEffect3EyeExposureInitializationSource Source{get;}
    public MapPostEffect3AllocationPlan Allocation{get;}
    public MapPostEffect3ApplyViewSlotSource ActualApplyView{get;}
    public MapPostEffect3BeginFunctorCounterReceipt ActualBegin=>begin;
    public long FrameNumber=>coverage.FrameNumber;
    public string ContextOwnerIdentity=>coverage.ContextOwnerIdentity;
    public string SourceSha256=>coverage.SourceSha256;
    public string ProgramSha256=>coverage.ProgramSha256;
    public string ConstantBufferBytesSha256=>coverage.RequiredConstantBufferBytesSha256[24];
    public int ParameterIndex=>61;
    public int InstanceIndex=>0;
    public uint NativeViewFormat=>41;
    public bool PriorTexelLoadExecutes=>true;
    public bool PriorPixelsHaveKnownContents=>false;
    public bool EstablishesCompleteResourceContents=>false;
    public bool EstablishesGpuCompletion=>false;
    public MapPostEffect3EyeComputeWriteCoverage WriteCoverage=>coverage;
    public string SourceEvidence=>Source.SourceEvidence+";"+begin.SourceEvidence+";"+ActualApplyView.SourceEvidence+
        ";fresh-owned61-original-initialData-null;actual-first-source104-positive-adaptation-load-texel0;unspecified-prior/no-ready";

    public void RequireCurrent()
    {
        Source.ValidateFor(Allocation);coverage.RequireCurrent();begin.RequireCurrent();
        table.ValidateCurrent(pointer);
        if(!ReferenceEquals(table.Allocation,Allocation)||table.FrameNumber!=FrameNumber||
            table.ContextOwnerIdentity!=ContextOwnerIdentity||table.SourceSha256!=SourceSha256||
            begin.Counter!=1||begin.SelectedFunctorIndex!=1||begin.ModuleOwnerIdentity!=ContextOwnerIdentity||
            begin.ModuleSha256!=SourceSha256||ActualApplyView.Index!=0||ActualApplyView.FrameNumber!=FrameNumber||
            coverage.SourcePassIndex!=104||
            !coverage.PriorExposureTexelLoadExecutes||!coverage.RequiresPriorExposureTexelContents||
            coverage.AllowsFreshUninitializedExposureStorage||coverage.ParameterIndex!=61||
            coverage.TextureMipLevel!=0||coverage.TextureWidth!=2||coverage.TextureHeight!=1||
            coverage.ActualDispatch!=new MapPostEffect3DispatchExtent(1,1,1)||
            coverage.LocalSize!=new MapPostEffect3DispatchExtent(1,1,1)||
            pointer.SlotRevision!=0||pointer.Reference.PhysicalAllocationParameterIndex!=61||
            pointer.Reference.InstanceIndex!=0||pointer.Reference.HasSourceSelectedMipView||
            pointer.Reference.ProducerOwnerIdentity!=ContextOwnerIdentity+"/texture61/side0")
            throw new InvalidDataException("Undefined exposure load is not the exact first Main View0 owned61 source invocation.");
    }
    public void RequireCurrentTextureTable(MapPostEffect3TextureParameterState actualTable)
    {
        if(!ReferenceEquals(actualTable,table))
            throw new InvalidDataException("Undefined exposure source needs its exact frozen native pointer table.");
        RequireCurrent();
    }
}

public sealed class LoadedMapPostEffect3EyeExposureInitializationSource
{
    internal LoadedMapPostEffect3EyeExposureInitializationSource(LoadedDeferredSamplerNativeSource native,string evidence)=>(Native,SourceEvidence)=(native,evidence);
    public LoadedDeferredSamplerNativeSource Native{get;}
    public string Locale=>Native.Locale;
    public string SourceEvidence{get;}
    public bool NativeInitialDataIsNull=>true;
    public bool NativeClearsExposurePixels=>false;
    public bool InitialPixelValuesAreDefined=>false;
    public void ValidateFor(MapPostEffect3AllocationPlan allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);MapPostEffect3ParameterState.ValidateSource(allocation.Source);
        var a=allocation.TextureAllocations.Single(t=>t.Parameter.Index==61);
        var shape=MapPostEffect3AllocationShapeReader.Read(a);
        if(allocation.ResizeOnly||!a.CreateInThisInvocation||a.InstanceCount!=1||
            a.Parameter.Ownership!=MapPostEffect3AllocationOwnership.OwnedDeclaration||
            shape.Dimension!=MapPostEffect3TextureDimension.Texture2D||shape.Width!=2||shape.Height!=1||
            shape.Depth!=1||shape.MipLevels!=1||!shape.AllowsUnorderedAccess||!shape.HasRenderTargetPath||
            shape.Formats.ResourceFormat!=39||shape.Formats.ShaderResourceFormat0!=41||
            shape.Formats.RenderTargetFormat0!=41||shape.Formats.UnorderedAccessFormat!=41)
            throw new InvalidDataException("Undefined exposure creation needs exact source-owned61 single-side fixed2x1 R32_FLOAT.");
        int[] writers=allocation.Passes.Where(p=>p.ActiveBindings.Any(b=>
            b.Table==MapPostEffect3BindingTable.TextureParameters&&b.ParameterIndex==61&&
            b.Role is MapPostEffect3BindingRole.UnorderedAccessReadWrite or MapPostEffect3BindingRole.RenderTargetWrite))
            .Select(p=>p.Pass.Index).ToArray();
        if(!writers.SequenceEqual(new[]{104}))
            throw new InvalidDataException("Source exposure61 has another directory pixel writer.");
        _=MapPostEffect3Functor1FootprintSource.RequireProgram(allocation,104,5,52);
    }
    public MapPostEffect3FreshUndefinedEyeExposureSource Prepare(
        MapPostEffect3AllocationPlan allocation,MapPostEffect3EyeComputeWriteCoverage coverage,
        MapPostEffect3TextureParameterState actualTable,MapPostEffect3BeginFunctorCounterReceipt actualBegin,
        MapPostEffect3ApplyViewSlotSource actualApplyView)
    {
        ArgumentNullException.ThrowIfNull(coverage);ArgumentNullException.ThrowIfNull(actualTable);
        ArgumentNullException.ThrowIfNull(actualBegin);ArgumentNullException.ThrowIfNull(actualApplyView);
        ValidateFor(allocation);
        var result=new MapPostEffect3FreshUndefinedEyeExposureSource(this,allocation,coverage,actualTable,actualBegin,actualApplyView);
        result.RequireCurrent();return result;
    }
}

public static class MapPostEffect3EyeExposureInitializationSource
{
    public static LoadedMapPostEffect3EyeExposureInitializationSource Load(GameResourceRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return RenderSamplerSourceAdapters.ReadEyeExposureInitializationSource(
            RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale));
    }
}
public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3EyeExposureInitializationSource ReadEyeExposureInitializationSource(
        LoadedDeferredSamplerNativeSource native)
    {

        return new(native,native.ResourceUri+";owned61-flags7;-v20→→→"+
            "-initialData=null→→-device.v28;CPU-owner/view-header-clears-only"+
            "factory8-v38=-bloom85/86-mipviews-and69-alias/no61;source104-sole61-directory-writer"+
            "first-CS52-positive-adaptation-reads-source-unspecified-u0(0,0);no-invented-clear/copy/initial-pixels");
    }
}
