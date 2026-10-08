using Godot;
using San14.Data;

/// <summary>The finite owned source World/frontend state at node193 Begin.
/// The typed World stock scope retains its actual Sky/class18/Terrain/F103
/// and callback gates. Native F4/scissor/depth are derived from the complete
/// pinned history, independently of Functor0 preparation. Each source Bind
/// borrows a real target view; only that view establishes its pixel extent.</summary>
public sealed partial class OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner
{
    public OriginalMapPostEffect3WorldControlledPreparation.CurrentFrame WorldPreparation { get; }
    public OriginalMapPostEffect3WorldStockGraphicsSourceOwner World { get; }
    public OriginalMapPostEffect3NativeFullscreenFrontendSource Source { get; }
    public OriginalMapPostEffect3EyeGraphicsCurrentState? LastRestoredNativeState { get; private set; }
    private readonly LoadedOriginalMapPostEffect3EyeGraphicsStateSource eyeState;
    private readonly OriginalModelRasterInputs ordinaryRaster;
    private readonly OriginalMapPostEffect3RetainedFrontendIngress? retainedIngress;
    private readonly uint currentColorStateF4;
    private readonly OriginalFinalEffectScissor? currentScissor;
    private readonly float currentMinimumDepth,currentMaximumDepth;
    private NativeContext? current;
    private int? lastReleasedGraphicsPass;
    private long revision;

    private OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner(
        OriginalMapPostEffect3WorldControlledPreparation.CurrentFrame actualPreparation,
        OriginalMapPostEffect3WorldStockGraphicsSourceOwner actualWorld,
        OriginalMapPostEffect3NativeFullscreenFrontendSource source,
        LoadedOriginalMapPostEffect3EyeGraphicsStateSource eyeState,
        OriginalMapPostEffect3RetainedFrontendIngress? retainedIngress=null)
    {
        WorldPreparation=actualPreparation;World=actualWorld;Source=source;this.eyeState=eyeState;this.retainedIngress=retainedIngress;
        if(source.Locale!=actualPreparation.Camera.ExecutableLocale||eyeState.Locale!=source.Locale)
            throw new InvalidDataException("Native frontend and Eye state need this actual Camera executable locale.");
        if(retainedIngress is null)
        {
            currentColorStateF4=source.AdmittedFreshWorldNativeColorStateF4;
            currentScissor=null;
            currentMinimumDepth=BitConverter.UInt32BitsToSingle(source.AdmittedFreshWorldMinimumDepthBits);
            currentMaximumDepth=BitConverter.UInt32BitsToSingle(source.AdmittedFreshWorldMaximumDepthBits);
        }
        else
        {
            currentColorStateF4=retainedIngress.Continuation.ColorStateF4;
            currentScissor=retainedIngress.Continuation.Scissor;
            currentMinimumDepth=retainedIngress.Continuation.MinimumDepth;
            currentMaximumDepth=retainedIngress.Continuation.MaximumDepth;
        }
        //CullNONE and disabled blend make winding/factors inactive. Their
        //backend representation does not replace the native F4 receipt.
        //Color-only Bind has no applicable stencil attachment.
        ordinaryRaster=new(RenderingDevice.PolygonCullMode.Disabled,
            RenderingDevice.PolygonFrontFace.CounterClockwise,false,false,RenderingDevice.CompareOperator.Always,
            Array.AsReadOnly(new[]{new OriginalModelBlendAttachment(false,RenderingDevice.BlendFactor.One,
                RenderingDevice.BlendFactor.Zero,RenderingDevice.BlendOperation.Add,RenderingDevice.BlendFactor.One,
                RenderingDevice.BlendFactor.Zero,RenderingDevice.BlendOperation.Add,true,true,true,true)}));
    }
    public static OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner CaptureFreshSource(
        OriginalGameRepository repository,OriginalMapPostEffect3WorldControlledPreparation.CurrentFrame actualPreparation,
        OriginalMapPostEffect3WorldStockGraphicsSourceOwner actualWorld)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(actualPreparation);
        ArgumentNullException.ThrowIfNull(actualWorld);actualPreparation.RequireCurrent();actualWorld.RequireCurrent();
        if(!ReferenceEquals(actualPreparation.Context,actualWorld.Context)||
            !ReferenceEquals(actualPreparation.Allocation,actualWorld.Bank.Source)||
            !actualWorld.CurrentStock.UsesConstructedFrontendState||actualPreparation.MainSelector.Selector!=0)
            throw new NotSupportedException("Native fullscreen profile needs this actual fresh Main0 World/frontend; an untracked retained generation needs its actual native history.");
        var source=OriginalDeferredSamplerSources.ReadNativeFullscreenFrontendSource(
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,actualPreparation.Camera.ExecutableLocale));
        return new(actualPreparation,actualWorld,source,OriginalMapPostEffect3EyeGraphicsStateSource.Load(repository,actualPreparation.Camera.ExecutableLocale));
    }
    internal static OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner CaptureFreshSource(
        OriginalGameRepository repository,OriginalMapPostEffect3WorldControlledPreparation.CurrentFrame preparation,
        OriginalMapPostEffect3WorldStockGraphicsSourceOwner world,OriginalMapPostEffect3WorldControlledStaticSources sources)
    {
        sources.RequireRepository(repository);preparation.RequireCurrent();world.RequireCurrent();
        if(sources.Native.Locale!=preparation.Camera.ExecutableLocale)throw new InvalidDataException("Native frontend needs its actual Camera executable locale.");
        if(!ReferenceEquals(preparation.Context,world.Context)||!ReferenceEquals(preparation.Allocation,world.Bank.Source)||
            !world.CurrentStock.UsesConstructedFrontendState||preparation.MainSelector.Selector!=0)
            throw new NotSupportedException("Native fullscreen profile needs this actual fresh Main0 World/frontend.");
        return new(preparation,world,sources.NativeFrontend,sources.EyeGraphics);
    }
    public string SourceEvidence=>Source.SourceEvidence+";"+World.SourceEvidence+";"+
        WorldPreparation.ActualFrontendParent.SourceEvidence+";"+(retainedIngress is null?
            "actual-source-created-first-Main-World/frontend":retainedIngress.SourceEvidence)+";not-fixture-state";
    public void RequireCurrent()
    {
        WorldPreparation.RequireCurrent();World.RequireCurrent();
        if(!ReferenceEquals(WorldPreparation.Context,World.Context)||!ReferenceEquals(WorldPreparation.Allocation,World.Bank.Source)||
            (retainedIngress is null&&!World.CurrentStock.UsesConstructedFrontendState)||!Source.ActualImmediateContextUsesWrapperRectangles||
            Source.OrdinaryBindHasMatchingDepthTarget||Source.RetainedActualLateCullMode!=1||Source.ModuleDepthWrite||
            Source.ModuleBlendEnabled||Source.ModuleColorWriteMask!=15)
            throw new InvalidDataException("Actual source frontend has left its admitted owned World history.");
        retainedIngress?.RequireCurrent(WorldPreparation,World);
    }
    private void RequireReleasedBeforeNextBind()
    {
        if(current is not null||actualDofCompute is not null||actualEyeCompute is not null||actualLensBefore is not null||pendingLensAfterSource is not null)
            throw new InvalidOperationException("Original frontend needs its actual preceding command and ordered After mip releases before another Select/Before.");
    }

    /// <summary>Perform the real selected Eye Before target/alias operation,
    /// capture its bank RTV and use that actual mip view extent. Apply the
    /// original Execute scissor/blend delta after the full post-Bind snapshot.
    /// The returned context is suitable for EyeBloomOwner.DrawIterationAsync.</summary>
    public async Task<IOriginalMapPostEffect3FullscreenDrawContextLease> BindEyeGraphicsAsync(
        OriginalMapPostEffect3EyeAdaptation2Frame frame,OriginalMapPostEffect3EyeBeforeBindings before)
    {
        RequireCurrent();frame.RequireCurrent();RequireReleasedBeforeNextBind();
        submissionLedger?.RequireEyePrepared(frame);
        if(!ReferenceEquals(frame.Allocation,World.Bank.Source)||frame.FrameNumber!=World.Context.FrameNumber||
            !frame.OrderedIterations.Contains(before.Iteration)||before.Iteration.IsCompute)
            throw new InvalidDataException("Eye Bind needs the actual next graphics iteration and released preceding native row.");
        directoryCursor?.EnterSelectedBefore(before.Iteration.SourcePassIndex,before.Iteration.NativeLoopIndex);
        World.TextureParameters.ApplyEyeBeforeAlias(frame,before);
        directoryCursor?.RecordBeforeReturned(before,before);directoryCursor?.ApplyEyeBefore(before);
        var binding=new NativeContext(this,before.Iteration.SourcePassIndex,checked(++revision),before.SourceEvidence);
        current=binding;
        try
        {
            var target=await World.Bank.CaptureEyeMipTargetLeaseAsync(World.TextureParameters,frame,before,binding);
            binding.BindActualTarget(target);
            directoryCursor?.Cache.RequireEyeCloneBindings(directoryCursor.RequireEyeCloneCatalog(),before,target);
            var selected=new OriginalMapPostEffect3EyeGraphicsSelectedTarget(World.TextureParameters.Require(target.ParameterIndex!.Value),
                target.InstanceIndex,target.NativeFirstMipLevel,target.Extent.X,target.Extent.Y,target.TargetViewRid.ToString(),target.SourceEvidence);
            var recipe=eyeState.Prepare(frame,before,World.TextureParameters,binding.NativeState,selected);
            binding.ApplyEyeExecute(recipe);actualEyeGraphicsHistoryBefore=before;directoryCursor?.RecordBindReturned(before,binding);return binding;
        }
        catch{current=null;actualEyeGraphicsHistoryBefore=null;throw;}
    }
    public void AcceptEyeGraphics(OriginalMapPostEffect3EyeBloomExecutionOwner actualEye,
        OriginalMapPostEffect3FullscreenCompletedPass completed)
    {
        ArgumentNullException.ThrowIfNull(actualEye);ArgumentNullException.ThrowIfNull(completed);RequireCurrent();
        if(!ReferenceEquals(actualEye.Bank,World.Bank)||!ReferenceEquals(actualEye.LastCompleted,completed)||
            !ReferenceEquals(actualEye.Graphics.LastCompletedPass,completed)||!actualEye.CompletedDraws.Contains(completed))
            throw new InvalidDataException("Eye frontend release needs this actual owner's submitted original graphics command.");
        var history=submissionLedger is null?null:submissionLedger.CaptureEyeGraphics(this,
            actualEyeGraphicsHistoryBefore??throw new InvalidDataException("Historical Eye graphics has no exact source Before."),actualEye,completed);
        ReleaseActualCompleted(completed);actualEyeGraphicsHistoryBefore=null;
        if(history is not null)submissionLedger!.AcceptAfterActualRelease(history);
    }
    internal NativeContext BeginCompositeBinding(OriginalMapPostEffect3Composite2BeforeBindings before)
    {
        RequireCurrent();before.RequireCurrent();RequireReleasedBeforeNextBind();
        if(!ReferenceEquals(before.Frame.Allocation,World.Bank.Source)||
            before.Frame.FrameNumber!=World.Context.FrameNumber||before.IsCompute)
            throw new InvalidDataException("Composite2 Bind requires its actual next selected source command and released caller row.");
        var result=new NativeContext(this,before.SourcePassIndex,checked(++revision),before.SourceEvidence);current=result;return result;
    }
    internal void ReleaseCompositeCoverage(OriginalMapPostEffect3Composite2GraphicsCoverageReceipt coverage)
    {coverage.RequireCurrentSubmission();ReleaseActualCompleted(coverage.Completed);}
    private void ReleaseActualCompleted(OriginalMapPostEffect3FullscreenCompletedPass completed)
    {
        var binding=current??throw new InvalidDataException("No actual native target row is bound for source Release.");
        binding.RequireCurrentForPass(completed.SourcePassIndex);
        if(completed.FrameNumber!=World.Context.FrameNumber||completed.ContextOwnerIdentity!=World.Bank.Source.ContextOwnerIdentity||
            completed.ActualViewport!=binding.ActualViewport||completed.Writes.Any(w=>
                w.SourceTargetRegister<0||w.SourceTargetRegister>=binding.ActualTargets.Count||
                w.StorageRid!=binding.ActualTargets[w.SourceTargetRegister].StorageRid||
                w.TargetViewRid!=binding.ActualTargets[w.SourceTargetRegister].TargetViewRid))
            throw new InvalidDataException("Completed fullscreen command differs from its actual native binding.");
        LastRestoredNativeState=binding.NativeState;
        lastReleasedGraphicsPass=completed.SourcePassIndex;
        //The per-draw GPU pipeline applies Execute's delta. Native release
        //restores its saved state and pops the counted target row95AB00;
        //the actual node192/193 owned parent remains the inherited caller.
        current=null;
    }
    internal sealed class NativeContext : IOriginalMapPostEffect3FullscreenDrawContextLease,IOriginalMapPostEffect3EyeCloneBindingContext
    {
        private readonly OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner owner;
        private readonly int pass;private readonly long acceptedRevision;private readonly string evidence;
        private IOriginalMapPostEffect3StockGraphicsTargetLease? target;
        private IReadOnlyList<IOriginalMapPostEffect3StockGraphicsTargetLease>? targets;
        private OriginalModelRasterInputs? boundRaster;
        private OriginalMapPostEffect3EyeNativeViewport? viewport;
        private OriginalModelRasterInputs? executeRaster;
        private Rect2I? executeScissor;
        private bool eyeExecute;
        internal NativeContext(OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner owner,int pass,long revision,string evidence)
            =>(this.owner,this.pass,acceptedRevision,this.evidence)=(owner,pass,revision,evidence);
        public long FrameNumber=>owner.World.Context.FrameNumber;
        public string ContextOwnerIdentity=>owner.World.Bank.Source.ContextOwnerIdentity;
        public string OwnedFrontendIdentity=>owner.World.CurrentStock.OwnedFrontendIdentity;
        public string ViewIdentity=>owner.World.CurrentStock.ViewIdentity;
        internal IOriginalMapPostEffect3StockGraphicsTargetLease ActualTarget=>target??throw new InvalidOperationException("Actual target view has not been bound.");
        internal IReadOnlyList<IOriginalMapPostEffect3StockGraphicsTargetLease> ActualTargets=>targets??
            (target is{} first?Array.AsReadOnly(new[]{first}):throw new InvalidOperationException("Actual target views have not been bound."));
        internal OriginalMapPostEffect3EyeGraphicsCurrentState NativeState=>new(FrameNumber,ContextOwnerIdentity,OwnedFrontendIdentity,ViewIdentity,
            viewport??throw new InvalidOperationException("Native viewport needs its actually selected target view."),owner.currentScissor,
            owner.currentColorStateF4,0,SourceEvidence+";post-Bind-source-Begin-blend0/full-raster-snapshot;source-owned-retained-scissor/endpoints");
        public Rect2I ActualViewport=>new(checked((int)NativeState.ActualViewport.Left),checked((int)NativeState.ActualViewport.Top),
            checked((int)NativeState.ActualViewport.Width),checked((int)NativeState.ActualViewport.Height));
        public float ActualViewportMinimumDepth=>NativeState.ActualViewport.MinimumDepth;
        public float ActualViewportMaximumDepth=>NativeState.ActualViewport.MaximumDepth;
        public Rect2I? ActualScissor=>eyeExecute?executeScissor:owner.currentScissor is{} s?new Rect2I(s.Left,s.Top,s.Width,s.Height):null;
        public OriginalModelRasterInputs ActualRaster=>executeRaster??boundRaster??owner.ordinaryRaster;
        public string SourceEvidence=>owner.SourceEvidence+";"+evidence+";actual-native-binding-revision="+acceptedRevision;
        public void RequireCurrentForPass(int sourcePassIndex)
        {
            owner.RequireCurrent();
            if(sourcePassIndex!=pass||!ReferenceEquals(owner.current,this)||owner.revision!=acceptedRevision)
                throw new InvalidDataException("Native fullscreen binding was released or superseded by another source pass.");
        }
        public void RequireEyeCloneBindings(OriginalMapPostEffect3EyeAdaptation2Frame frame,OriginalMapPostEffect3EyeBeforeBindings before,
            IOriginalMapPostEffect3FullscreenSelectedMipTargetLease actualTarget,IReadOnlyList<IOriginalMapPostEffect3ComputeTextureLease> reads)
        {
            RequireCurrentForPass(before.Iteration.SourcePassIndex);
            if(!ReferenceEquals(owner.actualEyeGraphicsHistoryBefore,before)||ActualTarget.StorageRid!=actualTarget.StorageRid||
                ActualTarget.TargetViewRid!=actualTarget.TargetViewRid)
                throw new InvalidDataException("Eye source clone join is not this exact currently bound Before/target context.");
            var cursor=owner.directoryCursor;
            if(cursor is null)return;
            var clones=cursor.RequireEyeCloneCatalog();
            if(!ReferenceEquals(clones.Source.Frame,frame))throw new InvalidDataException("Eye source clone join belongs to another prepared frame.");
            cursor.Cache.RequireEyeCloneBindings(clones,before,actualTarget,reads);
        }
        internal void BindActualTarget(IOriginalMapPostEffect3StockGraphicsTargetLease actual)
        {
            RequireCurrentForPass(pass);actual.RequireCurrentForPass(pass);
            if(target is not null||actual.FrameNumber!=FrameNumber||actual.ContextOwnerIdentity!=ContextOwnerIdentity||
                actual.OwnedFrontendIdentity!=OwnedFrontendIdentity||actual.ViewIdentity!=ViewIdentity)
                throw new InvalidDataException("Native target belongs to another current World/frontend binding.");
            target=actual;viewport=new(0,0,actual.Extent.X,actual.Extent.Y,owner.currentMinimumDepth,owner.currentMaximumDepth);
        }
        internal void BindActualTargets(IReadOnlyList<IOriginalMapPostEffect3StockGraphicsTargetLease> actual)
        {
            if(actual.Count is <1 or >4)throw new NotSupportedException("Native full raster requires its actual contiguous1..4 color target views.");
            BindActualTarget(actual[0]);
            foreach(var view in actual.Skip(1))
            {
                view.RequireCurrentForPass(pass);
                if(view.FrameNumber!=FrameNumber||view.ContextOwnerIdentity!=ContextOwnerIdentity||
                    view.OwnedFrontendIdentity!=OwnedFrontendIdentity||view.ViewIdentity!=ViewIdentity)
                    throw new InvalidDataException("Native MRT belongs to another actual World/frontend binding.");
            }
            targets=Array.AsReadOnly(actual.ToArray());
            boundRaster=owner.ordinaryRaster with{Attachments=Array.AsReadOnly(
                Enumerable.Repeat(owner.ordinaryRaster.Attachments[0],actual.Count).ToArray())};
        }
        internal void ApplyEyeExecute(OriginalMapPostEffect3EyeGraphicsStateRecipe recipe)
        {
            recipe.RequireCurrent();RequireCurrentForPass(pass);
            if(recipe.Before.Iteration.SourcePassIndex!=pass||recipe.ActualCurrentState!=NativeState)
                throw new InvalidDataException("Eye Execute recipe differs from its exact native post-Bind snapshot.");
            executeScissor=recipe.DrawScissor is{} s?new Rect2I(s.Left,s.Top,s.Width,s.Height):null;
            executeRaster=owner.ordinaryRaster;
            if(recipe.ChangesBlend)
                executeRaster=owner.ordinaryRaster with{Attachments=Array.AsReadOnly(new[]{owner.ordinaryRaster.Attachments[0] with
                {EnableBlend=true,SourceColor=RenderingDevice.BlendFactor.One,DestinationColor=RenderingDevice.BlendFactor.One,
                 ColorOperation=RenderingDevice.BlendOperation.Add,SourceAlpha=RenderingDevice.BlendFactor.One,
                 DestinationAlpha=RenderingDevice.BlendFactor.One,AlphaOperation=RenderingDevice.BlendOperation.Add}})};
            eyeExecute=true;
        }
    }
}
