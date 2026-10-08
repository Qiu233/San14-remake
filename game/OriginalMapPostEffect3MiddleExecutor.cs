using San14.Data;

/// <summary>Source pass8..19 execution, after Shaft and before Lens. Caller
/// leases retain the actual preceding image and sampler producers. The owner
/// uploads original complete CBs and invokes the actual selected compute
/// program; it never grants image readiness from dispatch alone.</summary>
public sealed class OriginalMapPostEffect3MiddleExecutor
{
    public OriginalMapPostEffect3ComputeExecutor Compute{get;}
    public OriginalMapPostEffect3OwnedFreshCameraFrame Parameters{get;}
    public OriginalMapPostEffect3TextureParameterState Textures{get;}
    public OriginalMapPostEffect3MiddleAllocationSides AllocationSides{get;}
    private readonly SemaphoreSlim serial=new(1,1);
    private readonly List<OriginalMapPostEffect3MiddlePassResult> results=[];
    private readonly HashSet<int> uploaded=[];
    private int nextPass=8;
    private bool failed;

    private OriginalMapPostEffect3MiddleExecutor(OriginalMapPostEffect3ComputeExecutor compute,
        OriginalMapPostEffect3OwnedFreshCameraFrame parameters,
        OriginalMapPostEffect3TextureParameterState textures,
        OriginalMapPostEffect3MiddleAllocationSides sides)
        =>(Compute,Parameters,Textures,AllocationSides)=(compute,parameters,textures,sides);

    /// <summary>Factory aliases must already have been copied in original
    /// all-Init order. This executor deliberately does not recopy an alias
    /// after preceding passes have changed their source slots.</summary>
    public static OriginalMapPostEffect3MiddleExecutor Create(
        OriginalMapPostEffect3ComputeExecutor compute,
        OriginalMapPostEffect3OwnedFreshCameraFrame parameters,
        OriginalMapPostEffect3TextureParameterState textures,
        OriginalMapPostEffect3FreshOwnedAllocationSides sides)
    {
        ArgumentNullException.ThrowIfNull(compute);ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(textures);ArgumentNullException.ThrowIfNull(sides);
        return CreateWithCurrentSides(compute,parameters,textures,
            OriginalMapPostEffect3MiddleAllocationSides.FromFresh(parameters,sides));
    }

    /// <summary>Use the actual retained invocation selections after its real
    /// factory initialization; constructor-zero selections are not reused.</summary>
    public static OriginalMapPostEffect3MiddleExecutor Create(
        OriginalMapPostEffect3ComputeExecutor compute,
        OriginalMapPostEffect3OwnedFreshCameraFrame parameters,
        OriginalMapPostEffect3TextureParameterState textures,
        OriginalMapPostEffect3RetainedModuleInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(compute);ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(textures);ArgumentNullException.ThrowIfNull(invocation);
        return CreateWithCurrentSides(compute,parameters,textures,
            OriginalMapPostEffect3MiddleAllocationSides.FromInvocation(parameters,invocation));
    }

    private static OriginalMapPostEffect3MiddleExecutor CreateWithCurrentSides(
        OriginalMapPostEffect3ComputeExecutor compute,OriginalMapPostEffect3OwnedFreshCameraFrame parameters,
        OriginalMapPostEffect3TextureParameterState textures,OriginalMapPostEffect3MiddleAllocationSides sides)
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
    public IReadOnlyList<OriginalMapPostEffect3MiddlePassResult> CompletedPasses=>results.AsReadOnly();

    /// <summary>Disabled source Before consumes the pass without inputs or a
    /// dispatch. Enabled source Before requires actual same-frame input leases.
    /// A failed attempt cannot be resumed using partial/stale image contents.</summary>
    public async Task<OriginalMapPostEffect3MiddlePassResult> ExecuteNextAsync(
        OriginalMapPostEffect3ComputeFrameInputs? actualInputs)
    {
        await serial.WaitAsync();
        try
        {
            if(failed)throw new InvalidOperationException("Middle execution failed; discard this source attempt.");
            if(nextPass>19)throw new InvalidOperationException("The original middle sequence has already finished.");
            var recipe=OriginalMapPostEffect3MiddleExecutionRecipe.Prepare(Parameters,nextPass);
            AllocationSides.ValidateFor(Compute.Bank.Source);
            OriginalMapPostEffect3ComputeCompletedPass? dispatch=null;
            IReadOnlyList<IOriginalMapPostEffect3ComputeMipTextureLease> readableOutputs=
                Array.Empty<IOriginalMapPostEffect3ComputeMipTextureLease>();
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
                readableOutputs=Array.AsReadOnly(OriginalMapPostEffect3MiddleWriteCoverage.Prepare(Compute.Bank.Source,recipe)
                    .Select(coverage=>Compute.Bank.PublishMiddleTextureCoverage(coverage,Compute,dispatch)).ToArray());
            }
            else if(actualInputs is not null)
                throw new InvalidDataException("Source-disabled middle pass must not manufacture GPU inputs.");
            recipe.RequireCurrent();AllocationSides.ValidateFor(Compute.Bank.Source);
            var result=new OriginalMapPostEffect3MiddlePassResult(recipe,dispatch){ReadableOutputs=readableOutputs};
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

public sealed record OriginalMapPostEffect3MiddlePassResult(
    OriginalMapPostEffect3MiddleExecutionRecipe Recipe,OriginalMapPostEffect3ComputeCompletedPass? ActualDispatch)
{
    public IReadOnlyList<IOriginalMapPostEffect3ComputeMipTextureLease> ReadableOutputs{get;init;}=
        Array.Empty<IOriginalMapPostEffect3ComputeMipTextureLease>();
    public bool CommitsFunctorHistory=>false;
    public bool EstablishesCompleteResourceContents=>false;
}
