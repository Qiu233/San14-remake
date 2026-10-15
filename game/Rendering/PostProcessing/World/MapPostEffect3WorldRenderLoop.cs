using San14.Data;

/// <summary>One ordinary World renderer generation. Initialization allocates
/// the owned resources once; each accepted request uses the same preparation,
/// module and device programs. Source state selects commands, never a frame
/// ordinal. Optional diagnostic observations belong to separate callers.</summary>
public sealed class MapPostEffect3WorldRenderLoop : IAsyncDisposable
{
    private readonly SceneViewNativeMapWorldDependencies dependencies;
    private readonly Func<NativeMapFrameContext,SceneViewNativeMapFrameCapture> capture;
    private readonly SemaphoreSlim gate=new(1,1);
    private MapSourceCreatedWorldPlayableHost? host;
    private MapPostEffect3FirstWorldExecutionOwner? resources;
    private MapPostEffect3WorldControlledPreparation? preparation;
    private MapPostEffect3WorldControlledModuleOwner? module;
    private MapPostEffect3WorldControlledFactoryFrame? activeFrame;
    private MapPostEffect3StaticLutTextureOwner? hdr;
    private readonly List<MapPostEffect3StaticLutTextureOwner> replacedHdr=[];
    private MapPostEffect3WorldExposurePublicationOwner? exposurePublication;
    private WorldMainDisplayParentOwner? displayParent;
    private WorldMainDisplaySurfaceSet? displaySurfaces;
    private WorldMainDisplayParentEntry? displayEntry;
    private LoadedMapPostEffect3ControlledHostSource? hostCompletionSource;
    private NativeMapFrameContext? completed;
    private IReadOnlyList<RuntimeStageTiming> completedTimings=Array.Empty<RuntimeStageTiming>();
    public RenderThreadSchedulingDiagnostics.Snapshot? LastCompletedScheduling { get; private set; }
    private bool failed,retiring,disposed;

    public MapPostEffect3WorldRenderLoop(SceneViewNativeMapWorldDependencies dependencies,
        Func<NativeMapFrameContext,SceneViewNativeMapFrameCapture> capture)
    {
        this.dependencies=dependencies??throw new ArgumentNullException(nameof(dependencies));
        this.capture=capture??throw new ArgumentNullException(nameof(capture));
    }

    public async Task InitializeAsync(MapSourceCreatedWorldPlayableHost actualHost)
    {
        ArgumentNullException.ThrowIfNull(actualHost);await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(host is not null||failed||retiring)throw new InvalidOperationException("World render-loop generation is already initialized, failed or retiring.");
            host=actualHost;
            resources=await MapPostEffect3FirstWorldExecutionOwner.BuildAsync(dependencies,actualHost,allowReadback:false);
            displayParent=new(WorldMainDisplayParentSource.Load(dependencies.Repository,
                dependencies.WorldSource.Bootstrap.First.Camera.ExecutableLocale),
                ()=>displaySurfaces??throw new InvalidOperationException("Actual display surfaces have not been selected."));
            preparation=MapPostEffect3WorldControlledPreparation.Create(dependencies,actualHost,resources.Bank.Source);
            hostCompletionSource=LoadedMapPostEffect3ControlledHostSource.Load(dependencies.Repository,preparation.ExecutableLocale);
            exposurePublication=await MapPostEffect3WorldExposurePublicationOwner.BuildAsync(
                dependencies.Repository,actualHost.Final.Chain.Exposure,preparation.ExecutableLocale);
        }
        catch { failed=true;throw; }
        finally { gate.Release(); }
    }

    public bool HasCompleted(NativeMapFrameContext context)=>
        !failed&&!retiring&&!disposed&&ReferenceEquals(completed,context);
    internal async Task ReloadPostEffectResourcesAsync()
    {
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(failed||retiring||preparation is null)
                throw new InvalidOperationException("Post-effect reload requires an initialized live World renderer.");
            preparation.InvalidatePostEffectResources();
        }
        finally { gate.Release(); }
    }
    public sealed record RuntimeStageTiming(string Stage,double ElapsedMilliseconds,double SincePreviousMilliseconds);
    /// <summary>CPU wall time at actual source boundaries. Does not measure
    /// GPU execution time or alter source state and submission selection.</summary>
    public IReadOnlyList<RuntimeStageTiming> LastCompletedTimings=>completedTimings;

    public MapPostEffect3WorldCommonClockPublication PrepareCommonClockBeforeGeometry(
        MapSourceCreatedWorldPlayableHost actualHost, NativeMapFrameContext context)
    {
        if(failed||retiring||disposed||preparation is null||!ReferenceEquals(actualHost,host))
            throw new InvalidOperationException("Geometry clock requires the initialized live ordinary World loop.");
        var actualCapture=capture(context);
        var frame=actualHost.Session.CurrentFrame??throw new InvalidDataException("Geometry clock lacks the current World frame.");
        return preparation.PrepareCommonClockBeforeGeometry(context,frame.PreparedSourceFrame!.Common,
            new(actualCapture.PresentationDeltaSeconds,actualCapture.SourceProvenance+";owned-SceneView-process-clock"));
    }

    public async Task<WorldMainDisplayParentEntry> PrepareMainDisplayEntryAsync(
        MapSourceCreatedWorldPlayableHost actualHost,NativeMapFrameContext context)
    {
        await gate.WaitAsync();
        try
        {
            if(failed||retiring||disposed||resources is null||displayParent is null||!ReferenceEquals(host,actualHost))
                throw new InvalidOperationException("Main display entry requires the live initialized ordinary renderer.");
            await resources.CompositeParent.RefreshOwnedExtentAsync(context,displayParent.RetireFramebufferAsync);
            displaySurfaces=resources.CompositeParent.CaptureActualDisplaySurfaces();
            var frame=actualHost.Session.CurrentFrame??throw new InvalidDataException("Main display entry lacks current geometry preparation.");
            var depth=actualHost.Viewport.PrepareSourceMainFrame(frame.Selection,frame.Feature.Binding.ViewIdentity);
            var extent=displaySurfaces.Color.Extent;
            return displayEntry=await displayParent.BeginCurrentAsync(new(extent.X,extent.Y,0,0,
                BitConverter.SingleToUInt32Bits(depth.ViewportMinimumDepth),BitConverter.SingleToUInt32Bits(depth.ViewportMaximumDepth)),false);
        }
        finally{gate.Release();}
    }

    public async Task<RenderOutputPresentation> RenderCurrentAsync(
        MapSourceCreatedWorldPlayableHost actualHost,NativeMapFrameContext context)
    {
        ArgumentNullException.ThrowIfNull(context);await gate.WaitAsync();
        try
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();
            using var scheduling=System.Environment.GetEnvironmentVariable("SAN14_RENDER_SCHEDULING_DIAGNOSTICS")=="1"
                ?RenderThreadSchedulingDiagnostics.BeginCapture(context.FrameNumber,"ordinary-postprocess"):null;
            var timings=new List<RuntimeStageTiming>();double priorMilliseconds=0;
            void Mark(string stage)
            {
                double now=clock.Elapsed.TotalMilliseconds;
                timings.Add(new(stage,now,now-priorMilliseconds));priorMilliseconds=now;
            }
            ObjectDisposedException.ThrowIf(disposed,this);
            if(failed||retiring||!ReferenceEquals(host,actualHost)||resources is null||preparation is null)
                throw new InvalidOperationException("World render-loop request needs its initialized live Host generation.");
            var actualCapture=capture(context);
            if(!float.IsFinite(actualCapture.PresentationDeltaSeconds)||actualCapture.PresentationDeltaSeconds<0)
                throw new InvalidDataException("World renderer requires its actual owned process-clock delta.");
            var accepted=actualHost.Session.CurrentFrame??throw new InvalidDataException("World has no current Main preparation.");
            if(!ReferenceEquals(accepted.Context,context))throw new InvalidDataException("World request belongs to another accepted context.");
            var camera=accepted.Lighting.CapturePreparedCameraFromSameAcceptedMain(accepted.MainCamera);
            var extentTransition=preparation.ReadActualExtentTransition(context);
            await DrainPendingBorrowersCoreAsync();
            await resources.RefreshOwnedExtentAsync(context);
            var parent=resources.CompositeParent.BindNode192And193Current(context,camera);
            var current=preparation.CaptureCurrent(context,parent,new(actualCapture.PresentationDeltaSeconds,
                actualCapture.SourceProvenance+";owned-SceneView-process-clock"),extentTransition);
            if(hdr is null)hdr=await MapPostEffect3StaticLutTextureOwner.BuildActualSelectedHdrAsync(
                dependencies.Repository,current.HdrLookup);
            else if(!hdr.TryRefreshSelectedHdrMetadata(current.HdrLookup))
            {
                // Upload before publishing the replacement. The previous
                // module/clock/61/DOF owners continue unchanged. Its old HDR
                // allocation outlives the current borrowed execution cleanup.
                var replacement=await MapPostEffect3StaticLutTextureOwner.BuildActualSelectedHdrAsync(
                    dependencies.Repository,current.HdrLookup);
                replacedHdr.Add(hdr);hdr=replacement;
            }
            Mark("current-camera-clock-hdr-selection");
            MapPostEffect3WorldControlledFactoryFrame frame;
            if(module is null)
            {
                var prepared=current.PrepareActualFresh(hdr.CaptureSourceSelectedHdrMetadata());
                frame=await MapPostEffect3WorldControlledFactoryFrame.BuildFreshAsync(dependencies.Repository,
                    actualHost,current,prepared,resources.Bank,resources.FreshAllocationSides,hdr,retainModuleLifetime:true);
                module=frame.ModuleLifetime??throw new InvalidDataException("Ordinary renderer did not retain its module owner.");
            }
            else frame=await module.BuildNextFrameAsync(actualHost,current,hdr);
            Mark("current-module-factory-preparation");
            RenderOutputPresentation presentation;
            activeFrame=frame;
            Exception? frameFailure=null;
            try
            {
                await frame.PrepareActualDirectoryAsync(dependencies.Repository,resources.Programs);
                Mark("current-directory-preparation");
                var returned=await MapPostEffect3WorldControlledExecution.ExecuteAsync(dependencies.Repository,
                    frame,resources.Bank,resources.Programs,dependencies.Compiler,hdr,Mark);
                frame.RequirePortReturned(frame.ActualDirectoryReturn);
                var output=resources.CompositeParent.CaptureCompletedControlledOutput(parent);
                bool lookupExposure=hostCompletionSource!.RequestsExposureLookupCandidate(
                    frame.Camera.CaptureAcceptedCamera().SourceSpecialRectangleFlag1510,current.AuxFields.Flags8);
                var exposurePointer=!lookupExposure||frame.Textures.ReadKnownNullablePointer(61) is null?null:
                    resources.Bank.RequireTexture(61,frame.Textures.Require(61).Reference.InstanceIndex);
                var tail=hostCompletionSource!.PrepareTail(frame.Camera,frame.Textures,current.AuxFields,
                    exposurePointer,actualHost.Final.Chain.Exposure,
                    "actual retained module61 wrapper and owned node1 global Exposure wrapper");
                var publication=await exposurePublication!.PublishAsync(frame,current.AuxFields,tail,returned);
                var authority=new CompletedOutputAuthority(frame,current,returned,output,tail,publication);
                var sourceCompleted=MapPostEffect3ControlledHostCompleted.CaptureAfterActualReturnedOutput(
                    authority,hostCompletionSource.ConstructOwnedWriterScope(current.World.First.Context.Source));
                (actualHost.GraphicsColorMaskOwner??throw new InvalidDataException("Normal World has no source graphics history."))
                    .CompleteControlledSourceHostOutput(context,sourceCompleted);
                Mark("caller-exposure-and-source-histories-completed");
                if(displayEntry is not null)
                {_=displayParent!.EndCurrent(displayEntry);displayEntry=null;}
                completedTimings=Array.AsReadOnly(timings.ToArray());
                completed=context;
                presentation=output.Presentation;
            }
            catch(Exception failure){frameFailure=failure;throw;}
            finally
            {
                try{await frame.DisposeAsync();activeFrame=null;}
                catch(Exception retirementFailure) when(frameFailure is not null)
                {
                    if(module?.PendingInvocationRetirement is{} pending)
                        throw new NativeMapBorrowerConstructionException(frameFailure,retirementFailure,pending);
                    throw new AggregateException(frameFailure,retirementFailure);
                }
            }
            await RetireReplacedHdrAsync();
            LastCompletedScheduling=scheduling?.CaptureSnapshot();
            return presentation;
        }
        catch { failed=true;throw; }
        finally { gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            if(disposed)return;
            retiring=true;
            await DrainPendingBorrowersCoreAsync();
            // A failed teardown remains retryable and keeps the actual owner.
            if(exposurePublication is not null){await exposurePublication.DisposeAsync();exposurePublication=null;}
            if(module is not null){await module.DisposeAsync();module=null;}
            await RetireReplacedHdrAsync();
            if(hdr is not null){await hdr.DisposeAsync();hdr=null;}
            if(displayParent is not null)
            {
                if(displayEntry is not null){displayParent.AbandonCurrentForRetirement();displayEntry=null;}
                await displayParent.DisposeAsync();displayParent=null;displaySurfaces=null;
            }
            if(resources is not null){await resources.DisposeAsync();resources=null;}
            completed=null;completedTimings=Array.Empty<RuntimeStageTiming>();preparation=null;host=null;disposed=true;
        }
        finally { gate.Release(); }
    }

    internal async Task DrainPendingBorrowersAsync()
    {
        await gate.WaitAsync();
        try{await DrainPendingBorrowersCoreAsync();}
        finally{gate.Release();}
    }
    private async Task DrainPendingBorrowersCoreAsync()
    {
        if(activeFrame is not null){await activeFrame.DisposeAsync();activeFrame=null;}
        if(module is not null)await module.RetireInvocationOwnersAsync();
        if(resources is not null)await resources.Compute.DrainPendingAsync();
    }

    private async Task RetireReplacedHdrAsync()
    {
        // Remove only after successful retirement; a failed FreeRid keeps the
        // real owner available to this loop's normal teardown retry.
        while(replacedHdr.Count!=0)
        {await replacedHdr[0].DisposeAsync();replacedHdr.RemoveAt(0);}
    }

    /// <summary>Closed join of the real returned module, completed target and
    /// source-selected caller tail. Keeps historical command authority after
    /// the borrowed factory frame and current stock packet are consumed.</summary>
    private sealed class CompletedOutputAuthority:IMapPostEffect3ControlledHostOutputAuthority
    {
        private readonly MapPostEffect3WorldControlledPortReturnReceipt returned;
        private readonly MapPostEffect3WorldCompositeSourceOwner.CompletedControlledOutput output;
        private readonly MapPostEffect3WorldExposurePublicationCompleted? publication;
        internal CompletedOutputAuthority(MapPostEffect3WorldControlledFactoryFrame frame,
            MapPostEffect3WorldControlledPreparation.CurrentFrame current,
            MapPostEffect3WorldControlledPortReturnReceipt returned,
            MapPostEffect3WorldCompositeSourceOwner.CompletedControlledOutput output,
            MapPostEffect3ControlledHostTailRecipe tail,
            MapPostEffect3WorldExposurePublicationCompleted? publication)
        {
            this.returned=returned;this.output=output;this.publication=publication;
            ControlledCamera=frame.Camera;CurrentContext=current.World.First.Context;ApplyView=frame.ApplyViewSlot;
            CurrentStock=frame.World.CurrentStock;TextureParameters=frame.Textures;
            CpuDirectoryReturned=frame.ActualDirectoryReturn.CpuDirectoryReturned;
            SourceControllerReturned=returned.SourceControllerReturn;
            ActualDeviceJoin=frame.ActualDirectoryReturn.SelectedDeviceJoin;PostApplyTail=tail;
            RequireActualReturnedOutput();
        }
        public MapPostEffect3OwnedControlledCameraFrame ControlledCamera{get;}
        public MainMapContextCurrentPacket CurrentContext{get;}
        public MapPostEffect3ApplyViewSlotSource ApplyView{get;}
        public MapCopy97CurrentStockPacket CurrentStock{get;}
        public MapPostEffect3TextureParameterState TextureParameters{get;}
        public MapPostEffect3Functor1SourceReceipt CpuDirectoryReturned{get;}
        public MapPostEffect3Functor1SourceReceipt SourceControllerReturned{get;}
        public MapPostEffect3Functor1SelectedDeviceJoin ActualDeviceJoin{get;}
        public int FinalOutputSourcePassIndex=>output.Draw.SourcePassIndex;
        public object ActualPresentationIdentity=>output.Presentation;
        public long ActualPresentationRevision=>output.Revision;
        public MapPostEffect3ControlledHostTailRecipe PostApplyTail{get;}
        public object? ActualPostApplyExposureCopyIdentity=>publication;
        public string SourceEvidence=>returned.Scope.SourceEvidence+";"+output.Draw.SourceEvidence+
            ";"+(publication?.SourceEvidence??PostApplyTail.SourceEvidence);
        public void RequireActualReturnedOutput()
        {
            returned.RequireHistoricalAuthority();output.RequireCurrent();PostApplyTail.RequireCurrent();
            if(returned.FrameNumber!=ControlledCamera.FrameNumber||output.Draw.FrameNumber!=returned.FrameNumber||
                !returned.Scope.OuterRestored||!ReferenceEquals(returned.SourceControllerReturn,SourceControllerReturned)||
                PostApplyTail.RequiresExposureCopy!=(publication is not null)||
                publication is not null&&(!ReferenceEquals(publication.Tail,PostApplyTail)||publication.FrameNumber!=returned.FrameNumber))
                throw new InvalidDataException("Normal output needs its real returned module, target and caller tail.");
            publication?.RequireCurrent();
        }
    }
}
