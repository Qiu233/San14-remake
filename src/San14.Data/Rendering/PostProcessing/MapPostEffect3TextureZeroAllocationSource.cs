namespace San14.Data;

/// <summary>Source receipt for a newly constructed native RT parameter bank
/// using the explicitly selected original default zero allocator. It does not
/// clear a retained bank, initialize GPU contents, or imply any later setters.
/// Every original external RT record begins with null first/second wrappers.</summary>
public sealed class LoadedMapPostEffect3TextureZeroAllocationSource
{
    private readonly LoadedMapPostEffect3ScalarZeroAllocationSource allocator;
    internal LoadedMapPostEffect3TextureZeroAllocationSource(
        LoadedMapPostEffect3ScalarZeroAllocationSource defaultAllocator,string evidence)
        =>(allocator,SourceEvidence)=(defaultAllocator,evidence);
    public string Locale=>allocator.Locale;
    public string SourceEvidence{get;}
    public int NativeTextureRecordByteCount=>72;
    public int NativeBufferRecordByteCount=>40;
    public bool InitializesGpuContents=>false;
    public bool AppliesToRetainedTextureBank=>false;

    public void ValidateFor(MapPostEffect3AllocationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        MapPostEffect3ParameterState.ValidateSource(plan.Source);
        if(Locale is not ("TC" or "SC")||plan.ResizeOnly||plan.TextureParameters.Count!=89||plan.BufferParameters.Count!=9||
            plan.ConstantBufferParameters.Count!=26||plan.TextureParameters.Select(t=>t.NameHash).Distinct().Count()!=89)
            throw new NotSupportedException("Fresh RT zero receipt requires the verified SC/TC non-resize original counted module.");
        // Native.. allocates the72B container header plus one
        //72B record per texture and40B records per buffer/constant-buffer.
        if(ReadNativeAllocationByteCountUnchecked(plan)!=7880)
            throw new InvalidDataException("Original fresh RT record allocation extent differs.");
        if(plan.TextureParameters[10].NameHash!=MapPostEffect3ParameterState.PrimaryNameHash("TAAOutputColor")||
            plan.TextureParameters[10].NativeFlags!=1||
            plan.TextureParameters[10].Ownership!=MapPostEffect3AllocationOwnership.ExternalParameterSlot)
            throw new InvalidDataException("Original fresh TAAOutputColor10 is not the unallocated external flags1 record.");
    }

    public int ReadNativeAllocationByteCount(MapPostEffect3AllocationPlan plan)
    {ValidateFor(plan);return ReadNativeAllocationByteCountUnchecked(plan);}

    /// <summary>The original allocation loop skips each flags-bit2-clear row.
    /// These slots therefore still contain the allocator's actual null pointer
    /// values before Apply/factory writers. The caller owns ordered mutation.</summary>
    public IReadOnlyList<int> ReadFreshZeroExternalTextureIndices(MapPostEffect3AllocationPlan plan)
    {
        ValidateFor(plan);
        return Array.AsReadOnly(plan.TextureParameters.Where(t=>(t.NativeFlags&4)==0).Select(t=>t.Index).ToArray());
    }

    /// <summary>Initial selected instance immediately after new owned texture
    /// allocation. Native pair writers preserve the zero selector byte0 while
    /// setting byte1 to pair presence. No retained/history side is inferred.</summary>
    public IReadOnlyDictionary<int,int> ReadFreshOwnedTextureAllocationSides(MapPostEffect3AllocationPlan plan)
    {
        ValidateFor(plan);
        if(plan.TextureAllocations.Count!=62||plan.TextureAllocations.Any(a=>!a.CreateInThisInvocation||
            a.Parameter.Ownership!=MapPostEffect3AllocationOwnership.OwnedDeclaration||
            a.InstanceCount!=(a.Parameter.HasTwoRuntimeInstances?2:1)))
            throw new InvalidDataException("Fresh owned texture side receipt requires the actual complete source allocation invocation.");
        return new System.Collections.ObjectModel.ReadOnlyDictionary<int,int>(
            plan.TextureAllocations.ToDictionary(a=>a.Parameter.Index,_=>0));
    }

    /// <summary>The original40B CB records share the whole-block zero scope.
    /// Allocation writes the two resource pointers and pair flag, not selector0.</summary>
    public IReadOnlyDictionary<int,int> ReadFreshOwnedConstantBufferAllocationSides(MapPostEffect3AllocationPlan plan)
    {
        ValidateFor(plan);
        if(plan.ConstantBufferAllocations.Count!=26||plan.ConstantBufferAllocations.Any(a=>!a.CreateInThisInvocation||
            a.Parameter.Ownership!=MapPostEffect3AllocationOwnership.OwnedDeclaration||
            a.InstanceCount!=(a.Parameter.HasTwoRuntimeInstances?2:1)))
            throw new InvalidDataException("Fresh CB side receipt requires the actual complete source allocation invocation.");
        return new System.Collections.ObjectModel.ReadOnlyDictionary<int,int>(
            plan.ConstantBufferAllocations.ToDictionary(a=>a.Parameter.Index,_=>0));
    }

    public MapPostEffect3FreshOwnedAllocationSides SelectFreshOwnedSides(MapPostEffect3AllocationPlan plan)
        =>new(plan,ReadFreshOwnedTextureAllocationSides(plan),ReadFreshOwnedConstantBufferAllocationSides(plan),
            ReadFreshOwnedBufferAllocationSides(plan),
            SourceEvidence+";fresh-owned-allocation="+plan.ContextOwnerIdentity+
            "-two-wrapper-writers-preserve-record-byte0;83-CB-pair-writer-preserves-record-byte0"+
            "byte1=pair-presence/not-selector;selected-first-instance0-before-factory/history-writers");

    /// <summary>The same original default allocator initializes each40B buffer
    /// record's selector byte0. This applies only to a fresh owned invocation,
    /// before any source history flip, and does not initialize buffer bytes.</summary>
    public IReadOnlyDictionary<int,int> ReadFreshOwnedBufferAllocationSides(MapPostEffect3AllocationPlan plan)
    {
        ValidateFor(plan);
        if(plan.BufferAllocations.Count!=8||plan.BufferAllocations.Any(a=>!a.CreateInThisInvocation||
            a.Parameter.Ownership!=MapPostEffect3AllocationOwnership.OwnedDeclaration||
            a.InstanceCount!=(a.Parameter.HasTwoRuntimeInstances?2:1)))
            throw new InvalidDataException("Fresh buffer side receipt requires the actual complete original40B owned allocation records.");
        return new System.Collections.ObjectModel.ReadOnlyDictionary<int,int>(
            plan.BufferAllocations.ToDictionary(a=>a.Parameter.Index,_=>0));
    }
    private static int ReadNativeAllocationByteCountUnchecked(MapPostEffect3AllocationPlan plan)
        =>checked((plan.TextureParameters.Count+1)*72+
            (plan.BufferParameters.Count+plan.ConstantBufferParameters.Count)*40);
}

/// <summary>Immutable source side recipe. Fresh allocation and retained F1
/// snapshots carry distinct provenance; neither grants GPU contents.</summary>
public sealed class MapPostEffect3FreshOwnedAllocationSides
{
    private readonly MapPostEffect3AllocationPlan plan;
    internal MapPostEffect3FreshOwnedAllocationSides(MapPostEffect3AllocationPlan source,
        IReadOnlyDictionary<int,int> textures,IReadOnlyDictionary<int,int> constantBuffers,
        IReadOnlyDictionary<int,int> buffers,string evidence,bool afterHistoryFlip=false)
        =>(plan,TextureInstances,ConstantBufferInstances,BufferInstances,SourceEvidence,AppliesAfterHistoryFlip)=
            (source,textures,constantBuffers,buffers,evidence,afterHistoryFlip);
    public IReadOnlyDictionary<int,int> TextureInstances{get;}
    public IReadOnlyDictionary<int,int> ConstantBufferInstances{get;}
    public IReadOnlyDictionary<int,int> BufferInstances{get;}
    public string SourceEvidence{get;}
    public string ContextOwnerIdentity=>plan.ContextOwnerIdentity;
    public bool ProvesGpuContentsInitialized=>false;
    public bool AppliesAfterHistoryFlip{get;}
    /// <summary>Current source selectors after a returned F1 invocation. This
    /// keeps the original allocation and does not grant any GPU contents.</summary>
    public static MapPostEffect3FreshOwnedAllocationSides CaptureFunctor1Current(
        MapPostEffect3AllocationPlan allocation,MapPostEffect3Functor1ModuleInvocation invocation)
    {
        invocation.RequireCurrent();
        if(!ReferenceEquals(invocation.Owner.Allocation,allocation))
            throw new InvalidDataException("Current F1 selectors belong to another actual allocation.");
        return new(allocation,invocation.TextureInstances,invocation.ConstantBufferInstances,
            invocation.BufferInstances,invocation.Begin.SourceEvidence+";actual-F1-current-side-tables/no-pixels",invocation.Begin.Counter>1);
    }
    public void ValidateFor(MapPostEffect3AllocationPlan allocation)
    {
        if(!ReferenceEquals(plan,allocation)||allocation.ResizeOnly)
            throw new InvalidDataException("Fresh side receipt does not belong to this original new allocation owner.");
    }
}

public static class MapPostEffect3TextureZeroAllocationSource
{
    public static LoadedMapPostEffect3TextureZeroAllocationSource Load(
        GameResourceRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        if(locale is not ("TC" or "SC"))throw new NotSupportedException("Fresh texture-bank allocation caller windows are verified for SC/TC.");
        var allocator=MapPostEffect3ScalarZeroAllocationSource.Load(repository,locale);
        var native=RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale);
        return RenderSamplerSourceAdapters.ReadPostEffect3TextureZeroAllocationSource(native,allocator);
    }
}

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3TextureZeroAllocationSource ReadPostEffect3TextureZeroAllocationSource(
        LoadedDeferredSamplerNativeSource native,LoadedMapPostEffect3ScalarZeroAllocationSource allocator)
    {
        if(native.Locale is not ("TC" or "SC")||allocator.Locale!=native.Locale)
            throw new NotSupportedException("Texture records require the positively joined original TC default zero allocator.");

        // Complete texture/buffer/CB allocation loops plus the four actual
        // texture pair/single setters: none writes record selector byte0.

        return new(allocator,native.ResourceUri+$";{native.Locale}:paired-source-profile;"+";"+allocator.SourceEvidence+
            ";metadata/-RTbank;-explicit-owned-default-allocator"+
            "-default-v30-whole-block=(textureCount+1)*72+(bufferCount+CBcount)*40"+
            "-container18=RTrecord-array;whole-block-zero-before-metadata"+
            "-flags-bit2-clear-skip;external-records-remain-zero-before-current-writers"+
            "26B-side-selector-zero->/295-first-wrapper-null;no-GPU-content-initialization;no-retained-bank-reset");
    }
}
