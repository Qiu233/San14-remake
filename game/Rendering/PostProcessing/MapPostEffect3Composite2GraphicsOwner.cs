using System.Security.Cryptography;
using Godot;
using San14.Data;

/// <summary>Actual same-invocation frontend and module producers. Each method
/// executes or captures the named original lifecycle operation. No fixture
/// raster, sampler carrier, asset pointer, target topology or initialized pixel defaults.</summary>
public interface IMapPostEffect3Composite2GraphicsInvocationLease
{
    long FrameNumber{get;}
    string ContextOwnerIdentity{get;}
    string SourceSha256{get;}
    string SourceEvidence{get;}
    void RequireCurrent(MapPostEffect3Composite2ExecutionFrame actualFrame);
    MapPostEffect3Composite2ModuleLuts CaptureCurrentModuleLuts();
    Task<MapPostEffect3Composite2BoundGraphics> ApplyBeforeAndBindGraphicsAsync(
        MapPostEffect3Composite2BeforeBindings before);
    IReadOnlyList<IMapPostEffect3ComputeSamplerLease> CaptureCurrentSamplerLeases(
        MapPostEffect3Composite2BeforeBindings before,
        IReadOnlyList<MapPostEffect3Composite2SamplerRegister> actualShaderRegisters);
    Task<IMapPostEffect3ComputeTextureLease> CaptureCurrentExternalTextureReadAsync(
        MapPostEffect3Composite2BeforeBindings before,MapPostEffect3Binding actualNativeBinding);
    Task AcceptCompletedGraphicsAsync(MapPostEffect3Composite2GraphicsCoverageReceipt actualCoverage);
}

public sealed record MapPostEffect3Composite2SamplerRegister(int NativeStageOrdinal,uint SourceRegister);

/// <summary>PS7/8 come from actual module+30/+38 manager sampler carriers.
/// The historical Data ModuleLuts/Wrapper member names are compatibility names;
/// slot81/83 HDR/LDR asset wrappers are independently supplied texture reads.
/// This capability
/// additionally joins the source Before setter and mutated sampler48 word.</summary>
public interface IMapPostEffect3Composite2ModuleLutSamplerLease:IMapPostEffect3ComputeSamplerLease
{
    MapPostEffect3ApplyView? ActualModuleWrapper{get;}
    ushort? ActualWrapperSampler48{get;}
    void RequireSelectedBeforeSetter(MapPostEffect3Composite2BeforeBindings before);
}

/// <summary>Complete cache slots0..3 after Select and Before, plus the actual
/// post-Bind frontend snapshot. An empty four-slot cache uses the real caller
/// target. Clearing RT0 alone never establishes that this cache is empty.</summary>
public sealed record MapPostEffect3Composite2BoundGraphics(
    MapPostEffect3Composite2BeforeBindings Before,
    MapPostEffect3Composite2ModuleLuts ActualModuleLutsAfterBefore,
    IReadOnlyList<IMapPostEffect3StockGraphicsTargetLease?> ActualNativeTargetSlots0To3,
    IMapPostEffect3StockGraphicsTargetLease? ActualInheritedCallerTarget,
    MapPostEffect3EyeGraphicsCurrentState ActualPostBindingNativeState,
    IMapPostEffect3FullscreenDrawContextLease ActualPostBindingDrawContext,
    string SourceEvidence);

/// <summary>Published only by this owner after the actual original draw. The
/// pixel-domain flag is calculated from submitted viewport/scissor/raster and
/// shader reads. A partial draw retains its receipt without full-pixel claims.</summary>
public sealed class MapPostEffect3Composite2GraphicsCoverageReceipt
{
    internal MapPostEffect3Composite2GraphicsCoverageReceipt(MapPostEffect3Composite2GraphicsOwner owner,
        MapPostEffect3Composite2GraphicsRecipe recipe,MapPostEffect3FullscreenCompletedPass completed,
        IMapPostEffect3StockGraphicsTargetLease target,bool complete)=>
        (Owner,Recipe,Completed,ActualTarget,HasCompleteTargetPixelDomain)=(owner,recipe,completed,target,complete);
    public MapPostEffect3Composite2GraphicsOwner Owner{get;}
    public MapPostEffect3Composite2GraphicsRecipe Recipe{get;}
    public MapPostEffect3FullscreenCompletedPass Completed{get;}
    public IMapPostEffect3StockGraphicsTargetLease ActualTarget{get;}
    public bool HasCompleteTargetPixelDomain{get;}
    public bool CommitsFunctorHistory=>false;
    public void RequireCurrentSubmission()
    {
        Recipe.RequireCurrent();
        if(!ReferenceEquals(Owner.Executor.LastCompletedPass,Completed)||
            !ReferenceEquals(Owner.CurrentCoverage,this))
            throw new InvalidDataException("Composite2 coverage is not this owner's latest actual submitted pass.");
    }
}

public sealed record MapPostEffect3Composite2CompletedGraphics(
    MapPostEffect3Composite2ExecutionFrame SourceFrame,
    IReadOnlyList<MapPostEffect3FullscreenCompletedPass> OrderedDraws,
    MapPostEffect3FullscreenCompletedPass FinalOutputDraw,string SourceEvidence)
{
    public bool CommitsFunctorHistory=>false;
    public bool ProvesAllSelectedGraphicsWereSubmitted=>OrderedDraws.Select(d=>d.SourcePassIndex)
        .SequenceEqual(SourceFrame.Prepared.PassSelection.OrderedFactory9Passes)&&OrderedDraws.All(d=>d.WasPixelDrawSubmitted);
}

/// <summary>Actual factory9 zero-CrossFilter ordinary HDR graphics lifecycle.
/// Owns its submission scope and borrows the immutable bank/program library.
/// Positive CrossFilter and SeparateHDR106/107 require their separate native
/// producers and are rejected before Init or uploads, without skipped success.</summary>
public sealed partial class MapPostEffect3Composite2GraphicsOwner:IAsyncDisposable
{
    public MapPostEffect3FullscreenGraphicsExecutor Executor{get;}
    public PostProcessResourceBank Bank=>Executor.Bank;
    public LoadedMapPostEffect3Composite2ExecutionSource Source{get;}
    public MapPostEffect3Composite2ExecutionFrame Frame{get;}
    public MapPostEffect3TextureParameterState TextureParameters{get;}
    public MapPostEffect3Composite2CompletedGraphics? LastCompleted{get;private set;}
    public MapPostEffect3Composite2GraphicsCoverageReceipt? CurrentCoverage{get;private set;}
    private readonly IMapPostEffect3Composite2GraphicsInvocationLease invocation;
    private readonly int cb22Side;
    private readonly SemaphoreSlim serial=new(1,1);
    private bool accepted,failed,retiring;
    private readonly List<MapPostEffect3FullscreenCompletedPass> selectedDraws=[];
    private Task? disposal;
    private MapPostEffect3Composite2GraphicsOwner(MapPostEffect3FullscreenGraphicsExecutor executor,
        LoadedMapPostEffect3Composite2ExecutionSource source,MapPostEffect3Composite2ExecutionFrame frame,
        MapPostEffect3TextureParameterState table,IMapPostEffect3Composite2GraphicsInvocationLease invocation,int side)
        =>(Executor,Source,Frame,TextureParameters,this.invocation,cb22Side)=(executor,source,frame,table,invocation,side);

    public static MapPostEffect3Composite2GraphicsOwner Create(
        MapPostEffect3ProgramLibrary borrowedPrograms,PostProcessResourceBank borrowedBank,
        LoadedMapPostEffect3Composite2ExecutionSource source,MapPostEffect3Composite2ExecutionFrame actualPrepared,
        MapPostEffect3TextureParameterState actualTable,IMapPostEffect3Composite2GraphicsInvocationLease actualInvocation,
        int actualCb22Instance)
    {
        ArgumentNullException.ThrowIfNull(actualPrepared);ArgumentNullException.ThrowIfNull(actualTable);
        ArgumentNullException.ThrowIfNull(actualInvocation);ArgumentNullException.ThrowIfNull(source);
        if(actualPrepared.HasAppliedInitialization)
        {
            _=actualPrepared.ValidateInitializationOutput79(actualTable);
            actualPrepared.Prepared.RequireCurrent();
        }
        else actualPrepared.ValidateInitializationFor(actualTable);
        actualInvocation.RequireCurrent(actualPrepared);
        if(actualPrepared.ActualInitialModuleLuts.ActualLdrWrapper38 is null)
            throw new InvalidDataException("Composite2 source module+38 point sampler carrier is required; a missing LDR asset83 does not make this carrier null.");
        if(!ReferenceEquals(actualPrepared.Allocation,borrowedBank.Source)||actualTable.Allocation!=borrowedBank.Source||
            actualInvocation.FrameNumber!=actualPrepared.FrameNumber||actualInvocation.ContextOwnerIdentity!=actualPrepared.OwnerIdentity||
            actualInvocation.SourceSha256!=actualPrepared.SourceSha256||string.IsNullOrWhiteSpace(actualInvocation.SourceEvidence))
            throw new InvalidDataException("Composite2 actual source frame/table/frontend belongs to another bank or invocation.");
        if(actualPrepared.RequiresCrossFilterConstants||actualPrepared.RequiresHdrRemakeConstants||
            actualPrepared.Prepared.PassSelection.OrderedFactory9Passes.Any(p=>p is >=92 and <=97 or 106 or 107))
            throw new NotSupportedException("Actual positive CrossFilter or SeparateHDR106/107 needs its original GPU producer; this graphics owner cannot skip it.");
        if(actualPrepared.Prepared.PassSelection.FinalCompositePass!=108||
            actualPrepared.Prepared.PassSelection.OrderedFactory9Passes.Any(p=>p is not(>=98 and <=101 or 108 or >=109 and <=116)))
            throw new NotSupportedException("Composite2 selected source graph is outside the closed ordinary-HDR graphics lifecycle.");
        if(!borrowedBank.ConstantBuffers.TryGetValue(22,out var cbSides)||(uint)actualCb22Instance>=cbSides.Count)
            throw new InvalidDataException("Composite2 actual CB22 instance is outside its owned allocation.");
        var cb=cbSides[actualCb22Instance];
        if(cb.Source.Parameter.ByteCount!=208)throw new InvalidDataException("Composite2 actual CB22 side differs from its native208B declaration.");
        return new(MapPostEffect3FullscreenGraphicsExecutor.Create(borrowedPrograms,borrowedBank),
            source,actualPrepared,actualTable,actualInvocation,actualCb22Instance);
    }

    public async Task<MapPostEffect3Composite2CompletedGraphics> ExecuteAsync()
    {
        await serial.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(LastCompleted is not null||failed)throw new InvalidOperationException("Composite2 source invocation is already returned or failed; use its real next invocation.");
            await InitializeExecutionAsync();
            while(selectedDraws.Count<Frame.Prepared.PassSelection.OrderedFactory9Passes.Count)
                await ExecuteSelectedCoreAsync(Frame.Prepared.PassSelection.OrderedFactory9Passes[selectedDraws.Count]);
            return LastCompleted??throw new InvalidDataException("Composite2 selected directory did not return.");
        }
        catch{failed=true;LastCompleted=null;throw;}
        finally{serial.Release();}
    }

    /// <summary>Submit exactly the next selected source wrapper. The original
    /// whole directory can interleave factory9 AddBloom100/101 with factory8
    /// compute102..104. This operation neither skips a selected draw nor
    /// publishes complete factory9 until all its ordered commands returned.</summary>
    public async Task<MapPostEffect3FullscreenCompletedPass> ExecuteNextSelectedPassAsync(int pass)
    {
        await serial.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(failed||LastCompleted is not null||selectedDraws.Count>=Frame.Prepared.PassSelection.OrderedFactory9Passes.Count||
                pass!=Frame.Prepared.PassSelection.OrderedFactory9Passes[selectedDraws.Count])
                throw new InvalidOperationException("Composite2 stepped execution needs its exact next unreturned selected wrapper.");
            await InitializeExecutionAsync();await ExecuteSelectedCoreAsync(pass);
            return selectedDraws[^1];
        }
        catch{failed=true;LastCompleted=null;throw;}
        finally{serial.Release();}
    }
    private async Task InitializeExecutionAsync()
    {
            RequireCurrent();if(accepted)return;accepted=true;
            var initialLuts=invocation.CaptureCurrentModuleLuts();
            RequireLuts(initialLuts,Frame.ActualInitialModuleLuts);
            if(initialLuts.ActualHdrSampler48!=Frame.ActualInitialModuleLuts.ActualHdrSampler48)
                throw new InvalidDataException("Composite2 Init sampler48 changed after the actual source snapshot.");
            await Bank.UploadCompleteConstantBufferAsync(Frame.Prepared.ConstantBuffer,cb22Side);
            if(Frame.HasAppliedInitialization)
            {
                //The real World scheduler has already performed the native
                //Init. Its typed frame validates79/78 and exact slot/null
                //revisions; Prepared retains the accepted pre-Init CB22
                //getter snapshot. Equal pointers alone cannot prove Init.
                _=Frame.ValidateInitializationOutput79(TextureParameters);
                Frame.Prepared.RequireCurrent();
            }
            else
            {
                Frame.ValidateInitializationFor(TextureParameters);
                TextureParameters.ApplyComposite2Initialization(Frame);
                Frame.Prepared.AcceptActualInitialization(Frame);
            }
    }
    private async Task ExecuteSelectedCoreAsync(int pass)
    {
                RequireCurrent();var currentLuts=invocation.CaptureCurrentModuleLuts();
                var before=Source.PrepareBefore(Frame,pass,currentLuts);
                var bound=await invocation.ApplyBeforeAndBindGraphicsAsync(before);
                RequireBound(before,bound);
                var targets=ResolveActualTargets(bound);
                var target=targets.Single().Target;
                if((target.ParameterIndex is null||TextureParameters.Require(target.ParameterIndex.Value).Reference.PhysicalAllocationParameterIndex is null)&&
                    target is not IMapPostEffect3FullscreenExternalWritableTargetLease)
                    throw new InvalidDataException("Composite2 actual external target needs its writer invalidation capability before submission.");
                var selected=new MapPostEffect3EyeGraphicsSelectedTarget(
                    target.ParameterIndex is{} parameter?TextureParameters.Require(parameter):null,target.InstanceIndex,
                    (target as IMapPostEffect3FullscreenSelectedMipTargetLease)?.NativeFirstMipLevel??0,
                    target.Extent.X,target.Extent.Y,target.TargetViewRid.ToString(),target.SourceEvidence);
                int count=bound.ActualNativeTargetSlots0To3.Count(t=>t is not null);
                var recipe=Source.PrepareGraphics(before,TextureParameters,bound.ActualPostBindingNativeState,selected,count);
                var sourceDraw=MapPostEffect3FullscreenSourceDraw.CaptureComposite2(recipe);
                var context=new ExecuteContext(bound.ActualPostBindingDrawContext,recipe);
                var nulls=MapPostEffect3NullShaderResource.CapturePassReads(Bank.Source,TextureParameters,pass,0);
                var compiled=await Executor.Library.GetCompiledPassAsync(pass,nulls);
                var textures=await CaptureTextureReadsAsync(before,compiled);
                var registers=compiled.Stages.SelectMany(stage=>stage.Bindings.Bindings.Where(b=>b.DescriptorKind=="Sampler")
                    .Select(b=>new MapPostEffect3Composite2SamplerRegister(stage.Source.StageCode==1?0:2,b.Source.BindPoint))).ToArray();
                var samplers=invocation.CaptureCurrentSamplerLeases(before,Array.AsReadOnly(registers));
                RequireSamplers(before,bound.ActualModuleLutsAfterBefore,registers,samplers);
                var sides=Bank.Source.Passes[pass].ActiveBindings.Where(b=>b.Role==MapPostEffect3BindingRole.ConstantBufferRead)
                    .Select(b=>b.ParameterIndex).Distinct().ToDictionary(i=>i,i=>i==22?cb22Side:
                        throw new NotSupportedException("Composite2 graphics needs another actual native constant-buffer side/writer."));
                var input=new MapPostEffect3FullscreenFrameInputs(sourceDraw,TextureParameters,textures,samplers,sides,targets,context);
                MapPostEffect3Functor1SubmissionLedger.RequireGraphicsInputs(input);
                var completed=await Executor.DrawAsync(pass,input);
                lastSubmittedProgram=(completed,compiled);
                CurrentCoverage=CaptureCoverage(recipe,target,completed);
                if(completed.Writes.SingleOrDefault()?.PhysicalAllocationParameterIndex is not null)
                    Bank.PublishComposite2GraphicsCoverage(CurrentCoverage);
                await invocation.AcceptCompletedGraphicsAsync(CurrentCoverage);
                RequireCurrent();selectedDraws.Add(completed);
            if(selectedDraws.Count!=Frame.Prepared.PassSelection.OrderedFactory9Passes.Count)return;
            var result=new MapPostEffect3Composite2CompletedGraphics(Frame,Array.AsReadOnly(selectedDraws.ToArray()),selectedDraws.Last(),
                Frame.SourceEvidence+";"+invocation.SourceEvidence+";actual-complete-selected-factory9-graphics;Execute-scissor-state-local-and-restored;After/Finish-success");
            if(!result.ProvesAllSelectedGraphicsWereSubmitted)throw new InvalidDataException("Composite2 did not submit every actual selected graphics pass.");
            LastCompleted=result;
    }

    private void RequireCurrent(){Frame.RequireCurrent();invocation.RequireCurrent(Frame);}
    private static void RequireLuts(MapPostEffect3Composite2ModuleLuts actual,MapPostEffect3Composite2ModuleLuts expected)
    {
        if(actual.FrameNumber!=expected.FrameNumber||actual.ContextOwnerIdentity!=expected.ContextOwnerIdentity||
            actual.ActualHdrWrapper30!=expected.ActualHdrWrapper30||actual.ActualLdrWrapper38!=expected.ActualLdrWrapper38||
            string.IsNullOrWhiteSpace(actual.SourceEvidence))throw new InvalidDataException("Composite2 actual module+30/+38 LUT identities changed.");
    }
    public ValueTask DisposeAsync()=>new(disposal??=RetireAsync());
    private async Task RetireAsync()
    {retiring=true;await serial.WaitAsync();try{CurrentCoverage=null;await Executor.DisposeAsync();}finally{serial.Release();}}
}
