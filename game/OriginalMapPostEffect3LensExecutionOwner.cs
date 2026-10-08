using Godot;
using San14.Data;

/// <summary>Current source resources outside the Lens-owned writes. The caller
/// retains their actual producers; this interface supplies no fallback texture,
/// sampler, primitive or raster. Component fixtures must identify themselves.
/// </summary>
public interface IOriginalMapPostEffect3LensCurrentSourceInputs
{
    void RequireCurrent();
    IOriginalMapPostEffect3ComputeTextureLease RequireReadableTexture(
        OriginalMapPostEffect3LensExecutionRecipe recipe,int parameterIndex,
        OriginalMapPostEffect3ResolvedTextureParameter actualReference);
    IOriginalMapPostEffect3ComputeSamplerLease RequireWrapperSampler(
        OriginalMapPostEffect3LensExecutionRecipe recipe,int nativeStageOrdinal,uint register);
    OriginalMapPostEffect3LensGhostFrameInputs PrepareGhostDraw(
        OriginalMapPostEffect3LensExecutionRecipe recipe,IOriginalMapPostEffect3ComputeBufferLease actualSprite,
        IOriginalMapPostEffect3ComputeTextureLease actualStarBurst);
}

public sealed record OriginalMapPostEffect3LensExecutionFrame(
    OriginalMapPostEffect3ParameterState Scalars,OriginalMapPostEffect3LensPresetParameterOwner Lens,
    OriginalMapPostEffect3ParameterLensFlareInitialization Initialization,
    OriginalMapPostEffect3TextureParameterState TextureParameters,
    IReadOnlyDictionary<int,int> ActualConstantBufferInstances,
    IReadOnlyDictionary<int,int> ActualBufferInstances,IOriginalMapPostEffect3LensCurrentSourceInputs SourceInputs)
{
    public OriginalMapPostEffect3MiddleExecutor? ActualFinishedMiddle{get;init;}
}

public sealed record OriginalMapPostEffect3LensSubmittedPass(int SourcePassIndex,bool SourceBeforeAllowsExecution,
    OriginalMapPostEffect3ComputeCompletedPass? Compute,OriginalMapPostEffect3LensGhostCompletedDraw? Ghost,
    string SourceEvidence);
public sealed record OriginalMapPostEffect3LensCompletedExecution(long FrameNumber,string ContextOwnerIdentity,
    IReadOnlyList<OriginalMapPostEffect3LensSubmittedPass> OrderedPasses,
    IReadOnlyList<OriginalMapPostEffect3ResolvedTextureParameter> OutputAliases,string SourceEvidence)
{
    public bool CompletesLensPass20Through43=>true;
    public bool CompletesFunctor0=>false;
    public bool CommitsHostHistory=>false;
}

/// <summary>Original factory4's ordered twenty-four pass owner. Borrows the
/// actual bank/library and current frame producers. Owns a compute executor and
/// the original manager sampler pair; no pass is substituted or silently omitted.
/// Native Before false is recorded. Missing readable mip coverage rejects at its
/// actual consumer and leaves a precise submitted prefix, never a complete frame.
/// </summary>
public sealed partial class OriginalMapPostEffect3LensExecutionOwner:IAsyncDisposable
{
    public OriginalMapPostEffect3ResourceBank Bank{get;}
    public OriginalMapPostEffect3ProgramLibrary Programs{get;}
    public OriginalMapPostEffect3ComputeExecutor Compute{get;}
    public OriginalMapPostEffect3LensGhostExecutor Ghost{get;}
    public OriginalMapPostEffect3SourceSamplerOwner Samplers{get;}
    public LoadedOriginalMapPostEffect3PassRegisterResetSource NativeRegisterResetSource{get;}
    public IReadOnlyList<OriginalMapPostEffect3LensSubmittedPass> LastSubmittedPrefix{get;private set;}=[];
    public OriginalMapPostEffect3LensCompletedExecution? LastCompleted{get;private set;}
    private readonly SemaphoreSlim serial=new(1,1);
    private readonly object lifetime=new();private bool retiring;private int accepted;
    private TaskCompletionSource? drained;private Task? disposal;
    private readonly string locale;
    private OriginalMapPostEffect3LensExecutionOwner(OriginalMapPostEffect3ProgramLibrary programs,
        OriginalMapPostEffect3ResourceBank bank,OriginalMapPostEffect3SourceSamplerOwner samplers,string nativeLocale,
        LoadedOriginalMapPostEffect3PassRegisterResetSource nativeRegisterResetSource)
        =>(Programs,Bank,Samplers,locale,NativeRegisterResetSource,Compute,Ghost)=(programs,bank,samplers,nativeLocale,nativeRegisterResetSource,
            OriginalMapPostEffect3ComputeExecutor.Create(programs,bank),OriginalMapPostEffect3LensGhostExecutor.Create(programs,bank));

    public static async Task<OriginalMapPostEffect3LensExecutionOwner> CreateBorrowedAsync(
        OriginalMapPostEffect3ProgramLibrary programs,OriginalMapPostEffect3ResourceBank bank,
        LoadedOriginalDeferredSamplerNativeSource actualManagerNativeSource,
        LoadedOriginalMapPostEffect3PassRegisterResetSource actualNativeRegisterResetSource)
    {
        ArgumentNullException.ThrowIfNull(programs);ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(actualManagerNativeSource);
        ArgumentNullException.ThrowIfNull(actualNativeRegisterResetSource);
        if(programs.SourceSha256!=bank.Source.Source.Sha256||bank.Source.Source.ReadFunctorPasses(0)
            .Where(p=>p.Index is >=20 and <=43).Count()!=24)
            throw new InvalidDataException("Lens owner requires this exact source Functor0/program/bank identity.");
        var samplers=await OriginalMapPostEffect3SourceSamplerOwner.BuildAsync(actualManagerNativeSource,bank.Source.ContextOwnerIdentity);
        return new(programs,bank,samplers,actualManagerNativeSource.Locale,actualNativeRegisterResetSource);
    }

    public Task<OriginalMapPostEffect3LensCompletedExecution> ExecuteAsync(OriginalMapPostEffect3LensExecutionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ValidateFrame(frame);
        lock(lifetime){ObjectDisposedException.ThrowIf(retiring,this);accepted++;}
        return ExecuteAcceptedAsync(frame with{
            ActualConstantBufferInstances=new Dictionary<int,int>(frame.ActualConstantBufferInstances),
            ActualBufferInstances=new Dictionary<int,int>(frame.ActualBufferInstances)});
    }

    private void ValidateFrame(OriginalMapPostEffect3LensExecutionFrame frame)
    {
        frame.SourceInputs.RequireCurrent();frame.Lens.ValidatePointerWrites(frame.Scalars);
        var init=frame.Initialization;
        if(frame.ActualFinishedMiddle is{} middle)
        {
            middle.RequireFinishedMiddlePasses();
            if(!ReferenceEquals(middle.Compute.Bank,Bank)||!ReferenceEquals(middle.Parameters.State,frame.Scalars)||
                !ReferenceEquals(middle.Textures,frame.TextureParameters)||
                !ReferenceEquals(middle.Parameters.RequirePreparedFactories().Lens,init))
                throw new InvalidDataException("Lens continuation needs this exact completed Middle and prior all-factory initialization.");
            foreach(var alias in init.TextureAliases)
            {
                var from=frame.TextureParameters.Require(alias.SourceParameterIndex).Reference;
                var to=frame.TextureParameters.Require(alias.DestinationParameterIndex).Reference;
                if(from.PhysicalAllocationParameterIndex!=to.PhysicalAllocationParameterIndex||from.InstanceIndex!=to.InstanceIndex||
                    from.ProducerOwnerIdentity!=to.ProducerOwnerIdentity)
                    throw new InvalidDataException("Lens all-Init alias changed before its real source family continuation.");
            }
        }
        if(frame.Scalars.FrameNumber!=init.FrameNumber||frame.Lens.FrameNumber!=init.FrameNumber||
            init.FrameNumber!=frame.TextureParameters.FrameNumber||init.SourceSha256!=Bank.Source.Source.Sha256||
            init.OwnerIdentity!=Bank.Source.ContextOwnerIdentity||frame.Scalars.OwnerIdentity!=init.OwnerIdentity||
            frame.TextureParameters.SourceSha256!=init.SourceSha256||frame.TextureParameters.ContextOwnerIdentity!=init.OwnerIdentity||
            frame.Lens.Source.ExecutableLocale!=locale)
            throw new InvalidDataException("Lens owner current scalar/pins/init/texture state/native sampler locale differs.");
        foreach(var cb in init.ConstantBuffers)
            if(!frame.ActualConstantBufferInstances.TryGetValue(cb.Declaration.Index,out int side)||
                !Bank.ConstantBuffers.TryGetValue(cb.Declaration.Index,out var sides)||(uint)side>=sides.Count)
                throw new InvalidDataException("Lens source constant buffer has no actual selected allocation side.");
        foreach(int index in new[]{0,1,2,3})
            if(!frame.ActualBufferInstances.TryGetValue(index,out int side)||!Bank.Buffers.TryGetValue(index,out var sides)||(uint)side>=sides.Count)
                throw new InvalidDataException("Lens source buffer has no actual selected allocation side.");
    }

    public ValueTask DisposeAsync()
    {
        lock(lifetime)
        {
            if(disposal is not null)return new(disposal);retiring=true;
            Task pending=accepted==0?Task.CompletedTask:(drained=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            return new(disposal=RetireAsync(pending));
        }
    }
    private async Task RetireAsync(Task pending)
    {
        await pending;await serial.WaitAsync();
        try{await Compute.DisposeAsync();await Samplers.DisposeAsync();LastCompleted=null;}
        finally{serial.Release();}
    }
}
