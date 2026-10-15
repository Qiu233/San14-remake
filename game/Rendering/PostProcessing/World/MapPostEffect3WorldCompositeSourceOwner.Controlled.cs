using Godot;
using San14.Data;

public sealed partial class MapPostEffect3WorldCompositeSourceOwner
{
    private long controlledTargetRevision;
    private long? controlledTargetCompleteFrame;
    private MapPostEffect3FullscreenCompletedPass? completedControlledDraw;

    internal CompletedControlledOutput CaptureCompletedControlledOutput(ParentBinding binding)
    {
        binding.RequireCurrent();
        lock(lifetime)
        {
            if(CompletedPresentation is null||controlledTargetCompleteFrame!=binding.FrameNumber||
                completedControlledDraw is not{} draw||draw.FrameNumber!=binding.FrameNumber)
                throw new InvalidDataException("Frontend has no completed controlled output for this binding.");
            return new(this,binding,draw,CompletedPresentation,controlledTargetRevision);
        }
    }

    internal sealed class CompletedControlledOutput
    {
        private readonly MapPostEffect3WorldCompositeSourceOwner owner;
        private readonly ParentBinding binding;
        internal CompletedControlledOutput(MapPostEffect3WorldCompositeSourceOwner owner,
            ParentBinding binding,MapPostEffect3FullscreenCompletedPass draw,
            RenderOutputPresentation presentation,long revision)
            =>(this.owner,this.binding,Draw,Presentation,Revision)=(owner,binding,draw,presentation,revision);
        internal MapPostEffect3FullscreenCompletedPass Draw{get;}
        internal RenderOutputPresentation Presentation{get;}
        internal long Revision{get;}
        internal void RequireCurrent()
        {
            binding.RequireCurrent();
            lock(owner.lifetime)
                if(owner.retiring||owner.controlledTargetCompleteFrame!=binding.FrameNumber||
                    owner.controlledTargetRevision!=Revision||!ReferenceEquals(owner.completedControlledDraw,Draw)||
                    !ReferenceEquals(owner.CompletedPresentation,Presentation)||
                    Draw.Writes is not[{StorageRid:var storage,TargetViewRid:var view,ParameterIndex:null}]||
                    storage!=owner.target.StorageRid||view!=owner.target.ShaderViewRid)
                    throw new InvalidDataException("Completed controlled presentation was superseded or retired.");
        }
    }

    /// <summary>The controlled route inherits the same actually bound frontend
    /// target as node193. The selected final or FXAA source draw decides when
    /// this lease is usable; neither its allocation nor binding grants pixels.</summary>
    public IMapPostEffect3StockGraphicsTargetLease LeaseControlledFinalTarget(
        ParentBinding binding, MapPostEffect3WorldStockGraphicsSourceOwner currentWorld,
        MapPostEffect3Composite2ExecutionFrame actualCompositeFrame)
    {
        ArgumentNullException.ThrowIfNull(binding); ArgumentNullException.ThrowIfNull(currentWorld);
        ArgumentNullException.ThrowIfNull(actualCompositeFrame);
        binding.RequireCurrent(); currentWorld.RequireCurrent(); actualCompositeFrame.RequireCurrent();
        if (!ReferenceEquals(binding.Owner, this) || !ReferenceEquals(currentWorld.Context, binding.Context) ||
            !ReferenceEquals(currentWorld.Bank.Source, actualCompositeFrame.Allocation) ||
            actualCompositeFrame.FrameNumber != binding.FrameNumber)
            throw new InvalidDataException("Controlled final target needs the exact current World/frontend/module frame.");
        return new ControlledFinalTargetLease(this, binding, currentWorld, actualCompositeFrame);
    }

    /// <summary>Joins the actual completed fullscreen command to the owned
    /// external frontend image. The callback must run before the next draw.
    /// Partial, blended or empty output cannot publish a complete new image.</summary>
    public void AcceptControlledFinalCoverage(ParentBinding binding,
        MapPostEffect3Composite2GraphicsCoverageReceipt actualCoverage)
    {
        ArgumentNullException.ThrowIfNull(binding); ArgumentNullException.ThrowIfNull(actualCoverage);
        binding.RequireCurrent(); actualCoverage.RequireCurrentSubmission();
        if (actualCoverage.ActualTarget is not ControlledFinalTargetLease lease ||
            !ReferenceEquals(lease.Owner, this) || !ReferenceEquals(lease.Binding, binding))
            throw new InvalidDataException("Controlled coverage belongs to another frontend target lease.");
        lease.RequireCurrentForPass(actualCoverage.Completed.SourcePassIndex);
        var write = actualCoverage.Completed.Writes.SingleOrDefault(w => w.SourceTargetRegister == 0);
        lock (lifetime)
        {
            if (write is null || !actualCoverage.Completed.WasPixelDrawSubmitted ||
                write.StorageRid != target.StorageRid || write.TargetViewRid != target.ShaderViewRid ||
                write.WriterRevision != controlledTargetRevision || write.ParameterIndex is not null ||
                actualCoverage.Completed.FrameNumber != binding.FrameNumber)
                throw new InvalidDataException("Controlled output has no matching current external writer revision.");
            if (actualCoverage.HasCompleteTargetPixelDomain)
                controlledTargetCompleteFrame = binding.FrameNumber;
        }
    }

    /// <summary>Keep the external image alive for its entire borrowed factory9
    /// execution. Presentation is published only after its final actual source
    /// draw and complete output coverage, then refreshed by the caller.</summary>
    public async Task<MapPostEffect3Composite2CompletedGraphics> ExecuteControlledCompositeAsync(
        ParentBinding binding, MapPostEffect3Composite2GraphicsOwner actualOwner)
    {
        ArgumentNullException.ThrowIfNull(binding); ArgumentNullException.ThrowIfNull(actualOwner);
        binding.RequireCurrent();
        AcceptBinding(binding);
        try
        {
            var completed = await actualOwner.ExecuteAsync(); binding.RequireCurrent();
            var final = completed.FinalOutputDraw;
            lock (lifetime)
            {
                if (!completed.ProvesAllSelectedGraphicsWereSubmitted || final.FrameNumber != binding.FrameNumber ||
                    final.Writes is not [{ StorageRid: var storage, TargetViewRid: var view, ParameterIndex: null }] ||
                    storage != target.StorageRid || view != target.ShaderViewRid ||
                    controlledTargetCompleteFrame != binding.FrameNumber)
                    throw new InvalidDataException("Controlled final has not completed this frontend image.");
                completedControlledDraw=final;CompletedPresentation = presentation;
            }
            return completed;
        }
        finally { lock (lifetime) { accepted--; if (retiring && accepted == 0) drained?.TrySetResult(); } }
    }

    public async Task<MapPostEffect3FullscreenCompletedPass> ExecuteControlledCompositePassAsync(
        ParentBinding binding,MapPostEffect3Composite2GraphicsOwner actualOwner,int sourcePass)
    {
        ArgumentNullException.ThrowIfNull(binding);ArgumentNullException.ThrowIfNull(actualOwner);binding.RequireCurrent();
        AcceptBinding(binding);
        try
        {
            var completed=await actualOwner.ExecuteNextSelectedPassAsync(sourcePass);binding.RequireCurrent();
            if(actualOwner.LastCompleted is{} returned)
            {
                var final=returned.FinalOutputDraw;
                lock(lifetime)
                {
                    if(!returned.ProvesAllSelectedGraphicsWereSubmitted||!ReferenceEquals(final,completed)||final.FrameNumber!=binding.FrameNumber||
                        final.Writes is not[{StorageRid:var storage,TargetViewRid:var view,ParameterIndex:null}]||
                        storage!=target.StorageRid||view!=target.ShaderViewRid||controlledTargetCompleteFrame!=binding.FrameNumber)
                        throw new InvalidDataException("Stepped controlled final has not completed this actual frontend image.");
                    completedControlledDraw=final;CompletedPresentation=presentation;
                }
            }
            return completed;
        }
        finally{lock(lifetime){accepted--;if(retiring&&accepted==0)drained?.TrySetResult();}}
    }

    private sealed record ControlledFinalTargetLease(MapPostEffect3WorldCompositeSourceOwner Owner,
        ParentBinding Binding, MapPostEffect3WorldStockGraphicsSourceOwner World,
        MapPostEffect3Composite2ExecutionFrame Frame)
        : IMapPostEffect3FullscreenExternalWritableTargetLease, IMapPostEffect3StockGraphicsReadableTargetLease
    {
        public long FrameNumber => Binding.FrameNumber;
        public string ContextOwnerIdentity => Frame.OwnerIdentity;
        public string OwnedFrontendIdentity => World.CurrentStock.OwnedFrontendIdentity;
        public string ViewIdentity => World.CurrentStock.ViewIdentity;
        public int? ParameterIndex => null;
        public int InstanceIndex => 0;
        public string ProducerOwnerIdentity => Binding.EffectiveSource.OwnerIdentity;
        public Rid StorageRid => Owner.target.StorageRid;
        public Rid TargetViewRid => Owner.target.ShaderViewRid;
        public uint NativeViewFormat => 91;
        public RenderingDevice.DataFormat DeviceFormat => Owner.target.TargetFormat;
        public Vector2I Extent => Owner.Extent;
        public string SourceEvidence => Binding.SourceEvidence + ";controlled-Composite2-actual-inherited-frontend-Output13BF;" + Frame.SourceEvidence;
        public void RequireCurrentForPass(int pass)
        {
            Binding.RequireCurrent(); World.RequireCurrent(); Frame.RequireCurrent();
            int selected = Frame.Prepared.PassSelection.FxaaPass ?? Frame.Prepared.PassSelection.FinalCompositePass;
            if (pass != selected || !Frame.Prepared.PassSelection.OrderedFactory9Passes.Contains(pass))
                throw new InvalidDataException("Controlled frontend target is not this source invocation's final inherited draw.");
        }
        public long InvalidateContentsBeforePixelWrite(int pass)
        {
            RequireCurrentForPass(pass);
            lock (Owner.lifetime)
            {
                Owner.controlledTargetCompleteFrame = null;
                return Owner.controlledTargetRevision = checked(Owner.controlledTargetRevision + 1);
            }
        }
        public void RequireReadableForPass(int pass)
        {
            RequireCurrentForPass(pass);
            lock (Owner.lifetime)
                if (Owner.controlledTargetCompleteFrame != FrameNumber)
                    throw new InvalidDataException("Controlled inherited destination has no complete current pixels for blending.");
        }
    }
}
