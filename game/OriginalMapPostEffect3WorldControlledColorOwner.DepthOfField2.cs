using Godot;
using San14.Data;

public sealed partial class OriginalMapPostEffect3WorldControlledColorOwner
{
    public OriginalMapPostEffect3DepthOfField2TargetView CaptureDepthOfField2TargetView(
        OriginalMapPostEffect3DepthOfField2BeforeBindings before,int register,
        OriginalMapPostEffect3ResolvedTextureParameter pointer)
    {
        ArgumentNullException.ThrowIfNull(before);world.RequireCurrent();before.RequireCurrent();
        table.ValidateCurrent(pointer);world.LeaseWorkingScene(pointer.ParameterIndex).RequireCurrent();
        if(register!=0||before.FrameNumber!=table.FrameNumber||before.ContextOwnerIdentity!=table.ContextOwnerIdentity||
            pointer.Reference.PhysicalAllocationParameterIndex is not null)
            throw new InvalidDataException("DOF2 external target must be this current World Scene at source RT0.");
        return new(register,pointer,scene.StorageRid,targetView,targetFormat,scene.TargetFormat,scene.ExplicitExtent,
            before.SourceEvidence+";actual-World-Scene-external-DOF2-view");
    }

    public IOriginalMapPostEffect3StockGraphicsTargetLease CaptureDepthOfField2Target(
        OriginalMapPostEffect3DepthOfField2BeforeBindings before,
        OriginalMapPostEffect3DepthOfField2TargetView view,
        IOriginalMapPostEffect3FullscreenDrawContextLease context)
    {
        ArgumentNullException.ThrowIfNull(context);context.RequireCurrentForPass(before.SourcePassIndex);
        var expected=CaptureDepthOfField2TargetView(before,view.SourceTargetRegister,view.ActualPointer);
        if(view.StorageRid!=expected.StorageRid||view.TargetViewRid!=expected.TargetViewRid||view.Extent!=expected.Extent||
            view.NativeViewFormat!=expected.NativeViewFormat||view.DeviceFormat!=expected.DeviceFormat||
            context.FrameNumber!=table.FrameNumber||context.ContextOwnerIdentity!=table.ContextOwnerIdentity||
            context.OwnedFrontendIdentity!=world.CurrentStock.OwnedFrontendIdentity||context.ViewIdentity!=world.CurrentStock.ViewIdentity)
            throw new InvalidDataException("DOF2 writable target differs from its actual World post-Bind view.");
        return new DofSceneTarget(this,view.ActualPointer,before,context);
    }

    public void AcceptDepthOfField2Coverage(OriginalMapPostEffect3DepthOfField2GraphicsCoverageReceipt coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);world.RequireCurrent();coverage.RequireCurrentSubmission();
        var targets=coverage.ActualTargets.Where(t=>t.Target is DofSceneTarget).ToArray();
        if(targets is not [var binding]||binding.Target is not DofSceneTarget target||!ReferenceEquals(target.Owner,this))
            throw new InvalidDataException("DOF2 coverage belongs to another external Scene owner.");
        target.RequireCurrentForPass(coverage.Completed.SourcePassIndex);
        var write=coverage.Completed.Writes.SingleOrDefault(w=>w.SourceTargetRegister==binding.SourceTargetRegister);
        lock(lifetime)
        {
            if(write is null||!coverage.Completed.WasPixelDrawSubmitted||write.ParameterIndex!=target.ParameterIndex||
                write.PhysicalAllocationParameterIndex is not null||write.InstanceIndex!=0||
                write.StorageRid!=scene.StorageRid||write.TargetViewRid!=targetView||write.NativeViewFormat!=targetFormat||
                write.WriterRevision!=revision||!coverage.CompleteTargetPixelDomains.TryGetValue(binding.SourceTargetRegister,out bool full))
                throw new InvalidDataException("DOF2 Scene publication does not join the current source write generation.");
            complete=full;
        }
    }

    private sealed record DofSceneTarget(OriginalMapPostEffect3WorldControlledColorOwner Owner,
        OriginalMapPostEffect3ResolvedTextureParameter Pointer,OriginalMapPostEffect3DepthOfField2BeforeBindings Before,
        IOriginalMapPostEffect3FullscreenDrawContextLease Context)
        :IOriginalMapPostEffect3FullscreenExternalWritableTargetLease,IOriginalMapPostEffect3StockGraphicsReadableTargetLease
    {
        public long FrameNumber=>Before.FrameNumber;
        public string ContextOwnerIdentity=>Before.ContextOwnerIdentity;
        public string OwnedFrontendIdentity=>Context.OwnedFrontendIdentity;
        public string ViewIdentity=>Context.ViewIdentity;
        public int? ParameterIndex=>Pointer.ParameterIndex;
        public int InstanceIndex=>Pointer.Reference.InstanceIndex;
        public string ProducerOwnerIdentity=>Pointer.Reference.ProducerOwnerIdentity;
        public Rid StorageRid=>Owner.scene.StorageRid;
        public Rid TargetViewRid=>Owner.targetView;
        public uint NativeViewFormat=>Owner.targetFormat;
        public RenderingDevice.DataFormat DeviceFormat=>Owner.scene.TargetFormat;
        public Vector2I Extent=>Owner.scene.ExplicitExtent;
        public string SourceEvidence=>Before.SourceEvidence+";actual-current-DOF2-Scene-target;"+Context.SourceEvidence;
        public void RequireCurrentForPass(int pass)
        {
            Owner.world.RequireCurrent();Before.RequireCurrent();Owner.table.ValidateCurrent(Pointer);
            Context.RequireCurrentForPass(pass);
            if(pass!=Before.SourcePassIndex||Pointer.Reference.PhysicalAllocationParameterIndex is not null||
                Pointer.Reference.ProducerOwnerIdentity!=Owner.scene.EffectiveSource.OwnerIdentity||InstanceIndex!=0)
                throw new InvalidDataException("DOF2 target no longer selects its actual external World Scene.");
        }
        public void RequireReadableForPass(int pass)
        {RequireCurrentForPass(pass);lock(Owner.lifetime)Owner.RequireComplete();}
        public long InvalidateContentsBeforePixelWrite(int pass)
        {
            RequireCurrentForPass(pass);
            lock(Owner.lifetime){Owner.complete=false;return Owner.revision=checked(Owner.revision+1);}
        }
    }
}
