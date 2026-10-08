using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using San14.Data;

/// <summary>Actual EB caller facts whose current producers are outside this
/// source preparation. Each value must come from the caller owner named by
/// SourceEvidence. No installed executable initializer is a current value.</summary>
public sealed record OriginalMapPostEffect3WorldControlledCallerFacts(
    long FrameNumber,int ActualGatherSizeTypeC0,bool ActualAdaptationLockPredicate,
    float ActualLdrLutIntensityXmm9,float ActualContextHeatScale254,int ActualAb5D10Result,
    byte ActualExecuteArgument3R8B,byte ActualExecuteArgument4R9B,byte ActualExecuteArgument5EntryRsp28,
    int ActualQualityLevel,OriginalNativeLogDispatch ActualOwnedLogDispatch,string SourceEvidence);

/// <summary>CPU preparation for the actual owned World and controlled
/// Functor1. It borrows the allocation plan, reads source bytes and captures
/// current CPU owners. Device uploads, factory hooks and execution remain
/// with their resource owners.</summary>
public sealed partial class OriginalMapPostEffect3WorldControlledPreparation
{
    private readonly OriginalSceneViewNativeMapWorldDependencies dependencies;
    private readonly OriginalMapSourceCreatedWorldPlayableHost host;
    private readonly LoadedObjects commonTemplate,lutDefinitions;
    private readonly LoadedOriginalMapSeasonPreparedLutSource transferSource;
    private readonly LoadedOriginalMapPostEffect3LutSelectorSource selectorSource;
    private readonly LoadedOriginalMapPostEffect3ControlledAuxSource auxSource;
    private readonly OriginalMapPostEffect3FallbackCameraConstructorWords rectangleSource;
    private readonly OriginalMapPostEffect3OwnedControlledCameraSources native;
    private readonly LoadedOriginalMapPostEffect3ControlledFreshCallerSource callerSource;
    private readonly object clockGate=new();
    private readonly OriginalMapOwnedCommonFrameClock commonClock;
    private bool controllerPrepared;
    public OriginalMapPostEffect3ControlledOwnedGraphicsConfiguration GraphicsConfiguration{get;}
    private OriginalMapPostEffect3WorldControlledPreparation(OriginalSceneViewNativeMapWorldDependencies dependencies,
        OriginalMapSourceCreatedWorldPlayableHost host,OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapFinalOutputPostEffect3Source source,OriginalMapPostEffect3OwnedControlledCameraSources native,
        LoadedObjects commonTemplate,LoadedObjects lutDefinitions,LoadedOriginalMapSeasonPreparedLutSource transfer,
        LoadedOriginalMapPostEffect3LutSelectorSource selector,LoadedOriginalMapPostEffect3ControlledAuxSource aux,
        OriginalMapPostEffect3FallbackCameraConstructorWords rectangles,LoadedOriginalMapPostEffect3ControlledFreshCallerSource callerSource)
    {
        this.dependencies=dependencies;this.host=host;Allocation=allocation;Source=source;this.native=native;
        this.commonTemplate=commonTemplate;this.lutDefinitions=lutDefinitions;transferSource=transfer;
        selectorSource=selector;auxSource=aux;rectangleSource=rectangles;this.callerSource=callerSource;
        GraphicsConfiguration=callerSource.CreateFreshOwnedConfiguration(allocation.ContextOwnerIdentity+"/actual-owned-native-configuration");
        // Original D0176F and E6BB1C are the actual zero writers for these
        // newly owned FrameSync/Common and Apply clock fields.
        commonClock=new(callerSource,OriginalMapFreshCommonResources.ReadSourceTemplate(commonTemplate));
    }
    public OriginalMapFinalOutputPostEffect3Source Source{get;}
    public string ExecutableLocale=>native.Camera.Native.Locale;
    public OriginalMapPostEffect3AllocationPlan Allocation{get;}
    public OriginalMapPostEffect3ActualExtentTransition ReadActualExtentTransition(OriginalNativeMapFrameContext context)
    {
        var extent=context.OutputFrame.Textures[0x064B746B].ExplicitExtent;
        return native.Resize.ReadActualExtentTransition(context.FrameNumber,Allocation.ContextOwnerIdentity,
            Allocation.ContextWidth,Allocation.ContextHeight,extent.X,extent.Y);
    }

    /// <summary>Pass the actual bank.Source. No scalar state or controller
    /// from a Functor0 preparation is imported.</summary>
    public static OriginalMapPostEffect3WorldControlledPreparation Create(
        OriginalSceneViewNativeMapWorldDependencies actualDependencies,
        OriginalMapSourceCreatedWorldPlayableHost actualHost,OriginalMapPostEffect3AllocationPlan actualBankSource)
    {
        ArgumentNullException.ThrowIfNull(actualDependencies);ArgumentNullException.ThrowIfNull(actualHost);
        ArgumentNullException.ThrowIfNull(actualBankSource);
        var repo=actualDependencies.Repository;
        string locale=actualDependencies.WorldSource.Bootstrap.First.Camera.ExecutableLocale;
        var source=OriginalMapFinalOutputPostEffect3Source.Load(repo);
        if(actualBankSource.ResizeOnly||actualBankSource.ContextWidth==0||actualBankSource.ContextHeight==0||
            actualBankSource.Source.Sha256!=OriginalMapFinalOutputPostEffect3Source.PayloadSha256||
            !actualBankSource.Source.Bytes.Span.SequenceEqual(source.Payload.Span))
            throw new InvalidDataException("World controlled preparation requires the actual complete source3SPK allocation plan.");
        return new(actualDependencies,actualHost,actualBankSource,source,
            OriginalMapPostEffect3OwnedControlledCameraSources.Load(repo,locale),
            repo.LoadObjects("KIDSSystemResource",OriginalMapFreshCommonResources.SourceFileId),
            repo.LoadObjects("KIDSSystemResource",0xD2ECCCDF),OriginalMapSeasonPreparedLutSource.Load(repo,locale),
            OriginalMapPostEffect3LutSelectorSource.Load(repo,locale),OriginalMapPostEffect3ControlledAuxSource.Load(repo,locale),
            OriginalDeferredSamplerSources.ReadFallbackCameraRectangleNativeSource(
                OriginalDeferredSamplerSources.Load(repo.SourceDirectory,locale)),OriginalMapPostEffect3ControlledFreshCallerSource.Load(repo,locale));
    }

    /// <summary>Capture after actual late World submission and actual
    /// node192/193 frontend parent binding. The selected LUT source is exposed
    /// before preparation so its GPU owner can produce the actual wrapper.</summary>
    public CurrentFrame CaptureCurrent(OriginalNativeMapFrameContext actualContext,
        OriginalMapPostEffect3WorldCompositeSourceOwner.ParentBinding actualFrontendParent,
        OriginalMapPostEffect3FirstWorldFrontendPreparationInputs actualClock,
        OriginalMapPostEffect3ActualExtentTransition? actualExtentTransition=null)
    {
        ArgumentNullException.ThrowIfNull(actualContext);ArgumentNullException.ThrowIfNull(actualFrontendParent);
        ArgumentNullException.ThrowIfNull(actualClock);ArgumentException.ThrowIfNullOrWhiteSpace(actualClock.ActualClockOwnerIdentity);
        if(!float.IsFinite(actualClock.ActualRendererElapsedDelta))
            throw new InvalidDataException("Controlled preparation requires this renderer's actual finite elapsed time.");
        var prepared=host.Session.CurrentFrame??throw new InvalidDataException("Actual World preparation is absent.");
        var world=dependencies.WorldSource.RequireCurrent(actualContext);
        RequireCurrent(actualContext,prepared,world,actualFrontendParent);
        prepared.Selection.Validate();prepared.Feature.ValidateFrame(prepared.MainCamera,prepared.Lighting);
        var camera=prepared.Lighting.CapturePreparedCameraFromSameAcceptedMain(prepared.MainCamera);
        var worldCamera=world.PreparedMainCamera;
        camera.RequireEquivalentCurrentPostEffectScalars(worldCamera);
        if(camera.ExecutableLocale!=ExecutableLocale)throw new InvalidDataException("Current Camera changed the initialized original executable locale.");
        if(!camera.Prefix.Span.SequenceEqual(worldCamera.Prefix.Span)||camera.FrameNumber!=actualContext.FrameNumber)
            throw new InvalidDataException("World and Session must own the same actual post-season Camera.");
        var slot=OriginalMapPostEffect3ApplyViewSlotSource.CaptureSameCurrentView(prepared.Selection.SourcePlan.Graph,
            world.Node51??throw new InvalidDataException("Actual Main node51 selected View is absent."),world.First);
        var gammaTarget=OriginalMapPostEffect3InheritedTargetGammaSource.Capture(actualFrontendParent.EffectiveSource,
            world.First.Context,actualFrontendParent.SourceEvidence);
        var aux=auxSource.CaptureFreshSelectedTemplate(commonTemplate,world.First.Common,host.Final.SelectedViewAux);
        if(aux.SelectedViewOwnerIdentity!=world.First.Lighting.Binding.ViewIdentity||slot.Index!=0)
            throw new NotSupportedException("Controlled World builder admits this actual fresh Main View0 owner only.");
        var rectangles=OriginalMapPostEffect3ApplyFallbackCameraRectangles.ConstructFreshSource(camera,rectangleSource,
            world.First.Common.SelectedViewOwnerIdentity+"/actual-fresh-Main-rectangles");
        rectangles.ValidateSameCamera(camera);
        var transfer=transferSource.CaptureActualMainPreparation(world.Rgb.Inputs,camera);
        var selector=selectorSource.ResolveOwnedMain(world.First.Context);
        selector.ValidateFor(world.First.Context);
        var repo=dependencies.Repository;
        var hdr=OriginalMapPostEffect3CurrentLutLookup.ResolveOwnedSource(repo,camera,true,lutDefinitions,
            "KIDSSystemResource","RRPreview",selector).JoinActualMainPreparation(transfer);
        var ldr=camera.LdrLutKey933==0?null:OriginalMapPostEffect3CurrentLutLookup.ResolveOwnedSource(repo,camera,false,
            lutDefinitions,"KIDSSystemResource","RRPreview",selector).JoinActualMainPreparation(transfer);
        var lens=OriginalMapPostEffect3LensPresetSource.LoadForCamera(repo,camera.Source,camera.ExecutableLocale);
        var lookups=OriginalMapPostEffect3ApplyFreshLutLookups.ConstructFreshSource(camera,
            Allocation.ContextOwnerIdentity+"/actual-World-controlled-lookups",lens,hdr,ldr);
        if(!lookups.HdrPrimary1828Present)
            throw new NotSupportedException("This controlled World branch requires its actual successful prepared HDR source.");
        var freshCaller=callerSource.CaptureFreshCurrent(camera,transfer,lookups);
        var currentClock=CaptureActualSourceClock(actualContext,world.First.Common,actualClock);

        // D59120 reads the same selected View+1B20 record+8, directly. The
        // existing World selected-record producer proves that independent
        // pointer; the inner F103 modifier payload is not selected here.
        var finalInput=OriginalMapFinalOutputProductionFrameInputs.FromWorldSourceSession(prepared,actualContext,world,
            world.Rgb.Inputs.Source.Season.Calendar,OriginalNativeCameraTangentDispatch.Sse);
        var selected=finalInput.SelectedExposureLight??throw new InvalidDataException("Actual World View1B20 light pointer is unresolved.");
        var record=world.CountedQueue.Records.Single(r=>r.RecordOwnerIdentity==selected.Identity.ProviderRecordOwner);
        var lightPacket=world.World.CopyAcceptedRecordLighting(record);
        if(selected.Identity.SelectedViewOwner!=aux.SelectedViewOwnerIdentity||
            !lightPacket.LocalLightData.Span.SequenceEqual(selected.CompleteLightData.Span))
            throw new InvalidDataException("D59120 needs the exact original accepted selected-record8 payload.");
        var light=OriginalMapPostEffect3ControlledLightLookup.CaptureActualLookup(camera,lightPacket,
            aux.SelectedViewOwnerIdentity+"/D59120-selected-record8",selected.SourceProvenance+
            ";TC:D59120-ViewCollection110[index]/View1B20/record8;actual-complete-World-record-owner");
        var raw=OriginalMapPostEffect3ApplyUncontrolledCameraInputs.ConstructFreshSource(camera,
            Allocation.ContextOwnerIdentity+"/controlled-source-fields-only");
        int fxaa=aux.Fxaa3A8|unchecked((int)raw.Word(0x414));
        float sourceGamma=BinaryPrimitives.ReadSingleLittleEndian(camera.Prefix.Span[0x174..]);
        if(!float.IsFinite(sourceGamma)||sourceGamma<=0)throw new InvalidDataException("Current Camera174 gamma is not positive finite.");
        float adjusted=gammaTarget.NativeTarget60Equals1||gammaTarget.ContextVirtual80Result?
            Divide(sourceGamma,BitConverter.UInt32BitsToSingle(0x400CCCCD)):sourceGamma;
        float gammaDivisor=Divide(1,adjusted); // actual EB5FA1 xmm13, before its later reciprocal writer.
        var selection=OriginalMapPostEffect3ApplyFunctorSelection.ConstructFreshDefault(Source,Allocation.Source,camera.FrameNumber,
            Allocation.ContextOwnerIdentity+"/actual-source-default-Apply-registry",slot.SourceEvidence);
        var extentTransition=actualExtentTransition??ReadActualExtentTransition(actualContext);
        if(extentTransition.FrameNumber!=actualContext.FrameNumber||extentTransition.OwnerIdentity!=Allocation.ContextOwnerIdentity||
            extentTransition.WorkingWrapperWidth40!=Allocation.ContextWidth||extentTransition.WorkingWrapperHeight42!=Allocation.ContextHeight)
            throw new InvalidDataException("Apply resize comparison must describe this actual module and current working extent.");
        return new(this,actualContext,prepared,world,actualFrontendParent,actualClock,camera,slot,aux,rectangles,
            transfer,selector,hdr,ldr,lookups,light,gammaDivisor,fxaa,selection,freshCaller,currentClock.Time254,currentClock.ApplyDelta,extentTransition);
    }

    private (float Time254,float ApplyDelta) CaptureActualSourceClock(OriginalNativeMapFrameContext context,
        OriginalMapFreshCommonResourceSnapshot actualCommon,OriginalMapPostEffect3FirstWorldFrontendPreparationInputs actualClock)
    {
        // Normal geometry has already published this request. Legacy
        // diagnostic captures use the same entry if none was prepared;
        // either path advances3348 at most once and consumes1E4 separately.
        lock(clockGate)
        {
            OriginalMapPostEffect3WorldCommonClockPublication publication;
            if(currentCommonClock is not null&&ReferenceEquals(currentCommonClock.Context,context))
            {
                // EB5E7A reads table254; it does not resample the upstream
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

    private void RequireCurrent(OriginalNativeMapFrameContext context,OriginalMapSourceCreatedNativeSessionPreparedFrame prepared,
        OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame world,
        OriginalMapPostEffect3WorldCompositeSourceOwner.ParentBinding parent)
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
        private readonly OriginalMapPostEffect3WorldControlledPreparation owner;
        private readonly OriginalMapPostEffect3FirstWorldFrontendPreparationInputs clock;
        private readonly OriginalMapPostEffect3ApplyFallbackCameraRectangles rectangles;
        private readonly OriginalMapPostEffect3ControlledLightLookup light;
        private readonly OriginalMapPostEffect3ApplyFunctorSelection selection;
        private readonly float gammaDivisor;
        private readonly int fxaa;
        private readonly float actualSourceApplyDelta;
        private readonly OriginalMapPostEffect3AcceptedCurrentLutFrame acceptedLut;
        private readonly OriginalMapPostEffect3AcceptedCameraState acceptedPreparedMain,acceptedWorldMain;
        private readonly long configurationRevision;
        private OriginalMapPostEffect3AcceptedCurrentLutFrame.ControlledCameraReceipt? acceptedControlledCamera;
        public OriginalMapPostEffect3ControlledFreshCaller FreshCaller{get;}
        public float ActualPublishedCommonBlockTime254{get;}
        public OriginalMapPostEffect3ActualExtentTransition ActualExtentTransition{get;}
        private bool preparedOnce;
        internal CurrentFrame(OriginalMapPostEffect3WorldControlledPreparation owner,OriginalNativeMapFrameContext context,
            OriginalMapSourceCreatedNativeSessionPreparedFrame prepared,OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame world,
            OriginalMapPostEffect3WorldCompositeSourceOwner.ParentBinding parent,
            OriginalMapPostEffect3FirstWorldFrontendPreparationInputs clock,OriginalMapCameraConstructionPacket camera,
            OriginalMapPostEffect3ApplyViewSlotSource slot,OriginalMapPostEffect3ControlledAuxFields aux,
            OriginalMapPostEffect3ApplyFallbackCameraRectangles rectangles,OriginalMapSeasonPreparedLutTransfer transfer,
            OriginalMapPostEffect3OwnedMainLutSelector selector,OriginalMapPostEffect3CurrentLutLookup hdr,
            OriginalMapPostEffect3CurrentLutLookup? ldr,OriginalMapPostEffect3ApplyFreshLutLookups lookups,
            OriginalMapPostEffect3ControlledLightLookup light,float gamma,int fxaa,OriginalMapPostEffect3ApplyFunctorSelection selection,
            OriginalMapPostEffect3ControlledFreshCaller freshCaller,float publishedTime,float applyDelta,
            OriginalMapPostEffect3ActualExtentTransition extentTransition)
        {
            this.owner=owner;Context=context;PreparedWorld=prepared;World=world;ActualFrontendParent=parent;this.clock=clock;
            Camera=camera;ApplyViewSlot=slot;AuxFields=aux;this.rectangles=rectangles;PreparedTransfer=transfer;MainSelector=selector;
            HdrLookup=hdr;LdrLookup=ldr;Lookups=lookups;this.light=light;gammaDivisor=gamma;this.fxaa=fxaa;this.selection=selection;
            FreshCaller=freshCaller;ActualPublishedCommonBlockTime254=publishedTime;actualSourceApplyDelta=applyDelta;
            ActualExtentTransition=extentTransition;
            // Complete static source acceptance belongs to capture. Queued GPU
            // work uses these detached values and exact receipt references.
            acceptedLut=OriginalMapPostEffect3AcceptedCurrentLutFrame.Capture(camera,transfer,selector,lookups);
            acceptedPreparedMain=OriginalMapPostEffect3AcceptedCameraState.Capture(prepared.MainCamera);
            acceptedWorldMain=OriginalMapPostEffect3AcceptedCameraState.Capture(world.First.Camera);
            configurationRevision=owner.GraphicsConfiguration.Revision;
        }
        public OriginalNativeMapFrameContext Context{get;}
        public OriginalMapSourceCreatedNativeSessionPreparedFrame PreparedWorld{get;}
        public OriginalSceneViewNativeMapWorldF103SourceOwner.CurrentFrame World{get;}
        public OriginalMapPostEffect3WorldCompositeSourceOwner.ParentBinding ActualFrontendParent{get;}
        public OriginalMapCameraConstructionPacket Camera{get;}
        public long FrameNumber=>Context.FrameNumber;
        public OriginalMapPostEffect3AllocationPlan Allocation=>owner.Allocation;
        public OriginalMapPostEffect3ApplyViewSlotSource ApplyViewSlot{get;}
        public OriginalMapPostEffect3ControlledAuxFields AuxFields{get;}
        public OriginalMapSeasonPreparedLutTransfer PreparedTransfer{get;}
        public OriginalMapPostEffect3OwnedMainLutSelector MainSelector{get;}
        public OriginalMapPostEffect3CurrentLutLookup HdrLookup{get;}
        public OriginalMapPostEffect3CurrentLutLookup? LdrLookup{get;}
        public OriginalMapPostEffect3ApplyFreshLutLookups Lookups{get;}
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
        public void ValidateControlledCamera(OriginalMapPostEffect3OwnedControlledCameraFrame actualFrame)
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
        public OriginalMapPostEffect3ControlledSelectedHdrTexture CaptureActualHdrTexture(
            OriginalMapPostEffect3ApplyView actualSelectedWrapper,ReadOnlyMemory<byte>? actualNativeWrapper28Metadata,
            string actualSelectionAndMetadataProducerEvidence)
        {
            RequireCurrent();return OriginalMapPostEffect3ControlledSelectedHdrTexture.CaptureActualStaticSelection(
                HdrLookup,actualSelectedWrapper,actualNativeWrapper28Metadata,actualSelectionAndMetadataProducerEvidence);
        }

        /// <summary>The actual first owned source invocation. Fresh controller
        /// creation supplies arg3; matching module/working dimensions supplies
        /// arg4; fresh Apply bit23 supplies arg5. Later retained invocations
        /// need their actual current controller/Apply lifecycle.</summary>
        public OriginalMapPostEffect3OwnedControlledCameraFrame PrepareActualFresh(
            OriginalMapPostEffect3ControlledSelectedHdrTexture actualSelectedHdr)
        {
            RequireCurrent();
            if(owner.controllerPrepared)
                throw new InvalidOperationException("Fresh controlled caller requires this owner's unprepared controller lifecycle.");
            var config=owner.GraphicsConfiguration;long revision=config.Revision;
            var facts=new OriginalMapPostEffect3WorldControlledCallerFacts(FrameNumber,FreshCaller.GatherSizeTypeC0,
                FreshCaller.AdaptationLock,FreshCaller.LdrIntensityXmm9,ActualPublishedCommonBlockTime254,config.ProjectionConvention,
                FreshCaller.FreshControllerArgument3,0,FreshCaller.FreshApplyArgument5,config.QualityLevel,config.LogDispatch,
                FreshCaller.SourceEvidence+";"+config.SourceEvidence+";"+owner.callerSource.SourceEvidence+
                ";actual-module-dimensions-equal-current-working-wrapper40/42->EB691A-arg4zero;"+
                "owned-FrameSync3348->D12ACB/D61BB4-current-block254;EB5E8D/96-actual-Apply-clock-subtract/update");
            var result=PrepareCore(actualSelectedHdr,facts,null,FreshCaller);
            if(config.Revision!=revision)throw new InvalidDataException("Native owned configuration changed during controlled preparation.");
            return result;
        }

        public OriginalMapPostEffect3OwnedControlledCameraFrame Prepare(
            OriginalMapPostEffect3ControlledSelectedHdrTexture actualSelectedHdr,
            OriginalMapPostEffect3WorldControlledCallerFacts actualCaller,
            OriginalMapPostEffect3ControllerScalarSnapshot actualPostApplyControllerScalars)
            =>PrepareCore(actualSelectedHdr,actualCaller,actualPostApplyControllerScalars,null);

        /// <summary>Later unchanged source controller/optics. Creation/reset
        /// writers are not rerun. Actual clock and Camera Apply writers are
        /// recaptured for this World; managed module return is required by the
        /// game module owner before this CPU-only preparation is requested.</summary>
        internal OriginalMapPostEffect3OwnedControlledCameraFrame PrepareActualRetained(
            OriginalMapPostEffect3ControlledSelectedHdrTexture actualSelectedHdr,
            OriginalMapPostEffect3OwnedControlledCameraFrame previous,OriginalMapPostEffect3Functor1ModuleState module,
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
                SourceEvidence=FreshCaller.SourceEvidence+";same-owned-controller-after-source-cleanup/no-E243F0-reset"};
            var facts=new OriginalMapPostEffect3WorldControlledCallerFacts(FrameNumber,FreshCaller.GatherSizeTypeC0,
                FreshCaller.AdaptationLock,FreshCaller.LdrIntensityXmm9,ActualPublishedCommonBlockTime254,config.ProjectionConvention,
                0,ActualExtentTransition.ControllerArgument4R9B,actualPriorApplyArgument5,config.QualityLevel,config.LogDispatch,
                config.SourceEvidence+";retained-controlled-owner-existing-controller->arg3zero;"+ActualExtentTransition.SourceEvidence+";"+
                actualPriorApplyPostwriterEvidence+";owned-previous-return-Apply-bit23->current-arg5;actual-continuous-FrameSync/Apply-delta");
            return PrepareCore(actualSelectedHdr,facts,snapshot,null,previous,module);
        }

        private OriginalMapPostEffect3OwnedControlledCameraFrame PrepareCore(
            OriginalMapPostEffect3ControlledSelectedHdrTexture actualSelectedHdr,
            OriginalMapPostEffect3WorldControlledCallerFacts actualCaller,
            OriginalMapPostEffect3ControllerScalarSnapshot? actualPostApplyControllerScalars,
            OriginalMapPostEffect3ControlledFreshCaller? actualFreshCaller,
            OriginalMapPostEffect3OwnedControlledCameraFrame? previous=null,OriginalMapPostEffect3Functor1ModuleState? module=null)
        {
            ArgumentNullException.ThrowIfNull(actualSelectedHdr);ArgumentNullException.ThrowIfNull(actualCaller);RequireCurrent();
            if(preparedOnce)throw new InvalidOperationException("This actual World controlled preparation was already consumed.");
            if(actualCaller.FrameNumber!=FrameNumber||actualPostApplyControllerScalars is{} suppliedSnapshot&&suppliedSnapshot.FrameNumber!=FrameNumber||
                !ReferenceEquals(actualSelectedHdr.Lookup,HdrLookup)||string.IsNullOrWhiteSpace(actualCaller.SourceEvidence))
                throw new InvalidDataException("Controlled caller/controller/selected HDR belong to another actual World frame.");
            var caller=new OriginalMapPostEffect3ControlledCameraCaller(FrameNumber,checked((int)Allocation.ContextWidth),
                checked((int)Allocation.ContextHeight),fxaa,gammaDivisor,actualSourceApplyDelta,
                actualCaller.ActualLdrLutIntensityXmm9,actualCaller.ActualGatherSizeTypeC0,actualCaller.ActualAdaptationLockPredicate,
                rectangles.Viewport1540,rectangles.Viewport1540,actualCaller.ActualAb5D10Result,actualCaller.ActualContextHeatScale254,
                actualCaller.SourceEvidence+";"+ApplyViewSlot.SourceEvidence+";"+rectangles.SourceEvidence+";"+
                ActualFrontendParent.SourceEvidence+";EB5FA1-actual-xmm13;EB6886/EB6890-actual-Aux3A8-OR-Camera414;"+
                "owned-renderer-clock="+clock.ActualClockOwnerIdentity);
            var execute=new OriginalMapPostEffect3ControllerExecuteCaller(FrameNumber,actualCaller.ActualExecuteArgument3R8B,
                actualCaller.ActualExecuteArgument4R9B,actualCaller.ActualExecuteArgument5EntryRsp28,actualCaller.ActualQualityLevel,
                selection.WithControllerIndex1A8,actualCaller.SourceEvidence+";"+selection.SourceEvidence);
            var inputs=new OriginalMapPostEffect3OwnedControlledCameraInputs(PreparedTransfer,MainSelector,Lookups,
                actualSelectedHdr,light,caller,ReadOnlyMemory<byte>.Empty,AuxFields.SourceEvidence,actualPostApplyControllerScalars,
                execute,actualCaller.ActualOwnedLogDispatch){ActualAuxFields=AuxFields,ActualFreshCaller=actualFreshCaller};
            var frame=previous is null?OriginalMapPostEffect3OwnedControlledCameraFrame.Prepare(owner.Source,Allocation,Camera,owner.native,inputs):
                OriginalMapPostEffect3OwnedControlledCameraFrame.PrepareRetained(owner.Source,Allocation,Camera,owner.native,inputs,previous,
                    module??throw new InvalidDataException("Retained Camera module identity absent."));
            var receipt=acceptedLut.AcceptControlledCamera(frame,Camera);
            RequireCurrent();acceptedControlledCamera=receipt;preparedOnce=true;owner.controllerPrepared=true;return frame;
        }
    }
}
