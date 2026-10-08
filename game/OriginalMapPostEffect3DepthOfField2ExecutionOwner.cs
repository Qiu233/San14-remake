using Godot;
using San14.Data;

/// <summary>Actual factory7 invocation producers. Bind receives the source
/// ordered MRT views after its Before setters. The frontend supplies its full
/// current post-Bind state; source Execute modifies only its proven fields.</summary>
public interface IOriginalMapPostEffect3DepthOfField2InvocationLease
{
    long FrameNumber{get;}
    string ContextOwnerIdentity{get;}
    string SourceSha256{get;}
    string SourceEvidence{get;}
    void RequireCurrent(OriginalMapPostEffect3ParameterDepthOfField2Update actualUpdate,
        OriginalMapPostEffect3TextureParameterState actualTable);
    OriginalMapPostEffect3DepthOfField2TargetView CaptureCurrentExternalTargetView(
        OriginalMapPostEffect3DepthOfField2BeforeBindings before,int register,
        OriginalMapPostEffect3ResolvedTextureParameter actualPointer);
    Task<IOriginalMapPostEffect3FullscreenDrawContextLease> ApplyBeforeAndBindGraphicsAsync(
        OriginalMapPostEffect3DepthOfField2BeforeBindings before,
        IReadOnlyList<OriginalMapPostEffect3DepthOfField2TargetView> actualTargetViews);
    IOriginalMapPostEffect3StockGraphicsTargetLease CaptureCurrentExternalTarget(
        OriginalMapPostEffect3DepthOfField2BeforeBindings before,
        OriginalMapPostEffect3DepthOfField2TargetView actualView,
        IOriginalMapPostEffect3FullscreenDrawContextLease actualPostBindingContext);
    Task<IOriginalMapPostEffect3ComputeTextureLease> CaptureCurrentExternalTextureReadAsync(
        OriginalMapPostEffect3DepthOfField2BeforeBindings before,OriginalMapPostEffect3Binding actualBinding);
    IReadOnlyList<IOriginalMapPostEffect3ComputeSamplerLease> CaptureCurrentSamplerLeases(
        OriginalMapPostEffect3DepthOfField2BeforeBindings before,
        IReadOnlyList<OriginalMapPostEffect3DepthOfField2SamplerRegister> actualShaderRegisters);
    Task AcceptCompletedGraphicsAsync(OriginalMapPostEffect3DepthOfField2GraphicsCoverageReceipt actualCoverage);
}

/// <summary>Actual admitted source sprite invocation, including the native
/// buffer-selector table. Compute and scatter lifecycle hooks preserve the
/// source frontend mode/target cache across all selected commands.</summary>
public interface IOriginalMapPostEffect3DepthOfField2SpriteInvocationLease:IOriginalMapPostEffect3DepthOfField2InvocationLease
{
    IReadOnlyDictionary<int,int> ActualBufferInstances{get;}
    Task ApplyBeforeAndBindComputeAsync(OriginalMapPostEffect3DepthOfField2BeforeBindings before);
    Task AcceptCompletedComputeAsync(OriginalMapPostEffect3DepthOfField2BeforeBindings before,
        OriginalMapPostEffect3ComputeExecutor actualCompute,OriginalMapPostEffect3ComputeCompletedPass completed);
    Task AcceptCompletedScatterAsync(OriginalMapPostEffect3DepthOfField2ScatterExecutor actualScatter,
        OriginalMapPostEffect3FullscreenCompletedPass completed);
}

/// <summary>Actual first module Begin and Main View0 join for the original
/// auto-exposure t2 value load. Other invocations need retained pixel history.</summary>
public interface IOriginalMapPostEffect3DepthOfField2FreshExposureInvocationLease:IOriginalMapPostEffect3DepthOfField2InvocationLease
{
    OriginalMapPostEffect3DepthOfField2FreshUndefinedExposureSource CaptureFreshUndefinedExposureSource(
        OriginalMapPostEffect3DepthOfField2GraphicsSource actualConsumer);
}

public sealed record OriginalMapPostEffect3DepthOfField2SamplerRegister(int NativeStageOrdinal,uint SourceRegister);
public interface IOriginalMapPostEffect3DepthOfField2RetainedExposureInvocationLease:IOriginalMapPostEffect3DepthOfField2InvocationLease
{
    bool HasRetainedExposure{get;}
    IOriginalMapPostEffect3ComputeMipTextureLease CaptureRetainedExposure(OriginalMapPostEffect3DepthOfField2GraphicsSource actualConsumer);
}

public sealed record OriginalMapPostEffect3DepthOfField2TargetView(int SourceTargetRegister,
    OriginalMapPostEffect3ResolvedTextureParameter ActualPointer,Rid StorageRid,Rid TargetViewRid,
    uint NativeViewFormat,RenderingDevice.DataFormat DeviceFormat,Vector2I Extent,string SourceEvidence);

/// <summary>The actual Before sampler setter, independent of LUT assets.
/// PS7/8 select module manager carriers30/38 through1163970.</summary>
public interface IOriginalMapPostEffect3DepthOfField2ManagerSamplerLease:IOriginalMapPostEffect3ComputeSamplerLease
{
    OriginalMapPostEffect3ManagerSamplerKind ManagerKind{get;}
    void RequireOriginalBeforeSetter(OriginalMapPostEffect3DepthOfField2BeforeBindings before);
}

/// <summary>Only this owner constructs a receipt after its actual draw. MRT
/// pixel completeness is per register. An unused-history copy can submit the
/// original read/draw while retaining no initialized history pixel claim.</summary>
public sealed class OriginalMapPostEffect3DepthOfField2GraphicsCoverageReceipt
{
    internal OriginalMapPostEffect3DepthOfField2GraphicsCoverageReceipt(OriginalMapPostEffect3DepthOfField2ExecutionOwner owner,
        OriginalMapPostEffect3DepthOfField2GraphicsSource source,OriginalMapPostEffect3FullscreenCompletedPass completed,
        IReadOnlyList<OriginalMapPostEffect3FullscreenTargetBinding> targets,IReadOnlyDictionary<int,bool> complete,
        bool allocatedOnlyUnusedHistory)
        =>(Owner,Source,Completed,ActualTargets,CompleteTargetPixelDomains,UsedAllocatedOnlyUnusedHistorySource)=
            (owner,source,completed,targets,complete,allocatedOnlyUnusedHistory);
    public OriginalMapPostEffect3DepthOfField2ExecutionOwner Owner{get;}
    public OriginalMapPostEffect3DepthOfField2GraphicsSource Source{get;}
    public OriginalMapPostEffect3FullscreenCompletedPass Completed{get;}
    public IReadOnlyList<OriginalMapPostEffect3FullscreenTargetBinding> ActualTargets{get;}
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

public sealed record OriginalMapPostEffect3DepthOfField2CompletedExecution(
    OriginalMapPostEffect3ParameterDepthOfField2Update Prepared,
    IReadOnlyList<OriginalMapPostEffect3FullscreenCompletedPass> OrderedDraws,
    IReadOnlyList<int> SourceMaskRejectedPasses,
    OriginalMapPostEffect3ResolvedTextureParameter ActualOutputColorAlias,
    OriginalMapPostEffect3DepthOfField2Controller FinishedController,string SourceEvidence)
{
    public bool CommitsFunctorHistory=>false;
    public IReadOnlyList<OriginalMapPostEffect3ComputeCompletedPass> OrderedComputePasses{get;init;}=[];
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
public sealed partial class OriginalMapPostEffect3DepthOfField2ExecutionOwner:IAsyncDisposable
{
    public OriginalMapPostEffect3FullscreenGraphicsExecutor Graphics{get;}
    public OriginalMapPostEffect3ComputeExecutor Compute{get;}
    public OriginalMapPostEffect3ResourceBank Bank=>Graphics.Bank;
    public OriginalMapPostEffect3ParameterState State{get;}
    public OriginalMapPostEffect3ParameterDepthOfField2Update Prepared{get;}
    public OriginalMapPostEffect3TextureParameterState TextureParameters{get;}
    public OriginalMapPostEffect3DepthOfField2ModuleResources ModuleResources{get;}
    public OriginalMapPostEffect3DepthOfField2CompletedExecution? LastCompleted{get;private set;}
    public OriginalMapPostEffect3DepthOfField2GraphicsCoverageReceipt? CurrentCoverage{get;private set;}
    public IReadOnlyList<OriginalMapPostEffect3FullscreenCompletedPass> SubmittedPrefix=>Array.AsReadOnly(submitted.ToArray());
    public IReadOnlyList<OriginalMapPostEffect3ComputeCompletedPass> SubmittedComputePrefix=>Array.AsReadOnly(computes.ToArray());
    public IReadOnlyDictionary<int,OriginalMapPostEffect3DepthOfField2SpritePrefix> CapturedSpritePrefixes=>
        new System.Collections.ObjectModel.ReadOnlyDictionary<int,OriginalMapPostEffect3DepthOfField2SpritePrefix>(
            new Dictionary<int,OriginalMapPostEffect3DepthOfField2SpritePrefix>(spritePrefixes));
    private readonly IOriginalMapPostEffect3DepthOfField2InvocationLease invocation;
    private readonly IReadOnlyDictionary<int,int> cbSides;
    private readonly SemaphoreSlim serial=new(1,1);
    private readonly List<OriginalMapPostEffect3FullscreenCompletedPass> submitted=[];
    private readonly List<OriginalMapPostEffect3ComputeCompletedPass> computes=[];
    private readonly List<int> submittedIndices=[];
    private readonly Dictionary<int,int> bufferSides=[];
    private OriginalMapPostEffect3DepthOfField2ScatterExecutor? scatter;
    private IOriginalMapPostEffect3ComputeBufferLease? currentArguments;
    public OriginalMapPostEffect3DepthOfField2StagingReadbackReceipt? ActualSpriteCountReadback{get;private set;}
    private readonly OriginalMapPostEffect3Functor1ModuleInvocation? cpuModuleInvocation;
    private readonly bool ownsModuleResources;
    private bool resourcesBorrowed;
    private bool accepted,failed,retiring;
    private Task? disposal;
    private OriginalMapPostEffect3DepthOfField2ExecutionOwner(OriginalMapPostEffect3FullscreenGraphicsExecutor graphics,
        OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3ParameterDepthOfField2Update prepared,
        OriginalMapPostEffect3TextureParameterState table,IOriginalMapPostEffect3DepthOfField2InvocationLease invocation,
        IReadOnlyDictionary<int,int> sides,OriginalMapPostEffect3DepthOfField2ModuleResources? moduleResources,
        OriginalMapPostEffect3Functor1ModuleInvocation? cpuInvocation)
    {
        (Graphics,State,Prepared,TextureParameters,this.invocation,cbSides)=(graphics,state,prepared,table,invocation,sides);
        Compute=OriginalMapPostEffect3ComputeExecutor.Create(graphics.Library,graphics.Bank);
        ModuleResources=moduleResources??OriginalMapPostEffect3DepthOfField2ModuleResources.CreateFrameOwned(graphics.Library,graphics.Bank);
        ownsModuleResources=moduleResources is null;cpuModuleInvocation=cpuInvocation;
    }

    public static OriginalMapPostEffect3DepthOfField2ExecutionOwner Create(
        OriginalMapPostEffect3ProgramLibrary borrowedPrograms,OriginalMapPostEffect3ResourceBank borrowedBank,
        OriginalMapPostEffect3ParameterState actualState,OriginalMapPostEffect3ParameterDepthOfField2Update actualPrepared,
        OriginalMapPostEffect3TextureParameterState actualTable,
        IOriginalMapPostEffect3DepthOfField2InvocationLease actualInvocation,IReadOnlyDictionary<int,int> actualConstantBufferSides)
        =>CreateCore(borrowedPrograms,borrowedBank,actualState,actualPrepared,actualTable,actualInvocation,actualConstantBufferSides,null,null);

    public static OriginalMapPostEffect3DepthOfField2ExecutionOwner CreateBorrowingModuleResources(
        OriginalMapPostEffect3DepthOfField2ModuleResources actualModuleResources,
        OriginalMapPostEffect3Functor1ModuleInvocation actualFunctorInvocation,
        OriginalMapPostEffect3ParameterState actualState,OriginalMapPostEffect3ParameterDepthOfField2Update actualPrepared,
        OriginalMapPostEffect3TextureParameterState actualTable,IOriginalMapPostEffect3DepthOfField2InvocationLease actualInvocation,
        IReadOnlyDictionary<int,int> actualConstantBufferSides)
    {
        ArgumentNullException.ThrowIfNull(actualModuleResources);ArgumentNullException.ThrowIfNull(actualFunctorInvocation);
        var buffers=(actualInvocation as IOriginalMapPostEffect3DepthOfField2SpriteInvocationLease)?.ActualBufferInstances
            ??new Dictionary<int,int>();
        actualModuleResources.ValidateCurrentSource(actualFunctorInvocation,actualPrepared,actualTable,
            actualModuleResources.Library,actualModuleResources.Bank,actualConstantBufferSides,buffers);
        return CreateCore(actualModuleResources.Library,actualModuleResources.Bank,actualState,actualPrepared,
            actualTable,actualInvocation,actualConstantBufferSides,actualModuleResources,actualFunctorInvocation);
    }
    private static OriginalMapPostEffect3DepthOfField2ExecutionOwner CreateCore(
        OriginalMapPostEffect3ProgramLibrary borrowedPrograms,OriginalMapPostEffect3ResourceBank borrowedBank,
        OriginalMapPostEffect3ParameterState actualState,OriginalMapPostEffect3ParameterDepthOfField2Update actualPrepared,
        OriginalMapPostEffect3TextureParameterState actualTable,
        IOriginalMapPostEffect3DepthOfField2InvocationLease actualInvocation,IReadOnlyDictionary<int,int> actualConstantBufferSides,
        OriginalMapPostEffect3DepthOfField2ModuleResources? moduleResources,OriginalMapPostEffect3Functor1ModuleInvocation? cpuInvocation)
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
        if(sprite&&(actualInvocation is not IOriginalMapPostEffect3DepthOfField2SpriteInvocationLease||
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
            .Where(b=>b.Role==OriginalMapPostEffect3BindingRole.ConstantBufferRead).Select(b=>b.ParameterIndex).Append(18).Distinct())
            if(!sides.TryGetValue(index,out int side)||!borrowedBank.ConstantBuffers.TryGetValue(index,out var buffers)||(uint)side>=buffers.Count)
                throw new InvalidDataException("DOF2 requires the actual allocated native CB side, including unused CB19 setters: "+index);
        var owner=new OriginalMapPostEffect3DepthOfField2ExecutionOwner(OriginalMapPostEffect3FullscreenGraphicsExecutor.Create(borrowedPrograms,borrowedBank),
            actualState,actualPrepared,actualTable,actualInvocation,sides,moduleResources,cpuInvocation);
        if(sprite)
        {
            var actual=((IOriginalMapPostEffect3DepthOfField2SpriteInvocationLease)actualInvocation).ActualBufferInstances;
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

    public async Task<OriginalMapPostEffect3DepthOfField2CompletedExecution> ExecuteAsync()
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
                var proof=OriginalMapPostEffect3DepthOfField2GraphicsSource.Prepare(State,Bank.Source,TextureParameters,Prepared,before);
                var views=proof.ReadEffectiveTargets().Select(t=>CaptureTargetView(before,t.Register,t.Texture)).ToArray();
                var actualContext=await invocation.ApplyBeforeAndBindGraphicsAsync(before,Array.AsReadOnly(views));
                var context=new ExecuteContext(this,proof,actualContext,views[0].Extent);
                context.RequireCurrentForPass(pass);
                var targets=views.Select(v=>new OriginalMapPostEffect3FullscreenTargetBinding(v.SourceTargetRegister,
                    CaptureTarget(before,v,context))).ToArray();
                var nulls=OriginalMapPostEffect3NullShaderResource.CapturePassReads(Bank.Source,TextureParameters,pass,0)
                    .Where(n=>!before.OrderedPixelTextureSetters.Any(s=>s.NativeRegisterIndex==n.SourceRegister)).ToArray();
                var compiled=await Graphics.Library.GetCompiledPassAsync(pass,nulls);
                CurrentSourceOperation="capture-current-texture-reads";
                var reads=await CaptureReadsAsync(proof,compiled);
                CaptureCurrentGraphicsReadInterfaces(compiled,reads);
                var registers=compiled.Stages.SelectMany(s=>s.Bindings.Bindings.Where(b=>b.DescriptorKind=="Sampler")
                    .Select(b=>new OriginalMapPostEffect3DepthOfField2SamplerRegister(s.Source.StageCode==1?0:2,b.Source.BindPoint))).ToArray();
                var samplers=invocation.CaptureCurrentSamplerLeases(before,Array.AsReadOnly(registers));
                RequireSamplers(before,registers,samplers);
                // Every setter remains in the source frontend cache. Only the
                // last setter per register is an actual device descriptor;
                // AA67 overwrites its first history t1 with current CoC60.
                var setters=before.OrderedPixelTextureSetters.GroupBy(s=>s.NativeRegisterIndex).Select(g=>g.Last())
                    .Select(s=>OriginalMapPostEffect3FullscreenTextureOverride.CaptureDepthOfField2(
                        TextureParameters,before,s,reads.Single(r=>r is ReadCapability c&&c.Rows.Any(row=>row.RegisterIndex==s.NativeRegisterIndex)))).ToArray();
                var sides=Bank.Source.Passes[pass].ActiveBindings.Where(b=>b.Role==OriginalMapPostEffect3BindingRole.ConstantBufferRead)
                    .Select(b=>b.ParameterIndex).Distinct().ToDictionary(i=>i,i=>cbSides[i]);
                CurrentSourceOperation="submit-original-graphics";
                var graphicsInput=new OriginalMapPostEffect3FullscreenFrameInputs(OriginalMapPostEffect3FullscreenSourceDraw.CaptureDepthOfField2(Prepared,before),
                    TextureParameters,reads,samplers,sides,targets,context){TextureOverrides=setters};
                var historicalInputs=OriginalMapPostEffect3Functor1SubmissionLedger.FreezeGraphicsInputs(graphicsInput);
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
            var result=new OriginalMapPostEffect3DepthOfField2CompletedExecution(Prepared,Array.AsReadOnly(submitted.ToArray()),
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
            var actual=(invocation as IOriginalMapPostEffect3DepthOfField2SpriteInvocationLease)?.ActualBufferInstances
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
