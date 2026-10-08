using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data;

/// <summary>Fresh, source selected factory6..9 preparation on the actual
/// submitted World. Pointer initialization and shader plans do not make any
/// newly allocated texture readable. GPU execution and completed source exit
/// remain the responsibility of the corresponding execution owners.</summary>
public sealed partial class OriginalMapPostEffect3WorldControlledFactoryFrame : IAsyncDisposable
{
    private readonly OriginalMapPostEffect3WorldControlledPreparation.CurrentFrame preparation;
    private OriginalMapPostEffect3DepthOfField2ModuleResources? depthOfFieldResources;
    private readonly OriginalMapPostEffect3RetainedShapeWrapperMetadata? retainedShapeMetadata;
    private bool disposed;
    public OriginalMapPostEffect3WorldControlledModuleOwner? ModuleLifetime{get;private set;}
    private OriginalMapPostEffect3WorldControlledFactoryFrame(
        OriginalGameRepository repository,
        OriginalMapPostEffect3WorldControlledPreparation.CurrentFrame preparation,
        OriginalMapPostEffect3OwnedControlledCameraFrame camera,
        OriginalMapPostEffect3TextureParameterState textures,
        OriginalMapPostEffect3WorldStockGraphicsSourceOwner world,
        OriginalMapPostEffect3Functor1ModuleState cpuModule,
        OriginalMapPostEffect3Functor1ModuleInvocation cpuInvocation,
        IReadOnlyList<OriginalMapPostEffect3Functor1FactoryCpuReturnedRecipe> factoryReturns,
        OriginalMapPostEffect3SourceSamplerOwner manager,
        OriginalMapPostEffect3ModuleSamplerCarrierState carriers,
        IOriginalMapPostEffect3ComputeExternalTextureLease hdr,
        LoadedOriginalMapPostEffect3InputDepthSource depthSource,
        OriginalMapPostEffect3ParameterLensFlare2Update lens,
        OriginalMapPostEffect3ParameterDepthOfField2Update dof,
        OriginalMapPostEffect3EyeAdaptation2Owner eyeOwner,
        OriginalMapPostEffect3EyeAdaptation2Frame eye,
        OriginalMapPostEffect3EyeCurrentInputs eyeInputs,
        OriginalMapPostEffect3ParameterConstantBuffer exposure,
        OriginalMapPostEffect3Composite2ExecutionFrame composite,
        OriginalMapPostEffect3FreshOwnedAllocationSides sides,
        OriginalMapPostEffect3WorldControlledModuleOwner module,
        OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner frontend,
        OriginalMapPostEffect3RetainedShapeWrapperMetadata? retainedShapeMetadata)
    {
        ModuleLifetime=module;
        this.retainedShapeMetadata=retainedShapeMetadata;
        this.preparation=preparation;CpuModule=cpuModule;CpuInvocation=cpuInvocation;
        FactoryReturns=factoryReturns;Begin=cpuInvocation.Begin;Camera=camera;Textures=textures;World=world;
        Manager=manager;Carriers=carriers;HdrRead=hdr;DepthSource=depthSource;
        Lens=lens;DepthOfField=dof;EyeOwner=eyeOwner;Eye=eye;EyeInputs=eyeInputs;
        Exposure=exposure;Composite=composite;AllocationSides=sides;
        WrapperDirectory=module.StaticSources.Wrappers
            .CapturePreparedDirectory(cpuInvocation,textures,lens,dof,eye,composite);
        var selected=WrapperDirectory.Wrappers.Where(r=>r.ExpectedSuccessfulDeviceIterationCount>0)
            .Select(r=>r.SourcePassIndex).ToHashSet();
        var directory=camera.Allocation.Source.ReadFunctorPasses(1);
        if(selected.Except(directory.Select(p=>p.Index)).Any()||directory.Any(p=>
            BinaryPrimitives.ReadUInt32LittleEndian(p.RawBytes.Span[8..]) is <6 or >9))
            throw new InvalidDataException("Controlled factory roster differs from the original Functor1 directory.");
        OrderedSelectedPasses=Array.AsReadOnly(WrapperDirectory.Wrappers
            .SelectMany(r=>Enumerable.Repeat(r.SourcePassIndex,r.ExpectedSuccessfulDeviceIterationCount)).ToArray());
        Frontend=frontend;
        SceneColor=new(world);
        InitializeSubmissionLedger();
    }

    public OriginalMapPostEffect3OwnedControlledCameraFrame Camera { get; }
    public OriginalMapPostEffect3TextureParameterState Textures { get; }
    public OriginalMapPostEffect3WorldStockGraphicsSourceOwner World { get; }
    public OriginalMapPostEffect3BeginFunctorCounterReceipt Begin { get; }
    public OriginalMapPostEffect3Functor1ModuleState CpuModule { get; }
    public OriginalMapPostEffect3Functor1ModuleInvocation CpuInvocation { get; }
    public IReadOnlyList<OriginalMapPostEffect3Functor1FactoryCpuReturnedRecipe> FactoryReturns { get; }
    public OriginalMapPostEffect3Functor1WrapperDirectory WrapperDirectory { get; }
    public OriginalMapPostEffect3ApplyViewSlotSource ApplyViewSlot=>preparation.ApplyViewSlot;
    public OriginalMapPostEffect3SourceSamplerOwner Manager { get; }
    public OriginalMapPostEffect3ModuleSamplerCarrierState Carriers { get; }
    public OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner Frontend { get; }
    public OriginalMapPostEffect3WorldControlledColorOwner SceneColor { get; }
    public IOriginalMapPostEffect3ComputeExternalTextureLease HdrRead { get; }
    public LoadedOriginalMapPostEffect3InputDepthSource DepthSource { get; }
    public OriginalMapPostEffect3ParameterLensFlare2Update Lens { get; }
    public OriginalMapPostEffect3ParameterDepthOfField2Update DepthOfField { get; }
    public OriginalMapPostEffect3EyeAdaptation2Owner EyeOwner { get; }
    public OriginalMapPostEffect3EyeAdaptation2Frame Eye { get; }
    public OriginalMapPostEffect3EyeCurrentInputs EyeInputs { get; }
    public OriginalMapPostEffect3ParameterConstantBuffer Exposure { get; }
    public OriginalMapPostEffect3Composite2ExecutionFrame Composite { get; }
    public OriginalMapPostEffect3FreshOwnedAllocationSides AllocationSides { get; }
    public IReadOnlyList<int> OrderedSelectedPasses { get; }
    public bool ProvesFunctorGpuCompletion => false;

    public static async Task<OriginalMapPostEffect3WorldControlledFactoryFrame> BuildFreshAsync(
        OriginalGameRepository repository,OriginalMapSourceCreatedWorldPlayableHost host,
        OriginalMapPostEffect3WorldControlledPreparation.CurrentFrame current,
        OriginalMapPostEffect3OwnedControlledCameraFrame camera,
        OriginalMapPostEffect3ResourceBank bank,
        OriginalMapPostEffect3FreshOwnedAllocationSides sides,
        OriginalMapPostEffect3StaticLutTextureOwner actualHdr,bool retainModuleLifetime=false)
    {
        var module=new OriginalMapPostEffect3WorldControlledModuleOwner(repository,bank,retainModuleLifetime,current.Camera.ExecutableLocale);
        try{return await BuildCoreAsync(repository,host,current,camera,bank,sides,actualHdr,module,null);}
        catch{await module.AbortConstructionAsync();throw;}
    }

    internal static Task<OriginalMapPostEffect3WorldControlledFactoryFrame> BuildRetainedAsync(
        OriginalGameRepository repository,OriginalMapSourceCreatedWorldPlayableHost host,
        OriginalMapPostEffect3WorldControlledPreparation.CurrentFrame current,OriginalMapPostEffect3OwnedControlledCameraFrame camera,
        OriginalMapPostEffect3StaticLutTextureOwner actualHdr,OriginalMapPostEffect3WorldControlledModuleOwner module,
        OriginalMapPostEffect3WorldControlledFactoryFrame previous)
        =>BuildCoreAsync(repository,host,current,camera,module.Bank,previous.AllocationSides,actualHdr,module,previous);

    private static async Task<OriginalMapPostEffect3WorldControlledFactoryFrame> BuildCoreAsync(
        OriginalGameRepository repository,OriginalMapSourceCreatedWorldPlayableHost host,
        OriginalMapPostEffect3WorldControlledPreparation.CurrentFrame current,OriginalMapPostEffect3OwnedControlledCameraFrame camera,
        OriginalMapPostEffect3ResourceBank bank,OriginalMapPostEffect3FreshOwnedAllocationSides sides,
        OriginalMapPostEffect3StaticLutTextureOwner actualHdr,OriginalMapPostEffect3WorldControlledModuleOwner module,
        OriginalMapPostEffect3WorldControlledFactoryFrame? previous)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(current);ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(bank);ArgumentNullException.ThrowIfNull(sides);
        ArgumentNullException.ThrowIfNull(actualHdr);
        current.RequireCurrent();camera.ValidateSameCamera(current.Camera);camera.Invocation.RequirePending();
        sides.ValidateFor(bank.Source);
        if(previous is null&&module.CpuModule is not null||previous is not null&&module.CpuModule is null)
            throw new InvalidDataException("Controlled factory construction must match its fresh or retained module lifecycle.");
        if(!ReferenceEquals(current.Allocation,bank.Source)||!ReferenceEquals(camera.Allocation,bank.Source))
            throw new InvalidDataException("Controlled factory Camera/preparation must borrow this exact bank allocation.");
        if(current.ApplyViewSlot.Index!=0)
            throw new NotSupportedException("Controlled factory source profile requires the actual Main View0 slot.");
        if(camera.Route.SelectedModuleFunctorIndex!=1)
            throw new NotSupportedException("Controlled factory source profile requires the selected Functor1 module.");
        if(host.Scene.RenderAttemptNumber!=current.FrameNumber)
            throw new InvalidDataException("Controlled factory preparation does not match the Host's current render attempt.");
        if(!ReferenceEquals(host.Session.CurrentFrame,current.PreparedWorld))
            throw new InvalidDataException("Controlled factory preparation does not borrow this Host's accepted current World.");
        if(current.LdrLookup is not null)
            throw new NotSupportedException("Controlled factory source profile has no admitted current LDR LUT selection.");
        // The uploaded volume is immutable, while selection packets belong
        // to the current Camera/lookup generation. Capture verifies the same
        // uploaded physical asset and its refreshed native metadata first.
        RequireCurrentHdrSelectionMetadata(actualHdr.CaptureSourceSelectedHdrMetadata(),
            current.HdrLookup,current.FrameNumber,actualHdr.OwnerIdentity);
        var sources=module.StaticSources;
        sources.RequireRepository(repository);
        if(sources.Native.Locale!=current.Camera.ExecutableLocale)throw new InvalidDataException("Factory source generation belongs to another executable locale.");
        var stock=sources.Stock;
        if(previous is null)await module.InitializeAsync(camera,sides);
        uint contextWord=module.RetainedContextWord1C48;
        var cpuModule=module.CpuModule!; // Fresh Init succeeded, or the retained lifecycle check found the published module.
        var cpuInvocation=cpuModule.BeginFrame(camera.State,camera.Invocation);
        sides=OriginalMapPostEffect3FreshOwnedAllocationSides.CaptureFunctor1Current(bank.Source,cpuInvocation);
        var table=previous is null?OriginalMapPostEffect3TextureParameterState.CreateFreshWithSourceZeroAllocator(
            bank.Source,current.FrameNumber,sides.TextureInstances,
            sources.TextureZero,sides.SourceEvidence):
            OriginalMapPostEffect3TextureParameterState.CreateRetainedForFunctor1Invocation(bank.Source,cpuInvocation,previous.Textures);
        var scene=current.Context.OutputFrame.Textures[0x064B746B];
        var depth=current.Context.TerrainFrame.Textures[0xE13DDB4F];
        var depthSource=sources.InputDepth;
        depthSource.RequireSelectedOwnedDepthDescriptor(depth.EffectiveSource);
        int colorIndex=camera.State.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,"InputColor")
            ??throw new InvalidDataException("Source InputColor declaration is absent.");
        int depthIndex=camera.State.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,"InputDepth")
            ??throw new InvalidDataException("Source InputDepth declaration is absent.");
        if(colorIndex!=0||depthIndex!=1)
            throw new InvalidDataException("Controlled InputColor/InputDepth source indices changed.");
        table.BindExternal(colorIndex,new(current.FrameNumber,scene.EffectiveSource.OwnerIdentity),
            "TC:EB69F0/D72210;actual same-attempt World Scene;"+stock.SourceEvidence);
        table.BindExternal(depthIndex,new(current.FrameNumber,depth.EffectiveSource.OwnerIdentity),
            "TC:EB6A23/D72210;actual same-attempt E13;"+depthSource.SourceEvidence);
        var controllerTextures=sources.LensController
            .PrepareInternal(camera.State,table,camera.Invocation.Snapshot);
        table.ApplyLensFlare2InternalControllerTextures(controllerTextures);
        var hdr=actualHdr.BindCurrentParameter(table,"InputHdr3dLut",current.HdrLookup.SourceEvidence);
        var world=OriginalMapPostEffect3WorldStockGraphicsSourceOwner.CaptureCurrent(host,current.Context,bank,table);
        world.LeaseWorkingScene(0).RequireCurrent();world.LeaseInputDepth(depthSource,1).RequireCurrent();
        var manager=module.Manager;
        cpuInvocation.AdoptCurrentTextureTable(table);
        var frontend=previous is null?
            OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner.CaptureFreshSource(repository,current,world,sources):
            OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner.CaptureRetainedSource(repository,current,world,module,cpuInvocation);
        try
        {
            var dofSource=sources.DepthOfField;
            var eyeSource=sources.Eye;
            await module.BeginGhostRegistryAsync(camera);
            frontend.BeginManagedModule(cpuInvocation,module.Cache);
            cpuInvocation.RecordFrontendBegin(cpuModule.ExitSource.CaptureFrontendBegin(cpuInvocation));
            var returnSource=sources.FactoryReturn;
            var factoryReturns=new List<OriginalMapPostEffect3Functor1FactoryCpuReturnedRecipe>();
            void Record(OriginalMapPostEffect3Functor1FactoryCpuReturnedRecipe actual)
            {actual.RecordIntoInvocation(cpuInvocation);factoryReturns.Add(actual);}
            var carriers=previous is null?OriginalMapPostEffect3ModuleSamplerCarrierState.CaptureOwnedSource(world,manager):
                OriginalMapPostEffect3ModuleSamplerCarrierState.CaptureRetainedSource(world,manager,previous.Carriers);
            var lensSource=sources.Lens;
            var lens=lensSource.Prepare(camera.State,bank.Source,
                lensSource.CaptureCurrentInputs(camera.State,bank.Source,table,contextWord,[],stock.SourceEvidence));
            Record(returnSource.CaptureLensReturned(cpuInvocation,table,lens));
            var color=world.LeaseWorkingScene(0);
            var extent=new OriginalMapPostEffect3DepthOfField2ExternalExtent(table.Require(0),
                checked((ushort)color.Extent.X),checked((ushort)color.Extent.Y),color.SourceEvidence);
            var dofExtents=new List<OriginalMapPostEffect3DepthOfField2ExternalExtent>();
            OriginalMapPostEffect3RetainedShapeWrapperMetadata? shapeMetadata=null;
            foreach(string name in new[]{"DOF2SpriteDofAccumRear","DOF2InputColor","DOF2PrefilteredColor","DOF2PreComposite","DOF2ShapeInfo"})
            {
                int index=camera.State.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,name,
                    OriginalMapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale)
                    ??throw new InvalidDataException("DOF2 actual source texture declaration absent: "+name);
                if(table.ReadKnownNullablePointer(index) is null)continue;
                var pointer=table.Require(index);
                if(pointer.Reference.PhysicalAllocationParameterIndex is not null)continue;
                if(index==58)
                {
                    shapeMetadata=module.CaptureRetainedShapeWrapperMetadata(cpuInvocation,world);
                    dofExtents.Add(shapeMetadata.Extent);
                }
                else
                {
                    var actual=world.LeaseWorkingScene(index);actual.RequireCurrent();
                    dofExtents.Add(new(pointer,checked((ushort)actual.Extent.X),checked((ushort)actual.Extent.Y),actual.SourceEvidence));
                }
            }
            var dof=dofSource.PrepareAndInitialize(camera.State,bank.Source,
                dofSource.CaptureCurrentInputs(camera.State,bank.Source,table,contextWord,
                    camera.Route.Preparation.Descriptor58,dofExtents,OriginalMapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale,
                    stock.SourceEvidence),cpuModule.DepthOfFieldController);
            table.ApplyDepthOfField2Alias(dof);
            cpuInvocation.AdoptDepthOfField2Initialization(dof);
            Record(returnSource.CaptureDepthOfFieldReturned(cpuInvocation,table,dof));
            if(previous is null)table.ApplyEyeResourceInitializationAlias(eyeSource);
            var eyeOwner=cpuModule.EyeController;
            var eye=eyeOwner.PrepareFrame(camera.State,bank.Source,OriginalNativeLogDispatch.Sse);
            table.ApplyEyeInitializationAlias(eye);
            var eyeExtent=new OriginalMapPostEffect3Composite2ExternalExtent(table.Require(68),extent.Width40,extent.Height42,extent.SourceEvidence);
            var eyeInputs=eyeSource.CaptureCurrentInputs(eye,table,contextWord,[eyeExtent],
                OriginalMapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale,stock.SourceEvidence);
            var exposure=eyeSource.PrepareExposure(eye,sources.DoubleLog,
                OriginalNativeLogDispatch.Sse);
            Record(returnSource.CaptureEyeReturned(cpuInvocation,table,eye,exposure));
            var compositeSource=sources.Composite;
            var compositeExtents=new List<OriginalMapPostEffect3Composite2ExternalExtent>();
            foreach(string name in new[]{"CMP2InputColor","PRECMP2InputBloom"})
            {
                int index=camera.State.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,name,
                    OriginalMapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale)
                    ??throw new InvalidDataException("Composite2 actual source texture declaration is absent: "+name);
                if(table.ReadKnownNullablePointer(index) is null)continue;
                var pointer=table.Require(index);
                if(pointer.Reference.PhysicalAllocationParameterIndex is not null)continue;
                var actual=world.LeaseWorkingScene(index);actual.RequireCurrent();
                compositeExtents.Add(new(pointer,checked((ushort)actual.Extent.X),checked((ushort)actual.Extent.Y),actual.SourceEvidence));
            }
            var compositeUpdate=compositeSource.Prepare(camera.State,bank.Source,
                compositeSource.CaptureCurrentInputs(camera.State,bank.Source,table,contextWord,compositeExtents,
                    OriginalMapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale,stock.SourceEvidence));
            var composite=sources.CompositeExecution.PrepareInitialization(
                camera.State,bank.Source,table,compositeUpdate,carriers.CaptureCurrent(),
                OriginalMapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale);
            table.ApplyComposite2Initialization(composite);
            composite.Prepared.AcceptActualInitialization(composite);
            Record(returnSource.CaptureCompositeReturned(cpuInvocation,table,composite));
            var result=new OriginalMapPostEffect3WorldControlledFactoryFrame(repository,current,camera,table,world,
                cpuModule,cpuInvocation,Array.AsReadOnly(factoryReturns.ToArray()),manager,
                carriers,hdr,depthSource,lens,dof,eyeOwner,eye,eyeInputs,exposure,composite,sides,module,frontend,shapeMetadata);
            result.RequireCurrent();module.Attach(result);return result;
        }
        catch(Exception failure)
        {
            try{cpuInvocation?.MarkUnreturnedFailure("Controlled factory preparation failed: "+failure.GetType().Name+": "+failure.Message);}
            finally
            {
                module.AbortUndeliveredConstruction(current.FrameNumber);
            }
            throw;
        }
    }

    internal static void RequireCurrentHdrSelectionMetadata(OriginalMapPostEffect3ControlledSelectedHdrTexture actualSelection,
        OriginalMapPostEffect3CurrentLutLookup actualCurrentLookup,long actualFrame,string actualPhysicalOwnerIdentity)
    {
        if(!ReferenceEquals(actualSelection.Lookup,actualCurrentLookup))
            throw new InvalidDataException("Controlled factory HDR owner still selects another prepared lookup generation.");
        if(actualSelection.View.FrameNumber!=actualFrame||actualSelection.Lookup.FrameNumber!=actualFrame)
            throw new InvalidDataException("Controlled factory HDR metadata frame does not match the current World.");
        if(actualSelection.View.OwnerIdentity!=actualPhysicalOwnerIdentity||string.IsNullOrWhiteSpace(actualPhysicalOwnerIdentity))
            throw new InvalidDataException("Controlled factory HDR metadata does not name this actual uploaded texture owner.");
    }

    public void RequireCurrent()
    {
        ObjectDisposedException.ThrowIf(disposed,this);preparation.RequireCurrent();
        CpuInvocation.RequireOwnedModule(CpuModule);CpuInvocation.RequireCurrentTextureTable(Textures);
        CpuInvocation.RequireCurrentDepthOfField2Prepared(DepthOfField,Textures);
        if(!FactoryReturns.Select(r=>r.FactoryIndex).SequenceEqual(new[]{6,7,8,9})||
            !CpuInvocation.RecordedCpuFactoryReturns.Select(r=>r.FactoryIndex).SequenceEqual(new[]{6,7,8,9}))
            throw new InvalidDataException("Controlled factories lost their ordered original CPU Prepare/Init returns.");
        Begin.RequireCurrent();
        if(Begin.Counter!=(uint)Camera.FrameNumber||Begin.SelectedFunctorIndex!=1||Begin.ModuleOwnerIdentity!=Camera.Allocation.ContextOwnerIdentity||
            Begin.ModuleSha256!=Camera.Allocation.Source.Sha256)
            throw new InvalidDataException("Controlled factories lost their actual fresh source module Begin publication.");
        Camera.Invocation.RequirePending();preparation.ValidateControlledCamera(Camera);
        World.RequireCurrent();HdrRead.RequireCurrent();Lens.RequireCurrent();DepthOfField.RequireCurrent();
        Eye.RequireCurrent();EyeInputs.RequireCurrent();Composite.RequireCurrent();Carriers.RequireCurrent();
    }

    public OriginalMapPostEffect3DepthOfField2ModuleResources AcquireDepthOfFieldModuleResources(
        OriginalMapPostEffect3ProgramLibrary programs,OriginalMapPostEffect3ResourceBank bank)
    {
        RequireCurrent();
        if(!ReferenceEquals(World.Bank,bank))
            throw new InvalidDataException("Controlled DOF module resources require the actual World bank.");
        if(depthOfFieldResources is null)
            depthOfFieldResources=ModuleLifetime?.AcquireDof(this,programs)??
                OriginalMapPostEffect3DepthOfField2ModuleResources.CreateBorrowed(programs,bank,CpuModule);
        else if(!ReferenceEquals(depthOfFieldResources.Library,programs)||!ReferenceEquals(depthOfFieldResources.Bank,bank))
            throw new InvalidDataException("Controlled DOF module resources cannot replace their physical bank or programs.");
        return depthOfFieldResources;
    }

    public object CapturePreparationEvidence()
    {
        RequireCurrent();string Hash(OriginalMapPostEffect3ParameterConstantBuffer cb)=>
            Convert.ToHexString(SHA256.HashData(cb.Bytes.Span));
        return new{actualWorldFrame=Camera.FrameNumber,sourceFunctor=1,sourceView=preparation.ApplyViewSlot.Index,
            actualBeginCounter=Begin.Counter,
            frontendIngress=Frontend.CaptureSourceIngressEvidence(),
            retainedShapeWrapperMetadata=retainedShapeMetadata?.CaptureEvidence(),
            cpuFactoryReturns=CpuInvocation.RecordedCpuFactoryReturns,
            singleOwnedModuleBegin=true,sourceCpuFrontendBeginOnly=true,
            sourceWrapperDirectoryEntries=WrapperDirectory.Wrappers.Count,
            sourceMaskRejectedEntries=WrapperDirectory.Wrappers.Count(r=>!r.MaskAdmitsBefore),
            wholeWrapperDirectoryReturned=false,
            selectedPasses=OrderedSelectedPasses,lensPasses=Lens.PassSelection.OrderedFactory6Passes,
            dofPasses=DepthOfField.PassSelection.OrderedFactory7Passes,
            eyeIterations=Eye.OrderedIterations,compositePasses=Composite.Prepared.PassSelection.OrderedFactory9Passes,
            lensCb17Sha256=Hash(Lens.ConstantBuffer),dofCb18Sha256=Hash(DepthOfField.ConstantBuffer),
            bloomCb20Sha256=Hash(Eye.BloomMainConstants),exposureCb24Sha256=Hash(Exposure),
            compositeCb22Sha256=Hash(Composite.Prepared.ConstantBuffer),
            shapeSide=DepthOfField.ShapeRequirement.RequestedSide,mainViewKeepsOwnedExposure61=true,
            controlledFactoryPreparationAccepted=true,selectedFunctorGpuCompleted=false,correctFinalAccepted=false,images=0};
    }

    public async ValueTask DisposeAsync()
    {
        if(disposed)return;DirectoryCursor?.RetireMetadataLoansForFrameDisposal();disposed=true;
        if(ModuleLifetime is{} module)
        {
            module.Detach(this);
            if(!module.RetainsAcrossFrames)await module.DisposeAsync();
        }
        else
        {
            try{if(depthOfFieldResources is not null)await depthOfFieldResources.DisposeAsync();}
            finally{try{await Manager.DisposeAsync();}finally{CpuModule.Dispose();}}
        }
    }
}
