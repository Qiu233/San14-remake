namespace San14.Data;

/// <summary>
/// Owned, deterministic node/descriptor storage for the normal initialization
/// profile. Native addresses and locks are not part of the independent runtime.
/// Caller supplies capacities; attaching explicitly creates a new empty pool.
/// </summary>
public sealed partial class OriginalCurrentListPool
{
    private readonly OriginalEventInitializationContext _state;
    private readonly Stack<int> _freeDescriptors = [];
    private readonly HashSet<int> _liveDescriptors = [];
    private OriginalHexListNodeReference _freeNodeHead = new(null);
    private int _createdNodes;
    private readonly object _synchronization = new();
    public int DescriptorCapacity { get; }
    public int NodeCapacity { get; }
    public int ActiveNodeCount { get; private set; }
    public int ActiveDescriptorCount => _liveDescriptors.Count;

    public OriginalCurrentListPool(OriginalEventInitializationContext state,int descriptorCapacity,int nodeCapacity)
    {
        ArgumentNullException.ThrowIfNull(state);
        if(descriptorCapacity<=0)throw new ArgumentOutOfRangeException(nameof(descriptorCapacity));
        if(nodeCapacity<0)throw new ArgumentOutOfRangeException(nameof(nodeCapacity));
        if(state.HexListAllocationProfile is not null)
            throw new InvalidOperationException("An allocation profile is already attached to this initialization state.");
        _state=state;DescriptorCapacity=descriptorCapacity;NodeCapacity=nodeCapacity;
        for(int key=descriptorCapacity-1;key>=0;key--)
        {
            state.SetKnownEmptyHexList(new(key));
            _freeDescriptors.Push(key);
        }
        // The owned runtime provides the count cell queried for a failed descriptor.
        // It does not infer such a value for imported/unprovided native pool state.
        state.SetHexListCount(new(null),0);
        state.HexListAllocationProfile=new()
        {
            NodePoolPresent=true,ListKeyCapacity=descriptorCapacity,
            TemporaryDescriptorAllocator=AllocateDescriptor,
            NodeAllocator=AllocateNode,
            NodeCountIncrementer=_=>ActiveNodeCount=checked(ActiveNodeCount+1),
            NodeReturner=ReturnNodeChain,
            NodeCountReturner=SubtractNodeCount,
            TemporaryDescriptorReleaser=ReleaseDescriptor,
            InternalPoolLockEnterer=()=>Monitor.Enter(_synchronization),
            InternalPoolLockExiter=()=>Monitor.Exit(_synchronization)
        };
        state.RegistryHeadTablePresent=true;
        // This owned initializer runs sequentially. No original worker lock is required.
        state.RegistryLockEnabled=false;
    }

    public OriginalHexListHandle ReserveDescriptor(int key)
    {
        if((uint)key>=(uint)DescriptorCapacity)throw new ArgumentOutOfRangeException(nameof(key));
        if(!_liveDescriptors.Add(key))throw new InvalidOperationException("Descriptor is already reserved; reuse its existing handle for aliases.");
        return new(key);
    }
    public OriginalHexListHandle AllocateDescriptor()
    {
        while(_freeDescriptors.TryPop(out int key))
            if(_liveDescriptors.Add(key))return new(key);
        return new(null);
    }
    private void ReleaseDescriptor(OriginalHexListHandle handle)
    {
        int key=handle.Identity??throw new InvalidDataException("Null descriptor cannot be released to the owned pool.");
        if(!_liveDescriptors.Remove(key))throw new InvalidDataException("Descriptor was not allocated by this pool or is already released.");
        _freeDescriptors.Push(key);
        // Reallocation preserves its current row. The consumer clears nodes first.
    }
    private OriginalHexListNodeReference AllocateNode(OriginalHexListHandle _)
    {
        if(_freeNodeHead.Identity.HasValue)
        {
            OriginalHexListNodeReference node=_freeNodeHead;
            _freeNodeHead=_state.ReadHexListNodeNext(node)
                ??throw new InvalidDataException("Current owned free-chain next pointer is unknown.");
            return node;
        }
        return _createdNodes<NodeCapacity ? new(++_createdNodes) : new(null);
    }
    private void ReturnNodeChain(OriginalHexListHandle _,OriginalHexListNodeReference head,OriginalHexListNodeReference tail)
    {
        _state.SetHexListNodeNext(tail,_freeNodeHead);
        _freeNodeHead=head;
    }
    private void SubtractNodeCount(OriginalHexListHandle _,int count)
    {
        if(count<0 || count>ActiveNodeCount)
            throw new InvalidDataException("Current list count exceeds the owned pool's active node count.");
        ActiveNodeCount-=count;
    }
}
