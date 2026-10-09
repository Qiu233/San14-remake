using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data;

/// <summary>Fresh, source selected factory6..9 preparation on the actual
/// submitted World. Pointer initialization and shader plans do not make any
/// newly allocated texture readable. GPU execution and completed source exit
/// remain the responsibility of the corresponding execution owners.</summary>
public sealed partial class MapPostEffect3WorldControlledFactoryFrame : IAsyncDisposable
{
    private readonly MapPostEffect3WorldControlledPreparation.CurrentFrame preparation;
    private MapPostEffect3DepthOfField2ModuleResources? depthOfFieldResources;
    private readonly MapPostEffect3RetainedShapeWrapperMetadata? retainedShapeMetadata;
    private bool disposed;
    public MapPostEffect3WorldControlledModuleOwner? ModuleLifetime{get;private set;}
    private MapPostEffect3WorldControlledFactoryFrame(
        GameResourceRepository repository,
        MapPostEffect3WorldControlledPreparation.CurrentFrame preparation,
        MapPostEffect3OwnedControlledCameraFrame camera,
        MapPostEffect3TextureParameterState textures,
        MapPostEffect3WorldStockGraphicsSourceOwner world,
        MapPostEffect3Functor1ModuleState cpuModule,
        MapPostEffect3Functor1ModuleInvocation cpuInvocation,
        IReadOnlyList<MapPostEffect3Functor1FactoryCpuReturnedRecipe> factoryReturns,
        MapPostEffect3SourceSamplerOwner manager,
        MapPostEffect3ModuleSamplerCarrierState carriers,
        IMapPostEffect3ComputeExternalTextureLease hdr,
        LoadedMapPostEffect3InputDepthSource depthSource,
        MapPostEffect3ParameterLensFlare2Update lens,
        MapPostEffect3ParameterDepthOfField2Update dof,
        MapPostEffect3EyeAdaptation2Owner eyeOwner,
        MapPostEffect3EyeAdaptation2Frame eye,
        MapPostEffect3EyeCurrentInputs eyeInputs,
        MapPostEffect3ParameterConstantBuffer exposure,
        MapPostEffect3Composite2ExecutionFrame composite,
        MapPostEffect3FreshOwnedAllocationSides sides,
        MapPostEffect3WorldControlledModuleOwner module,
        MapPostEffect3WorldNativeFullscreenFrontendOwner frontend,
        MapPostEffect3RetainedShapeWrapperMetadata? retainedShapeMetadata)
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

    public MapPostEffect3OwnedControlledCameraFrame Camera { get; }
    public MapPostEffect3TextureParameterState Textures { get; }
    public MapPostEffect3WorldStockGraphicsSourceOwner World { get; }
    public MapPostEffect3BeginFunctorCounterReceipt Begin { get; }
    public MapPostEffect3Functor1ModuleState CpuModule { get; }
    public MapPostEffect3Functor1ModuleInvocation CpuInvocation { get; }
    public IReadOnlyList<MapPostEffect3Functor1FactoryCpuReturnedRecipe> FactoryReturns { get; }
    public MapPostEffect3Functor1WrapperDirectory WrapperDirectory { get; }
    public MapPostEffect3ApplyViewSlotSource ApplyViewSlot=>preparation.ApplyViewSlot;
    public MapPostEffect3SourceSamplerOwner Manager { get; }
    public MapPostEffect3ModuleSamplerCarrierState Carriers { get; }
    public MapPostEffect3WorldNativeFullscreenFrontendOwner Frontend { get; }
    public MapPostEffect3WorldControlledColorOwner SceneColor { get; }
    public IMapPostEffect3ComputeExternalTextureLease HdrRead { get; }
    public LoadedMapPostEffect3InputDepthSource DepthSource { get; }
    public MapPostEffect3ParameterLensFlare2Update Lens { get; }
    public MapPostEffect3ParameterDepthOfField2Update DepthOfField { get; }
    public MapPostEffect3EyeAdaptation2Owner EyeOwner { get; }
    public MapPostEffect3EyeAdaptation2Frame Eye { get; }
    public MapPostEffect3EyeCurrentInputs EyeInputs { get; }
    public MapPostEffect3ParameterConstantBuffer Exposure { get; }
    public MapPostEffect3Composite2ExecutionFrame Composite { get; }
    public MapPostEffect3FreshOwnedAllocationSides AllocationSides { get; }
    public IReadOnlyList<int> OrderedSelectedPasses { get; }
    public bool ProvesFunctorGpuCompletion => false;

    public static async Task<MapPostEffect3WorldControlledFactoryFrame> BuildFreshAsync(
        GameResourceRepository repository,MapSourceCreatedWorldPlayableHost host,
        MapPostEffect3WorldControlledPreparation.CurrentFrame current,
        MapPostEffect3OwnedControlledCameraFrame camera,
        PostProcessResourceBank bank,
        MapPostEffect3FreshOwnedAllocationSides sides,
        MapPostEffect3StaticLutTextureOwner actualHdr,bool retainModuleLifetime=false)
    {
        var module=new MapPostEffect3WorldControlledModuleOwner(repository,bank,retainModuleLifetime,current.Camera.ExecutableLocale);
        try{return await BuildCoreAsync(repository,host,current,camera,bank,sides,actualHdr,module,null);}
        catch{await module.AbortConstructionAsync();throw;}
    }

    internal static Task<MapPostEffect3WorldControlledFactoryFrame> BuildRetainedAsync(
        GameResourceRepository repository,MapSourceCreatedWorldPlayableHost host,
        MapPostEffect3WorldControlledPreparation.CurrentFrame current,MapPostEffect3OwnedControlledCameraFrame camera,
        MapPostEffect3StaticLutTextureOwner actualHdr,MapPostEffect3WorldControlledModuleOwner module,
        MapPostEffect3WorldControlledFactoryFrame previous)
        =>BuildCoreAsync(repository,host,current,camera,module.Bank,previous.AllocationSides,actualHdr,module,previous);

    private static async Task<MapPostEffect3WorldControlledFactoryFrame> BuildCoreAsync(
        GameResourceRepository repository,MapSourceCreatedWorldPlayableHost host,
        MapPostEffect3WorldControlledPreparation.CurrentFrame current,MapPostEffect3OwnedControlledCameraFrame camera,
        PostProcessResourceBank bank,MapPostEffect3FreshOwnedAllocationSides sides,
        MapPostEffect3StaticLutTextureOwner actualHdr,MapPostEffect3WorldControlledModuleOwner module,
        MapPostEffect3WorldControlledFactoryFrame? previous)
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
        sides=MapPostEffect3FreshOwnedAllocationSides.CaptureFunctor1Current(bank.Source,cpuInvocation);
        var table=previous is null?MapPostEffect3TextureParameterState.CreateFreshWithSourceZeroAllocator(
            bank.Source,current.FrameNumber,sides.TextureInstances,
            sources.TextureZero,sides.SourceEvidence):
            MapPostEffect3TextureParameterState.CreateRetainedForFunctor1Invocation(bank.Source,cpuInvocation,previous.Textures);
        var scene=current.Context.OutputFrame.Textures[0x064B746B];
        var depth=current.Context.TerrainFrame.Textures[0xE13DDB4F];
        var depthSource=sources.InputDepth;
        depthSource.RequireSelectedOwnedDepthDescriptor(depth.EffectiveSource);
        int colorIndex=camera.State.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,"InputColor")
            ??throw new InvalidDataException("Source InputColor declaration is absent.");
        int depthIndex=camera.State.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,"InputDepth")
            ??throw new InvalidDataException("Source InputDepth declaration is absent.");
        if(colorIndex!=0||depthIndex!=1)
            throw new InvalidDataException("Controlled InputColor/InputDepth source indices changed.");
        table.BindExternal(colorIndex,new(current.FrameNumber,scene.EffectiveSource.OwnerIdentity),
            "actual same-attempt World Scene;"+stock.SourceEvidence);
        table.BindExternal(depthIndex,new(current.FrameNumber,depth.EffectiveSource.OwnerIdentity),
            "actual same-attempt depth;"+depthSource.SourceEvidence);
        var controllerTextures=sources.LensController
            .PrepareInternal(camera.State,table,camera.Invocation.Snapshot);
        table.ApplyLensFlare2InternalControllerTextures(controllerTextures);
        var hdr=actualHdr.BindCurrentParameter(table,"InputHdr3dLut",current.HdrLookup.SourceEvidence);
        var world=MapPostEffect3WorldStockGraphicsSourceOwner.CaptureCurrent(host,current.Context,bank,table);
        world.LeaseWorkingScene(0).RequireCurrent();world.LeaseInputDepth(depthSource,1).RequireCurrent();
        var manager=module.Manager;
        cpuInvocation.AdoptCurrentTextureTable(table);
        var frontend=previous is null?
            MapPostEffect3WorldNativeFullscreenFrontendOwner.CaptureFreshSource(repository,current,world,sources):
            MapPostEffect3WorldNativeFullscreenFrontendOwner.CaptureRetainedSource(repository,current,world,module,cpuInvocation);
        try
        {
            var dofSource=sources.DepthOfField;
            var eyeSource=sources.Eye;
            await module.BeginGhostRegistryAsync(camera);
            frontend.BeginManagedModule(cpuInvocation,module.Cache);
            cpuInvocation.RecordFrontendBegin(cpuModule.ExitSource.CaptureFrontendBegin(cpuInvocation));
            var returnSource=sources.FactoryReturn;
            var factoryReturns=new List<MapPostEffect3Functor1FactoryCpuReturnedRecipe>();
            void Record(MapPostEffect3Functor1FactoryCpuReturnedRecipe actual)
            {actual.RecordIntoInvocation(cpuInvocation);factoryReturns.Add(actual);}
            var carriers=previous is null?MapPostEffect3ModuleSamplerCarrierState.CaptureOwnedSource(world,manager):
                MapPostEffect3ModuleSamplerCarrierState.CaptureRetainedSource(world,manager,previous.Carriers);
            var lensSource=sources.Lens;
            var lens=lensSource.Prepare(camera.State,bank.Source,
                lensSource.CaptureCurrentInputs(camera.State,bank.Source,table,contextWord,[],stock.SourceEvidence));
            Record(returnSource.CaptureLensReturned(cpuInvocation,table,lens));
            var color=world.LeaseWorkingScene(0);
            var extent=new MapPostEffect3DepthOfField2ExternalExtent(table.Require(0),
                checked((ushort)color.Extent.X),checked((ushort)color.Extent.Y),color.SourceEvidence);
            var dofExtents=new List<MapPostEffect3DepthOfField2ExternalExtent>();
            MapPostEffect3RetainedShapeWrapperMetadata? shapeMetadata=null;
            foreach(string name in new[]{"DOF2SpriteDofAccumRear","DOF2InputColor","DOF2PrefilteredColor","DOF2PreComposite","DOF2ShapeInfo"})
            {
                int index=camera.State.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,name,
                    MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale)
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
                    camera.Route.Preparation.Descriptor58,dofExtents,MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale,
                    stock.SourceEvidence),cpuModule.DepthOfFieldController);
            table.ApplyDepthOfField2Alias(dof);
            cpuInvocation.AdoptDepthOfField2Initialization(dof);
            Record(returnSource.CaptureDepthOfFieldReturned(cpuInvocation,table,dof));
            if(previous is null)table.ApplyEyeResourceInitializationAlias(eyeSource);
            var eyeOwner=cpuModule.EyeController;
            var eye=eyeOwner.PrepareFrame(camera.State,bank.Source,NativeLogDispatch.Sse);
            table.ApplyEyeInitializationAlias(eye);
            var eyeExtent=new MapPostEffect3Composite2ExternalExtent(table.Require(68),extent.Width40,extent.Height42,extent.SourceEvidence);
            var eyeInputs=eyeSource.CaptureCurrentInputs(eye,table,contextWord,[eyeExtent],
                MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale,stock.SourceEvidence);
            var exposure=eyeSource.PrepareExposure(eye,sources.DoubleLog,
                NativeLogDispatch.Sse);
            Record(returnSource.CaptureEyeReturned(cpuInvocation,table,eye,exposure));
            var compositeSource=sources.Composite;
            var compositeExtents=new List<MapPostEffect3Composite2ExternalExtent>();
            foreach(string name in new[]{"CMP2InputColor","PRECMP2InputBloom"})
            {
                int index=camera.State.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,name,
                    MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale)
                    ??throw new InvalidDataException("Composite2 actual source texture declaration is absent: "+name);
                if(table.ReadKnownNullablePointer(index) is null)continue;
                var pointer=table.Require(index);
                if(pointer.Reference.PhysicalAllocationParameterIndex is not null)continue;
                var actual=world.LeaseWorkingScene(index);actual.RequireCurrent();
                compositeExtents.Add(new(pointer,checked((ushort)actual.Extent.X),checked((ushort)actual.Extent.Y),actual.SourceEvidence));
            }
            var compositeUpdate=compositeSource.Prepare(camera.State,bank.Source,
                compositeSource.CaptureCurrentInputs(camera.State,bank.Source,table,contextWord,compositeExtents,
                    MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale,stock.SourceEvidence));
            var composite=sources.CompositeExecution.PrepareInitialization(
                camera.State,bank.Source,table,compositeUpdate,carriers.CaptureCurrent(),
                MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale);
            table.ApplyComposite2Initialization(composite);
            composite.Prepared.AcceptActualInitialization(composite);
            Record(returnSource.CaptureCompositeReturned(cpuInvocation,table,composite));
            var result=new MapPostEffect3WorldControlledFactoryFrame(repository,current,camera,table,world,
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

    internal static void RequireCurrentHdrSelectionMetadata(MapPostEffect3ControlledSelectedHdrTexture actualSelection,
        MapPostEffect3CurrentLutLookup actualCurrentLookup,long actualFrame,string actualPhysicalOwnerIdentity)
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

    public MapPostEffect3DepthOfField2ModuleResources AcquireDepthOfFieldModuleResources(
        MapPostEffect3ProgramLibrary programs,PostProcessResourceBank bank)
    {
        RequireCurrent();
        if(!ReferenceEquals(World.Bank,bank))
            throw new InvalidDataException("Controlled DOF module resources require the actual World bank.");
        if(depthOfFieldResources is null)
            depthOfFieldResources=ModuleLifetime?.AcquireDof(this,programs)??
                MapPostEffect3DepthOfField2ModuleResources.CreateBorrowed(programs,bank,CpuModule);
        else if(!ReferenceEquals(depthOfFieldResources.Library,programs)||!ReferenceEquals(depthOfFieldResources.Bank,bank))
            throw new InvalidDataException("Controlled DOF module resources cannot replace their physical bank or programs.");
        return depthOfFieldResources;
    }

    public object CapturePreparationEvidence()
    {
        RequireCurrent();string Hash(MapPostEffect3ParameterConstantBuffer cb)=>
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
