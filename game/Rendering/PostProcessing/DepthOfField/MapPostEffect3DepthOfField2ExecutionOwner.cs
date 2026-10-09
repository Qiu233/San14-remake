using Godot;
using San14.Data;

/// <summary>Actual factory7 invocation producers. Bind receives the source
/// ordered MRT views after its Before setters. The frontend supplies its full
/// current post-Bind state; source Execute modifies only its proven fields.</summary>
public interface IMapPostEffect3DepthOfField2InvocationLease
{
    long FrameNumber{get;}
    string ContextOwnerIdentity{get;}
    string SourceSha256{get;}
    string SourceEvidence{get;}
    void RequireCurrent(MapPostEffect3ParameterDepthOfField2Update actualUpdate,
        MapPostEffect3TextureParameterState actualTable);
    MapPostEffect3DepthOfField2TargetView CaptureCurrentExternalTargetView(
        MapPostEffect3DepthOfField2BeforeBindings before,int register,
        MapPostEffect3ResolvedTextureParameter actualPointer);
    Task<IMapPostEffect3FullscreenDrawContextLease> ApplyBeforeAndBindGraphicsAsync(
        MapPostEffect3DepthOfField2BeforeBindings before,
        IReadOnlyList<MapPostEffect3DepthOfField2TargetView> actualTargetViews);
    IMapPostEffect3StockGraphicsTargetLease CaptureCurrentExternalTarget(
        MapPostEffect3DepthOfField2BeforeBindings before,
        MapPostEffect3DepthOfField2TargetView actualView,
        IMapPostEffect3FullscreenDrawContextLease actualPostBindingContext);
    Task<IMapPostEffect3ComputeTextureLease> CaptureCurrentExternalTextureReadAsync(
        MapPostEffect3DepthOfField2BeforeBindings before,MapPostEffect3Binding actualBinding);
    IReadOnlyList<IMapPostEffect3ComputeSamplerLease> CaptureCurrentSamplerLeases(
        MapPostEffect3DepthOfField2BeforeBindings before,
        IReadOnlyList<MapPostEffect3DepthOfField2SamplerRegister> actualShaderRegisters);
    Task AcceptCompletedGraphicsAsync(MapPostEffect3DepthOfField2GraphicsCoverageReceipt actualCoverage);
}

/// <summary>Actual admitted source sprite invocation, including the native
/// buffer-selector table. Compute and scatter lifecycle hooks preserve the
/// source frontend mode/target cache across all selected commands.</summary>
public interface IMapPostEffect3DepthOfField2SpriteInvocationLease:IMapPostEffect3DepthOfField2InvocationLease
{
    IReadOnlyDictionary<int,int> ActualBufferInstances{get;}
    Task ApplyBeforeAndBindComputeAsync(MapPostEffect3DepthOfField2BeforeBindings before);
    Task AcceptCompletedComputeAsync(MapPostEffect3DepthOfField2BeforeBindings before,
        MapPostEffect3ComputeExecutor actualCompute,MapPostEffect3ComputeCompletedPass completed);
    Task AcceptCompletedScatterAsync(MapPostEffect3DepthOfField2ScatterExecutor actualScatter,
        MapPostEffect3FullscreenCompletedPass completed);
}

/// <summary>Actual first module Begin and Main View0 join for the original
/// auto-exposure t2 value load. Other invocations need retained pixel history.</summary>
public interface IMapPostEffect3DepthOfField2FreshExposureInvocationLease:IMapPostEffect3DepthOfField2InvocationLease
{
    MapPostEffect3DepthOfField2FreshUndefinedExposureSource CaptureFreshUndefinedExposureSource(
        MapPostEffect3DepthOfField2GraphicsSource actualConsumer);
}

public sealed record MapPostEffect3DepthOfField2SamplerRegister(int NativeStageOrdinal,uint SourceRegister);
public interface IMapPostEffect3DepthOfField2RetainedExposureInvocationLease:IMapPostEffect3DepthOfField2InvocationLease
{
    bool HasRetainedExposure{get;}
    IMapPostEffect3ComputeMipTextureLease CaptureRetainedExposure(MapPostEffect3DepthOfField2GraphicsSource actualConsumer);
}

public sealed record MapPostEffect3DepthOfField2TargetView(int SourceTargetRegister,
    MapPostEffect3ResolvedTextureParameter ActualPointer,Rid StorageRid,Rid TargetViewRid,
    uint NativeViewFormat,RenderingDevice.DataFormat DeviceFormat,Vector2I Extent,string SourceEvidence);

/// <summary>The actual Before sampler setter, independent of LUT assets.
/// PS7/8 select module manager carriers30/38.</summary>
public interface IMapPostEffect3DepthOfField2ManagerSamplerLease:IMapPostEffect3ComputeSamplerLease
{
    MapPostEffect3ManagerSamplerKind ManagerKind{get;}
    void RequireSelectedBeforeSetter(MapPostEffect3DepthOfField2BeforeBindings before);
}

/// <summary>Only this owner constructs a receipt after its actual draw. MRT
/// pixel completeness is per register. An unused-history copy can submit the
/// original read/draw while retaining no initialized history pixel claim.</summary>
public sealed class MapPostEffect3DepthOfField2GraphicsCoverageReceipt
{
    internal MapPostEffect3DepthOfField2GraphicsCoverageReceipt(MapPostEffect3DepthOfField2ExecutionOwner owner,
        MapPostEffect3DepthOfField2GraphicsSource source,MapPostEffect3FullscreenCompletedPass completed,
        IReadOnlyList<MapPostEffect3FullscreenTargetBinding> targets,IReadOnlyDictionary<int,bool> complete,
        bool allocatedOnlyUnusedHistory)
        =>(Owner,Source,Completed,ActualTargets,CompleteTargetPixelDomains,UsedAllocatedOnlyUnusedHistorySource)=
            (owner,source,completed,targets,complete,allocatedOnlyUnusedHistory);
    public MapPostEffect3DepthOfField2ExecutionOwner Owner{get;}
    public MapPostEffect3DepthOfField2GraphicsSource Source{get;}
    public MapPostEffect3FullscreenCompletedPass Completed{get;}
    public IReadOnlyList<MapPostEffect3FullscreenTargetBinding> ActualTargets{get;}
    public IReadOnlyDictionary<int,bool> CompleteTargetPixelDomains{get;}
    public bool UsedAllocatedOnlyUnusedHistorySource{get;}
    public bool CommitsFunctorHistory=>false;
    public void RequireCurrentSubmission()
    {
        Source.RequireCurrent();
        if(!ReferenceEquals(Owner.Graphics.LastCompletedPass,Completed)||!ReferenceEquals(Owner.CurrentCoverage,this))
            throw new InvalidDataException("DOF2 coverage is not this owner's latest actual submitted pass.");
    }
}

public sealed record MapPostEffect3DepthOfField2CompletedExecution(
    MapPostEffect3ParameterDepthOfField2Update Prepared,
    IReadOnlyList<MapPostEffect3FullscreenCompletedPass> OrderedDraws,
    IReadOnlyList<int> SourceMaskRejectedPasses,
    MapPostEffect3ResolvedTextureParameter ActualOutputColorAlias,
    MapPostEffect3DepthOfField2Controller FinishedController,string SourceEvidence)
{
    public bool CommitsFunctorHistory=>false;
    public IReadOnlyList<MapPostEffect3ComputeCompletedPass> OrderedComputePasses{get;init;}=[];
    public IReadOnlyList<int> OrderedSubmittedSourcePassIndices{get;init;}=[];
    public IReadOnlyDictionary<int,uint> ActualGpuSpriteCounts{get;init;}=new Dictionary<int,uint>();
    public bool ProvesAllSelectedPassesWereSubmitted=>OrderedSubmittedSourcePassIndices
        .SequenceEqual(Prepared.PassSelection.OrderedFactory7Passes)&&OrderedDraws.All(d=>d.WasPixelDrawSubmitted);
}

/// <summary>Source capability2 factory7 ordinary procedural sprite
    /// lifecycle. Owns fullscreen/compute/indirect submissions and source-created
/// shape through a module resource borrow; borrows bank/programs. Source-selected
/// staging maps and conditional destination preservation retain actual writers.
/// Image scatter, 4K and cropped profiles require their separate producers.</summary>
public sealed partial class MapPostEffect3DepthOfField2ExecutionOwner:IAsyncDisposable
{
    public MapPostEffect3FullscreenGraphicsExecutor Graphics{get;}
    public MapPostEffect3ComputeExecutor Compute{get;}
    public PostProcessResourceBank Bank=>Graphics.Bank;
    public MapPostEffect3ParameterState State{get;}
    public MapPostEffect3ParameterDepthOfField2Update Prepared{get;}
    public MapPostEffect3TextureParameterState TextureParameters{get;}
    public MapPostEffect3DepthOfField2ModuleResources ModuleResources{get;}
    public MapPostEffect3DepthOfField2CompletedExecution? LastCompleted{get;private set;}
    public MapPostEffect3DepthOfField2GraphicsCoverageReceipt? CurrentCoverage{get;private set;}
    public IReadOnlyList<MapPostEffect3FullscreenCompletedPass> SubmittedPrefix=>Array.AsReadOnly(submitted.ToArray());
    public IReadOnlyList<MapPostEffect3ComputeCompletedPass> SubmittedComputePrefix=>Array.AsReadOnly(computes.ToArray());
    public IReadOnlyDictionary<int,MapPostEffect3DepthOfField2SpritePrefix> CapturedSpritePrefixes=>
        new System.Collections.ObjectModel.ReadOnlyDictionary<int,MapPostEffect3DepthOfField2SpritePrefix>(
            new Dictionary<int,MapPostEffect3DepthOfField2SpritePrefix>(spritePrefixes));
    private readonly IMapPostEffect3DepthOfField2InvocationLease invocation;
    private readonly IReadOnlyDictionary<int,int> cbSides;
    private readonly SemaphoreSlim serial=new(1,1);
    private readonly List<MapPostEffect3FullscreenCompletedPass> submitted=[];
    private readonly List<MapPostEffect3ComputeCompletedPass> computes=[];
    private readonly List<int> submittedIndices=[];
    private readonly Dictionary<int,int> bufferSides=[];
    private MapPostEffect3DepthOfField2ScatterExecutor? scatter;
    private IMapPostEffect3ComputeBufferLease? currentArguments;
    public MapPostEffect3DepthOfField2StagingReadbackReceipt? ActualSpriteCountReadback{get;private set;}
    private readonly MapPostEffect3Functor1ModuleInvocation? cpuModuleInvocation;
    private readonly bool ownsModuleResources;
    private bool resourcesBorrowed;
    private bool accepted,failed,retiring;
    private Task? disposal;
    private MapPostEffect3DepthOfField2ExecutionOwner(MapPostEffect3FullscreenGraphicsExecutor graphics,
        MapPostEffect3ParameterState state,MapPostEffect3ParameterDepthOfField2Update prepared,
        MapPostEffect3TextureParameterState table,IMapPostEffect3DepthOfField2InvocationLease invocation,
        IReadOnlyDictionary<int,int> sides,MapPostEffect3DepthOfField2ModuleResources? moduleResources,
        MapPostEffect3Functor1ModuleInvocation? cpuInvocation)
    {
        (Graphics,State,Prepared,TextureParameters,this.invocation,cbSides)=(graphics,state,prepared,table,invocation,sides);
        Compute=MapPostEffect3ComputeExecutor.Create(graphics.Library,graphics.Bank);
        ModuleResources=moduleResources??MapPostEffect3DepthOfField2ModuleResources.CreateFrameOwned(graphics.Library,graphics.Bank);
        ownsModuleResources=moduleResources is null;cpuModuleInvocation=cpuInvocation;
    }

    public static MapPostEffect3DepthOfField2ExecutionOwner Create(
        MapPostEffect3ProgramLibrary borrowedPrograms,PostProcessResourceBank borrowedBank,
        MapPostEffect3ParameterState actualState,MapPostEffect3ParameterDepthOfField2Update actualPrepared,
        MapPostEffect3TextureParameterState actualTable,
        IMapPostEffect3DepthOfField2InvocationLease actualInvocation,IReadOnlyDictionary<int,int> actualConstantBufferSides)
        =>CreateCore(borrowedPrograms,borrowedBank,actualState,actualPrepared,actualTable,actualInvocation,actualConstantBufferSides,null,null);

    public static MapPostEffect3DepthOfField2ExecutionOwner CreateBorrowingModuleResources(
        MapPostEffect3DepthOfField2ModuleResources actualModuleResources,
        MapPostEffect3Functor1ModuleInvocation actualFunctorInvocation,
        MapPostEffect3ParameterState actualState,MapPostEffect3ParameterDepthOfField2Update actualPrepared,
        MapPostEffect3TextureParameterState actualTable,IMapPostEffect3DepthOfField2InvocationLease actualInvocation,
        IReadOnlyDictionary<int,int> actualConstantBufferSides)
    {
        ArgumentNullException.ThrowIfNull(actualModuleResources);ArgumentNullException.ThrowIfNull(actualFunctorInvocation);
        var buffers=(actualInvocation as IMapPostEffect3DepthOfField2SpriteInvocationLease)?.ActualBufferInstances
            ??new Dictionary<int,int>();
        actualModuleResources.ValidateCurrentSource(actualFunctorInvocation,actualPrepared,actualTable,
            actualModuleResources.Library,actualModuleResources.Bank,actualConstantBufferSides,buffers);
        return CreateCore(actualModuleResources.Library,actualModuleResources.Bank,actualState,actualPrepared,
            actualTable,actualInvocation,actualConstantBufferSides,actualModuleResources,actualFunctorInvocation);
    }
    private static MapPostEffect3DepthOfField2ExecutionOwner CreateCore(
        MapPostEffect3ProgramLibrary borrowedPrograms,PostProcessResourceBank borrowedBank,
        MapPostEffect3ParameterState actualState,MapPostEffect3ParameterDepthOfField2Update actualPrepared,
        MapPostEffect3TextureParameterState actualTable,
        IMapPostEffect3DepthOfField2InvocationLease actualInvocation,IReadOnlyDictionary<int,int> actualConstantBufferSides,
        MapPostEffect3DepthOfField2ModuleResources? moduleResources,MapPostEffect3Functor1ModuleInvocation? cpuInvocation)
    {
        ArgumentNullException.ThrowIfNull(actualPrepared);ArgumentNullException.ThrowIfNull(actualTable);
        ArgumentNullException.ThrowIfNull(actualInvocation);ArgumentNullException.ThrowIfNull(actualState);
        ArgumentNullException.ThrowIfNull(actualConstantBufferSides);
        actualPrepared.RequireCurrentScalarState(actualState);actualPrepared.RequireCurrentTextureTable(actualTable);
        actualInvocation.RequireCurrent(actualPrepared,actualTable);
        if(!ReferenceEquals(actualTable.Allocation,borrowedBank.Source)||actualState.FrameNumber!=actualPrepared.FrameNumber||
            actualState.OwnerIdentity!=borrowedBank.Source.ContextOwnerIdentity||actualState.SourceSha256!=borrowedBank.Source.Source.Sha256||
            actualInvocation.FrameNumber!=actualPrepared.FrameNumber||actualInvocation.ContextOwnerIdentity!=actualState.OwnerIdentity||
            actualInvocation.SourceSha256!=actualState.SourceSha256||string.IsNullOrWhiteSpace(actualInvocation.SourceEvidence))
            throw new InvalidDataException("DOF2 state/table/current invocation belongs to another actual source bank.");
        var selected=actualPrepared.PassSelection.OrderedFactory7Passes;
        bool sprite=selected.Any(p=>p is >=55 and <=65 or 70 or 71);
        if(sprite&&(actualInvocation is not IMapPostEffect3DepthOfField2SpriteInvocationLease||
            !selected.Contains(55)||!selected.Contains(57)||!selected.Contains(58)||!selected.Contains(62)||!selected.Contains(63)||
            !(selected.Contains(66)^selected.Contains(67))||!selected.Contains(70)||
            selected.Any(p=>p is 56 or >=59 and <=61 or 64 or 65 or 68 or 69 or 71)))
            throw new NotSupportedException("DOF2 selected sprite path requires actual procedural55/CS45+46/scatter62+63/CoC66-or67/precompose70; 4K/image profiles need their real adapters.");
        if(actualPrepared.NextController.UsesCroppedViewport168||selected.Any(p=>p is 69 or >=78 and <=83))
            throw new NotSupportedException("DOF2 cropped source domain needs its retained outside-scissor producer; the current ordinary owner cannot replace that viewport.");
        if(selected.Any(p=>p is not(53 or 54 or 55 or 57 or 58 or 62 or 63 or 66 or 67 or 68 or 70 or >=72 and <=77)))
            throw new NotSupportedException("DOF2 source mask selected an unclosed ordinary profile.");
        var sides=new System.Collections.ObjectModel.ReadOnlyDictionary<int,int>(new Dictionary<int,int>(actualConstantBufferSides));
        foreach(int index in selected.SelectMany(p=>borrowedBank.Source.Passes[p].ActiveBindings)
            .Where(b=>b.Role==MapPostEffect3BindingRole.ConstantBufferRead).Select(b=>b.ParameterIndex).Append(18).Distinct())
            if(!sides.TryGetValue(index,out int side)||!borrowedBank.ConstantBuffers.TryGetValue(index,out var buffers)||(uint)side>=buffers.Count)
                throw new InvalidDataException("DOF2 requires the actual allocated native CB side, including unused CB19 setters: "+index);
        var owner=new MapPostEffect3DepthOfField2ExecutionOwner(MapPostEffect3FullscreenGraphicsExecutor.Create(borrowedPrograms,borrowedBank),
            actualState,actualPrepared,actualTable,actualInvocation,sides,moduleResources,cpuInvocation);
        if(sprite)
        {
            var actual=((IMapPostEffect3DepthOfField2SpriteInvocationLease)actualInvocation).ActualBufferInstances;
            foreach(int index in new[]{5,6,7})
            {
                if(!actual.TryGetValue(index,out int side)||!borrowedBank.Buffers.TryGetValue(index,out var buffers)||(uint)side>=buffers.Count)
                    throw new InvalidDataException("DOF2 source invocation omitted its actual owned buffer side: "+index);
                owner.bufferSides.Add(index,side);
            }
            if(!borrowedBank.RequireBuffer(5,owner.bufferSides[5]).SupportsIndirectArgumentBinding)
                throw new NotSupportedException("DOF2 actual B5 allocation lacks its original indirect argument device usage.");
        }
        return owner;
    }

    public async Task<MapPostEffect3DepthOfField2CompletedExecution> ExecuteAsync()
    {
        await serial.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(accepted||failed)throw new InvalidOperationException("DOF2 invocation is already accepted or failed; provide its actual next invocation.");
            accepted=true;CurrentSourceOperation="validate-current-invocation";RequireCurrent();
            await ModuleResources.BorrowAsync(this,cpuModuleInvocation,cbSides,bufferSides);resourcesBorrowed=true;
            if(bufferSides.Count!=0)
            {
                CurrentSourceOperation="borrow-module-source-staging";
                scatter=await ModuleResources.RequireScatterAsync(this);
            }
            CurrentSourceOperation="prepare-current-shape";
            await PrepareShapeAsync();
            CurrentSourceOperation="upload-source-CB18";
            await Bank.UploadCompleteConstantBufferAsync(Prepared.ConstantBuffer,cbSides[18]);
            CurrentSourceOperation="adopt-actual-source-Init";
            if(Prepared.AcceptedInitialization is null)TextureParameters.ApplyDepthOfField2Alias(Prepared);
            Prepared.RequireActualInitialization(TextureParameters);
            foreach(int pass in Prepared.PassSelection.OrderedFactory7Passes)
            {
                CurrentSourcePassIndex=pass;CurrentSourceOperation="source-Before";currentGraphicsReadInterfaces="none-captured";
                RequireCurrent();
                if(pass==57&&Prepared.RequiresSpriteCountReadback)
                {
                    CurrentSourceOperation="source-Before57-actual-staging-map32B";
                    ActualSpriteCountReadback=await ModuleResources.CaptureActiveStagingReadbackAsync(this,RequireCurrent);
                    ActualSpriteCountReadback.RequireCurrent();
                }
                var before=Prepared.PrepareBefore(pass,pass==57?ActualSpriteCountReadback?.SourceReadback:null);
                if(before.RequiresGenericComputeDispatch)
                {CurrentSourceOperation="source-compute";await ExecuteComputeAsync(before);submittedIndices.Add(pass);continue;}
                if(before.RequiresNativeIndirectDraw)
                {CurrentSourceOperation="source-indirect-scatter";await ExecuteScatterAsync(before);submittedIndices.Add(pass);continue;}
                CurrentSourceOperation="capture-source-graphics-and-targets";
                var proof=MapPostEffect3DepthOfField2GraphicsSource.Prepare(State,Bank.Source,TextureParameters,Prepared,before);
                var views=proof.ReadEffectiveTargets().Select(t=>CaptureTargetView(before,t.Register,t.Texture)).ToArray();
                var actualContext=await invocation.ApplyBeforeAndBindGraphicsAsync(before,Array.AsReadOnly(views));
                var context=new ExecuteContext(this,proof,actualContext,views[0].Extent);
                context.RequireCurrentForPass(pass);
                var targets=views.Select(v=>new MapPostEffect3FullscreenTargetBinding(v.SourceTargetRegister,
                    CaptureTarget(before,v,context))).ToArray();
                var nulls=MapPostEffect3NullShaderResource.CapturePassReads(Bank.Source,TextureParameters,pass,0)
                    .Where(n=>!before.OrderedPixelTextureSetters.Any(s=>s.NativeRegisterIndex==n.SourceRegister)).ToArray();
                var compiled=await Graphics.Library.GetCompiledPassAsync(pass,nulls);
                CurrentSourceOperation="capture-current-texture-reads";
                var reads=await CaptureReadsAsync(proof,compiled);
                CaptureCurrentGraphicsReadInterfaces(compiled,reads);
                var registers=compiled.Stages.SelectMany(s=>s.Bindings.Bindings.Where(b=>b.DescriptorKind=="Sampler")
                    .Select(b=>new MapPostEffect3DepthOfField2SamplerRegister(s.Source.StageCode==1?0:2,b.Source.BindPoint))).ToArray();
                var samplers=invocation.CaptureCurrentSamplerLeases(before,Array.AsReadOnly(registers));
                RequireSamplers(before,registers,samplers);
                // Every setter remains in the source frontend cache. Only the
                // last setter per register is an actual device descriptor;
                // AA67 overwrites its first history t1 with current CoC60.
                var setters=before.OrderedPixelTextureSetters.GroupBy(s=>s.NativeRegisterIndex).Select(g=>g.Last())
                    .Select(s=>MapPostEffect3FullscreenTextureOverride.CaptureDepthOfField2(
                        TextureParameters,before,s,reads.Single(r=>r is ReadCapability c&&c.Rows.Any(row=>row.RegisterIndex==s.NativeRegisterIndex)))).ToArray();
                var sides=Bank.Source.Passes[pass].ActiveBindings.Where(b=>b.Role==MapPostEffect3BindingRole.ConstantBufferRead)
                    .Select(b=>b.ParameterIndex).Distinct().ToDictionary(i=>i,i=>cbSides[i]);
                CurrentSourceOperation="submit-original-graphics";
                var graphicsInput=new MapPostEffect3FullscreenFrameInputs(MapPostEffect3FullscreenSourceDraw.CaptureDepthOfField2(Prepared,before),
                    TextureParameters,reads,samplers,sides,targets,context){TextureOverrides=setters};
                var historicalInputs=MapPostEffect3Functor1SubmissionLedger.FreezeGraphicsInputs(graphicsInput);
                var completed=await Graphics.DrawAsync(pass,graphicsInput);
                lastHistoricalGraphicsInputs=(completed,compiled,historicalInputs);
                submitted.Add(completed);submittedIndices.Add(pass);
                CurrentSourceOperation="publish-actual-graphics-coverage";
                CurrentCoverage=CaptureCoverage(proof,Array.AsReadOnly(targets),completed,reads.Any(r=>r is ReadCapability{AllocatedOnlyUnusedHistory:true}));
                foreach(var target in targets)
                {
                    if(!CurrentCoverage.CompleteTargetPixelDomains[target.SourceTargetRegister])continue;
                    if(target.Target.ParameterIndex==58&&ownedShape is not null)PublishShapeCoverage(CurrentCoverage);
                    else if(TextureParameters.Require(target.Target.ParameterIndex!.Value).Reference.PhysicalAllocationParameterIndex is not null)
                        Bank.PublishDepthOfField2GraphicsCoverage(CurrentCoverage,target.SourceTargetRegister);
                }
                await invocation.AcceptCompletedGraphicsAsync(CurrentCoverage);
                if(!completed.WasPixelDrawSubmitted)throw new InvalidDataException("DOF2 actual selected draw had an empty pixel domain; this invocation cannot claim all selected commands.");
            }
            CurrentSourceOperation="source-Finish";RequireCurrent();var output=TextureParameters.Require(Prepared.InitializeAliasTargetIndex);
            var input=TextureParameters.Require(Prepared.InitializeAliasSourceIndex);
            if(output.Reference!=input.Reference)throw new InvalidDataException("DOF2 output68 lost the original Init copy of its input0 wrapper/side.");
            var result=new MapPostEffect3DepthOfField2CompletedExecution(Prepared,Array.AsReadOnly(submitted.ToArray()),
                Array.AsReadOnly(Enumerable.Range(53,31).Where(p=>!Prepared.PassSelection.OrderedFactory7Passes.Contains(p)).ToArray()),
                output,Prepared.NextController.ApplySourceFinish(true),Prepared.SourceEvidence+";"+invocation.SourceEvidence+
                ";actual-all-selected-Dof2-commands;MRT-and-real-indirect-preserved;source-After-noop/Finish-count10;CB19-original-not-declared;no-Functor-history-commit")
                {OrderedComputePasses=Array.AsReadOnly(computes.ToArray()),OrderedSubmittedSourcePassIndices=Array.AsReadOnly(submittedIndices.ToArray()),
                    ActualGpuSpriteCounts=new System.Collections.ObjectModel.ReadOnlyDictionary<int,uint>(spritePrefixes.ToDictionary(p=>p.Key,p=>p.Value.ActualSpriteCount))};
            if(!result.ProvesAllSelectedPassesWereSubmitted)throw new InvalidDataException("DOF2 did not submit every actual selected pass.");
            ModuleResources.RecordCompleted(this,result);return LastCompleted=result;
        }
        catch(Exception ex)
        {
            failed=true;LastCompleted=null;
            LastFailureEvidence=$"sourcePass={CurrentSourcePassIndex?.ToString()??"none"};operation={CurrentSourceOperation};"+
                $"submitted=[{string.Join(",",submittedIndices)}];actualReadInterfaces=[{currentGraphicsReadInterfaces}]";
            if(resourcesBorrowed)ModuleResources.RecordFailure(this,LastFailureEvidence);
            throw new InvalidDataException("DOF2 actual source execution failed: "+LastFailureEvidence,ex);
        }
        finally{serial.Release();}
    }
    private void RequireCurrent()
    {
        Prepared.RequireCurrentTextureTable(TextureParameters);invocation.RequireCurrent(Prepared,TextureParameters);
        if(cpuModuleInvocation is not null)
            cpuModuleInvocation.RequireCurrentDepthOfField2Prepared(Prepared,TextureParameters);
        if(resourcesBorrowed)ModuleResources.RequireActive(this);
        Prepared.AcceptedInitialization?.RequireCurrent(TextureParameters);
        if(bufferSides.Count!=0)
        {
            var actual=(invocation as IMapPostEffect3DepthOfField2SpriteInvocationLease)?.ActualBufferInstances
                ??throw new InvalidDataException("DOF2 current sprite side producer disappeared.");
            if(bufferSides.Any(s=>!actual.TryGetValue(s.Key,out int side)||side!=s.Value))
                throw new InvalidDataException("DOF2 actual buffer side changed after accepting this source invocation.");
        }
    }
    public ValueTask DisposeAsync()=>new(disposal??=RetireAsync());
    private async Task RetireAsync()
    {
        retiring=true;await serial.WaitAsync();
        try
        {
            CurrentCoverage=null;
            try{await Compute.DisposeAsync();}
            finally{try{await Graphics.DisposeAsync();}finally{ReleaseFrameShape();scatter=null;}}
        }
        finally
        {
            try{if(resourcesBorrowed){ModuleResources.Release(this);resourcesBorrowed=false;}
                if(ownsModuleResources)await ModuleResources.DisposeAsync();}
            finally{serial.Release();}
        }
    }
}
