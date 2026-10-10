using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using San14.Data;

/// <summary>Actual post effect caller facts whose current producers are outside this
/// source preparation. Each value must come from the caller owner named by
/// SourceEvidence. No installed executable initializer is a current value.</summary>
public sealed record MapPostEffect3WorldControlledCallerFacts(
    long FrameNumber,int ActualGatherSizeTypeC0,bool ActualAdaptationLockPredicate,
    float ActualLdrLutIntensityXmm9,float ActualContextHeatScale254,int ActualProjectionClipMode,
    byte ActualExecuteArgument3R8B,byte ActualExecuteArgument4R9B,byte ActualExecuteArgument5EntryRsp28,
    int ActualQualityLevel,NativeLogDispatch ActualOwnedLogDispatch,string SourceEvidence);

/// <summary>CPU preparation for the actual owned World and controlled
/// Functor1. It borrows the allocation plan, reads source bytes and captures
/// current CPU owners. Device uploads, factory hooks and execution remain
/// with their resource owners.</summary>
public sealed partial class MapPostEffect3WorldControlledPreparation
{
    private readonly SceneViewNativeMapWorldDependencies dependencies;
    private readonly MapSourceCreatedWorldPlayableHost host;
    private readonly LoadedObjects commonTemplate,lutDefinitions;
    private readonly LoadedMapSeasonPreparedLutSource transferSource;
    private readonly LoadedMapPostEffect3LutSelectorSource selectorSource;
    private readonly LoadedMapPostEffect3ControlledAuxSource auxSource;
    private readonly MapPostEffect3FallbackCameraConstructorWords rectangleSource;
    private readonly MapPostEffect3OwnedControlledCameraSources native;
    private readonly LoadedMapPostEffect3ControlledFreshCallerSource callerSource;
    private readonly object clockGate=new();
    private readonly MapOwnedCommonFrameClock commonClock;
    private bool controllerPrepared;
    public MapPostEffect3ControlledOwnedGraphicsConfiguration GraphicsConfiguration{get;}
    private MapPostEffect3WorldControlledPreparation(SceneViewNativeMapWorldDependencies dependencies,
        MapSourceCreatedWorldPlayableHost host,MapPostEffect3AllocationPlan allocation,
        MapFinalOutputPostEffect3Source source,MapPostEffect3OwnedControlledCameraSources native,
        LoadedObjects commonTemplate,LoadedObjects lutDefinitions,LoadedMapSeasonPreparedLutSource transfer,
        LoadedMapPostEffect3LutSelectorSource selector,LoadedMapPostEffect3ControlledAuxSource aux,
        MapPostEffect3FallbackCameraConstructorWords rectangles,LoadedMapPostEffect3ControlledFreshCallerSource callerSource)
    {
        this.dependencies=dependencies;this.host=host;Allocation=allocation;Source=source;this.native=native;
        this.commonTemplate=commonTemplate;this.lutDefinitions=lutDefinitions;transferSource=transfer;
        selectorSource=selector;auxSource=aux;rectangleSource=rectangles;this.callerSource=callerSource;
        GraphicsConfiguration=callerSource.CreateFreshOwnedConfiguration(allocation.ContextOwnerIdentity+"/actual-owned-native-configuration");
        // Fresh FrameSync/Common and Apply clock owners initialize these
        // fields to zero.
        commonClock=new(callerSource,CommonRenderResources.ImportSettings(commonTemplate));
    }
    public MapFinalOutputPostEffect3Source Source{get;}
    public string ExecutableLocale=>native.Camera.Native.Locale;
    public MapPostEffect3AllocationPlan Allocation{get;}
    public MapPostEffect3ActualExtentTransition ReadActualExtentTransition(NativeMapFrameContext context)
    {
        var extent=context.OutputFrame.Textures[0x064B746B].ExplicitExtent;
        return native.Resize.ReadActualExtentTransition(context.FrameNumber,Allocation.ContextOwnerIdentity,
            Allocation.ContextWidth,Allocation.ContextHeight,extent.X,extent.Y);
    }

    /// <summary>Pass the actual bank.Source. No scalar state or controller
    /// from a Functor0 preparation is imported.</summary>
    public static MapPostEffect3WorldControlledPreparation Create(
        SceneViewNativeMapWorldDependencies actualDependencies,
        MapSourceCreatedWorldPlayableHost actualHost,MapPostEffect3AllocationPlan actualBankSource)
    {
        ArgumentNullException.ThrowIfNull(actualDependencies);ArgumentNullException.ThrowIfNull(actualHost);
        ArgumentNullException.ThrowIfNull(actualBankSource);
        var repo=actualDependencies.Repository;
        string locale=actualDependencies.WorldSource.Bootstrap.First.Camera.ExecutableLocale;
        var source=MapFinalOutputPostEffect3Source.Load(repo);
        if(actualBankSource.ResizeOnly||actualBankSource.ContextWidth==0||actualBankSource.ContextHeight==0||
            actualBankSource.Source.Sha256!=MapFinalOutputPostEffect3Source.PayloadSha256||
            !actualBankSource.Source.Bytes.Span.SequenceEqual(source.Payload.Span))
            throw new InvalidDataException("World controlled preparation requires the actual complete source3SPK allocation plan.");
        return new(actualDependencies,actualHost,actualBankSource,source,
            MapPostEffect3OwnedControlledCameraSources.Load(repo,locale),
            repo.LoadObjects("KIDSSystemResource",CommonRenderResources.SourceFileId),
            repo.LoadObjects("KIDSSystemResource",0xD2ECCCDF),MapSeasonPreparedLutSource.Load(repo,locale),
            MapPostEffect3LutSelectorSource.Load(repo,locale),MapPostEffect3ControlledAuxSource.Load(repo,locale),
            RenderSamplerSourceAdapters.ReadFallbackCameraRectangleNativeSource(
                RenderSamplerSourceAdapters.Load(repo.SourceDirectory,locale)),MapPostEffect3ControlledFreshCallerSource.Load(repo,locale));
    }

    /// <summary>Capture after actual late World submission and actual
    /// node192/193 frontend parent binding. The selected LUT source is exposed
    /// before preparation so its GPU owner can produce the actual wrapper.</summary>
    public CurrentFrame CaptureCurrent(NativeMapFrameContext actualContext,
        MapPostEffect3WorldCompositeSourceOwner.ParentBinding actualFrontendParent,
        MapPostEffect3FirstWorldFrontendPreparationInputs actualClock,
        MapPostEffect3ActualExtentTransition? actualExtentTransition=null)
    {
        ArgumentNullException.ThrowIfNull(actualContext);ArgumentNullException.ThrowIfNull(actualFrontendParent);
        ArgumentNullException.ThrowIfNull(actualClock);ArgumentException.ThrowIfNullOrWhiteSpace(actualClock.ActualClockOwnerIdentity);
        if(!float.IsFinite(actualClock.ActualRendererElapsedDelta))
            throw new InvalidDataException("Controlled preparation requires this renderer's actual finite elapsed time.");
        var prepared=host.Session.CurrentFrame??throw new InvalidDataException("Actual World preparation is absent.");
        var world=dependencies.WorldSource.RequireCurrent(actualContext);
        RequireCurrent(actualContext,prepared,world,actualFrontendParent);
        prepared.Feature.ValidateFrame(prepared.MainCamera,prepared.Lighting);
        var camera=prepared.Lighting.CapturePreparedCameraFromSameAcceptedMain(prepared.MainCamera);
        var worldCamera=world.PreparedMainCamera;
        camera.RequireEquivalentCurrentPostEffectScalars(worldCamera);
        if(camera.ExecutableLocale!=ExecutableLocale)throw new InvalidDataException("Current Camera changed the initialized original executable locale.");
        if(!camera.Prefix.Span.SequenceEqual(worldCamera.Prefix.Span)||camera.FrameNumber!=actualContext.FrameNumber)
            throw new InvalidDataException("World and Session must own the same actual post-season Camera.");
        var slot=MapPostEffect3ApplyViewSlotSource.CaptureSameCurrentView(prepared.Selection.SourcePlan.Graph,
            world.Node51??throw new InvalidDataException("Actual Main node51 selected View is absent."),world.First);
        var gammaTarget=MapPostEffect3InheritedTargetGammaSource.Capture(actualFrontendParent.EffectiveSource,
            world.First.Context,actualFrontendParent.SourceEvidence);
        var aux=auxSource.CaptureFreshSelectedTemplate(commonTemplate,world.First.Common,host.Final.SelectedViewAux);
        if(aux.SelectedViewOwnerIdentity!=world.First.Lighting.Binding.ViewIdentity||slot.Index!=0)
            throw new NotSupportedException("Controlled World builder admits this actual fresh Main View0 owner only.");
        var rectangles=MapPostEffect3ApplyFallbackCameraRectangles.ConstructFreshSource(camera,rectangleSource,
            world.First.Common.SelectedViewOwnerIdentity+"/actual-fresh-Main-rectangles");
        rectangles.ValidateSameCamera(camera);
        var transfer=transferSource.CaptureActualMainPreparation(world.Rgb.Inputs,camera);
        var selector=selectorSource.ResolveOwnedMain(world.First.Context);
        selector.ValidateFor(world.First.Context);
        var repo=dependencies.Repository;
        var hdr=MapPostEffect3CurrentLutLookup.ResolveOwnedSource(repo,camera,true,lutDefinitions,
            "KIDSSystemResource","RRPreview",selector).JoinActualMainPreparation(transfer);
        var ldr=camera.LdrLutKey933==0?null:MapPostEffect3CurrentLutLookup.ResolveOwnedSource(repo,camera,false,
            lutDefinitions,"KIDSSystemResource","RRPreview",selector).JoinActualMainPreparation(transfer);
        var lens=MapPostEffect3LensPresetSource.LoadForCamera(repo,camera.Source,camera.ExecutableLocale);
        var lookups=MapPostEffect3ApplyFreshLutLookups.ConstructFreshSource(camera,
            Allocation.ContextOwnerIdentity+"/actual-World-controlled-lookups",lens,hdr,ldr);
        if(!lookups.HdrPrimary1828Present)
            throw new NotSupportedException("This controlled World branch requires its actual successful prepared HDR source.");
        var freshCaller=callerSource.CaptureFreshCurrent(camera,transfer,lookups);
        var currentClock=CaptureActualSourceClock(actualContext,world.First.Common,actualClock);

        // The controlled light lookup reads the selected View+1B20 record+8 directly. The
        // existing World selected-record producer proves that independent
        // pointer; the inner F103 modifier payload is not selected here.
        var finalInput=MapFinalOutputProductionFrameInputs.FromWorldSourceSession(prepared,actualContext,world,
            world.Rgb.Inputs.Source.Season.Calendar,NativeCameraTangentDispatch.Sse);
        var selected=finalInput.SelectedExposureLight??throw new InvalidDataException("Actual World View1B20 light pointer is unresolved.");
        var record=world.CountedQueue.Records.Single(r=>r.RecordOwnerIdentity==selected.Identity.ProviderRecordOwner);
        var lightPacket=world.World.CopyAcceptedRecordLighting(record);
        if(selected.Identity.SelectedViewOwner!=aux.SelectedViewOwnerIdentity||
            !lightPacket.LocalLightData.Span.SequenceEqual(selected.CompleteLightData.Span))
            throw new InvalidDataException("The controlled light lookup needs the exact accepted selected-record8 payload.");
        var light=MapPostEffect3ControlledLightLookup.CaptureActualLookup(camera,lightPacket,
            aux.SelectedViewOwnerIdentity+"/controlled-selected-record",selected.SourceProvenance+
            ";selected-view-light-record;actual-complete-World-record-owner");
        var raw=MapPostEffect3ApplyUncontrolledCameraInputs.ConstructFreshSource(camera,
            Allocation.ContextOwnerIdentity+"/controlled-source-fields-only");
        int fxaa=aux.Fxaa3A8|unchecked((int)raw.Word(0x414));
        float sourceGamma=BinaryPrimitives.ReadSingleLittleEndian(camera.Prefix.Span[0x174..]);
        if(!float.IsFinite(sourceGamma)||sourceGamma<=0)throw new InvalidDataException("Current Camera174 gamma is not positive finite.");
        float adjusted=gammaTarget.NativeTarget60Equals1||gammaTarget.ContextVirtual80Result?
            Divide(sourceGamma,BitConverter.UInt32BitsToSingle(0x400CCCCD)):sourceGamma;
        float gammaDivisor=Divide(1,adjusted); // Capture before the later reciprocal writer.
        var selection=MapPostEffect3ApplyFunctorSelection.ConstructFreshDefault(Source,Allocation.Source,camera.FrameNumber,
            Allocation.ContextOwnerIdentity+"/actual-source-default-Apply-registry",slot.SourceEvidence);
        var extentTransition=actualExtentTransition??ReadActualExtentTransition(actualContext);
        if(extentTransition.FrameNumber!=actualContext.FrameNumber||extentTransition.OwnerIdentity!=Allocation.ContextOwnerIdentity||
            extentTransition.WorkingWrapperWidth40!=Allocation.ContextWidth||extentTransition.WorkingWrapperHeight42!=Allocation.ContextHeight)
            throw new InvalidDataException("Apply resize comparison must describe this actual module and current working extent.");
        return new(this,actualContext,prepared,world,actualFrontendParent,actualClock,camera,slot,aux,rectangles,
            transfer,selector,hdr,ldr,lookups,light,gammaDivisor,fxaa,selection,freshCaller,currentClock.Time254,currentClock.ApplyDelta,extentTransition);
    }

    private (float Time254,float ApplyDelta) CaptureActualSourceClock(NativeMapFrameContext context,
        MapCommonRenderState actualCommon,MapPostEffect3FirstWorldFrontendPreparationInputs actualClock)
    {
        // Normal geometry has already published this request. Legacy
        // diagnostic captures use the same entry if none was prepared;
        // either path advances3348 at most once and consumes1E4 separately.
        lock(clockGate)
        {
            MapPostEffect3WorldCommonClockPublication publication;
            if(currentCommonClock is not null&&ReferenceEquals(currentCommonClock.Context,context))
            {
                // Apply reads the published Common time; it does not resample the upstream
                // App delta after geometry. Keep the actual clock owner join.
                publication=currentCommonClock;publication.RequireCurrent();
                publication.SourceClock.RequireCurrentForCommon(actualCommon);
                if(publication.SourceClock.ActualClockOwnerIdentity!=actualClock.ActualClockOwnerIdentity)
                    throw new InvalidDataException("Apply cannot consume another actual Common clock owner.");
            }
            else publication=PrepareCommonClockBeforeGeometry(context,actualCommon,actualClock);
            var apply=commonClock.ConsumeApplyCurrent(publication.SourceClock);
            return(publication.CommonTime254,apply.ApplyDelta);
        }
    }

    private void RequireCurrent(NativeMapFrameContext context,MapSourceCreatedNativeSessionPreparedFrame prepared,
        SceneViewNativeMapWorldF103SourceOwner.CurrentFrame world,
        MapPostEffect3WorldCompositeSourceOwner.ParentBinding parent)
    {
        parent.RequireCurrent();
        var late=host.Scene.LastWorldDeferredSubmittedFrame??throw new InvalidDataException("Actual World late draw has not submitted.");
        host.Session.RequireSubmittedWorld(late);late.RequireCurrent(context);
        if(!ReferenceEquals(host.Session.CurrentFrame,prepared)||!ReferenceEquals(host.Session.CurrentWorld,world)||
            !ReferenceEquals(dependencies.WorldSource.RequireCurrent(context),world)||!ReferenceEquals(prepared.Context,context)||
            !ReferenceEquals(prepared.PreparedSourceFrame,world.First)||!ReferenceEquals(parent.Context,context)||
            parent.ContextOwnerIdentity!=world.First.Context.Source.OwnedHostIdentity||
            world.First.Common.FrameNumber!=context.FrameNumber||context.FrameNumber<=0||
            !context.OutputFrame.Textures.TryGetValue(0x064B746B,out var scene)||
            scene.ExplicitExtent.X!=(int)Allocation.ContextWidth||scene.ExplicitExtent.Y!=(int)Allocation.ContextHeight||
            parent.Owner.Extent!=scene.ExplicitExtent)
            throw new InvalidDataException("Controlled preparation belongs to another live World, parent, View or allocation extent.");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Divide(float a,float b)=>a/b;

    /// <summary>A same-current-World preparation boundary. Only this private
    /// constructor joins the actual owners. Successful source lookup remains
    /// separate from the selected device wrapper and current controller.</summary>
    public sealed class CurrentFrame
    {
        private readonly MapPostEffect3WorldControlledPreparation owner;
        private readonly MapPostEffect3FirstWorldFrontendPreparationInputs clock;
        private readonly MapPostEffect3ApplyFallbackCameraRectangles rectangles;
        private readonly MapPostEffect3ControlledLightLookup light;
        private readonly MapPostEffect3ApplyFunctorSelection selection;
        private readonly float gammaDivisor;
        private readonly int fxaa;
        private readonly float actualSourceApplyDelta;
        private readonly MapPostEffect3AcceptedCurrentLutFrame acceptedLut;
        private readonly MapPostEffect3AcceptedCameraState acceptedPreparedMain,acceptedWorldMain;
        private readonly long configurationRevision;
        private MapPostEffect3AcceptedCurrentLutFrame.ControlledCameraReceipt? acceptedControlledCamera;
        public MapPostEffect3ControlledFreshCaller FreshCaller{get;}
        public float ActualPublishedCommonBlockTime254{get;}
        public MapPostEffect3ActualExtentTransition ActualExtentTransition{get;}
        private bool preparedOnce;
        internal CurrentFrame(MapPostEffect3WorldControlledPreparation owner,NativeMapFrameContext context,
            MapSourceCreatedNativeSessionPreparedFrame prepared,SceneViewNativeMapWorldF103SourceOwner.CurrentFrame world,
            MapPostEffect3WorldCompositeSourceOwner.ParentBinding parent,
            MapPostEffect3FirstWorldFrontendPreparationInputs clock,MapCameraConstructionPacket camera,
            MapPostEffect3ApplyViewSlotSource slot,MapPostEffect3ControlledAuxFields aux,
            MapPostEffect3ApplyFallbackCameraRectangles rectangles,MapSeasonPreparedLutTransfer transfer,
            MapPostEffect3OwnedMainLutSelector selector,MapPostEffect3CurrentLutLookup hdr,
            MapPostEffect3CurrentLutLookup? ldr,MapPostEffect3ApplyFreshLutLookups lookups,
            MapPostEffect3ControlledLightLookup light,float gamma,int fxaa,MapPostEffect3ApplyFunctorSelection selection,
            MapPostEffect3ControlledFreshCaller freshCaller,float publishedTime,float applyDelta,
            MapPostEffect3ActualExtentTransition extentTransition)
        {
            this.owner=owner;Context=context;PreparedWorld=prepared;World=world;ActualFrontendParent=parent;this.clock=clock;
            Camera=camera;ApplyViewSlot=slot;AuxFields=aux;this.rectangles=rectangles;PreparedTransfer=transfer;MainSelector=selector;
            HdrLookup=hdr;LdrLookup=ldr;Lookups=lookups;this.light=light;gammaDivisor=gamma;this.fxaa=fxaa;this.selection=selection;
            FreshCaller=freshCaller;ActualPublishedCommonBlockTime254=publishedTime;actualSourceApplyDelta=applyDelta;
            ActualExtentTransition=extentTransition;
            // Complete static source acceptance belongs to capture. Queued GPU
            // work uses these detached values and exact receipt references.
            acceptedLut=MapPostEffect3AcceptedCurrentLutFrame.Capture(camera,transfer,selector,lookups);
            acceptedPreparedMain=MapPostEffect3AcceptedCameraState.Capture(prepared.MainCamera);
            acceptedWorldMain=MapPostEffect3AcceptedCameraState.Capture(world.First.Camera);
            configurationRevision=owner.GraphicsConfiguration.Revision;
        }
        public NativeMapFrameContext Context{get;}
        public MapSourceCreatedNativeSessionPreparedFrame PreparedWorld{get;}
        public SceneViewNativeMapWorldF103SourceOwner.CurrentFrame World{get;}
        public MapPostEffect3WorldCompositeSourceOwner.ParentBinding ActualFrontendParent{get;}
        public MapCameraConstructionPacket Camera{get;}
        public long FrameNumber=>Context.FrameNumber;
        public MapPostEffect3AllocationPlan Allocation=>owner.Allocation;
        public MapPostEffect3ApplyViewSlotSource ApplyViewSlot{get;}
        public MapPostEffect3ControlledAuxFields AuxFields{get;}
        public MapSeasonPreparedLutTransfer PreparedTransfer{get;}
        public MapPostEffect3OwnedMainLutSelector MainSelector{get;}
        public MapPostEffect3CurrentLutLookup HdrLookup{get;}
        public MapPostEffect3CurrentLutLookup? LdrLookup{get;}
        public MapPostEffect3ApplyFreshLutLookups Lookups{get;}
        public bool ProvesGpuExecution=>false;
        public void RequireCurrent()
        {
            owner.RequireCurrent(Context,PreparedWorld,World,ActualFrontendParent);
            MainSelector.ValidateFor(World.First.Context);
            acceptedLut.ValidateCurrent(Camera,PreparedTransfer,MainSelector,Lookups);
            acceptedPreparedMain.ValidateCurrent(PreparedWorld.MainCamera);acceptedWorldMain.ValidateCurrent(World.First.Camera);
            acceptedLut.ValidateCurrentPreparedPrefix(PreparedWorld.Lighting.PreparedCameraPrefix.Span);
            acceptedLut.ValidateCurrentPreparedPrefix(World.First.Lighting.PreparedCameraPrefix.Span);
            if(owner.GraphicsConfiguration.Revision!=configurationRevision)
                throw new InvalidDataException("Current owned graphics configuration changed after World controlled capture.");
        }

        /// <summary>Short current check for this exact CPU prepared controlled
        /// frame. The full route/LUT source proof was accepted once at Prepare;
        /// this method preserves Camera/current owners and the exact bank refs.</summary>
        public void ValidateControlledCamera(MapPostEffect3OwnedControlledCameraFrame actualFrame)
        {
            ArgumentNullException.ThrowIfNull(actualFrame);RequireCurrent();
            var receipt=acceptedControlledCamera??throw new InvalidDataException("World controlled Camera preparation has not been accepted.");
            receipt.ValidateCurrent(actualFrame,Camera);
            if(!ReferenceEquals(actualFrame.Allocation,Allocation))
                throw new InvalidDataException("Accepted controlled Camera belongs to another actual World bank.");
        }

        /// <summary>Call after the actual LUT owner selects this source's
        /// wrapper. Native metadata28 is explicit, including a proven null;
        /// no descriptor identity is inferred from a nonnull593 handle.</summary>
        public MapPostEffect3ControlledSelectedHdrTexture CaptureActualHdrTexture(
            MapPostEffect3ApplyView actualSelectedWrapper,ReadOnlyMemory<byte>? actualNativeWrapper28Metadata,
            string actualSelectionAndMetadataProducerEvidence)
        {
            RequireCurrent();return MapPostEffect3ControlledSelectedHdrTexture.CaptureActualStaticSelection(
                HdrLookup,actualSelectedWrapper,actualNativeWrapper28Metadata,actualSelectionAndMetadataProducerEvidence);
        }

        /// <summary>The actual first owned source invocation. Fresh controller
        /// creation supplies arg3; matching module/working dimensions supplies
        /// arg4; fresh Apply bit23 supplies arg5. Later retained invocations
        /// need their actual current controller/Apply lifecycle.</summary>
        public MapPostEffect3OwnedControlledCameraFrame PrepareActualFresh(
            MapPostEffect3ControlledSelectedHdrTexture actualSelectedHdr)
        {
            RequireCurrent();
            if(owner.controllerPrepared)
                throw new InvalidOperationException("Fresh controlled caller requires this owner's unprepared controller lifecycle.");
            var config=owner.GraphicsConfiguration;long revision=config.Revision;
            var facts=new MapPostEffect3WorldControlledCallerFacts(FrameNumber,FreshCaller.GatherSizeTypeC0,
                FreshCaller.AdaptationLock,FreshCaller.LdrIntensityXmm9,ActualPublishedCommonBlockTime254,config.ProjectionConvention,
                FreshCaller.FreshControllerArgument3,0,FreshCaller.FreshApplyArgument5,config.QualityLevel,config.LogDispatch,
                FreshCaller.SourceEvidence+";"+config.SourceEvidence+";"+owner.callerSource.SourceEvidence+
                ";actual-module-dimensions-equal-current-working-wrapper/arg4zero;"+
                "owned-FrameSync-published-Common-time;actual-Apply-clock-subtract/update");
            var result=PrepareCore(actualSelectedHdr,facts,null,FreshCaller);
            if(config.Revision!=revision)throw new InvalidDataException("Native owned configuration changed during controlled preparation.");
            return result;
        }

        public MapPostEffect3OwnedControlledCameraFrame Prepare(
            MapPostEffect3ControlledSelectedHdrTexture actualSelectedHdr,
            MapPostEffect3WorldControlledCallerFacts actualCaller,
            MapPostEffect3ControllerScalarSnapshot actualPostApplyControllerScalars)
            =>PrepareCore(actualSelectedHdr,actualCaller,actualPostApplyControllerScalars,null);

        /// <summary>Later unchanged source controller/optics. Creation/reset
        /// writers are not rerun. Actual clock and Camera Apply writers are
        /// recaptured for this World; managed module return is required by the
        /// game module owner before this CPU-only preparation is requested.</summary>
        internal MapPostEffect3OwnedControlledCameraFrame PrepareActualRetained(
            MapPostEffect3ControlledSelectedHdrTexture actualSelectedHdr,
            MapPostEffect3OwnedControlledCameraFrame previous,MapPostEffect3Functor1ModuleState module,
            byte actualPriorApplyArgument5,string actualPriorApplyPostwriterEvidence)
        {
            RequireCurrent();
            if(previous.FrameNumber+1!=FrameNumber||previous.Invocation.NativeControllerReturn!=true||
                !ReferenceEquals(previous.Allocation,Allocation)||previous.Controller.ChangeFStopA1!=0)
                throw new InvalidDataException("Retained Apply requires its exact returned previous controller.");
            var config=owner.GraphicsConfiguration;
            var snapshot=previous.Controller.CaptureCurrentSnapshot(FrameNumber) with{
                StopBladeCount6C=FreshCaller.StopBladeCount6C,Diaphragm9C=FreshCaller.Diaphragm9C,
                EnableDofA4=FreshCaller.GatherSizeTypeC0,
                SourceEvidence=FreshCaller.SourceEvidence+";same-owned-controller-after-source-cleanup/no-controller-reset"};
            var facts=new MapPostEffect3WorldControlledCallerFacts(FrameNumber,FreshCaller.GatherSizeTypeC0,
                FreshCaller.AdaptationLock,FreshCaller.LdrIntensityXmm9,ActualPublishedCommonBlockTime254,config.ProjectionConvention,
                0,ActualExtentTransition.ControllerArgument4R9B,actualPriorApplyArgument5,config.QualityLevel,config.LogDispatch,
                config.SourceEvidence+";retained-controlled-owner-existing-controller->arg3zero;"+ActualExtentTransition.SourceEvidence+";"+
                actualPriorApplyPostwriterEvidence+";owned-previous-return-Apply-bit23->current-arg5;actual-continuous-FrameSync/Apply-delta");
            return PrepareCore(actualSelectedHdr,facts,snapshot,null,previous,module);
        }

        private MapPostEffect3OwnedControlledCameraFrame PrepareCore(
            MapPostEffect3ControlledSelectedHdrTexture actualSelectedHdr,
            MapPostEffect3WorldControlledCallerFacts actualCaller,
            MapPostEffect3ControllerScalarSnapshot? actualPostApplyControllerScalars,
            MapPostEffect3ControlledFreshCaller? actualFreshCaller,
            MapPostEffect3OwnedControlledCameraFrame? previous=null,MapPostEffect3Functor1ModuleState? module=null)
        {
            ArgumentNullException.ThrowIfNull(actualSelectedHdr);ArgumentNullException.ThrowIfNull(actualCaller);RequireCurrent();
            if(preparedOnce)throw new InvalidOperationException("This actual World controlled preparation was already consumed.");
            if(actualCaller.FrameNumber!=FrameNumber||actualPostApplyControllerScalars is{} suppliedSnapshot&&suppliedSnapshot.FrameNumber!=FrameNumber||
                !ReferenceEquals(actualSelectedHdr.Lookup,HdrLookup)||string.IsNullOrWhiteSpace(actualCaller.SourceEvidence))
                throw new InvalidDataException("Controlled caller/controller/selected HDR belong to another actual World frame.");
            var caller=new MapPostEffect3ControlledCameraCaller(FrameNumber,checked((int)Allocation.ContextWidth),
                checked((int)Allocation.ContextHeight),fxaa,gammaDivisor,actualSourceApplyDelta,
                actualCaller.ActualLdrLutIntensityXmm9,actualCaller.ActualGatherSizeTypeC0,actualCaller.ActualAdaptationLockPredicate,
                rectangles.Viewport1540,rectangles.Viewport1540,actualCaller.ActualProjectionClipMode,actualCaller.ActualContextHeatScale254,
                actualCaller.SourceEvidence+";"+ApplyViewSlot.SourceEvidence+";"+rectangles.SourceEvidence+";"+
                ActualFrontendParent.SourceEvidence+";actual-mode3-word;Aux3A8-OR-Camera414;"+
                "owned-renderer-clock="+clock.ActualClockOwnerIdentity);
            var execute=new MapPostEffect3ControllerExecuteCaller(FrameNumber,actualCaller.ActualExecuteArgument3R8B,
                actualCaller.ActualExecuteArgument4R9B,actualCaller.ActualExecuteArgument5EntryRsp28,actualCaller.ActualQualityLevel,
                selection.WithControllerIndex1A8,actualCaller.SourceEvidence+";"+selection.SourceEvidence);
            var inputs=new MapPostEffect3OwnedControlledCameraInputs(PreparedTransfer,MainSelector,Lookups,
                actualSelectedHdr,light,caller,ReadOnlyMemory<byte>.Empty,AuxFields.SourceEvidence,actualPostApplyControllerScalars,
                execute,actualCaller.ActualOwnedLogDispatch){ActualAuxFields=AuxFields,ActualFreshCaller=actualFreshCaller};
            var frame=previous is null?MapPostEffect3OwnedControlledCameraFrame.Prepare(owner.Source,Allocation,Camera,owner.native,inputs):
                MapPostEffect3OwnedControlledCameraFrame.PrepareRetained(owner.Source,Allocation,Camera,owner.native,inputs,previous,
                    module??throw new InvalidDataException("Retained Camera module identity absent."));
            var receipt=acceptedLut.AcceptControlledCamera(frame,Camera);
            RequireCurrent();acceptedControlledCamera=receipt;preparedOnce=true;owner.controllerPrepared=true;return frame;
        }
    }
}
