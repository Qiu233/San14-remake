using System.Collections.ObjectModel;

namespace San14.Data;

/// <summary>CPU handler state for one owned Functor0 module generation.
/// Constructor histories and allocation selectors are created once. This
/// owner models original Init/Before/exit writes, not GPU contents, native
/// sampler state, texture-pointer aliases or Host presentation acceptance.</summary>
public sealed class OriginalMapPostEffect3RetainedModuleState : IDisposable
{
    private readonly OriginalMapPostEffect3OwnedModuleFunctorCounter counter;
    private readonly Dictionary<int,int> textureSides, bufferSides, constantBufferSides;
    private readonly IReadOnlyList<OriginalMapPostEffect3HistorySlot> flipSlots;
    private readonly object lifetime = new();
    private OriginalMapPostEffect3RetainedModuleInvocation? current;
    private OriginalMapPostEffect3RetainedCpuExit? lastExit;
    private long lastStartedFrame;
    private bool disposed, unresolved;
    public OriginalMapPostEffect3AllocationPlan Allocation { get; }
    public OriginalMapPostEffect3ParameterMotionBlurHistory MotionBlurHistory { get; private set; }
    public OriginalMapPostEffect3ParameterTemporalAAHistory TemporalAAHistory { get; private set; }
    public OriginalMapPostEffect3LensFlareHistory LensHistory { get; private set; }
    internal object Sync=>lifetime;

    private OriginalMapPostEffect3RetainedModuleState(OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3FreshOwnedAllocationSides sides,
        LoadedOriginalMapPostEffect3BeginFunctorCounterSource native)
    {
        Allocation=allocation;
        counter=native.ConstructFreshOwnedModule(allocation.Source,allocation.ContextOwnerIdentity);
        textureSides=new(sides.TextureInstances);bufferSides=new(sides.BufferInstances);
        constantBufferSides=new(sides.ConstantBufferInstances);
        MotionBlurHistory=OriginalMapPostEffect3ParameterMotionBlurHistory.ConstructFresh(allocation.Source,allocation.ContextOwnerIdentity);
        TemporalAAHistory=OriginalMapPostEffect3ParameterTemporalAAHistory.ConstructFresh(allocation.Source,allocation.ContextOwnerIdentity);
        LensHistory=OriginalMapPostEffect3LensFlareHistory.ConstructFresh(allocation.Source,allocation.ContextOwnerIdentity);
        flipSlots=OriginalMapPostEffect3PassStateReader.ReadHistoryFlip(allocation,true);
        foreach(var slot in flipSlots) _=Sides(slot.Table)[slot.ParameterIndex];
    }

    public static OriginalMapPostEffect3RetainedModuleState ConstructFreshOwned(
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3FreshOwnedAllocationSides sides,
        LoadedOriginalMapPostEffect3BeginFunctorCounterSource actualBeginSource)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(sides);
        ArgumentNullException.ThrowIfNull(actualBeginSource);sides.ValidateFor(allocation);
        var protocol=OriginalMapPostEffect3PassStateReader.ReadFunctorProtocol(allocation.Source,0,actualBeginSource.Locale);
        uint finishHook=OriginalMapPostEffect3PassStateReader.FactoriesForLocale(actualBeginSource.Locale)[0].FinishHook;
        if(!protocol.Where(p=>p.Operation==OriginalMapPostEffect3FunctorOperation.ExecutePass)
                .Select(p=>p.SourcePassIndex).SequenceEqual(Enumerable.Range(0,45).Select(i=>(int?)i)) ||
            protocol.Where(p=>p.Operation==OriginalMapPostEffect3FunctorOperation.FinishHook).Any(p=>p.SourceHook!=finishHook))
            throw new NotSupportedException("Retained CPU state requires the actual Functor0 order and its literal true Finish hooks.");
        return new(allocation,sides,actualBeginSource);
    }

    /// <summary>The caller supplies the real new World frame identity. It must
    /// advance independently of native CPU54; duplicate frames cannot become
    /// a second invocation by relabelling the same Camera state.</summary>
    public OriginalMapPostEffect3RetainedModuleInvocation BeginFrame(long actualFrameNumber)
    {
        lock(lifetime)
        {
            RequireLive();
            if(actualFrameNumber<=lastStartedFrame || current is { IsExited: false })
                throw new InvalidOperationException("Retained module needs a new frame after the preceding source exit.");
            OriginalMapPostEffect3ParameterState.ValidateSource(Allocation.Source);
            var previousExit=lastExit;
            var begin=counter.Begin(0);lastStartedFrame=actualFrameNumber;
            lastExit=null;
            return current=new(this,actualFrameNumber,begin,Snapshot(textureSides),Snapshot(bufferSides),Snapshot(constantBufferSides),previousExit);
        }
    }

    internal void RequireCurrent(OriginalMapPostEffect3RetainedModuleInvocation invocation)
    {
        lock(lifetime)
        {
            RequireLive();
            if(!ReferenceEquals(current,invocation))throw new InvalidDataException("Retained module invocation was replaced.");
            invocation.Begin.RequireCurrent();
        }
    }
    private void RequireLive()
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(unresolved)throw new InvalidOperationException("An unresolved source writer invalidated this retained module generation.");
    }
    private Dictionary<int,int> Sides(OriginalMapPostEffect3BindingTable table)=>table switch
    {
        OriginalMapPostEffect3BindingTable.TextureParameters=>textureSides,
        OriginalMapPostEffect3BindingTable.BufferParameters=>bufferSides,
        OriginalMapPostEffect3BindingTable.ConstantBufferParameters=>constantBufferSides,
        _=>throw new ArgumentOutOfRangeException(nameof(table))
    };
    private static IReadOnlyDictionary<int,int> Snapshot(Dictionary<int,int> source)
        =>new ReadOnlyDictionary<int,int>(new Dictionary<int,int>(source));

    internal OriginalMapPostEffect3OwnedFactoryInitialization Initialize(
        OriginalMapPostEffect3RetainedModuleInvocation invocation,OriginalMapPostEffect3OwnedFreshCameraFrame frame,
        bool actualVelocityWrapperPresent,OriginalMapPostEffect3ParameterAliasMode aliasMode)
    {
        lock(lifetime)
        {
            RequireCurrent(invocation);
            if(!ReferenceEquals(frame.Allocation,Allocation)||frame.FrameNumber!=invocation.FrameNumber)
                throw new InvalidDataException("Factory Init needs this module's actual new Camera frame.");
            try
            {
                var init=frame.PrepareFactories(invocation.Begin.Counter,actualVelocityWrapperPresent,
                    invocation.Begin.SourceEvidence,MotionBlurHistory,LensHistory,aliasMode);
                // These are native handler Init writes, not a deferred commit
                // after presentation. Do not roll them back on a later failure.
                MotionBlurHistory=init.MotionBlur.NextHistory;LensHistory=init.Lens.NextHistory;
                return init;
            }
            catch { unresolved=true;throw; }
        }
    }

    internal OriginalMapPostEffect3TemporalAAFirstPassPreparation TemporalBefore(
        OriginalMapPostEffect3RetainedModuleInvocation invocation,OriginalMapPostEffect3OwnedFreshCameraFrame frame)
    {
        lock(lifetime)
        {
            RequireCurrent(invocation);
            try
            {
                var result=frame.PrepareTemporalAABefore(TemporalAAHistory);
                TemporalAAHistory=result.BeforeHook.NextHistory;return result;
            }
            catch { unresolved=true;throw; }
        }
    }

    internal OriginalMapPostEffect3RetainedCpuExit ApplyExit(OriginalMapPostEffect3RetainedModuleInvocation invocation)
    {
        lock(lifetime)
        {
            RequireCurrent(invocation);
            OriginalMapPostEffect3ParameterState.ValidateSource(Allocation.Source);
            if(invocation.FrontendBeginSucceeded==true)
                foreach(var slot in flipSlots)Sides(slot.Table)[slot.ParameterIndex]^=1;
            return lastExit=new(this,invocation,invocation.FrameNumber,Allocation.ContextOwnerIdentity,invocation.Begin.Counter,
                invocation.FrontendBeginSucceeded==true,Snapshot(textureSides),Snapshot(bufferSides),Snapshot(constantBufferSides),
                MotionBlurHistory,TemporalAAHistory,LensHistory);
        }
    }
    internal void MarkUnresolved(OriginalMapPostEffect3RetainedModuleInvocation invocation)
    {lock(lifetime){RequireCurrent(invocation);unresolved=true;}}
    internal void RequireCpuExit(OriginalMapPostEffect3RetainedCpuExit exit,OriginalMapPostEffect3RetainedModuleInvocation invocation)
    {
        lock(lifetime)
        {
            RequireCurrent(invocation);
            if(!ReferenceEquals(lastExit,exit)||!invocation.IsExited)
                throw new InvalidDataException("CPU exit belongs to another or unfinished module invocation.");
        }
    }
    public void Dispose(){lock(lifetime){if(disposed)return;disposed=true;counter.Dispose();current=null;lastExit=null;}}
}

/// <summary>One module invocation with source-ordered CPU writes. Processed
/// pass records are a driver's CPU cursor: they prove no draw, texture contents
/// or GPU completion. A graphics owner must separately join its actual pass
/// receipts before accepting any presentation or persistent GPU history.</summary>
public sealed class OriginalMapPostEffect3RetainedModuleInvocation
{
    private readonly OriginalMapPostEffect3RetainedModuleState owner;
    private readonly OriginalMapPostEffect3RetainedCpuExit? previousExit;
    private OriginalMapPostEffect3OwnedFreshCameraFrame? initializedFrame;
    private bool temporalPrepared;
    private int nextPass;
    internal OriginalMapPostEffect3RetainedModuleInvocation(OriginalMapPostEffect3RetainedModuleState owner,long frame,
        OriginalMapPostEffect3BeginFunctorCounterReceipt begin,IReadOnlyDictionary<int,int> textures,
        IReadOnlyDictionary<int,int> buffers,IReadOnlyDictionary<int,int> constantBuffers,
        OriginalMapPostEffect3RetainedCpuExit? previousExit)
        =>(this.owner,FrameNumber,Begin,TextureInstances,BufferInstances,ConstantBufferInstances,this.previousExit)=
            (owner,frame,begin,textures,buffers,constantBuffers,previousExit);
    public long FrameNumber { get; }
    public OriginalMapPostEffect3BeginFunctorCounterReceipt Begin { get; }
    public IReadOnlyDictionary<int,int> TextureInstances { get; }
    public IReadOnlyDictionary<int,int> BufferInstances { get; }
    public IReadOnlyDictionary<int,int> ConstantBufferInstances { get; }
    public bool? FrontendBeginSucceeded { get; private set; }
    public bool IsExited { get; private set; }
    public int NextSourcePassIndex=>nextPass;
    public void RequireCurrent()=>owner.RequireCurrent(this);
    public void RequireModule(OriginalMapPostEffect3AllocationPlan actualAllocation)
    {
        ArgumentNullException.ThrowIfNull(actualAllocation);
        lock(owner.Sync)
        {
            RequireCurrent();
            if(!ReferenceEquals(owner.Allocation,actualAllocation))
                throw new InvalidDataException("Invocation belongs to another actual module allocation.");
        }
    }
    internal void ValidateRetainedParameterFrame(OriginalMapPostEffect3AllocationPlan allocation,long previousFrameNumber)
    {
        lock(owner.Sync)
        {
            RequireOpen();
            if(!ReferenceEquals(owner.Allocation,allocation)||previousExit is null||
                !previousExit.AppliedSourceHistoryFlip||previousExit.FrameNumber!=previousFrameNumber||
                previousFrameNumber>=FrameNumber)
                throw new InvalidDataException("Retained parameters require this module's preceding completed source invocation.");
        }
    }
    private void RequireOpen()
    {RequireCurrent();if(IsExited)throw new InvalidOperationException("Source invocation already exited.");}

    /// <summary>Report the actual frontend Begin result, after the native
    /// counter publication. False consumes the counter but allows no Init or
    /// pass, and performs no history selector flip.</summary>
    public void ReportFrontendBeginResult(bool actualSucceeded)
    {
        lock(owner.Sync)
        {
            RequireOpen();
            if(FrontendBeginSucceeded is not null)throw new InvalidOperationException("Frontend Begin result already reported.");
            FrontendBeginSucceeded=actualSucceeded;
        }
    }
    public void ReportSourceFrontendBeginResult(OriginalMapPostEffect3FrontendBeginSourceResult source)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock(owner.Sync){source.ValidateFor(this);ReportFrontendBeginResult(source.SourceLiteralReturn);}
    }
    public void RequireInitializedFrame(OriginalMapPostEffect3OwnedFreshCameraFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        lock(owner.Sync)
        {
            RequireOpen();
            if(!ReferenceEquals(initializedFrame,frame))
                throw new InvalidDataException("Actual factory frame was not initialized by this retained module invocation.");
        }
    }
    public OriginalMapPostEffect3OwnedFactoryInitialization PrepareFactories(
        OriginalMapPostEffect3OwnedFreshCameraFrame actualFrame,bool actualVelocityWrapperPresent,
        OriginalMapPostEffect3ParameterAliasMode aliasMode)
    {
        ArgumentNullException.ThrowIfNull(actualFrame);
        lock(owner.Sync)
        {
            RequireOpen();
            if(FrontendBeginSucceeded!=true||initializedFrame is not null)
                throw new InvalidOperationException("Source factories initialize once after successful frontend Begin.");
            var result=owner.Initialize(this,actualFrame,actualVelocityWrapperPresent,aliasMode);
            initializedFrame=actualFrame;return result;
        }
    }
    public OriginalMapPostEffect3TemporalAAFirstPassPreparation PrepareTemporalAABefore()
    {
        lock(owner.Sync)
        {
            RequireOpen();
            if(initializedFrame is null||temporalPrepared)
                throw new InvalidOperationException("Source pass0 Before follows all Init hooks exactly once.");
            var result=owner.TemporalBefore(this,initializedFrame);temporalPrepared=true;return result;
        }
    }
    /// <summary>Called after the driver processes the original source pass,
    /// including a legitimate native skipped branch. Does not determine the
    /// branch or manufacture a GPU receipt for that pass.</summary>
    public void RecordProcessedSourcePass(int actualSourcePassIndex)
    {
        lock(owner.Sync)
        {
            RequireOpen();
            if(!temporalPrepared||nextPass>=45||actualSourcePassIndex!=nextPass)
                throw new InvalidDataException("CPU Functor0 cursor requires its actual source pass order0..44.");
            nextPass++;
        }
    }
    /// <summary>Model the known BF9280 Finish hooks/flag clears and source
    ///114AB40 exit flip after the successful branch finishes frontend End.
    /// Failed Begin exits without frontend End or flip. The receipt
    /// remains CPU-only; even a fully processed cursor proves no GPU output.</summary>
    public OriginalMapPostEffect3RetainedCpuExit ApplySourceExitAfterFrontendEnd()
    {
        lock(owner.Sync)
        {
            RequireOpen();
            if(FrontendBeginSucceeded is null||FrontendBeginSucceeded==true&&nextPass!=45)
                throw new InvalidOperationException("Source exit requires the Begin result and complete processed Functor0 cursor.");
            var result=owner.ApplyExit(this);IsExited=true;return result;
        }
    }
    public void MarkFailedOrUnresolvedWriter(){lock(owner.Sync){RequireOpen();owner.MarkUnresolved(this);}}
}

/// <summary>CPU source-state result only. Initial histories are retained and
/// pair selectors flipped; actual texture readiness, sampler state, GPU and
/// Host completion must be provided by their separate owners.</summary>
public sealed class OriginalMapPostEffect3RetainedCpuExit
{
    private readonly OriginalMapPostEffect3RetainedModuleState owner;
    private readonly OriginalMapPostEffect3RetainedModuleInvocation invocation;
    internal OriginalMapPostEffect3RetainedCpuExit(OriginalMapPostEffect3RetainedModuleState owner,
        OriginalMapPostEffect3RetainedModuleInvocation invocation,long frame,string context,uint counter,bool flipped,
        IReadOnlyDictionary<int,int> textures,IReadOnlyDictionary<int,int> buffers,IReadOnlyDictionary<int,int> constantBuffers,
        OriginalMapPostEffect3ParameterMotionBlurHistory motion,OriginalMapPostEffect3ParameterTemporalAAHistory temporal,
        OriginalMapPostEffect3LensFlareHistory lens)
        =>(this.owner,this.invocation,FrameNumber,ContextOwnerIdentity,NativeCounter,AppliedSourceHistoryFlip,
            TextureInstances,BufferInstances,ConstantBufferInstances,MotionBlurHistory,TemporalAAHistory,LensHistory)=
            (owner,invocation,frame,context,counter,flipped,textures,buffers,constantBuffers,motion,temporal,lens);
    public long FrameNumber { get; }
    public string ContextOwnerIdentity { get; }
    public uint NativeCounter { get; }
    public bool AppliedSourceHistoryFlip { get; }
    public IReadOnlyDictionary<int,int> TextureInstances { get; }
    public IReadOnlyDictionary<int,int> BufferInstances { get; }
    public IReadOnlyDictionary<int,int> ConstantBufferInstances { get; }
    public OriginalMapPostEffect3ParameterMotionBlurHistory MotionBlurHistory { get; }
    public OriginalMapPostEffect3ParameterTemporalAAHistory TemporalAAHistory { get; }
    public OriginalMapPostEffect3LensFlareHistory LensHistory { get; }
    public void RequireCurrent()=>owner.RequireCpuExit(this,invocation);
    public bool CompletesGpuFunctor=>false;
    public bool CommitsHostPresentation=>false;
}
