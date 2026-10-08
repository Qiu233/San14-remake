using Godot;
using San14.Data;

public sealed partial class OriginalMapPostEffect3DepthOfField2ModuleResources
{
    // Bounded by the actual two accumulation parameters and physical sides.
    // Contents keep their real writer frame/revision, without an old frame,
    // table, invocation or completed command graph.
    private readonly Dictionary<(int Parameter,int Side),AccumulationContents> accumulation=[];
    private sealed record AccumulationContents(IOriginalMapPostEffect3ComputeMipTextureLease Pixels,long Revision);

    internal void RecordAccumulation(OriginalMapPostEffect3DepthOfField2ExecutionOwner owner,
        OriginalMapPostEffect3FullscreenCompletedPass completed,IOriginalMapPostEffect3ComputeMipTextureLease pixels)
    {
        RequireActive(owner);pixels.RequireReadableMipRange(0,1);
        if(Scatter is null||!ReferenceEquals(Scatter.LastCompletedPass,completed)||
            completed.FrameNumber!=owner.Prepared.FrameNumber||completed.SourcePassIndex is not(62 or 63)||
            completed.Writes is not[var write]||write.WriterRevision is not long revision||
            write.PhysicalAllocationParameterIndex!=pixels.ParameterIndex||write.InstanceIndex!=pixels.InstanceIndex||
            write.StorageRid!=pixels.StorageRid||pixels.FrameNumber!=completed.FrameNumber||
            pixels.ParameterIndex!=(completed.SourcePassIndex==62?63:64))
            throw new InvalidDataException("Retained accumulation requires the actual published source scatter writer.");
        accumulation[(pixels.ParameterIndex,pixels.InstanceIndex)]=new(pixels,revision);
    }

    internal OriginalMapPostEffect3DepthOfField2RetainedAccumulation CaptureAccumulation(
        OriginalMapPostEffect3DepthOfField2ExecutionOwner owner,
        OriginalMapPostEffect3DepthOfField2BeforeBindings before,
        OriginalMapPostEffect3DepthOfField2TargetView view)
    {
        RequireActive(owner);before.RequirePrepared(owner.Prepared);owner.TextureParameters.ValidateCurrent(view.ActualPointer);
        int parameter=before.SourcePassIndex==62?63:64;int side=view.ActualPointer.Reference.InstanceIndex;
        if(before.SourcePassIndex is not(62 or 63)||before.RequiresNativeClearMaskAndColorOverride||
            !owner.Prepared.RequiresSpriteCountReadback||owner.ActualSpriteCountReadback is null||
            !accumulation.TryGetValue((parameter,side),out var contents))
            throw new InvalidDataException("Source no-clear scatter needs actual Count0 Before57 and its retained destination pixels.");
        var result=new OriginalMapPostEffect3DepthOfField2RetainedAccumulation(this,owner,before,view,contents.Pixels,contents.Revision);
        result.RequireCurrent();return result;
    }
    internal void RequireAccumulation(OriginalMapPostEffect3DepthOfField2RetainedAccumulation actual,
        OriginalMapPostEffect3DepthOfField2ExecutionOwner owner,
        OriginalMapPostEffect3DepthOfField2BeforeBindings before,OriginalMapPostEffect3DepthOfField2TargetView view,
        IOriginalMapPostEffect3ComputeMipTextureLease pixels,long revision)
    {
        RequireActive(owner);before.RequirePrepared(owner.Prepared);owner.TextureParameters.ValidateCurrent(view.ActualPointer);
        int parameter=before.SourcePassIndex==62?63:64;int side=view.ActualPointer.Reference.InstanceIndex;
        if(before.SourcePassIndex is not(62 or 63)||before.RequiresNativeClearMaskAndColorOverride||
            view.ActualPointer.ParameterIndex!=parameter||view.ActualPointer.Reference.PhysicalAllocationParameterIndex!=parameter||
            view.ActualPointer.Reference.HasSourceSelectedMipView||
            !accumulation.TryGetValue((parameter,side),out var contents)||!ReferenceEquals(contents.Pixels,pixels)||contents.Revision!=revision||
            pixels.FrameNumber<=0||pixels.FrameNumber>=owner.Prepared.FrameNumber||pixels.StorageRid!=view.StorageRid||
            pixels.NativeViewFormat!=10||pixels.DeviceFormat!=view.DeviceFormat||
            pixels.Extent!=new Vector3I(view.Extent.X,view.Extent.Y,1)||revision<=0||
            !ReferenceEquals(actual.Module,this))
            throw new InvalidDataException("Retained scatter destination differs from its actual module/physical side/writer/extent.");
        pixels.RequireReadableMipRange(0,1);
    }
}

/// <summary>One current no-clear consumer of actual retained accumulation
/// pixels. The old writer identity remains explicit; source counters or CPU
/// Return cannot construct this capability.</summary>
public sealed class OriginalMapPostEffect3DepthOfField2RetainedAccumulation
{
    internal readonly OriginalMapPostEffect3DepthOfField2ModuleResources Module;
    private readonly OriginalMapPostEffect3DepthOfField2ExecutionOwner owner;
    private readonly OriginalMapPostEffect3DepthOfField2BeforeBindings before;
    private readonly OriginalMapPostEffect3DepthOfField2TargetView view;
    private readonly IOriginalMapPostEffect3ComputeMipTextureLease pixels;
    internal OriginalMapPostEffect3DepthOfField2RetainedAccumulation(OriginalMapPostEffect3DepthOfField2ModuleResources module,
        OriginalMapPostEffect3DepthOfField2ExecutionOwner owner,OriginalMapPostEffect3DepthOfField2BeforeBindings before,
        OriginalMapPostEffect3DepthOfField2TargetView view,IOriginalMapPostEffect3ComputeMipTextureLease pixels,long revision)
        =>(Module,this.owner,this.before,this.view,this.pixels,WriterRevision)=(module,owner,before,view,pixels,revision);
    public long WriterFrameNumber=>pixels.FrameNumber;
    public long WriterRevision{get;}
    public Rid StorageRid=>pixels.StorageRid;
    public int ParameterIndex=>pixels.ParameterIndex;
    public int InstanceIndex=>pixels.InstanceIndex;
    // Keep provenance by fixed physical identity and writer revision. Do not
    // recursively embed every older preserved destination's evidence string.
    public string SourceEvidence=>$"source-DOF2-scatter63/64/physicalRID={StorageRid.Id}/side={InstanceIndex};"+
        "source-Before57-real-count0-map;conditional-clear-absent/retain-complete-destination;"+
        $"actual-prior-writer-frame={WriterFrameNumber}/revision={WriterRevision};no-current-frame-readiness-upgrade";
    public void RequireCurrent()=>Module.RequireAccumulation(this,owner,before,view,pixels,WriterRevision);
    internal void RequireFor(OriginalMapPostEffect3DepthOfField2ScatterInputs input)
    {
        RequireCurrent();
        if(!ReferenceEquals(input.Prepared,owner.Prepared)||!ReferenceEquals(input.TextureParameters,owner.TextureParameters)||
            !ReferenceEquals(input.Before,before)||input.ActualTargetView!=view||!ReferenceEquals(input.PriorDestination,this))
            throw new InvalidDataException("Source preserved destination belongs to another current scatter submission.");
    }
}
