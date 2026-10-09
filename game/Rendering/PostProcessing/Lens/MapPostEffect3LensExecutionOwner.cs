using Godot;
using San14.Data;

/// <summary>Current source resources outside the Lens-owned writes. The caller
/// retains their actual producers; this interface supplies no fallback texture,
/// sampler, primitive or raster. Component fixtures must identify themselves.
/// </summary>
public interface IMapPostEffect3LensCurrentSourceInputs
{
    void RequireCurrent();
    IMapPostEffect3ComputeTextureLease RequireReadableTexture(
        MapPostEffect3LensExecutionRecipe recipe,int parameterIndex,
        MapPostEffect3ResolvedTextureParameter actualReference);
    IMapPostEffect3ComputeSamplerLease RequireWrapperSampler(
        MapPostEffect3LensExecutionRecipe recipe,int nativeStageOrdinal,uint register);
    MapPostEffect3LensGhostFrameInputs PrepareGhostDraw(
        MapPostEffect3LensExecutionRecipe recipe,IMapPostEffect3ComputeBufferLease actualSprite,
        IMapPostEffect3ComputeTextureLease actualStarBurst);
}

public sealed record MapPostEffect3LensExecutionFrame(
    MapPostEffect3ParameterState Scalars,MapPostEffect3LensPresetParameterOwner Lens,
    MapPostEffect3ParameterLensFlareInitialization Initialization,
    MapPostEffect3TextureParameterState TextureParameters,
    IReadOnlyDictionary<int,int> ActualConstantBufferInstances,
    IReadOnlyDictionary<int,int> ActualBufferInstances,IMapPostEffect3LensCurrentSourceInputs SourceInputs)
{
    public MapPostEffect3MiddleExecutor? ActualFinishedMiddle{get;init;}
}

public sealed record MapPostEffect3LensSubmittedPass(int SourcePassIndex,bool SourceBeforeAllowsExecution,
    MapPostEffect3ComputeCompletedPass? Compute,MapPostEffect3LensGhostCompletedDraw? Ghost,
    string SourceEvidence);
public sealed record MapPostEffect3LensCompletedExecution(long FrameNumber,string ContextOwnerIdentity,
    IReadOnlyList<MapPostEffect3LensSubmittedPass> OrderedPasses,
    IReadOnlyList<MapPostEffect3ResolvedTextureParameter> OutputAliases,string SourceEvidence)
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
public sealed partial class MapPostEffect3LensExecutionOwner:IAsyncDisposable
{
    public PostProcessResourceBank Bank{get;}
    public MapPostEffect3ProgramLibrary Programs{get;}
    public MapPostEffect3ComputeExecutor Compute{get;}
    public MapPostEffect3LensGhostExecutor Ghost{get;}
    public MapPostEffect3SourceSamplerOwner Samplers{get;}
    public LoadedMapPostEffect3PassRegisterResetSource NativeRegisterResetSource{get;}
    public IReadOnlyList<MapPostEffect3LensSubmittedPass> LastSubmittedPrefix{get;private set;}=[];
    public MapPostEffect3LensCompletedExecution? LastCompleted{get;private set;}
    private readonly SemaphoreSlim serial=new(1,1);
    private readonly object lifetime=new();private bool retiring;private int accepted;
    private TaskCompletionSource? drained;private Task? disposal;
    private readonly string locale;
    private MapPostEffect3LensExecutionOwner(MapPostEffect3ProgramLibrary programs,
        PostProcessResourceBank bank,MapPostEffect3SourceSamplerOwner samplers,string nativeLocale,
        LoadedMapPostEffect3PassRegisterResetSource nativeRegisterResetSource)
        =>(Programs,Bank,Samplers,locale,NativeRegisterResetSource,Compute,Ghost)=(programs,bank,samplers,nativeLocale,nativeRegisterResetSource,
            MapPostEffect3ComputeExecutor.Create(programs,bank),MapPostEffect3LensGhostExecutor.Create(programs,bank));

    public static async Task<MapPostEffect3LensExecutionOwner> CreateBorrowedAsync(
        MapPostEffect3ProgramLibrary programs,PostProcessResourceBank bank,
        LoadedDeferredSamplerNativeSource actualManagerNativeSource,
        LoadedMapPostEffect3PassRegisterResetSource actualNativeRegisterResetSource)
    {
        ArgumentNullException.ThrowIfNull(programs);ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(actualManagerNativeSource);
        ArgumentNullException.ThrowIfNull(actualNativeRegisterResetSource);
        if(programs.SourceSha256!=bank.Source.Source.Sha256||bank.Source.Source.ReadFunctorPasses(0)
            .Where(p=>p.Index is >=20 and <=43).Count()!=24)
            throw new InvalidDataException("Lens owner requires this exact source Functor0/program/bank identity.");
        var samplers=await MapPostEffect3SourceSamplerOwner.BuildAsync(actualManagerNativeSource,bank.Source.ContextOwnerIdentity);
        return new(programs,bank,samplers,actualManagerNativeSource.Locale,actualNativeRegisterResetSource);
    }

    public Task<MapPostEffect3LensCompletedExecution> ExecuteAsync(MapPostEffect3LensExecutionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ValidateFrame(frame);
        lock(lifetime){ObjectDisposedException.ThrowIf(retiring,this);accepted++;}
        return ExecuteAcceptedAsync(frame with{
            ActualConstantBufferInstances=new Dictionary<int,int>(frame.ActualConstantBufferInstances),
            ActualBufferInstances=new Dictionary<int,int>(frame.ActualBufferInstances)});
    }

    private void ValidateFrame(MapPostEffect3LensExecutionFrame frame)
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
