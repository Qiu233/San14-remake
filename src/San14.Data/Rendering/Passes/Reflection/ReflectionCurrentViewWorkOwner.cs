namespace San14.Data;

/// <summary>Actual per-View Field work. The Field and snapshot identity are
/// retained independently of an optional successfully prepared capture View.</summary>
public sealed class ReflectionCurrentViewNode
{
    internal ReflectionCurrentViewNode(ReflectionCurrentViewWorkOwner owner,
        ReflectionCaptureFieldInstance field, int ordinal, uint flags, int offset,
        int capacity, bool captureRequested, ReflectionCaptureCullingSource? capture,
        ReflectionCurrentViewNode? nextRoot)
    {
        Owner=owner; Field=field; ManagerPosition=field.ManagerPosition; SourceOrdinal=ordinal; SnapshotFlags=flags;
        ArenaByteOffset=offset; Capacity=capacity; CaptureRequested=captureRequested;
        Capture=capture; NextRoot=nextRoot;
        CaptureCounters=Array.AsReadOnly(Enumerable.Range(0,captureRequested?8:0)
            .Select(_=>new ReflectionCurrentPrimitiveCounter(capacity)).ToArray());
    }
    public ReflectionCurrentViewWorkOwner Owner {get;}
    public ReflectionCaptureFieldInstance Field {get;}
    public System.Numerics.Vector4 ManagerPosition {get;}
    public int SourceOrdinal {get;}
    public uint SnapshotFlags {get;}
    public int ArenaByteOffset {get;}
    public int Capacity {get;}
    public bool CaptureRequested {get;}
    public ReflectionCaptureCullingSource? Capture {get;}
    public ReflectionCurrentViewNode? NextRoot {get;}
    public ReflectionCurrentViewNode? NextChild {get;internal set;}
    public IReadOnlyList<ReflectionCurrentPrimitiveCounter> CaptureCounters {get;}
}

/// <summary>Negative encoded count during collection, positive count after
/// sealing. Pointer capacity has the source's strict less-than boundary.</summary>
public sealed class ReflectionCurrentPrimitiveCounter
{
    private readonly List<DeferredEarlyShadowCurrentPrimitiveTuple> tuples=[];
    private readonly IReadOnlyList<DeferredEarlyShadowCurrentPrimitiveTuple> readOnlyTuples;
    internal ReflectionCurrentPrimitiveCounter(int capacity){Capacity=capacity;readOnlyTuples=tuples.AsReadOnly();}
    public int Capacity {get;}
    public int EncodedCount {get;private set;}=-1;
    public IReadOnlyList<DeferredEarlyShadowCurrentPrimitiveTuple> InsertionOrder=>readOnlyTuples;
    internal bool Append(IReadOnlyList<DeferredEarlyShadowCurrentPrimitiveTuple> span)
    {
        if(EncodedCount>=0)throw new InvalidOperationException("Capture counter was sealed.");
        if(span.Count>=Capacity-tuples.Count)return false;
        tuples.AddRange(span); EncodedCount-=span.Count; return true;
    }
    internal void Seal()=>EncodedCount=tuples.Count;
}

public sealed record ReflectionCurrentNodePublication(ReflectionCurrentViewNode? PublishedNode,
    ReflectionCurrentViewNode? NativeReturnedNode, bool WorkItemAllocated, bool WorkItemQueued);

/// <summary>D69760/D06E30: current root1C80 and work1C90 share the acquired
/// Frame18 cursor with Main/shadow tuples. A published root is retained if the
/// final work allocation or strict work-list append fails.</summary>
public sealed class ReflectionCurrentViewWorkOwner
{
    private readonly List<ReflectionCurrentViewNode> work=[];
    private readonly object gate=new();
    private bool sealedForCollection;
    private readonly IReadOnlyList<ReflectionCurrentViewNode> readOnlyWork;
    private ReflectionCurrentViewWorkOwner(DeferredEarlyShadowCurrentCommonFrameArena actualFrame,
        string actualViewIdentity,int workStorageOffset)
    {
        Frame=actualFrame;ViewIdentity=actualViewIdentity;WorkStorageArenaByteOffset=workStorageOffset;readOnlyWork=work.AsReadOnly();
    }
    /// <summary>D06FA0 reserves sixteen pointer slots after the current View's
    /// preceding allocations. Do not invoke this for already accounted storage.
    /// An unavailable native work-pointer block is an unsupported invalid View,
    /// not an empty work owner with fabricated available pointer slots.</summary>
    public static ReflectionCurrentViewWorkOwner ConstructFreshCurrentAfterPriorViewAllocations(
        DeferredEarlyShadowCurrentCommonFrameArena actualFrame,string actualViewIdentity)
    {
        ArgumentNullException.ThrowIfNull(actualFrame);actualFrame.RequireCurrent();
        ArgumentException.ThrowIfNullOrWhiteSpace(actualViewIdentity);
        if(!actualFrame.Arena.TryReserve(0x80,out int offset))
            throw new InvalidOperationException("The actual current View has no allocated sixteen-entry Field work block.");
        return new(actualFrame,actualViewIdentity,offset);
    }
    public DeferredEarlyShadowCurrentCommonFrameArena Frame {get;}
    public string ViewIdentity {get;}
    public int WorkCapacity=>16;
    public int WorkStorageArenaByteOffset {get;}
    private ReflectionCurrentViewNode? root;
    public ReflectionCurrentViewNode? Root {get{RequireCurrent();return root;}private set=>root=value;}
    public int RootCount {get;private set;}
    public int EncodedWorkCount {get;private set;}=-1;
    public IReadOnlyList<ReflectionCurrentViewNode> WorkInsertionOrder{get{RequireCurrent();return readOnlyWork;}}
    public bool IsSealedForCollection=>sealedForCollection;
    public void RequireCurrent()=>Frame.RequireCurrent();

    /// <summary>One actual D69760 call after scheduling. Null capture means
    /// that the real capture preparation failed; it never removes the node.
    /// Capacity5000 is F37E80's normal call, not the target resource's depth.</summary>
    public ReflectionCurrentNodePublication PublishActualNode(ReflectionCaptureFieldInstance actualField,
        int actualScheduledStart, int actualChildIndex, uint actualSnapshotFlags,
        ReflectionCaptureCullingSource? actualPreparedCapture,
        ReflectionCurrentViewNode? actualPreviousChild=null, int actualPrimitiveCapacity=5000)
    {
        ArgumentNullException.ThrowIfNull(actualField); actualField.RequireCurrent(); RequireCurrent();
        if(actualChildIndex<0||actualPrimitiveCapacity<0)throw new ArgumentOutOfRangeException(nameof(actualChildIndex));
        if(actualScheduledStart<0&&actualPreparedCapture is not null)
            throw new InvalidDataException("A negative scheduled start cannot have a capture View.");
        if(actualPreviousChild is not null&&!ReferenceEquals(actualPreviousChild.Owner,this))
            throw new InvalidDataException("A child belongs to another current View.");
        lock(gate)
        {
            if(sealedForCollection)throw new InvalidOperationException("Field registration has finished for this current View.");
            RequireCurrent();
            int stride=checked((actualPrimitiveCapacity*8+15)&~15);
            bool requested=actualScheduledStart>=0;
            int bytes=checked((stride*3+0x2bf+(requested?stride*8+0x80:0))&~15);
            if(!Frame.Arena.TryReserve(bytes,out int offset))return new(null,null,false,false);
            var node=new ReflectionCurrentViewNode(this,actualField,
                requested?unchecked(actualScheduledStart+actualChildIndex):actualScheduledStart,
                actualSnapshotFlags,offset,actualPrimitiveCapacity,requested,actualPreparedCapture,
                actualPreviousChild is null?Root:null);
            // This publication precedes both the 16-byte tail allocation and
            // work-list reservation. Neither refusal rolls it back.
            if(actualPreviousChild is null){Root=node;RootCount++;}
            else actualPreviousChild.NextChild=node;
            if(!Frame.Arena.TryReserve(16,out _))return new(node,null,false,false);
            if(EncodedWorkCount>=0||work.Count+1>=WorkCapacity)return new(node,null,true,false);
            work.Add(node); EncodedWorkCount--; return new(node,node,true,true);
        }
    }

    /// <summary>D695D0's one actual View call. Its first root call occurs even
    /// when count70 is zero. A failed later native return becomes the following
    /// call's null parent, while any already published node remains reachable.
    /// The complete prepared capture array contains actual per-cell results,
    /// including failed/null results, not a success delegate.</summary>
    public IReadOnlyList<ReflectionCurrentNodePublication> RegisterActualScheduledField(
        ReflectionCaptureFieldInstance actualField,int actualScheduledStart,uint actualCount70,
        uint actualSnapshotFlags,IReadOnlyList<ReflectionCaptureCullingSource?> actualPreparedCaptures)
    {
        ArgumentNullException.ThrowIfNull(actualPreparedCaptures);
        uint calls=actualCount70>1?actualCount70:1;
        if(calls>int.MaxValue||actualPreparedCaptures.Count!=(int)calls)
            throw new InvalidDataException("Field registration needs one actual prepared result per reached native child call.");
        var results=new List<ReflectionCurrentNodePublication>((int)calls);
        var first=PublishActualNode(actualField,actualScheduledStart,0,actualSnapshotFlags,actualPreparedCaptures[0]);
        results.Add(first);
        if(first.NativeReturnedNode is null)return results.AsReadOnly();
        var previous=first.NativeReturnedNode;
        for(int child=1;child<(int)calls;child++)
        {
            var current=PublishActualNode(actualField,actualScheduledStart,child,actualSnapshotFlags,
                actualPreparedCaptures[child],previous);
            results.Add(current);previous=current.NativeReturnedNode;
        }
        return results.AsReadOnly();
    }

    public void SealRegistration()
    {
        lock(gate){RequireCurrent();sealedForCollection=true;}
    }

    internal void SealCaptureCounters()
    {
        RequireCurrent();
        foreach(var node in work)foreach(var counter in node.CaptureCounters)counter.Seal();
    }
}
