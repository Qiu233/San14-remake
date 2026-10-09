using Godot;
using San14.Data;

/// <summary>The actual scatter submission owner exposes its borrowed bank
/// and latest original indirect command. Allocation metadata is insufficient.</summary>
public interface IMapPostEffect3DepthOfField2ScatterSubmissionSource
{
    PostProcessResourceBank Bank { get; }
    MapPostEffect3FullscreenCompletedPass? LastCompletedPass { get; }
}

public sealed partial class MapPostEffect3WorldNativeFullscreenFrontendOwner
{
    private MapPostEffect3DepthOfField2BeforeBindings? actualDofCompute;
    /// <summary>Actual source57/58 Select/Before/compute Bind. CS mode0
    /// uses its counted resources without pushing a color target row. Native
    /// mode changes and ResetPass clear all four cached RT slots. This records
    /// the transition and requires the subsequent actual GPU dispatch.</summary>
    public void BindDepthOfField2Compute(MapPostEffect3DepthOfField2BeforeBindings before)
    {
        ArgumentNullException.ThrowIfNull(before);RequireCurrent();before.RequireCurrent();RequireReleasedBeforeNextBind();
        var selected=MapPostEffect3PassStateReader.Read(World.Bank.Source.Source,Source.Locale)[before.SourcePassIndex];
        if(before.FrameNumber!=World.Context.FrameNumber||
            before.ContextOwnerIdentity!=World.Bank.Source.ContextOwnerIdentity||before.SourcePassIndex is not(57 or 58)||
            !before.RequiresGenericComputeDispatch||selected.Factory.Index!=7||
            selected.AutomaticExecution!=MapPostEffect3AutomaticExecution.ComputeDispatch||
            World.Bank.Source.Source.Passes[before.SourcePassIndex].ProgramReferences.Where(r=>r.StageIndex is not null).ToArray() is not [{StageCode:5}])
            throw new InvalidDataException("DOF2 compute Bind requires this actual57/58 CS and the released preceding graphics row.");
        // Select CS/mode0 and reset the
        //complete cache when mode differs. Every preceding standard graphics
        // wrapper also reset cached bindings. No color target is inherited as
        //a compute output and no pixel contents are granted by this binding.
        directoryCursor?.EnterSelectedBefore(before.SourcePassIndex,0);
        directoryCursor?.RecordBeforeReturned(before);directoryCursor?.ApplyDepthBefore(before);
        actualDofCompute=before;revision=checked(revision+1);
        directoryCursor?.RecordBindReturned(before,before);
    }
    public void AcceptDepthOfField2Compute(MapPostEffect3DepthOfField2BeforeBindings before,
        MapPostEffect3ComputeExecutor actualExecutor,MapPostEffect3ComputeCompletedPass completed)
    {
        ArgumentNullException.ThrowIfNull(before);ArgumentNullException.ThrowIfNull(actualExecutor);
        ArgumentNullException.ThrowIfNull(completed);RequireCurrent();before.RequireCurrent();
        var reference=World.Bank.Source.Source.Passes[before.SourcePassIndex].ProgramReferences.Single(r=>r.StageCode==5);
        var shader=World.Bank.Source.Source.GetProgram(5,reference.StageIndex!.Value);
        if(!ReferenceEquals(actualDofCompute,before)||!ReferenceEquals(actualExecutor.Bank,World.Bank)||
            !ReferenceEquals(actualExecutor.LastCompletedPass,completed)||completed.SourcePassIndex!=before.SourcePassIndex||
            completed.FrameNumber!=before.FrameNumber||completed.ContextOwnerIdentity!=before.ContextOwnerIdentity||
            completed.ProgramSha256!=shader.Sha256||completed.OrderedEffectiveBindings.Count==0)
            throw new InvalidDataException("DOF2 compute Release requires its actual selected shader dispatch and current resource receipt.");
        // Dispatch the automatic CS command and clear the
        // CS frontend binding resets all native cached resources.
        //The GPU executor has already submitted the original dispatch here.
        var history=submissionLedger?.CaptureDepthCompute(this,before,actualExecutor,completed);
        actualDofCompute=null;
        if(history is not null)submissionLedger!.AcceptAfterActualRelease(history);
    }
    /// <summary>Source post-Bind state for the actual factory7 ordered MRT
    /// metadata. Its owner applies Execute scissor/blend afterwards. These
    /// metadata capabilities do not supply target pixels or writable leases.</summary>
    public IMapPostEffect3FullscreenDrawContextLease BindDepthOfField2(
        MapPostEffect3DepthOfField2BeforeBindings before,
        IReadOnlyList<MapPostEffect3DepthOfField2TargetView> actualTargetViews)
    {
        ArgumentNullException.ThrowIfNull(before);ArgumentNullException.ThrowIfNull(actualTargetViews);
        RequireCurrent();before.RequireCurrent();RequireReleasedBeforeNextBind();
        var state=MapPostEffect3PassStateReader.Read(World.Bank.Source.Source,Source.Locale)[before.SourcePassIndex];
        if(before.FrameNumber!=World.Context.FrameNumber||
            before.ContextOwnerIdentity!=World.Bank.Source.ContextOwnerIdentity||before.RequiresGenericComputeDispatch||
            before.RequiresNativeIndirectDraw&&before.SourcePassIndex is not(62 or 63)||state.Factory.Index!=7||state.NativeFlags!=0||
            state.AutomaticExecution!=MapPostEffect3AutomaticExecution.SpecificFactoryGraphics||
            actualTargetViews.Count is <1 or >4||!actualTargetViews.Select(v=>v.SourceTargetRegister).SequenceEqual(Enumerable.Range(0,actualTargetViews.Count)))
            throw new NotSupportedException("DOF2 Bind requires this actual ordinary source command and complete contiguous color MRT metadata.");
        var selected=World.Bank.Source.Passes[before.SourcePassIndex].ActiveBindings
            .Where(r=>r.Role==MapPostEffect3BindingRole.RenderTargetWrite).GroupBy(r=>r.RegisterIndex)
            .ToDictionary(g=>(int)g.Key,g=>World.TextureParameters.Require(g.Last().ParameterIndex));
        if(before.ActualTarget0 is{} dynamicTarget)selected[0]=dynamicTarget;
        if(selected.Count!=actualTargetViews.Count||actualTargetViews.Any(v=>
            !selected.TryGetValue(v.SourceTargetRegister,out var pointer)||pointer!=v.ActualPointer||
            !v.StorageRid.IsValid||!v.TargetViewRid.IsValid||v.Extent.X<=0||v.Extent.Y<=0||string.IsNullOrWhiteSpace(v.SourceEvidence)))
            throw new InvalidDataException("DOF2 current target metadata omitted its final directory/Before target setter.");
        foreach(var view in actualTargetViews)World.TextureParameters.ValidateCurrent(view.ActualPointer);
        directoryCursor?.EnterSelectedBefore(before.SourcePassIndex,0);
        directoryCursor?.RecordBeforeReturned(before);directoryCursor?.ApplyDepthBefore(before);
        directoryCursor?.Cache.RequireActualTargets(before.SourcePassIndex,actualTargetViews);
        var binding=new NativeContext(this,before.SourcePassIndex,checked(++revision),before.SourceEvidence);
        current=binding;
        try
        {
            var metadata=actualTargetViews.Select(v=>(IMapPostEffect3StockGraphicsTargetLease)new DofTargetMetadata(this,before,v)).ToArray();
            binding.BindActualTargets(Array.AsReadOnly(metadata));directoryCursor?.RecordBindReturned(before,binding);return binding;
        }
        catch{current=null;throw;}
    }
    public void AcceptDepthOfField2Graphics(MapPostEffect3DepthOfField2GraphicsCoverageReceipt coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);RequireCurrent();coverage.RequireCurrentSubmission();
        if(!ReferenceEquals(coverage.Owner.Bank,World.Bank))throw new InvalidDataException("DOF2 release belongs to another actual module owner.");
        var history=submissionLedger?.CaptureDepthGraphics(this,coverage);
        ReleaseActualCompleted(coverage.Completed);
        if(history is not null)submissionLedger!.AcceptAfterActualRelease(history);
    }
    public void AcceptDepthOfField2Scatter(IMapPostEffect3DepthOfField2ScatterSubmissionSource actualExecutor,
        MapPostEffect3FullscreenCompletedPass completed)
    {
        ArgumentNullException.ThrowIfNull(actualExecutor);ArgumentNullException.ThrowIfNull(completed);RequireCurrent();
        if(!ReferenceEquals(actualExecutor.Bank,World.Bank)||!ReferenceEquals(actualExecutor.LastCompletedPass,completed)||
            completed.SourcePassIndex is not(62 or 63))
            throw new InvalidDataException("DOF2 scatter release requires the actual source62/63 executor and current original indirect submission.");
        var history=submissionLedger is null?null:submissionLedger.CaptureDepthScatter(this,
            actualExecutor as MapPostEffect3DepthOfField2ScatterExecutor??
                throw new InvalidDataException("Historical scatter authority requires the sealed real submission executor."),completed);
        ReleaseActualCompleted(completed);
        if(history is not null)submissionLedger!.AcceptAfterActualRelease(history);
    }
    private sealed record DofTargetMetadata(MapPostEffect3WorldNativeFullscreenFrontendOwner Owner,
        MapPostEffect3DepthOfField2BeforeBindings Before,MapPostEffect3DepthOfField2TargetView View)
        : IMapPostEffect3StockGraphicsTargetLease
    {
        public long FrameNumber=>Before.FrameNumber;public string ContextOwnerIdentity=>Before.ContextOwnerIdentity;
        public string OwnedFrontendIdentity=>Owner.World.CurrentStock.OwnedFrontendIdentity;
        public string ViewIdentity=>Owner.World.CurrentStock.ViewIdentity;
        public int? ParameterIndex=>View.ActualPointer.ParameterIndex;
        public int InstanceIndex=>View.ActualPointer.Reference.InstanceIndex;
        public string ProducerOwnerIdentity=>View.ActualPointer.Reference.ProducerOwnerIdentity;
        public Rid StorageRid=>View.StorageRid;public Rid TargetViewRid=>View.TargetViewRid;
        public uint NativeViewFormat=>View.NativeViewFormat;public RenderingDevice.DataFormat DeviceFormat=>View.DeviceFormat;
        public Vector2I Extent=>View.Extent;public string SourceEvidence=>View.SourceEvidence+";actual-DOF2-target-metadata-for-native-Bind;no-pixel-or-writer-claim";
        public void RequireCurrentForPass(int pass)
        {
            Owner.RequireCurrent();Before.RequireCurrent();Owner.World.TextureParameters.ValidateCurrent(View.ActualPointer);
            if(pass!=Before.SourcePassIndex)throw new InvalidDataException("DOF2 target metadata is for another actual source command.");
        }
    }
}
