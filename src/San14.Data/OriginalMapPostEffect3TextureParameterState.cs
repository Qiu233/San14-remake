namespace San14.Data;

/// <summary>A native texture-table pointer identity, separate from GPU contents.
/// An alias copies this value when its source setter runs; it is not a live
/// name redirection. External contents still require their producer's lease.</summary>
public sealed record OriginalMapPostEffect3TextureParameterReference(
    int? PhysicalAllocationParameterIndex,int InstanceIndex,string ProducerOwnerIdentity,
    string SourceEvidence)
{
    // copies the wrapper dimensions; replaces only its views.
    // These are physical subresources of the existing allocation, not another
    // allocation or a claim that the selected pixels have been initialized.
    public uint? NativeFirstMipLevel{get;init;}
    public uint? NativeMipLevelCount{get;init;}
    public bool HasSourceSelectedMipView=>NativeFirstMipLevel is not null&&NativeMipLevelCount is not null;
}

public sealed record OriginalMapPostEffect3ResolvedTextureParameter(long FrameNumber,
    string ContextOwnerIdentity,int ParameterIndex,long SlotRevision,
    OriginalMapPostEffect3TextureParameterReference Reference);

/// <summary>Owned runtime texture-table state for the actual counted module.
/// Keeps native ordered pointer assignments, including null and aliases.
/// It neither initializes pixels nor evaluates hooks or flips buffer history.</summary>
public sealed partial class OriginalMapPostEffect3TextureParameterState
{
    private readonly OriginalMapPostEffect3AllocationPlan allocation;
    private readonly OriginalMapPostEffect3ParameterState names;
    private readonly OriginalMapPostEffect3TextureParameterReference?[] references;
    private readonly long[] revisions;
    private readonly bool[] known;
    private ExposureScaleSwap? exposureSwap;
    public long FrameNumber{get;}
    public OriginalMapPostEffect3AllocationPlan Allocation=>allocation;
    public string ContextOwnerIdentity=>allocation.ContextOwnerIdentity;
    public string SourceSha256=>allocation.Source.Sha256;

    private OriginalMapPostEffect3TextureParameterState(OriginalMapPostEffect3AllocationPlan plan,
        long frame,OriginalMapPostEffect3ParameterState scalarNames)
    {
        allocation=plan;FrameNumber=frame;names=scalarNames;
        references=new OriginalMapPostEffect3TextureParameterReference?[plan.TextureParameters.Count];
        revisions=new long[references.Length];
        known=new bool[references.Length];
    }

    /// <summary>The caller supplies every actual selected allocation side.
    /// External slots remain unknown; allocated owners carry no readiness claim.</summary>
    public static OriginalMapPostEffect3TextureParameterState CreateFresh(
        OriginalMapPostEffect3AllocationPlan allocation,long frameNumber,
        IReadOnlyDictionary<int,int> actualAllocationSides,string allocationEvidence)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(actualAllocationSides);
        ArgumentException.ThrowIfNullOrWhiteSpace(allocationEvidence);
        if(frameNumber<=0)throw new InvalidDataException("Texture table needs an owned frame.");
        OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        int[] owned=allocation.TextureAllocations.Select(a=>a.Parameter.Index).Order().ToArray();
        if(!actualAllocationSides.Keys.Order().SequenceEqual(owned))
            throw new InvalidDataException("Texture table needs exactly the actual allocated sides.");
        var state=new OriginalMapPostEffect3TextureParameterState(allocation,frameNumber,
            OriginalMapPostEffect3ParameterState.CreateUninitialized(allocation.Source,frameNumber,allocation.ContextOwnerIdentity));
        foreach(var a in allocation.TextureAllocations)
        {
            int side=actualAllocationSides[a.Parameter.Index];
            if((uint)side>=a.InstanceCount)throw new InvalidDataException("Texture allocation side is outside the original declaration.");
            state.references[a.Parameter.Index]=new(a.Parameter.Index,side,
                allocation.ContextOwnerIdentity+$"/texture{a.Parameter.Index}/side{side}",allocationEvidence);
            state.known[a.Parameter.Index]=true;
        }
        return state;
    }

    public static OriginalMapPostEffect3TextureParameterState CreateFreshWithSourceZeroAllocator(
        OriginalMapPostEffect3AllocationPlan allocation,long frameNumber,
        IReadOnlyDictionary<int,int> actualAllocationSides,
        LoadedOriginalMapPostEffect3TextureZeroAllocationSource actualSource,string allocationEvidence)
    {
        ArgumentNullException.ThrowIfNull(actualSource);actualSource.ValidateFor(allocation);
        var state=CreateFresh(allocation,frameNumber,actualAllocationSides,allocationEvidence);
        foreach(int index in actualSource.ReadFreshZeroExternalTextureIndices(allocation))
        {
            state.references[index]=null;
            state.known[index]=true; // Construction, before later ordered setters.
        }
        return state;
    }

    /// <summary>Reuse the preceding native pointer table after the real module
    /// exit. Owned records resolve their newly selected wrapper; aliases keep
    /// their copied old pointer until a current setter runs. No pixel lease is
    /// transferred by this operation.</summary>
    public static OriginalMapPostEffect3TextureParameterState CreateRetainedForInvocation(
        OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3RetainedModuleInvocation actualInvocation,
        OriginalMapPostEffect3TextureParameterState previousActualTable)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(actualInvocation);
        ArgumentNullException.ThrowIfNull(previousActualTable);
        actualInvocation.ValidateRetainedParameterFrame(allocation,previousActualTable.FrameNumber);
        if(!ReferenceEquals(previousActualTable.allocation,allocation)||previousActualTable.exposureSwap is not null)
            throw new InvalidDataException("Retained texture table requires the same allocation and a restored exposure swap.");
        var state=new OriginalMapPostEffect3TextureParameterState(allocation,actualInvocation.FrameNumber,
            OriginalMapPostEffect3ParameterState.CreateUninitialized(allocation.Source,actualInvocation.FrameNumber,allocation.ContextOwnerIdentity));
        Array.Copy(previousActualTable.references,state.references,state.references.Length);
        Array.Copy(previousActualTable.known,state.known,state.known.Length);
        Array.Copy(previousActualTable.revisions,state.revisions,state.revisions.Length);
        foreach(var a in allocation.TextureAllocations)
        {
            int index=a.Parameter.Index;var old=previousActualTable.references[index];
            // Only the source-owned wrapper record is switched here. A native
            // pointer replacement needs its particular source writer modeled.
            if(!previousActualTable.known[index]||old is null||old.PhysicalAllocationParameterIndex!=index)
                throw new NotSupportedException("An owned texture record was replaced without a recovered retained-wrapper writer.");
            int side=actualInvocation.TextureInstances[index];
            if((uint)side>=a.InstanceCount)throw new InvalidDataException("Retained texture side exceeds its native allocation.");
            if(side!=old.InstanceIndex)
                state.Assign(index,new(index,side,allocation.ContextOwnerIdentity+$"/texture{index}/side{side}",
                    "TC; source exit wrapper selection"+actualInvocation.Begin.SourceEvidence));
        }
        return state;
    }

    public void BindExternal(int parameterIndex,OriginalMapPostEffect3ApplyView actualView,string writerEvidence)
    {
        CheckIndex(parameterIndex);ArgumentNullException.ThrowIfNull(actualView);
        ArgumentException.ThrowIfNullOrWhiteSpace(writerEvidence);
        if(allocation.TextureParameters[parameterIndex].Ownership!=OriginalMapPostEffect3AllocationOwnership.ExternalParameterSlot||
            actualView.FrameNumber!=FrameNumber||string.IsNullOrWhiteSpace(actualView.OwnerIdentity))
            throw new InvalidDataException("Texture input needs its actual external slot and same-frame owner.");
        Assign(parameterIndex,new(null,0,actualView.OwnerIdentity,writerEvidence));
    }

    public void ClearExternal(int parameterIndex,string writerEvidence)
    {
        CheckIndex(parameterIndex);ArgumentException.ThrowIfNullOrWhiteSpace(writerEvidence);
        if(allocation.TextureParameters[parameterIndex].Ownership!=OriginalMapPostEffect3AllocationOwnership.ExternalParameterSlot)
            throw new InvalidDataException("External cleanup cannot clear an allocated native texture owner.");
        Assign(parameterIndex,null);
    }

    /// <summary>The temporary replacement is allowed only on the actual
    /// owned EA2 slot. Its saved pointer is restored by , independently of
    /// the external parameter assignment API. This grants no pixel readiness.</summary>
    public ExposureScaleSwap BeginExposureScaleSwap(bool actualOutputExposureScalePredicate,
        OriginalMapPostEffect3ApplyView? actualExposureScale,string sourceEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);
        if(exposureSwap is not null)throw new InvalidOperationException("Exposure scale swap is already active.");
        int index=names.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,"EA2OutputExposureScale")
            ??throw new InvalidDataException("Original EA2 scale slot is absent.");
        CheckIndex(index);
        if(index!=61||allocation.TextureParameters[index].Ownership!=OriginalMapPostEffect3AllocationOwnership.OwnedDeclaration||!known[index])
            throw new InvalidDataException("Exposure swap requires the actual initialized owned EA2 slot.");
        var saved=references[index];
        var plan=OriginalMapPostEffect3ApplyRecipe.PrepareExposureScaleSwap(FrameNumber,
            actualOutputExposureScalePredicate,actualExposureScale,
            saved is null?null:new(FrameNumber,saved.ProducerOwnerIdentity),sourceEvidence);
        if(plan.BeforeExecution.Count!=0)
        {
            var write=plan.BeforeExecution.Single();
            Assign(index,new(null,0,write.View!.OwnerIdentity,write.SourceEvidence));
        }
        return exposureSwap=new(this,index,saved,revisions[index],plan);
    }

    public sealed class ExposureScaleSwap
    {
        private readonly OriginalMapPostEffect3TextureParameterState owner;
        private readonly int index;
        private readonly OriginalMapPostEffect3TextureParameterReference? saved;
        private readonly long revision;
        private readonly OriginalMapPostEffect3ApplyBindingPlan plan;
        private bool completed;
        internal ExposureScaleSwap(OriginalMapPostEffect3TextureParameterState owner,int index,
            OriginalMapPostEffect3TextureParameterReference? saved,long revision,OriginalMapPostEffect3ApplyBindingPlan plan)
        {this.owner=owner;this.index=index;this.saved=saved;this.revision=revision;this.plan=plan;}
        public void Restore()
        {
            if(completed||!ReferenceEquals(owner.exposureSwap,this)||owner.revisions[index]!=revision)
                throw new InvalidDataException("Exposure scale swap was consumed or its current slot changed.");
            if(plan.AfterExecution.Count!=0)
            {
                if(saved is null)
                    throw new InvalidDataException("Original EA2 saved pointer restore differs.");
                owner.Assign(index,saved);
            }
            completed=true;owner.exposureSwap=null;
        }
    }

    public void ApplyExternalWrites(IReadOnlyList<OriginalMapPostEffect3ApplyBinding> orderedWrites)
    {
        ArgumentNullException.ThrowIfNull(orderedWrites);
        // Validate the complete transaction before any native table assignment.
        var operations=orderedWrites.Select(w=>
        {
            int index=names.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,w.Name)
                ??throw new InvalidDataException("Apply external texture name is absent: "+w.Name);
            CheckIndex(index);ArgumentException.ThrowIfNullOrWhiteSpace(w.SourceEvidence);
            if(allocation.TextureParameters[index].Ownership!=OriginalMapPostEffect3AllocationOwnership.ExternalParameterSlot||
                w.View is{}v&&(v.FrameNumber!=FrameNumber||string.IsNullOrWhiteSpace(v.OwnerIdentity)))
                throw new InvalidDataException("Apply external texture write belongs to another frame/slot.");
            return (Index:index,Reference:w.View is{}view?
                new OriginalMapPostEffect3TextureParameterReference(null,0,view.OwnerIdentity,w.SourceEvidence):null);
        }).ToArray();
        foreach(var op in operations)Assign(op.Index,op.Reference);
    }

    public void ApplyLightShaftAlias(OriginalMapPostEffect3ParameterLightShaftInitialization source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.FrameNumber!=FrameNumber||source.OwnerIdentity!=ContextOwnerIdentity||source.SourceSha256!=SourceSha256)
            throw new InvalidDataException("LightShaft alias belongs to another module/frame/owner.");
        var a=source.OutputTextureAlias;
        Copy(a.SourceParameterIndex,a.TargetParameterIndex,a.SourceEvidence);
    }

    public void ApplyDepthOfFieldAlias(OriginalMapPostEffect3ParameterDepthOfFieldUpdate source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.FrameNumber!=FrameNumber||source.OwnerIdentity!=ContextOwnerIdentity)
            throw new InvalidDataException("DOF alias belongs to another frame/owner.");
        var a=source.TextureAlias;
        Copy(a.SourceParameterIndex,a.DestinationParameterIndex,"TC;source DOF init texture pointer copy");
    }

    public void ApplyLensFlareAliases(OriginalMapPostEffect3ParameterLensFlareInitialization source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.FrameNumber!=FrameNumber||source.OwnerIdentity!=ContextOwnerIdentity||source.SourceSha256!=SourceSha256)
            throw new InvalidDataException("LensFlare aliases belong to another module/frame/owner.");
        foreach(var a in source.TextureAliases)
        {
            CheckIndex(a.SourceParameterIndex);CheckIndex(a.DestinationParameterIndex);
            ArgumentException.ThrowIfNullOrWhiteSpace(a.SourceEvidence);
            if(!known[a.SourceParameterIndex])throw new NotSupportedException("LensFlare texture alias source is unknown.");
        }
        foreach(var a in source.TextureAliases)Copy(a.SourceParameterIndex,a.DestinationParameterIndex,a.SourceEvidence);
    }

    public void ApplyMotionBlurAlias(OriginalMapPostEffect3ParameterMotionBlurUpdate source,
        OriginalMapPostEffect3ParameterAliasMode actualAliasMode)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.FrameNumber!=FrameNumber||source.NextHistory.OwnerIdentity!=ContextOwnerIdentity||source.NextHistory.SourceSha256!=SourceSha256)
            throw new InvalidDataException("MotionBlur alias belongs to another module/frame/owner.");
        string expected=source.NextHistory.Enabled10?"MBOutputColorInternal":"MBInputColor";
        if(source.OutputTextureAliasSourceName!=expected)
            throw new InvalidDataException("MotionBlur alias disagrees with its actual native enable branch.");
        int from=names.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,expected,actualAliasMode)
            ??throw new InvalidDataException("MotionBlur alias input name is absent.");
        int to=names.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,"MBOutputColor",actualAliasMode)
            ??throw new InvalidDataException("MotionBlur alias output name is absent.");
        Copy(from,to,source.NextHistory.Enabled10?"TC;enabled MB pointer copy":"TC;disabled MB pointer copy");
    }

    public void ApplyPreparedFactoryAliases(OriginalMapPostEffect3OwnedFreshCameraFrame frame,
        OriginalMapPostEffect3ParameterAliasMode actualAliasMode)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if(frame.FrameNumber!=FrameNumber||frame.OwnerIdentity!=ContextOwnerIdentity||frame.Allocation.Source.Sha256!=SourceSha256)
            throw new InvalidDataException("Factory aliases belong to another owned Apply frame.");
        var init=frame.RequirePreparedFactories();
        ApplyLightShaftAlias(init.Shaft);
        ApplyDepthOfFieldAlias(init.DepthOfField);
        ApplyMotionBlurAlias(init.MotionBlur,actualAliasMode);
        ApplyLensFlareAliases(init.Lens);
    }

    public void ApplyEyeResourceInitializationAlias(LoadedOriginalMapPostEffect3EyeAdaptation2Source source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ApplyEyeAlias(source.ReadResourceInitializationAlias(allocation));
    }

    public void ApplyEyeInitializationAlias(OriginalMapPostEffect3EyeAdaptation2Frame frame)
    {
        RequireEyeFrame(frame);ApplyEyeAlias(frame.InitializeOutputAlias);
    }

    public void ApplyEyeBeforeAlias(OriginalMapPostEffect3EyeAdaptation2Frame frame,
        OriginalMapPostEffect3EyeBeforeBindings before)
    {
        RequireEyeFrame(frame);ArgumentNullException.ThrowIfNull(before);
        if(!frame.OrderedIterations.Contains(before.Iteration))
            throw new InvalidDataException("Eye Before alias belongs to another counted iteration.");
        if(before.PrefilterAliasChange is{} alias)ApplyEyeAlias(alias);
    }

    public void ApplyDepthOfField2Alias(OriginalMapPostEffect3ParameterDepthOfField2Update source)
    {
        ArgumentNullException.ThrowIfNull(source);source.RequireCurrentTextureTable(this);
        source.RequireInitializationPending();
        if(!ReferenceEquals(source.NextController.Allocation,allocation)||source.FrameNumber!=FrameNumber||
            source.ConstantBuffer.OwnerIdentity!=ContextOwnerIdentity||source.ConstantBuffer.SourceSha256!=SourceSha256||
            source.InitializeAliasSourceIndex!=0||source.InitializeAliasTargetIndex!=68)
            throw new InvalidDataException("DOF2 Init alias belongs to another current source frame/allocation.");
        Copy(source.InitializeAliasSourceIndex,source.InitializeAliasTargetIndex,source.SourceEvidence);
        source.AcceptActualInitialization(this);
    }

    public void ApplyComposite2Initialization(OriginalMapPostEffect3Composite2ExecutionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);frame.ValidateInitializationFor(this);
        // Source Prepare precedes Init. Copy the original selected pointers
        // and sides in native order, then capture the accepted destination
        // revisions. This changes no prepared CB22 bytes or GPU pixels.
        foreach(var alias in frame.InitializationAliases)
            Copy(alias.SourceParameterIndex,alias.DestinationParameterIndex,alias.SourceEvidence);
        frame.AcceptInitialization(this);
    }

    private void RequireEyeFrame(OriginalMapPostEffect3EyeAdaptation2Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);frame.RequireCurrent();
        if(!ReferenceEquals(frame.Allocation,allocation)||frame.FrameNumber!=FrameNumber||frame.OwnerIdentity!=ContextOwnerIdentity)
            throw new InvalidDataException("Eye alias belongs to another allocation/frame/controller.");
    }

    private void ApplyEyeAlias(OriginalMapPostEffect3EyeTextureAliasRecipe recipe)
    {
        CheckIndex(recipe.TargetParameterIndex);ArgumentException.ThrowIfNullOrWhiteSpace(recipe.SourceEvidence);
        if(allocation.TextureParameters[recipe.TargetParameterIndex].Ownership!=OriginalMapPostEffect3AllocationOwnership.ExternalParameterSlot)
            throw new InvalidDataException("Eye cloned pointer setter requires its counted alias slot.");
        if(recipe.SourceParameterIndex is{} from)
        {
            if(recipe.PhysicalTextureParameterIndex is not null||recipe.NativeMipLevel is not null)
                throw new InvalidDataException("Eye pointer copy cannot also select a physical mip.");
            Copy(from,recipe.TargetParameterIndex,recipe.SourceEvidence);return;
        }
        if(recipe.PhysicalTextureParameterIndex is not (85 or 86)||recipe.NativeMipLevel is not{} mip)
            throw new InvalidDataException("Eye clone requires the recovered Bloom85/86 physical wrapper.");
        int physical=recipe.PhysicalTextureParameterIndex.Value;
        var current=Require(physical).Reference;
        var shape=OriginalMapPostEffect3AllocationShapeReader.Read(allocation.TextureAllocations.Single(a=>a.Parameter.Index==physical));
        if(current.PhysicalAllocationParameterIndex!=physical||current.HasSourceSelectedMipView||
            mip<0||mip>=shape.MipLevels-1)
            throw new InvalidDataException("Eye clone differs from its actual full counted wrapper or source view array.");
        Assign(recipe.TargetParameterIndex,new(physical,current.InstanceIndex,
            current.ProducerOwnerIdentity+"/source-cloned-mip"+mip,recipe.SourceEvidence)
            {NativeFirstMipLevel=checked((uint)mip),NativeMipLevelCount=1});
    }

    public void ApplyDisabledTemporalAAAlias(OriginalMapPostEffect3TemporalAAFirstPassPreparation source,
        OriginalMapPostEffect3ParameterAliasMode actualAliasMode)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.FrameNumber!=FrameNumber||source.OwnerIdentity!=ContextOwnerIdentity||
            source.BeforeHook.NextHistory.SourceSha256!=SourceSha256||
            source.Disposition!=OriginalMapPostEffect3TemporalAAFirstPassDisposition.DisabledNativeTextureAliasRequiresProducer||
            !source.BeforeHook.RequiresDisabledTextureAlias)
            throw new InvalidDataException("TemporalAA alias requires the actual same-module disabled Before hook.");
        int? Find(string name)=>names.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,name,actualAliasMode);
        int? from=Find("TAAInputColor");
        if(from is{} primary)
        {
            CheckIndex(primary);
            if(!known[primary])throw new NotSupportedException("TemporalAA primary input pointer is unknown.");
            if(references[primary] is null)from=null;
        }
        from??=Find("InputColor");
        int? to=Find("TAAOutputColor")??Find("OutputTemporal");
        if(to is null)throw new InvalidDataException("TemporalAA source output lookup is absent.");
        if(from is{} input)Copy(input,to.Value,"TC;disabled TAA current pointer copy");
        else Assign(to.Value,null); // Both source lookups explicitly absent.
    }

    public OriginalMapPostEffect3ResolvedTextureParameter Require(int parameterIndex)
    {
        CheckIndex(parameterIndex);
        if(!known[parameterIndex])throw new NotSupportedException("Native texture parameter has no source initialization or current assignment.");
        return new(FrameNumber,ContextOwnerIdentity,parameterIndex,revisions[parameterIndex],
            references[parameterIndex]??throw new NotSupportedException("Current native texture parameter is null; no fallback allocation is inferred."));
    }

    public OriginalMapPostEffect3TextureParameterReference? ReadKnownNullablePointer(int parameterIndex)
    {
        CheckIndex(parameterIndex);
        if(!known[parameterIndex])throw new NotSupportedException("Native texture pointer is unknown; absence cannot be inferred.");
        return references[parameterIndex];
    }

    internal long CaptureKnownNullRevision(int parameterIndex)
    {
        if(ReadKnownNullablePointer(parameterIndex) is not null)
            throw new InvalidDataException("A source null SRV receipt requires an explicitly known null pointer.");
        return revisions[parameterIndex];
    }

    internal void ValidateKnownNullRevision(int parameterIndex,long revision)
    {
        if(CaptureKnownNullRevision(parameterIndex)!=revision)
            throw new InvalidDataException("Accepted source null texture assignment changed while execution was queued.");
    }

    public void ValidateCurrent(OriginalMapPostEffect3ResolvedTextureParameter accepted)
    {
        ArgumentNullException.ThrowIfNull(accepted);CheckIndex(accepted.ParameterIndex);
        if(accepted.FrameNumber!=FrameNumber||accepted.ContextOwnerIdentity!=ContextOwnerIdentity||
            accepted.SlotRevision!=revisions[accepted.ParameterIndex]||
            !ReferenceEquals(accepted.Reference,references[accepted.ParameterIndex]))
            throw new InvalidDataException("Accepted texture parameter was replaced or belongs to another frame/owner.");
    }

    private void Copy(int source,int target,string sourceEvidence)
    {
        CheckIndex(source);CheckIndex(target);ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);
        if(!known[source])throw new NotSupportedException($"Texture alias source{source} to target{target} is unknown; no null input is inferred.");
        // Native init copies the currently resolved pointer, including null.
        // Later writes to source cannot retroactively redirect this target.
        Assign(target,references[source]);
    }
    private void Assign(int index,OriginalMapPostEffect3TextureParameterReference? value)
    {references[index]=value;known[index]=true;revisions[index]=checked(revisions[index]+1);}
    private void CheckIndex(int index)
    {if((uint)index>=references.Length)throw new ArgumentOutOfRangeException(nameof(index));}
}
