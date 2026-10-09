using Godot;
using San14.Data;

/// <summary>Factory9 on the actual controlled Main World, following actual
/// Eye bloom and compute submissions. The target cache, module carrier words
/// and source frontend state are owned here; textures borrow their real
/// Scene/bank/Static producers. Completing this fragment makes no claim about
/// absent preceding Lens/DOF factories or a complete Functor invocation.</summary>
public sealed class MapPostEffect3WorldComposite2GraphicsInvocation
    : IMapPostEffect3Composite2GraphicsInvocationLease,IMapPostEffect3Composite2FreshExposureInvocationLease,
      IMapPostEffect3Composite2RetainedExposureInvocationLease
{
    public MapPostEffect3WorldNativeFullscreenFrontendOwner Frontend { get; }
    public MapPostEffect3Composite2ExecutionFrame Frame { get; }
    public IMapPostEffect3StockGraphicsTargetLease ActualFinalCallerTarget { get; }
    private readonly MapPostEffect3ModuleSamplerCarrierState carriers;
    private readonly MapPostEffect3WorldControlledColorOwner scene;
    private readonly MapPostEffect3EyeBloomExecutionOwner eye;
    private readonly MapPostEffect3EyeAdaptation2Frame eyeFrame;
    private readonly MapPostEffect3ComputeExecutor compute;
    private readonly IReadOnlyList<MapPostEffect3ComputeCompletedPass> computeReceipts;
    private readonly MapPostEffect3WorldControlledFactoryFrame? directoryFrame;
    private readonly LoadedMapPostEffect3EyeExposureInitializationSource? exposureInitialization;
    private readonly MapPostEffect3FullscreenCompletedPass[] graphicsReceipts;
    private readonly IReadOnlyDictionary<int,IMapPostEffect3ComputeExternalTextureLease> externalReads;
    private readonly MapPostEffect3StaticLutTextureOwner hdr;
    private readonly MapPostEffect3NativeFullscreenTargetCache? cache;
    private MapPostEffect3Composite2BeforeBindings? bound;
    private int completedCount;
    private readonly MapPostEffect3WorldCompositeSourceOwner.ParentBinding parent;

    private MapPostEffect3WorldComposite2GraphicsInvocation(
        MapPostEffect3WorldNativeFullscreenFrontendOwner frontend,
        MapPostEffect3Composite2ExecutionFrame frame,MapPostEffect3ModuleSamplerCarrierState carriers,
        MapPostEffect3WorldControlledColorOwner scene,MapPostEffect3StaticLutTextureOwner hdr,
        MapPostEffect3EyeBloomExecutionOwner eye,MapPostEffect3EyeAdaptation2Frame eyeFrame,
        MapPostEffect3ComputeExecutor compute,IReadOnlyList<MapPostEffect3ComputeCompletedPass> computeReceipts,
        MapPostEffect3FullscreenCompletedPass[] graphicsReceipts,
        IReadOnlyDictionary<int,IMapPostEffect3ComputeExternalTextureLease> reads,
        MapPostEffect3WorldControlledFactoryFrame? directoryFrame=null,
        LoadedMapPostEffect3EyeExposureInitializationSource? exposureInitialization=null)
    {
        (Frontend,Frame,this.carriers,this.scene,this.hdr,this.eye,this.eyeFrame,this.compute,this.computeReceipts,
            this.graphicsReceipts,externalReads)=(frontend,frame,carriers,scene,hdr,eye,eyeFrame,compute,computeReceipts,graphicsReceipts,reads);
        parent=frontend.WorldPreparation.ActualFrontendParent;
        this.directoryFrame=directoryFrame;this.exposureInitialization=exposureInitialization;
        ActualFinalCallerTarget=parent.Owner.LeaseControlledFinalTarget(parent,frontend.World,frame);
        cache=frontend.UsesActualDirectoryCursor?null:
            frontend.Source.ConstructAfterActualCompute104(frame.Allocation,frame.Prepared.PassSelection.OrderedFactory9Passes.First());
    }
    public static MapPostEffect3WorldComposite2GraphicsInvocation CaptureActual(
        MapPostEffect3WorldNativeFullscreenFrontendOwner actualFrontend,
        MapPostEffect3Composite2ExecutionFrame actualPreparedFrame,
        MapPostEffect3ModuleSamplerCarrierState actualCarriers,
        MapPostEffect3WorldControlledColorOwner actualScene,
        MapPostEffect3StaticLutTextureOwner actualSelectedHdr,
        IMapPostEffect3ComputeExternalTextureLease actualBoundHdr81,
        MapPostEffect3EyeBloomExecutionOwner actualEye,
        MapPostEffect3EyeAdaptation2Frame actualEyeFrame,
        MapPostEffect3ComputeExecutor actualEyeCompute,
        IReadOnlyList<MapPostEffect3ComputeCompletedPass> actualOrderedCompute102To104,
        IReadOnlyList<IMapPostEffect3ComputeExternalTextureLease>? actualOtherExternalReads=null)
        =>CaptureCore(actualFrontend,actualPreparedFrame,actualCarriers,actualScene,actualSelectedHdr,actualBoundHdr81,
            actualEye,actualEyeFrame,actualEyeCompute,actualOrderedCompute102To104.ToArray(),actualOtherExternalReads);
    public static MapPostEffect3WorldComposite2GraphicsInvocation CaptureActualDirectoryPrefix(
        MapPostEffect3WorldControlledFactoryFrame actualFrame,MapPostEffect3StaticLutTextureOwner actualHdr,
        MapPostEffect3EyeBloomExecutionOwner actualEye,MapPostEffect3ComputeExecutor actualCompute,
        IReadOnlyList<MapPostEffect3ComputeCompletedPass> actualOrderedComputePrefix,
        LoadedMapPostEffect3EyeExposureInitializationSource initialization)
    {
        actualFrame.RequireCurrent();
        if(!actualFrame.Frontend.UsesActualDirectoryCursor||actualFrame.DirectoryCursor is null)
            throw new InvalidDataException("Interleaved Composite needs this actual live source72 directory cursor.");
        return CaptureCore(actualFrame.Frontend,actualFrame.Composite,actualFrame.Carriers,actualFrame.SceneColor,actualHdr,
            actualFrame.HdrRead,actualEye,actualFrame.Eye,actualCompute,actualOrderedComputePrefix,null,actualFrame,initialization);
    }
    private static MapPostEffect3WorldComposite2GraphicsInvocation CaptureCore(
        MapPostEffect3WorldNativeFullscreenFrontendOwner actualFrontend,MapPostEffect3Composite2ExecutionFrame actualPreparedFrame,
        MapPostEffect3ModuleSamplerCarrierState actualCarriers,MapPostEffect3WorldControlledColorOwner actualScene,
        MapPostEffect3StaticLutTextureOwner actualSelectedHdr,IMapPostEffect3ComputeExternalTextureLease actualBoundHdr81,
        MapPostEffect3EyeBloomExecutionOwner actualEye,MapPostEffect3EyeAdaptation2Frame actualEyeFrame,
        MapPostEffect3ComputeExecutor actualEyeCompute,IReadOnlyList<MapPostEffect3ComputeCompletedPass> actualOrderedCompute102To104,
        IReadOnlyList<IMapPostEffect3ComputeExternalTextureLease>? actualOtherExternalReads,
        MapPostEffect3WorldControlledFactoryFrame? directoryFrame=null,
        LoadedMapPostEffect3EyeExposureInitializationSource? initialization=null)
    {
        ArgumentNullException.ThrowIfNull(actualFrontend);ArgumentNullException.ThrowIfNull(actualPreparedFrame);
        ArgumentNullException.ThrowIfNull(actualCarriers);ArgumentNullException.ThrowIfNull(actualScene);
        ArgumentNullException.ThrowIfNull(actualSelectedHdr);ArgumentNullException.ThrowIfNull(actualBoundHdr81);
        ArgumentNullException.ThrowIfNull(actualEye);ArgumentNullException.ThrowIfNull(actualEyeFrame);
        ArgumentNullException.ThrowIfNull(actualEyeCompute);ArgumentNullException.ThrowIfNull(actualOrderedCompute102To104);
        actualFrontend.RequireCurrent();
        if(actualPreparedFrame.HasAppliedInitialization)
        {
            _=actualPreparedFrame.ValidateInitializationOutput79(actualFrontend.World.TextureParameters);
            actualPreparedFrame.Prepared.RequireCurrent();
        }
        else actualPreparedFrame.ValidateInitializationFor(actualFrontend.World.TextureParameters);
        actualBoundHdr81.RequireCurrent();actualSelectedHdr.RequireCurrent();actualCarriers.RequireCurrent();
        var hdrSelection=actualSelectedHdr.CaptureSourceSelectedHdrMetadata();
        if(!ReferenceEquals(hdrSelection.Lookup,actualFrontend.WorldPreparation.HdrLookup)||
            actualBoundHdr81.ParameterIndex!=81||actualBoundHdr81.ProducerOwnerIdentity!=actualSelectedHdr.OwnerIdentity||
            actualBoundHdr81.StorageRid!=actualSelectedHdr.TextureRid||actualBoundHdr81.ShaderViewRid!=actualSelectedHdr.TextureRid||
            actualBoundHdr81.Extent.Z!=actualSelectedHdr.VolumeDepth)
            throw new InvalidDataException("Composite2 HDR81 requires the actual current Main-selected Static upload and source setter.");
        var reads=new Dictionary<int,IMapPostEffect3ComputeExternalTextureLease>{{81,actualBoundHdr81}};
        foreach(var read in actualOtherExternalReads??[])
        {
            read.RequireCurrent();
            if(read.ParameterIndex is 68 or 79 or 81||!reads.TryAdd(read.ParameterIndex,read))
                throw new InvalidDataException("Composite2 external capability duplicates an actual Scene or selected HDR producer.");
        }
        //A failed actual LDR lookup is a known-null3D pointer, independently
        //of the required manager point carrier38. Do not substitute a volume.
        if(actualFrontend.WorldPreparation.LdrLookup is null&&
            (reads.ContainsKey(83)||actualFrontend.World.TextureParameters.ReadKnownNullablePointer(83) is not null))
            throw new InvalidDataException("The actual missing Main LDR83 lookup must retain its known-null native pointer.");
        var result=new MapPostEffect3WorldComposite2GraphicsInvocation(actualFrontend,actualPreparedFrame,
            actualCarriers,actualScene,actualSelectedHdr,actualEye,actualEyeFrame,actualEyeCompute,
            actualOrderedCompute102To104,actualEye.CompletedDraws.ToArray(),
            new System.Collections.ObjectModel.ReadOnlyDictionary<int,IMapPostEffect3ComputeExternalTextureLease>(reads),directoryFrame,initialization);
        result.RequireCurrent(actualPreparedFrame);return result;
    }
    public long FrameNumber=>Frame.FrameNumber;
    public string ContextOwnerIdentity=>Frame.OwnerIdentity;
    public string SourceSha256=>Frame.SourceSha256;
    public string SourceEvidence=>Frontend.SourceEvidence+";"+Frame.SourceEvidence+";"+carriers.CaptureCurrent().SourceEvidence+
        ";actual-Eye-antecedents-before-each-factory9-source-index;source72-can-interleave-100/101-before102;source-Init-aliases-preserved;no-complete-Functor-claim";
    public void RequireCurrent(MapPostEffect3Composite2ExecutionFrame actualFrame)
    {
        Frontend.RequireCurrent();Frame.RequireCurrent();eyeFrame.RequireCurrent();carriers.RequireCurrent();hdr.RequireCurrent();parent.RequireCurrent();
        if(Frame.HasAppliedInitialization)
        {
            _=Frame.ValidateInitializationOutput79(Frontend.World.TextureParameters);
            Frame.Prepared.RequireCurrent();
        }
        var world=Frontend.World;
        if(!ReferenceEquals(actualFrame,Frame)||!ReferenceEquals(Frame.Allocation,world.Bank.Source)||
            !ReferenceEquals(eyeFrame.Allocation,world.Bank.Source)||!ReferenceEquals(eye.Bank,world.Bank)||
            !ReferenceEquals(compute.Bank,world.Bank)||FrameNumber!=world.Context.FrameNumber||
            FrameNumber!=eyeFrame.FrameNumber||ContextOwnerIdentity!=world.Bank.Source.ContextOwnerIdentity||
            SourceSha256!=world.Bank.Source.Source.Sha256||Frame.RequiresCrossFilterConstants||Frame.RequiresHdrRemakeConstants)
            throw new InvalidDataException("Composite2 actual World/current factory frame or supported branch changed.");
        var captured=carriers.CaptureCurrent();
        if(!ReferenceEquals(captured.ActualLinearWrapper30,Frame.InitialModuleSamplerWrappers.ActualLinearWrapper30)||
            !ReferenceEquals(captured.ActualPointWrapper38,Frame.InitialModuleSamplerWrappers.ActualPointWrapper38))
            throw new InvalidDataException("Composite2 Init captured another actual module sampler carrier owner.");
        var graphics=eye.CompletedDraws;
        var frontendCompute=Frontend.CompletedEyeCompute;
        var expectedGraphics=eyeFrame.OrderedIterations.Where(i=>!i.IsCompute).Select(i=>i.SourcePassIndex);
        var expectedCompute=eyeFrame.OrderedIterations.Where(i=>i.IsCompute).Select(i=>i.SourcePassIndex).ToArray();
        if(!graphicsReceipts.Select(d=>d.SourcePassIndex).SequenceEqual(expectedGraphics)||
            !expectedCompute.SequenceEqual(new[]{102,103,104})||computeReceipts.Count>expectedCompute.Length||
            !computeReceipts.Select(d=>d.SourcePassIndex).SequenceEqual(expectedCompute.Take(computeReceipts.Count))||
            directoryFrame is null&&computeReceipts.Count!=expectedCompute.Length||
            graphics.Count!=graphicsReceipts.Length||!graphics.Zip(graphicsReceipts).All(p=>ReferenceEquals(p.First,p.Second))||
            frontendCompute.Count!=computeReceipts.Count||!frontendCompute.Zip(computeReceipts).All(p=>ReferenceEquals(p.First,p.Second))||
            !ReferenceEquals(eye.LastCompleted,graphicsReceipts.LastOrDefault())||
            !ReferenceEquals(compute.LastCompletedPass,computeReceipts.LastOrDefault()))
            throw new InvalidDataException("Actual Eye selected graphics or sealed ordered compute prefix changed before Composite2.");
        foreach(var draw in graphicsReceipts)
            if(draw.FrameNumber!=FrameNumber||draw.ContextOwnerIdentity!=ContextOwnerIdentity||!draw.WasPixelDrawSubmitted||draw.Writes.Count!=1)
                throw new InvalidDataException("Eye graphics antecedent lacks its actual current target submission.");
        foreach(var dispatched in computeReceipts)
        {
            var selected=world.Bank.Source.Source.Passes[dispatched.SourcePassIndex].ProgramReferences.Single(r=>r.StageCode==5);
            var program=world.Bank.Source.Source.GetProgram(5,selected.StageIndex!.Value);
            if(dispatched.FrameNumber!=FrameNumber||dispatched.ContextOwnerIdentity!=ContextOwnerIdentity||
                dispatched.ProgramSha256!=program.Sha256||!dispatched.OrderedEffectiveBindings.Any())
                throw new InvalidDataException("Eye compute antecedent lacks the actual source program and selected resource bindings.");
        }
        foreach(var read in externalReads.Values)read.RequireCurrent();
    }
    public MapPostEffect3Composite2ModuleLuts CaptureCurrentModuleLuts()
    {RequireCurrent(Frame);return carriers.CaptureCurrent();}
    public Task<MapPostEffect3Composite2BoundGraphics> ApplyBeforeAndBindGraphicsAsync(
        MapPostEffect3Composite2BeforeBindings before)
    {
        RequireCurrent(Frame);before.RequireCurrent();
        var selected=Frame.Prepared.PassSelection.OrderedFactory9Passes;
        if(bound is not null||!ReferenceEquals(before.Frame,Frame)||completedCount>=selected.Count||
            before.SourcePassIndex!=selected[completedCount])
            throw new InvalidDataException("Composite2 actual Select/Before/Bind order differs from its original current selection.");
        var antecedents=eyeFrame.OrderedIterations.Where(i=>i.SourcePassIndex<before.SourcePassIndex);
        if(directoryFrame is not null&&(!graphicsReceipts.Select(d=>d.SourcePassIndex).SequenceEqual(antecedents.Where(i=>!i.IsCompute).Select(i=>i.SourcePassIndex))||
            !computeReceipts.Select(d=>d.SourcePassIndex).SequenceEqual(antecedents.Where(i=>i.IsCompute).Select(i=>i.SourcePassIndex))))
            throw new InvalidDataException("Composite2 needs exactly the real Eye submissions preceding this source directory index.");
        Frontend.BeginCompositeSourceBefore(before);carriers.ApplyBefore(before);
        var rows=cache is not null?cache.SelectAndApplyBefore(before,Frontend.World.TextureParameters):Frontend.ApplyCompositeSourceBefore(before);
        if(rows.Skip(1).Any(r=>r is not null))
            throw new NotSupportedException("Actual nonempty Composite2 target1..3 needs its separate original MRT adapter.");
        var context=Frontend.BeginCompositeBinding(before);
        IMapPostEffect3StockGraphicsTargetLease?[] targets=new IMapPostEffect3StockGraphicsTargetLease?[4];
        if(rows[0] is{} rt)
        {
            if(Frontend.World.TextureParameters.Require(rt.ParameterIndex).Reference.PhysicalAllocationParameterIndex is not null)
                targets[0]=Frontend.World.Bank.CaptureComposite2TargetLease(Frontend.World.TextureParameters,before,context,rt.ParameterIndex);
            else if(rt.ParameterIndex==68)
                targets[0]=scene.CaptureActualAddBloomTarget(before,context);
            else throw new NotSupportedException("Composite2 selected external target lacks its exact current native writer capability.");
        }
        context.BindActualTarget(targets[0]??ActualFinalCallerTarget);bound=before;
        Frontend.AcceptCompositeSourceBind(before,context);
        return Task.FromResult(new MapPostEffect3Composite2BoundGraphics(before,carriers.CaptureCurrent(),
            Array.AsReadOnly(targets),ActualFinalCallerTarget,context.NativeState,context,
            SourceEvidence+";"+(cache?.SourceEvidence??"actual-global292-slot-cache")+";actual-Select/Before/Bind;full-four-slot-cache;actual-inherited-parent"));
    }
    public IReadOnlyList<IMapPostEffect3ComputeSamplerLease> CaptureCurrentSamplerLeases(
        MapPostEffect3Composite2BeforeBindings before,IReadOnlyList<MapPostEffect3Composite2SamplerRegister> registers)
    {
        RequireBound(before);
        if(registers.Any(r=>r.NativeStageOrdinal!=2||r.SourceRegister is not(7 or 8)))
            throw new NotSupportedException("Composite2 selected shader needs an additional actual counted-wrapper sampler producer.");
        return MapPostEffect3Composite2GraphicsOwner.LeaseSourceModuleCarrierSamplers(carriers.Manager,before,registers);
    }
    public Task<IMapPostEffect3ComputeTextureLease> CaptureCurrentExternalTextureReadAsync(
        MapPostEffect3Composite2BeforeBindings before,MapPostEffect3Binding nativeBinding)
    {
        RequireBound(before);
        if(!Frontend.World.Bank.Source.Passes[before.SourcePassIndex].ActiveBindings.Contains(nativeBinding)||
            nativeBinding.Table!=MapPostEffect3BindingTable.TextureParameters||
            nativeBinding.Role!=MapPostEffect3BindingRole.ShaderResourceRead||nativeBinding.NativeViewKind!=1)
            throw new InvalidDataException("External texture lease is not the actual counted native read setter.");
        IMapPostEffect3ComputeTextureLease read=nativeBinding.ParameterIndex is 68 or 79?
            scene.CaptureCurrentRead(nativeBinding.ParameterIndex):
            externalReads.TryGetValue(nativeBinding.ParameterIndex,out var supplied)?supplied:
            throw new NotSupportedException("Current external texture has no actual producer for source slot"+nativeBinding.ParameterIndex+".");
        read.RequireCurrent();return Task.FromResult(read);
    }
    public Task AcceptCompletedGraphicsAsync(MapPostEffect3Composite2GraphicsCoverageReceipt coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);RequireBound(coverage.Recipe.Before);coverage.RequireCurrentSubmission();
        if(!ReferenceEquals(coverage.Owner.Frame,Frame))throw new InvalidDataException("Composite2 coverage belongs to another actual source owner.");
        var history=Frontend.PrepareCompositeHistory(coverage);
        if(coverage.ActualTarget.ParameterIndex==68&&coverage.Completed.Writes.Single().PhysicalAllocationParameterIndex is null)
            scene.AcceptAddBloomCoverage(coverage);
        else if(coverage.ActualTarget.ParameterIndex is null)parent.Owner.AcceptControlledFinalCoverage(parent,coverage);
        Frontend.ReleaseCompositeCoverage(coverage);cache?.ReleaseAndResetAfterCompleted(coverage.Recipe.Before);
        bound=null;completedCount++;Frontend.AcceptCompositeHistoryAfterActualRelease(history);return Task.CompletedTask;
    }
    private void RequireBound(MapPostEffect3Composite2BeforeBindings before)
    {
        RequireCurrent(Frame);before.RequireCurrent();
        if(!ReferenceEquals(before,bound))throw new InvalidDataException("Composite2 resource capability refers to another current native Bind.");
    }
    /// <summary>The actual parent owner pins its image throughout execution.
    /// Call this boundary before using the numeric readback gate.</summary>
    public Task<MapPostEffect3Composite2CompletedGraphics> ExecuteControlledAsync(
        MapPostEffect3Composite2GraphicsOwner actualOwner)
    {
        RequireCurrent(Frame);
        if(!ReferenceEquals(actualOwner.Frame,Frame))throw new InvalidDataException("Controlled execution owner belongs to another actual factory frame.");
        return parent.Owner.ExecuteControlledCompositeAsync(parent,actualOwner);
    }
    public Task<MapPostEffect3FullscreenCompletedPass> ExecuteControlledPassAsync(
        MapPostEffect3Composite2GraphicsOwner actualOwner,int pass)
    {
        RequireCurrent(Frame);
        if(directoryFrame is null||!ReferenceEquals(actualOwner.Frame,Frame))
            throw new InvalidDataException("Stepped Composite needs its actual same directory-owned source frame.");
        return parent.Owner.ExecuteControlledCompositePassAsync(parent,actualOwner,pass);
    }
    public bool RequiresFreshUndefinedExposure(MapPostEffect3Composite2BeforeBindings before)
    {RequireBound(before);return directoryFrame is not null&&directoryFrame.Begin.Counter==1&&computeReceipts.Count==0&&before.SourcePassIndex is 100 or 101;}
    public bool RequiresRetainedExposure(MapPostEffect3Composite2BeforeBindings before)
    {RequireBound(before);return directoryFrame is not null&&directoryFrame.Begin.Counter>1&&computeReceipts.Count==0&&before.SourcePassIndex is 100 or 101;}
    public IMapPostEffect3ComputeMipTextureLease CaptureRetainedExposure(
        MapPostEffect3Composite2BeforeBindings before,MapPostEffect3Composite2ReadFootprint proof)
    {
        RequireBound(before);
        if(!RequiresRetainedExposure(before)||directoryFrame is null)throw new InvalidDataException("Actual preceding104 AddBloom source absent.");
        return directoryFrame.ModuleLifetime!.CapturePriorExposure(directoryFrame,before.SourcePassIndex,proof,
            proof.ShaderReads.Single(r=>r.NativeBinding.ParameterIndex==61));
    }
    public IMapPostEffect3ComputeTextureLease CaptureFreshUndefinedExposure(
        MapPostEffect3Composite2BeforeBindings before,MapPostEffect3Composite2ReadFootprint proof)
    {
        RequireBound(before);
        if(!RequiresFreshUndefinedExposure(before)||directoryFrame is null||exposureInitialization is null)
            throw new InvalidDataException("Cold AddBloom exposure needs its actual first source directory invocation.");
        var source=MapPostEffect3Composite2AddBloomExposureSource.Prepare(exposureInitialization,before,proof,
            Frontend.World.TextureParameters,directoryFrame.Begin,directoryFrame.ApplyViewSlot);
        return Frontend.World.Bank.CaptureComposite2FreshUndefinedExposure(Frontend.World.TextureParameters,source);
    }
}
