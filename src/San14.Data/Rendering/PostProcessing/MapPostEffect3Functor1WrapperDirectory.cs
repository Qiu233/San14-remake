namespace San14.Data;

/// <summary>Exact four prepared factory identities for the current72 CPU
/// directory. CPU function-return recording and device receipt joining are
/// separate gates. No rejected directory entry becomes a submitted GPU pass.</summary>
public sealed class MapPostEffect3Functor1WrapperDirectory
{
    private readonly MapPostEffect3Functor1ModuleInvocation invocation;
    private readonly MapPostEffect3TextureParameterState table;
    private readonly MapPostEffect3ParameterLensFlare2Update lens;
    private readonly MapPostEffect3ParameterDepthOfField2Update dof;
    private readonly MapPostEffect3EyeAdaptation2Frame eye;
    private readonly MapPostEffect3Composite2ExecutionFrame composite;
    private readonly List<MapPostEffect3Functor1CpuSourceReturnObservation> observations=[];
    private MapPostEffect3Functor1SourceReceipt? returned;
    internal MapPostEffect3Functor1WrapperDirectory(LoadedMapPostEffect3Functor1WrapperSource source,
        MapPostEffect3Functor1ModuleInvocation invocation,MapPostEffect3TextureParameterState table,
        MapPostEffect3ParameterLensFlare2Update lens,MapPostEffect3ParameterDepthOfField2Update dof,
        MapPostEffect3EyeAdaptation2Frame eye,MapPostEffect3Composite2ExecutionFrame composite)
    {
        this.invocation=invocation;this.table=table;this.lens=lens;this.dof=dof;this.eye=eye;this.composite=composite;
        SourceFields=new(lens.PassSelection.NativeMaskF4,dof.PassSelection.NativeMaskA8,eye.NativeMask1E8,
            eye.LevelCount,composite.Prepared.PassSelection.NativeMask64);
        SourceEvidence=source.SourceEvidence;RequirePreparedSource();
        if(invocation.RecordedCpuWrapperReturns.Count!=0||!invocation.RecordedCpuFactoryReturns.Select(r=>r.FactoryIndex).SequenceEqual([6,7,8,9]))
            throw new InvalidDataException("Functor1 wrapper directory starts after the actual four Prepare/Init callbacks and before wrapper45.");
        Wrappers=source.ReadRecipes(table.Allocation.Source,SourceFields,[lens,dof,eye,composite]);
        CheckSelected(6,lens.PassSelection.OrderedFactory6Passes);CheckSelected(7,dof.PassSelection.OrderedFactory7Passes);
        CheckSelected(9,composite.Prepared.PassSelection.OrderedFactory9Passes);
        var eyeExpected=Wrappers.Where(r=>r.FactoryIndex==8).SelectMany(r=>Enumerable.Range(0,r.ExpectedSuccessfulDeviceIterationCount)
            .Select(i=>(r.SourcePassIndex,r.NativeCachedPassOrdinal,i,r.NativeWrapperLoopBound)));
        if(!eyeExpected.SequenceEqual(eye.OrderedIterations.Select(i=>(i.SourcePassIndex,i.NativeCachedPassOrdinal,i.NativeLoopIndex,i.NativeLoopCount))))
            throw new InvalidDataException("Functor1 Eye native mask/repeat recipes differ from its actual prepared ordered iterations.");
        void CheckSelected(int factory,IReadOnlyList<int> actual)
        {if(!Wrappers.Where(r=>r.FactoryIndex==factory&&r.ExpectedSuccessfulDeviceIterationCount>0).Select(r=>r.SourcePassIndex).SequenceEqual(actual))
            throw new InvalidDataException("Functor1 mask/cache mapping differs from actual factory selection: "+factory);}
    }
    public long FrameNumber=>invocation.FrameNumber;
    public string ContextOwnerIdentity=>table.ContextOwnerIdentity;
    public string ModuleSourceSha256=>table.SourceSha256;
    public MapPostEffect3Functor1MaskSnapshot SourceFields{get;}
    public IReadOnlyList<MapPostEffect3Functor1WrapperRecipe> Wrappers{get;}
    public IReadOnlyList<MapPostEffect3Functor1CpuSourceReturnObservation> CpuSourceReturns=>Array.AsReadOnly(observations.ToArray());
    public string SourceEvidence{get;}
    public bool ProvesGpuCompletion=>false;
    private void RequirePreparedSource()
    {
        invocation.RequireCurrentDepthOfField2Prepared(dof,table);
        lens.RequireCurrentTextureTable(table);dof.RequireActualInitialization(table);eye.RequireCurrent();composite.RequireCurrent();
        composite.Prepared.RequireOwnedLifecycle(invocation.State,table.Allocation,table);
        if(lens.FrameNumber!=FrameNumber||lens.OwnerIdentity!=ContextOwnerIdentity||lens.ConstantBuffer.SourceSha256!=ModuleSourceSha256||
            !ReferenceEquals(eye.State,invocation.State)||!ReferenceEquals(eye.Allocation,table.Allocation)||
            !ReferenceEquals(composite.State,invocation.State)||!ReferenceEquals(composite.CurrentTextureTable,table)||
            !composite.HasAppliedInitialization||SourceFields!=new MapPostEffect3Functor1MaskSnapshot(
                lens.PassSelection.NativeMaskF4,dof.PassSelection.NativeMaskA8,eye.NativeMask1E8,eye.LevelCount,composite.Prepared.PassSelection.NativeMask64))
            throw new InvalidDataException("Functor1 wrapper directory uses foreign or changed actual prepared factory state.");
    }
    internal void RequireCurrent()
    {
        invocation.RequireCurrent();
        if(returned is null)RequirePreparedSource();
        else{returned.RequireCurrent();lens.RequireCurrent();dof.RequireCurrent();eye.RequireCurrent();composite.RequireCurrent();}
    }
    /// <summary>Ported CPU source traversal: v58's proved literal return,
    /// rather than a caller-guessed AL. This observation grants no device work.
    /// Call only after this wrapper's actual port callback chain has returned;
    /// an unreturned callback must stop traversal and mark the module failure.</summary>
    public MapPostEffect3Functor1CpuSourceReturnObservation RecordSourceFunctionReturn(
        MapPostEffect3Functor1WrapperRecipe recipe,MapPostEffect3Functor1CpuWrapperTrace actualCpuTrace)
    {
        RequirePreparedSource();
        if(actualCpuTrace is null||!ReferenceEquals(actualCpuTrace.Recipe,recipe)||!actualCpuTrace.FunctionReturned||
            actualCpuTrace.SourceFunctionReturnAl!=recipe.SourceFinalizeReturnAl||
            returned is not null||observations.Count==72||!ReferenceEquals(Wrappers[observations.Count],recipe)||
            invocation.RecordedCpuWrapperReturns.Count!=observations.Count)
            throw new InvalidDataException("Functor1 CPU wrapper return requires this directory's exact next recipe, once, in source45..116 order.");
        var observation=new MapPostEffect3Functor1CpuSourceReturnObservation(this,recipe,actualCpuTrace);
        invocation.RecordCpuWrapperReturn(recipe.SourcePassIndex,recipe.SourceFinalizeReturnAl);
        observations.Add(observation);return observation;
    }
    public MapPostEffect3Functor1SourceReceipt SealCpuDirectoryReturned()
    {
        RequirePreparedSource();
        if(returned is not null||observations.Count!=72||invocation.RecordedCpuWrapperReturns.Count!=72)
            throw new InvalidDataException("Functor1 CPU directory receipt requires all72 source wrapper returns.");
        return returned=invocation.SealDirectoryReturned();
    }
    /// <summary>Joins exactly the source-selected successful iterations and
    /// required Lens After work. The external adapter retains actual device
    /// authority; the72 source literal returns cannot substitute for it.</summary>
    public MapPostEffect3Functor1SelectedDeviceJoin JoinActualSelectedDeviceReceipts(
        IReadOnlyList<IMapPostEffect3Functor1ActualDeviceReceipt> actualReceipts)
    {
        ArgumentNullException.ThrowIfNull(actualReceipts);RequireCurrent();
        var expected=Wrappers.SelectMany(r=>Enumerable.Range(0,r.ExpectedSuccessfulDeviceIterationCount)
            .Select(i=>(r.SourcePassIndex,Loop:i,Kind:MapPostEffect3Functor1DeviceReceiptKind.FactoryExecuteAndAutomaticRelease)))
            .Concat(Wrappers.Where(r=>r.RequiresLensAfterDeviceJoin).Select(r=>(r.SourcePassIndex,Loop:0,
                Kind:MapPostEffect3Functor1DeviceReceiptKind.LensAfterMipChain))).OrderBy(x=>x.SourcePassIndex).ThenBy(x=>x.Loop).ThenBy(x=>x.Kind).ToArray();
        var actual=actualReceipts.OrderBy(x=>x.SourcePassIndex).ThenBy(x=>x.NativeLoopIndex).ThenBy(x=>x.Kind).ToArray();
        if(!actual.Select(r=>(r.SourcePassIndex,r.NativeLoopIndex,r.Kind)).SequenceEqual(expected))
            throw new InvalidDataException("Functor1 actual device roster differs from source masks/repeats/Lens After; missing, extra or duplicate submissions cannot complete this graph.");
        var identities=new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach(var receipt in actual)
        {
            var recipe=Wrappers[receipt.SourcePassIndex-45];
            if(receipt.FrameNumber!=FrameNumber||receipt.ContextOwnerIdentity!=ContextOwnerIdentity||receipt.ModuleSourceSha256!=ModuleSourceSha256||
                !ReferenceEquals(receipt.SourcePreparedIdentity,recipe.SourcePreparedIdentity)||receipt.ActualSubmissionIdentity is null||
                !identities.Add(receipt.ActualSubmissionIdentity)||string.IsNullOrWhiteSpace(receipt.SourceEvidence))
                throw new InvalidDataException("Functor1 device receipt is foreign, stale, guessed or reused for another native command.");
            receipt.RequireActualForSource(recipe);
        }
        return new(this,Array.AsReadOnly(actual));
    }
}

public sealed class MapPostEffect3Functor1CpuSourceReturnObservation
{
    private readonly MapPostEffect3Functor1WrapperDirectory directory;
    internal MapPostEffect3Functor1CpuSourceReturnObservation(MapPostEffect3Functor1WrapperDirectory directory,
        MapPostEffect3Functor1WrapperRecipe recipe,MapPostEffect3Functor1CpuWrapperTrace trace)
        =>(this.directory,Recipe,CpuTrace)=(directory,recipe,trace);
    public MapPostEffect3Functor1WrapperRecipe Recipe{get;}
    public MapPostEffect3Functor1CpuWrapperTrace CpuTrace{get;}
    public long FrameNumber=>directory.FrameNumber;
    public byte SourceFunctionReturnAl=>Recipe.SourceFinalizeReturnAl;
    public bool WasMaskRejected=>Recipe.SourceSkip==MapPostEffect3Functor1SourceSkip.BeforeMaskRejected;
    public bool HadEmptyNativeRepeat=>Recipe.SourceSkip==MapPostEffect3Functor1SourceSkip.EmptyNativeRepeat;
    public bool ProvesNativeProcessCallbackObservation=>false;
    public bool ProvesGpuSubmission=>false;
    public bool CommitsFunctorHistory=>false;
    public void RequireCurrent()=>directory.RequireCurrent();
}

public sealed class MapPostEffect3Functor1SelectedDeviceJoin
{
    private readonly MapPostEffect3Functor1WrapperDirectory directory;
    private readonly (long Frame,string Owner,string Sha,int Pass,int Loop,MapPostEffect3Functor1DeviceReceiptKind Kind,
        object Prepared,object Submitted,string Evidence)[] identities;
    internal MapPostEffect3Functor1SelectedDeviceJoin(MapPostEffect3Functor1WrapperDirectory directory,
        IReadOnlyList<IMapPostEffect3Functor1ActualDeviceReceipt> receipts)
    {
        this.directory=directory;ActualReceipts=receipts;
        identities=receipts.Select(r=>(r.FrameNumber,r.ContextOwnerIdentity,r.ModuleSourceSha256,r.SourcePassIndex,
            r.NativeLoopIndex,r.Kind,r.SourcePreparedIdentity,r.ActualSubmissionIdentity,r.SourceEvidence)).ToArray();
    }
    public long FrameNumber=>directory.FrameNumber;
    public IReadOnlyList<IMapPostEffect3Functor1ActualDeviceReceipt> ActualReceipts{get;}
    public bool CommitsFunctorHistory=>false;
    public bool ProvesActualFrontendEnd=>false;
    public bool CommitsHostPresentation=>false;
    public void RequireActualReceipts()
    {
        directory.RequireCurrent();
        for(int i=0;i<ActualReceipts.Count;i++)
        {
            var receipt=ActualReceipts[i];var original=identities[i];
            if(receipt.FrameNumber!=original.Frame||receipt.ContextOwnerIdentity!=original.Owner||receipt.ModuleSourceSha256!=original.Sha||
                receipt.SourcePassIndex!=original.Pass||receipt.NativeLoopIndex!=original.Loop||receipt.Kind!=original.Kind||
                !ReferenceEquals(receipt.SourcePreparedIdentity,original.Prepared)||!ReferenceEquals(receipt.ActualSubmissionIdentity,original.Submitted)||
                receipt.SourceEvidence!=original.Evidence)
                throw new InvalidDataException("Functor1 joined device receipt changed its accepted command/frame/source identity.");
            receipt.RequireActualForSource(directory.Wrappers[original.Pass-45]);
        }
    }
}
