using San14.Data;

/// <summary>Source pass8..19 execution, after Shaft and before Lens. Caller
/// leases retain the actual preceding image and sampler producers. The owner
/// uploads original complete CBs and invokes the actual selected compute
/// program; it never grants image readiness from dispatch alone.</summary>
public sealed class MapPostEffect3MiddleExecutor
{
    public MapPostEffect3ComputeExecutor Compute{get;}
    public MapPostEffect3OwnedFreshCameraFrame Parameters{get;}
    public MapPostEffect3TextureParameterState Textures{get;}
    public MapPostEffect3MiddleAllocationSides AllocationSides{get;}
    private readonly SemaphoreSlim serial=new(1,1);
    private readonly List<MapPostEffect3MiddlePassResult> results=[];
    private readonly HashSet<int> uploaded=[];
    private int nextPass=8;
    private bool failed;

    private MapPostEffect3MiddleExecutor(MapPostEffect3ComputeExecutor compute,
        MapPostEffect3OwnedFreshCameraFrame parameters,
        MapPostEffect3TextureParameterState textures,
        MapPostEffect3MiddleAllocationSides sides)
        =>(Compute,Parameters,Textures,AllocationSides)=(compute,parameters,textures,sides);

    /// <summary>Factory aliases must already have been copied in original
    /// all-Init order. This executor deliberately does not recopy an alias
    /// after preceding passes have changed their source slots.</summary>
    public static MapPostEffect3MiddleExecutor Create(
        MapPostEffect3ComputeExecutor compute,
        MapPostEffect3OwnedFreshCameraFrame parameters,
        MapPostEffect3TextureParameterState textures,
        MapPostEffect3FreshOwnedAllocationSides sides)
    {
        ArgumentNullException.ThrowIfNull(compute);ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(textures);ArgumentNullException.ThrowIfNull(sides);
        return CreateWithCurrentSides(compute,parameters,textures,
            MapPostEffect3MiddleAllocationSides.FromFresh(parameters,sides));
    }

    /// <summary>Use the actual retained invocation selections after its real
    /// factory initialization; constructor-zero selections are not reused.</summary>
    public static MapPostEffect3MiddleExecutor Create(
        MapPostEffect3ComputeExecutor compute,
        MapPostEffect3OwnedFreshCameraFrame parameters,
        MapPostEffect3TextureParameterState textures,
        MapPostEffect3RetainedModuleInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(compute);ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(textures);ArgumentNullException.ThrowIfNull(invocation);
        return CreateWithCurrentSides(compute,parameters,textures,
            MapPostEffect3MiddleAllocationSides.FromInvocation(parameters,invocation));
    }

    private static MapPostEffect3MiddleExecutor CreateWithCurrentSides(
        MapPostEffect3ComputeExecutor compute,MapPostEffect3OwnedFreshCameraFrame parameters,
        MapPostEffect3TextureParameterState textures,MapPostEffect3MiddleAllocationSides sides)
    {
        parameters.RequireCurrent();sides.ValidateFor(compute.Bank.Source);
        if(!ReferenceEquals(parameters.Allocation,compute.Bank.Source)||
            textures.FrameNumber!=parameters.FrameNumber||textures.SourceSha256!=parameters.Allocation.Source.Sha256||
            textures.ContextOwnerIdentity!=parameters.OwnerIdentity)
            throw new InvalidDataException("Middle execution needs the actual initialized module frame and pointer table.");
        return new(compute,parameters,textures,sides);
    }

    public int NextSourcePassIndex=>nextPass;
    public bool FinishedMiddlePasses=>nextPass==20&&!failed;
    public IReadOnlyList<MapPostEffect3MiddlePassResult> CompletedPasses=>results.AsReadOnly();

    /// <summary>Disabled source Before consumes the pass without inputs or a
    /// dispatch. Enabled source Before requires actual same-frame input leases.
    /// A failed attempt cannot be resumed using partial/stale image contents.</summary>
    public async Task<MapPostEffect3MiddlePassResult> ExecuteNextAsync(
        MapPostEffect3ComputeFrameInputs? actualInputs)
    {
        await serial.WaitAsync();
        try
        {
            if(failed)throw new InvalidOperationException("Middle execution failed; discard this source attempt.");
            if(nextPass>19)throw new InvalidOperationException("The original middle sequence has already finished.");
            var recipe=MapPostEffect3MiddleExecutionRecipe.Prepare(Parameters,nextPass);
            AllocationSides.ValidateFor(Compute.Bank.Source);
            MapPostEffect3ComputeCompletedPass? dispatch=null;
            IReadOnlyList<IMapPostEffect3ComputeMipTextureLease> readableOutputs=
                Array.Empty<IMapPostEffect3ComputeMipTextureLease>();
            if(recipe.ExecutesPass)
            {
                if(actualInputs is null||actualInputs.FrameNumber!=Parameters.FrameNumber||
                    !ReferenceEquals(actualInputs.TextureParameters,Textures)||actualInputs.LensExecution is not null)
                    throw new InvalidDataException("Enabled middle pass requires its actual same-frame pointer table and compute inputs.");
                var cbs=recipe.ConstantBufferBindings.Select(b=>b.ConstantBuffer).DistinctBy(b=>b.Declaration.Index).ToArray();
                var sides=cbs.ToDictionary(b=>b.Declaration.Index,b=>AllocationSides.ConstantBufferInstances[b.Declaration.Index]);
                if(!actualInputs.ConstantBufferInstances.OrderBy(p=>p.Key).SequenceEqual(sides.OrderBy(p=>p.Key)))
                    throw new InvalidDataException("Middle pass CB sides differ from its actual source binding rows.");
                foreach(var cb in cbs)
                    if(uploaded.Add(cb.Declaration.Index))
                        await Compute.Bank.UploadCompleteConstantBufferAsync(cb,sides[cb.Declaration.Index]);
                recipe.RequireCurrent();AllocationSides.ValidateFor(Compute.Bank.Source);
                dispatch=await Compute.DispatchAsync(nextPass,actualInputs);
                AllocationSides.ValidateFor(Compute.Bank.Source);
                if(!ReferenceEquals(Compute.LastCompletedPass,dispatch)||dispatch.SourcePassIndex!=nextPass||
                    dispatch.FrameNumber!=Parameters.FrameNumber||dispatch.ContextOwnerIdentity!=Parameters.OwnerIdentity)
                    throw new InvalidDataException("Actual middle dispatch did not publish this source pass receipt.");
                // Each positive source store domain is joined to this exact
                // completed dispatch, physical storage and writer revision.
                // Later writes invalidate the resulting leases automatically.
                readableOutputs=Array.AsReadOnly(MapPostEffect3MiddleWriteCoverage.Prepare(Compute.Bank.Source,recipe)
                    .Select(coverage=>Compute.Bank.PublishMiddleTextureCoverage(coverage,Compute,dispatch)).ToArray());
            }
            else if(actualInputs is not null)
                throw new InvalidDataException("Source-disabled middle pass must not manufacture GPU inputs.");
            recipe.RequireCurrent();AllocationSides.ValidateFor(Compute.Bank.Source);
            var result=new MapPostEffect3MiddlePassResult(recipe,dispatch){ReadableOutputs=readableOutputs};
            results.Add(result);nextPass++;
            return result;
        }
        catch{failed=true;throw;}
        finally{serial.Release();}
    }

    public void RequireFinishedMiddlePasses()
    {
        Parameters.RequireCurrent();AllocationSides.ValidateFor(Compute.Bank.Source);
        if(!FinishedMiddlePasses||results.Count!=12||
            !results.Select(r=>r.Recipe.SourcePassIndex).SequenceEqual(Enumerable.Range(8,12)))
            throw new InvalidOperationException("Lens must follow all original middle passes, including Before-skipped passes.");
    }
}

public sealed record MapPostEffect3MiddlePassResult(
    MapPostEffect3MiddleExecutionRecipe Recipe,MapPostEffect3ComputeCompletedPass? ActualDispatch)
{
    public IReadOnlyList<IMapPostEffect3ComputeMipTextureLease> ReadableOutputs{get;init;}=
        Array.Empty<IMapPostEffect3ComputeMipTextureLease>();
    public bool CommitsFunctorHistory=>false;
    public bool EstablishesCompleteResourceContents=>false;
}
