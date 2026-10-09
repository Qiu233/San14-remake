using System.Collections.ObjectModel;

namespace San14.Data;

public enum OriginalMapPostEffect3Functor1SourceOperation
{
    FrontendBeginCandidate,FrontendBeginRecorded,DirectoryReturned,FinishCandidate,FinishApplied,
    FrontendEndCandidate,FrontendEndRecorded,HistoryFlipped,OuterRestorationCandidate,ModuleReturned,
    ControllerCleanupCandidate,ControllerReturned
}
public sealed record OriginalMapPostEffect3Functor1HookFlags(bool Prepared8,bool Initialized9);
public sealed record OriginalMapPostEffect3Functor1CpuWrapperReturn(int SourcePassIndex,byte ReturnAl);
public sealed record OriginalMapPostEffect3Functor1CpuFactoryReturn(int FactoryIndex,byte PrepareAl,byte InitializeAl);

/// <summary>Closed, exact-invocation CPU protocol receipt. No source literal,
/// cursor or side transition proves a GPU submission, pixel readiness or End.</summary>
public sealed class OriginalMapPostEffect3Functor1SourceReceipt
{
    internal OriginalMapPostEffect3Functor1SourceReceipt(OriginalMapPostEffect3Functor1ModuleInvocation invocation,
        OriginalMapPostEffect3Functor1SourceOperation operation,
        OriginalMapPostEffect3DepthOfField2Controller? dofCandidate=null)
    {
        Invocation=invocation;Operation=operation;DepthOfField2FinishCandidate=dofCandidate;
        SourceDofController=invocation.Owner.DepthOfFieldController;
        SourceRearFlag=SourceDofController.RearScatterEnabled14;SourceForeFlag=SourceDofController.ForeScatterEnabled15;
        EyeSnapshot=invocation.Owner.EyeController.CaptureRetainedControllerSnapshot();
    }
    internal OriginalMapPostEffect3Functor1ModuleInvocation Invocation{get;}
    internal OriginalMapPostEffect3DepthOfField2Controller SourceDofController{get;}
    internal bool SourceRearFlag{get;}
    internal bool SourceForeFlag{get;}
    public OriginalMapPostEffect3Functor1SourceOperation Operation{get;}
    public long FrameNumber=>Invocation.FrameNumber;
    public uint NativeCounter=>Invocation.Begin.Counter;
    public string ContextOwnerIdentity=>Invocation.Owner.Allocation.ContextOwnerIdentity;
    public OriginalMapPostEffect3DepthOfField2Controller? DepthOfField2FinishCandidate{get;}
    public OriginalMapPostEffect3EyeRetainedControllerSnapshot EyeSnapshot{get;}
    public bool CompletesGpuFunctor=>false;
    public bool ProvesActualFrontendEnd=>false;
    public bool ProvesActualTemporaryBufferCleanup=>false;
    public bool CommitsHostPresentation=>false;
    public byte? SourceLiteralReturn=>Operation is OriginalMapPostEffect3Functor1SourceOperation.FrontendBeginCandidate or
        OriginalMapPostEffect3Functor1SourceOperation.FrontendEndCandidate or OriginalMapPostEffect3Functor1SourceOperation.ModuleReturned? (byte)1:null;
    public uint? NativeFrontendEndArgument=>Operation is OriginalMapPostEffect3Functor1SourceOperation.FrontendEndCandidate or
        OriginalMapPostEffect3Functor1SourceOperation.FrontendEndRecorded?uint.MaxValue:null;
    public IReadOnlyList<int> SourceFinishFactoryOrder=>Operation is OriginalMapPostEffect3Functor1SourceOperation.FinishCandidate or
        OriginalMapPostEffect3Functor1SourceOperation.FinishApplied?[6,7,8,9]:[];
    public int? RequiredTemporaryBufferCleanupIndex=>Operation==OriginalMapPostEffect3Functor1SourceOperation.ControllerCleanupCandidate?4:null;
    public IReadOnlyList<int> RequiredTemporaryBufferPointerOffsets=>RequiredTemporaryBufferCleanupIndex is null?[]:[8,16,24,32];
    public string SourceEvidence=>Invocation.Owner.ExitSource.SourceEvidence+";closed-Functor1-CPU-step="+Operation;
    public void RequireCurrent()=>Invocation.RequireCurrent();
}

/// <summary>Functor1-specific module lifetime. Owns CPU counter, source side
/// selectors, DOF controller and Eye caches. Shape/staging/GPU contents stay
/// with separate module-lifetime device owners. No Functor0 exit is accepted.</summary>
public sealed class OriginalMapPostEffect3Functor1ModuleState:IDisposable
{
    private readonly OriginalMapPostEffect3OwnedModuleFunctorCounter counter;
    private readonly Dictionary<int,int> textures,buffers,cbs;
    private readonly IReadOnlyList<OriginalMapPostEffect3HistorySlot> pairs;
    private OriginalMapPostEffect3Functor1ModuleInvocation? current;
    private long lastFrame;
    private bool disposed,unresolved;
    internal object Sync{get;}=new();
    public OriginalMapPostEffect3AllocationPlan Allocation{get;}
    public LoadedOriginalMapPostEffect3Functor1ExitSource ExitSource{get;}
    public string Locale=>ExitSource.Locale;
    public OriginalMapPostEffect3DepthOfField2Controller DepthOfFieldController{get;internal set;}
    public OriginalMapPostEffect3EyeAdaptation2Owner EyeController{get;}
    public bool HasUnreturnedFailure=>unresolved;
    private OriginalMapPostEffect3Functor1ModuleState(OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3FreshOwnedAllocationSides sides,LoadedOriginalMapPostEffect3BeginFunctorCounterSource begin,
        LoadedOriginalMapPostEffect3Functor1ExitSource exit,OriginalMapPostEffect3DepthOfField2Controller dof,
        OriginalMapPostEffect3EyeAdaptation2Owner eye)
    {
        Allocation=allocation;ExitSource=exit;DepthOfFieldController=dof;EyeController=eye;
        counter=begin.ConstructFreshOwnedModule(allocation.Source,allocation.ContextOwnerIdentity);
        textures=new(sides.TextureInstances);buffers=new(sides.BufferInstances);cbs=new(sides.ConstantBufferInstances);
        pairs=OriginalMapPostEffect3PassStateReader.ReadHistoryFlip(allocation,true);
    }
    public static OriginalMapPostEffect3Functor1ModuleState ConstructFreshOwned(OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3FreshOwnedAllocationSides sides,LoadedOriginalMapPostEffect3BeginFunctorCounterSource begin,
        LoadedOriginalMapPostEffect3Functor1ExitSource exit,OriginalMapPostEffect3DepthOfField2Controller actualFreshDof,
        OriginalMapPostEffect3EyeAdaptation2Owner actualFreshEye)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(sides);
        ArgumentNullException.ThrowIfNull(begin);ArgumentNullException.ThrowIfNull(exit);
        ArgumentNullException.ThrowIfNull(actualFreshDof);ArgumentNullException.ThrowIfNull(actualFreshEye);
        sides.ValidateFor(allocation);OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        string locale=begin.Locale;
        var profile=OriginalMapPostEffect3PassStateReader.ReadFunctorProtocol(allocation.Source,1,locale);
        var finish=OriginalMapPostEffect3PassStateReader.FactoriesForLocale(locale).Skip(6)
            .Select(f=>(f.Index,(OriginalMapPostEffect3Hook?)f.FinishHook));
        if(exit.Locale!=locale||actualFreshDof.Locale!=locale||actualFreshEye.Locale!=locale||
            sides.AppliesAfterHistoryFlip||!profile.Where(s=>s.Operation==OriginalMapPostEffect3FunctorOperation.ExecutePass).Select(s=>s.SourcePassIndex)
                .SequenceEqual(Enumerable.Range(45,72).Select(i=>(int?)i))||
            !profile.Where(s=>s.Operation==OriginalMapPostEffect3FunctorOperation.FinishHook).Select(s=>(s.FactoryIndex,s.SourceHook))
                .SequenceEqual(finish)||
            !ReferenceEquals(actualFreshDof.Allocation,allocation)||actualFreshDof.FrameNumber!=0||actualFreshDof.Count10!=2||
            actualFreshDof.HistoryFlip16||actualFreshDof.SpriteSide28!=0||actualFreshDof.ShapeFix4K||actualFreshDof.ShapeMultiResolution||
            actualFreshDof.ShapeHeight!=1080||actualFreshDof.InitializeFix4K||actualFreshDof.InitializeMultiResolution||
            actualFreshDof.InitializeHeight!=1080||actualFreshDof.RearScatterEnabled14||actualFreshDof.ForeScatterEnabled15)
            throw new InvalidDataException("Functor1 fresh state requires its exact native directory/Finish hooks and fresh same-allocation DOF controller.");
        actualFreshEye.RequireOwnedModuleForRetention(allocation);
        var eye=actualFreshEye.CaptureRetainedControllerSnapshot();
        if(eye.PreparedFrameNumber!=0||eye.CroppedViewport294||eye.ActiveFourKWrapper)
            throw new InvalidDataException("Functor1 construction cannot reset an already retained Eye controller.");
        return new(allocation,sides,begin,exit,actualFreshDof,actualFreshEye);
    }
    public OriginalMapPostEffect3Functor1ModuleInvocation BeginFrame(OriginalMapPostEffect3ParameterState actualState,
        OriginalMapPostEffect3ControllerScalarInvocation actualController)
    {
        lock(Sync)
        {
            RequireLive();ArgumentNullException.ThrowIfNull(actualState);ArgumentNullException.ThrowIfNull(actualController);
            if(actualState.FrameNumber<=lastFrame||current is not null&&!current.IsControllerReturned||
                !ReferenceEquals(actualController.State,actualState)||actualController.Caller.ActualSelectedFunctorIndex!=1||
                actualController.Snapshot.ControllerOwnerIdentity!=Allocation.ContextOwnerIdentity)
                throw new InvalidDataException("Functor1 Begin requires a later exact controller/scalar invocation after the preceding full CPU return.");
            actualController.RequirePending();actualState.BindOwnedAllocation(Allocation);
            var previous=current;previous?.ForgetOlderRetainedInvocation();var receipt=counter.Begin(1);lastFrame=actualState.FrameNumber;
            return current=new(this,actualState,actualController,receipt,Snapshot(textures),Snapshot(buffers),Snapshot(cbs),previous);
        }
    }
    public OriginalMapPostEffect3ParameterState CloneRetainedScalarState(OriginalMapPostEffect3ParameterState actualPrevious,long nextFrame)
    {
        lock(Sync)
        {
            RequireLive();
            if(current is null||!current.IsControllerReturned||!ReferenceEquals(current.State,actualPrevious))
                throw new InvalidDataException("Functor1 scalar retention needs its exact returned preceding invocation.");
            actualPrevious.RequireRetainedOwnedAllocation(Allocation,nextFrame);return actualPrevious.CloneForNewFrame(nextFrame);
        }
    }
    internal void RequireCurrent(OriginalMapPostEffect3Functor1ModuleInvocation invocation)
    {
        lock(Sync){RequireLive();if(!ReferenceEquals(current,invocation))throw new InvalidDataException("Functor1 invocation was replaced.");invocation.Begin.RequireCurrent();}
    }
    private void RequireLive(){ObjectDisposedException.ThrowIf(disposed,this);if(unresolved)throw new InvalidOperationException("Functor1 generation has an unreturned source failure.");}
    internal void Fail(OriginalMapPostEffect3Functor1ModuleInvocation invocation){RequireCurrent(invocation);unresolved=true;}
    internal void Flip(OriginalMapPostEffect3Functor1ModuleInvocation invocation)
    {RequireCurrent(invocation);foreach(var slot in pairs)(slot.Table switch{
        OriginalMapPostEffect3BindingTable.TextureParameters=>textures,OriginalMapPostEffect3BindingTable.BufferParameters=>buffers,_=>cbs})[slot.ParameterIndex]^=1;}
    internal static IReadOnlyDictionary<int,int> Snapshot(Dictionary<int,int> values)=>new ReadOnlyDictionary<int,int>(new Dictionary<int,int>(values));
    internal (IReadOnlyDictionary<int,int>,IReadOnlyDictionary<int,int>,IReadOnlyDictionary<int,int>) ReadSides()=>
        (Snapshot(textures),Snapshot(buffers),Snapshot(cbs));
    public void Dispose(){lock(Sync){if(disposed)return;disposed=true;counter.Dispose();}}
}

public sealed class OriginalMapPostEffect3Functor1ModuleInvocation
{
    private readonly OriginalMapPostEffect3ControllerScalarInvocation controller;
    private OriginalMapPostEffect3Functor1ModuleInvocation? previous;
    internal void ForgetOlderRetainedInvocation()=>previous=null;
    private readonly Dictionary<int,OriginalMapPostEffect3Functor1HookFlags> flags=new();
    private readonly List<OriginalMapPostEffect3Functor1CpuWrapperReturn> wrappers=[];
    private readonly List<OriginalMapPostEffect3Functor1CpuFactoryReturn> factoryReturns=[];
    private OriginalMapPostEffect3TextureParameterState? table;
    private OriginalMapPostEffect3ParameterDepthOfField2Update? dofUpdate;
    private OriginalMapPostEffect3Functor1SourceReceipt? last,pending;
    private OriginalMapPostEffect3Functor1SourceReceipt? directoryReturned;
    private int nextFactory=6;
    private int phase; // Begin,Init,Directory,Finish,End,Flip,Returned,ControllerReturned.
    internal OriginalMapPostEffect3Functor1ModuleInvocation(OriginalMapPostEffect3Functor1ModuleState owner,
        OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3ControllerScalarInvocation controller,
        OriginalMapPostEffect3BeginFunctorCounterReceipt begin,IReadOnlyDictionary<int,int> textures,
        IReadOnlyDictionary<int,int> buffers,IReadOnlyDictionary<int,int> cbs,OriginalMapPostEffect3Functor1ModuleInvocation? previous)
    {Owner=owner;State=state;this.controller=controller;Begin=begin;TextureInstances=textures;BufferInstances=buffers;
     ConstantBufferInstances=cbs;this.previous=previous;foreach(int i in Enumerable.Range(6,4))flags[i]=new(false,false);}
    internal OriginalMapPostEffect3Functor1ModuleState Owner{get;}
    public OriginalMapPostEffect3ParameterState State{get;}
    public long FrameNumber=>State.FrameNumber;
    public OriginalMapPostEffect3BeginFunctorCounterReceipt Begin{get;}
    public IReadOnlyDictionary<int,int> TextureInstances{get;private set;}
    public IReadOnlyDictionary<int,int> BufferInstances{get;private set;}
    public IReadOnlyDictionary<int,int> ConstantBufferInstances{get;private set;}
    public IReadOnlyDictionary<int,OriginalMapPostEffect3Functor1HookFlags> HookFlags=>new ReadOnlyDictionary<int,OriginalMapPostEffect3Functor1HookFlags>(new Dictionary<int,OriginalMapPostEffect3Functor1HookFlags>(flags));
    public IReadOnlyList<OriginalMapPostEffect3Functor1CpuWrapperReturn> RecordedCpuWrapperReturns=>Array.AsReadOnly(wrappers.ToArray());
    public IReadOnlyList<OriginalMapPostEffect3Functor1CpuFactoryReturn> RecordedCpuFactoryReturns=>Array.AsReadOnly(factoryReturns.ToArray());
    public bool IsControllerReturned=>phase==7;
    public string? UnreturnedFailureEvidence{get;private set;}
    public void RequireCurrent(){Owner.RequireCurrent(this);if(!IsControllerReturned)controller.RequirePending();}
    /// <summary>Identity join for a separate device module owner. This validates
    /// the CPU invocation only; it grants no GPU contents or completion.</summary>
    public void RequireOwnedModule(OriginalMapPostEffect3Functor1ModuleState actualModule)
    {
        RequireCurrent();
        if(!ReferenceEquals(Owner,actualModule))
            throw new InvalidDataException("Functor1 device retention requires its exact CPU module owner.");
    }
    public void RequireCurrentTextureTable(OriginalMapPostEffect3TextureParameterState actualTable)
    {
        lock(Owner.Sync){RequireCurrent();if(!ReferenceEquals(table,actualTable))
            throw new InvalidDataException("Functor1 factory callbacks require the exact adopted current texture table.");}
    }
    /// <summary>Directory-return phase2 identity, before global Finish.
    /// The execution-phase DOF guard remains phase1. A returned directory
    /// cannot be used to submit another factory or authorize End/history.</summary>
    public void RequireCurrentReturnedDirectory(OriginalMapPostEffect3Functor1SourceReceipt actual,
        OriginalMapPostEffect3TextureParameterState actualTable)
    {
        lock(Owner.Sync)
        {
            RequireCurrent();
            if(phase!=2||pending is not null||!ReferenceEquals(actual,last)||!ReferenceEquals(actual.Invocation,this)||
                actual.Operation!=OriginalMapPostEffect3Functor1SourceOperation.DirectoryReturned||!ReferenceEquals(table,actualTable)||
                wrappers.Count!=72||dofUpdate is null||!ReferenceEquals(Owner.DepthOfFieldController,dofUpdate.NextController))
                throw new InvalidDataException("Returned directory requires the exact phase2 source receipt/table/DOF handler before Finish.");
            dofUpdate.RequireCurrentScalarState(State);dofUpdate.RequireActualInitialization(actualTable);
        }
    }
    /// <summary>Device factory7 work must use the exact adopted source Init,
    /// scalar state and table, before global Finish replaces the handler.</summary>
    public void RequireCurrentDepthOfField2Prepared(OriginalMapPostEffect3ParameterDepthOfField2Update actual,
        OriginalMapPostEffect3TextureParameterState actualTable)
    {
        lock(Owner.Sync)
        {
            RequireCurrent();
            if(phase!=1||!ReferenceEquals(table,actualTable)||!ReferenceEquals(dofUpdate,actual)||
                !ReferenceEquals(Owner.DepthOfFieldController,actual.NextController))
                throw new InvalidDataException("Functor1 device factory7 requires its exact adopted current Init before whole-directory Finish.");
            actual.RequireCurrentScalarState(State);actual.RequireActualInitialization(actualTable);
        }
    }
    internal void ValidateRetainedParameterFrame(OriginalMapPostEffect3AllocationPlan allocation,long previousFrame)
    {RequireCurrent();if(!ReferenceEquals(Owner.Allocation,allocation)||previous is null||!previous.IsControllerReturned||
        previous.FrameNumber!=previousFrame||previousFrame>=FrameNumber)throw new InvalidDataException("Functor1 retention needs its exact preceding CPU return.");}
    internal void ValidateRetainedTextureTable(OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3TextureParameterState actualPrevious)
    {
        ValidateRetainedParameterFrame(allocation,actualPrevious.FrameNumber);
        if(previous is null||!ReferenceEquals(previous.table,actualPrevious))
            throw new InvalidDataException("Functor1 texture retention needs the exact table adopted by its preceding source invocation.");
    }
    public void AdoptCurrentTextureTable(OriginalMapPostEffect3TextureParameterState actualTable)
    {
        lock(Owner.Sync)
        {
            RequireCurrent();if(table is not null||phase>1||!ReferenceEquals(actualTable.Allocation,Owner.Allocation)||actualTable.FrameNumber!=FrameNumber)
                throw new InvalidDataException("Functor1 table adoption requires its exact current allocation/frame before Init.");
            foreach(var a in Owner.Allocation.TextureAllocations)
            {var p=actualTable.Require(a.Parameter.Index);if(p.Reference.PhysicalAllocationParameterIndex!=a.Parameter.Index||p.Reference.InstanceIndex!=TextureInstances[a.Parameter.Index])
                throw new InvalidDataException("Functor1 table does not select its actual native allocation sides.");}
            table=actualTable;
        }
    }
    public void AdoptDepthOfField2Initialization(OriginalMapPostEffect3ParameterDepthOfField2Update actual)
    {
        lock(Owner.Sync)
        {
            RequireCurrent();if(phase!=1||nextFactory!=7||table is null||dofUpdate is not null||!ReferenceEquals(actual.PreviousController,Owner.DepthOfFieldController))
                throw new InvalidDataException("Functor1 DOF Init requires its exact preceding retained handler and factory order.");
            actual.RequireCurrentScalarState(State);actual.RequireActualInitialization(table);
            Owner.DepthOfFieldController=actual.NextController;dofUpdate=actual; // Native Init writes persist before later failures.
        }
    }
    public void RecordFactoryPrepareInitializeReturns(int factory,byte actualPrepareReturn,byte actualInitializeReturn)
    {
        lock(Owner.Sync)
        {
            RequireCurrent();if(phase!=1||table is null||factory!=nextFactory||factory>9||factory==7&&dofUpdate is null)
                throw new InvalidDataException("Functor1 Prepare/Init callback returns require once-only source order6/7/8/9 and actual DOF Init.");
            if(factory==8)Owner.EyeController.RequireCurrentPreparedStateForRetention(State);
            flags[factory]=new(true,true);nextFactory++;
            factoryReturns.Add(new(factory,actualPrepareReturn,actualInitializeReturn));
            // Original flags are set even when a callback returned AL0. The
            // outer dispatcher does not stop; these observations prove no GPU.
        }
    }
    public void RecordCpuWrapperReturn(int sourcePassIndex,byte actualWrapperReturn)
    {
        lock(Owner.Sync)
        {RequireCurrent();if(phase!=1||nextFactory!=10||sourcePassIndex!=45+wrappers.Count||wrappers.Count==72)
            throw new InvalidDataException("Functor1 CPU wrapper cursor requires every original directory index45..116 once, in order.");
         wrappers.Add(new(sourcePassIndex,actualWrapperReturn));}
    }
    public OriginalMapPostEffect3Functor1SourceReceipt SealDirectoryReturned()
    {
        lock(Owner.Sync)
        {RequireCurrent();if(phase!=1||nextFactory!=10||wrappers.Count!=72||pending is not null)
            throw new InvalidDataException("Functor1 Finish cannot precede a returned complete72-wrapper directory.");
         phase=2;return directoryReturned=last=new(this,OriginalMapPostEffect3Functor1SourceOperation.DirectoryReturned);}
    }
    public void RequireCurrentExitedDirectory(OriginalMapPostEffect3Functor1SourceReceipt actual,
        OriginalMapPostEffect3TextureParameterState actualTable)
    {
        lock(Owner.Sync)
        {
            RequireCurrent();
            if(phase<2||pending is not null||!ReferenceEquals(actual,directoryReturned)||!ReferenceEquals(table,actualTable)||
                wrappers.Count!=72||actual.Operation!=OriginalMapPostEffect3Functor1SourceOperation.DirectoryReturned)
                throw new InvalidDataException("Exit diagnostics require the exact sealed source directory/table; execution cannot resume.");
        }
    }
    internal OriginalMapPostEffect3Functor1SourceReceipt CaptureCandidate(LoadedOriginalMapPostEffect3Functor1ExitSource source,
        OriginalMapPostEffect3Functor1SourceOperation operation,OriginalMapPostEffect3Functor1SourceReceipt? input)
    {
        lock(Owner.Sync)
        {
            RequireCurrent();if(!ReferenceEquals(source,Owner.ExitSource)||pending is not null)
                throw new InvalidDataException("Functor1 source candidate is foreign or already pending.");
            var expected=operation switch{
                OriginalMapPostEffect3Functor1SourceOperation.FrontendBeginCandidate=>(0,(OriginalMapPostEffect3Functor1SourceOperation?)null),
                OriginalMapPostEffect3Functor1SourceOperation.FinishCandidate=>(2,(OriginalMapPostEffect3Functor1SourceOperation?)OriginalMapPostEffect3Functor1SourceOperation.DirectoryReturned),
                OriginalMapPostEffect3Functor1SourceOperation.FrontendEndCandidate=>(3,(OriginalMapPostEffect3Functor1SourceOperation?)OriginalMapPostEffect3Functor1SourceOperation.FinishApplied),
                OriginalMapPostEffect3Functor1SourceOperation.OuterRestorationCandidate=>(5,(OriginalMapPostEffect3Functor1SourceOperation?)OriginalMapPostEffect3Functor1SourceOperation.HistoryFlipped),
                OriginalMapPostEffect3Functor1SourceOperation.ControllerCleanupCandidate=>(6,(OriginalMapPostEffect3Functor1SourceOperation?)OriginalMapPostEffect3Functor1SourceOperation.ModuleReturned),
                _=>throw new InvalidDataException("Functor1 operation has no source candidate.")};
            if(phase!=expected.Item1||expected.Item2 is null&&input is not null||expected.Item2 is not null&&
                (input is null||!ReferenceEquals(input,last)||input.Operation!=expected.Item2))
                throw new InvalidDataException("Functor1 source candidate is not after its required exact preceding receipt.");
            if(input is not null)RequireStable(input);
            return pending=new(this,operation,operation==OriginalMapPostEffect3Functor1SourceOperation.FinishCandidate?
                Owner.DepthOfFieldController.ApplySourceFinish(true):null);
        }
    }
    private void RequireStable(OriginalMapPostEffect3Functor1SourceReceipt receipt)
    {receipt.RequireCurrent();if(!ReferenceEquals(receipt.Invocation,this)||!ReferenceEquals(receipt.SourceDofController,Owner.DepthOfFieldController)||
        receipt.SourceRearFlag!=Owner.DepthOfFieldController.RearScatterEnabled14||receipt.SourceForeFlag!=Owner.DepthOfFieldController.ForeScatterEnabled15||
        receipt.EyeSnapshot!=Owner.EyeController.CaptureRetainedControllerSnapshot())throw new InvalidDataException("Functor1 source handler changed after its captured protocol step.");}
    private void Consume(OriginalMapPostEffect3Functor1SourceReceipt receipt,OriginalMapPostEffect3Functor1SourceOperation operation,int expectedPhase)
    {RequireCurrent();if(phase!=expectedPhase||!ReferenceEquals(pending,receipt)||receipt.Operation!=operation)
        throw new InvalidDataException("Functor1 source candidate is stale, duplicated or out of order.");RequireStable(receipt);pending=null;}
    public OriginalMapPostEffect3Functor1SourceReceipt RecordFrontendBegin(OriginalMapPostEffect3Functor1SourceReceipt source)
    {lock(Owner.Sync){Consume(source,OriginalMapPostEffect3Functor1SourceOperation.FrontendBeginCandidate,0);phase=1;
        return last=new(this,OriginalMapPostEffect3Functor1SourceOperation.FrontendBeginRecorded);}}
    public OriginalMapPostEffect3Functor1SourceReceipt ApplyFinishCandidate(OriginalMapPostEffect3Functor1SourceReceipt source)
    {lock(Owner.Sync){Consume(source,OriginalMapPostEffect3Functor1SourceOperation.FinishCandidate,2);
        Owner.DepthOfFieldController=source.DepthOfField2FinishCandidate??throw new InvalidDataException("DOF Finish candidate missing.");
        foreach(int i in Enumerable.Range(6,4))flags[i]=new(false,false);phase=3;
        return last=new(this,OriginalMapPostEffect3Functor1SourceOperation.FinishApplied);}}
    public OriginalMapPostEffect3Functor1SourceReceipt RecordFrontendEnd(OriginalMapPostEffect3Functor1SourceReceipt source)
    {lock(Owner.Sync){Consume(source,OriginalMapPostEffect3Functor1SourceOperation.FrontendEndCandidate,3);phase=4;
        return last=new(this,OriginalMapPostEffect3Functor1SourceOperation.FrontendEndRecorded);}}
    public OriginalMapPostEffect3Functor1SourceReceipt ApplyHistoryFlip(OriginalMapPostEffect3Functor1SourceReceipt end)
    {lock(Owner.Sync){RequireCurrent();if(phase!=4||!ReferenceEquals(last,end)||end.Operation!=OriginalMapPostEffect3Functor1SourceOperation.FrontendEndRecorded)
        throw new InvalidDataException("Functor1 pair flip needs its exact preceding source End receipt.");RequireStable(end);Owner.Flip(this);
        (TextureInstances,BufferInstances,ConstantBufferInstances)=Owner.ReadSides();phase=5;
        return last=new(this,OriginalMapPostEffect3Functor1SourceOperation.HistoryFlipped);}}
    public OriginalMapPostEffect3Functor1SourceReceipt RecordOuterRestoration(OriginalMapPostEffect3Functor1SourceReceipt source)
    {lock(Owner.Sync){Consume(source,OriginalMapPostEffect3Functor1SourceOperation.OuterRestorationCandidate,5);phase=6;
        return last=new(this,OriginalMapPostEffect3Functor1SourceOperation.ModuleReturned);}}
    public OriginalMapPostEffect3Functor1SourceReceipt ApplyControllerReturnAfterSourceCleanup(OriginalMapPostEffect3Functor1SourceReceipt source)
    {lock(Owner.Sync){Consume(source,OriginalMapPostEffect3Functor1SourceOperation.ControllerCleanupCandidate,6);
        // CPU protocol models the preceding buffer4 pointer/ref cleanup recipe.
        // Actual buffer registry/ref release remains an explicit device adapter.
        if(!controller.ReportModuleReturn(1))throw new InvalidDataException("Pinned Functor1 native return bit differs.");phase=7;
        return last=new(this,OriginalMapPostEffect3Functor1SourceOperation.ControllerReturned);}}
    public void MarkUnreturnedFailure(string actualFailureEvidence)
    {lock(Owner.Sync){RequireCurrent();ArgumentException.ThrowIfNullOrWhiteSpace(actualFailureEvidence);
        if(IsControllerReturned)throw new InvalidDataException("Returned Functor1 cannot be relabelled as unreturned.");
        UnreturnedFailureEvidence=actualFailureEvidence;Owner.Fail(this);}}
}
