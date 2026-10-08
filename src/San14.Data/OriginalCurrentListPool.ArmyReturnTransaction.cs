using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace San14.Data;

public sealed partial class OriginalCurrentListPool
{
    internal OriginalEventInitializationContext ArmyReturnOwner => _state;
    internal OriginalArmyReturnGovernorPoolCapacity ArmyReturnCapacity => new(NodeCapacity - ActiveNodeCount,
        DescriptorCapacity - ActiveDescriptorCount);
    internal bool OwnsArmyReturnDescriptor(OriginalRegistryListHandle descriptor)
        => !descriptor.Identity.HasValue || _liveDescriptors.Contains(descriptor.Identity.Value);

    internal string ArmyReturnFingerprint() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    {
        DescriptorCapacity, NodeCapacity, ActiveNodeCount, _createdNodes,
        FreeDescriptors = _freeDescriptors.ToArray(), LiveDescriptors = _liveDescriptors.Order().ToArray(),
        _freeNodeHead, Fields = _state.ArmyReturnSharedListFingerprint()
    }))));

    internal bool OwnsArmyReturnProfile()
    {
        var profile = _state.HexListAllocationProfile;
        return profile is { NodePoolPresent: true } && profile.ListKeyCapacity == DescriptorCapacity &&
            ReferenceEquals(profile.NodeAllocator?.Target, this) &&
            ReferenceEquals(profile.NodeCountIncrementer?.Target, this) &&
            ReferenceEquals(profile.TemporaryDescriptorAllocator?.Target, this) &&
            ReferenceEquals(profile.TemporaryDescriptorReleaser?.Target, this) &&
            ReferenceEquals(profile.NodeReturner?.Target, this) && ReferenceEquals(profile.NodeCountReturner?.Target, this);
    }

    internal OriginalCurrentListPool DetachForArmyReturn(OriginalEventInitializationContext target)
    {
        if (!OwnsArmyReturnProfile()) throw new InvalidDataException("Current shared list allocation profile is not the complete owned pool.");
        return new(target, this);
    }
    private OriginalCurrentListPool(OriginalEventInitializationContext target, OriginalCurrentListPool source)
    {
        _state = target; DescriptorCapacity = source.DescriptorCapacity; NodeCapacity = source.NodeCapacity;
        foreach (int key in source._freeDescriptors.Reverse()) _freeDescriptors.Push(key);
        _liveDescriptors.UnionWith(source._liveDescriptors); _freeNodeHead = source._freeNodeHead;
        _createdNodes = source._createdNodes; ActiveNodeCount = source.ActiveNodeCount;
        target.HexListAllocationProfile = new()
        {
            NodePoolPresent = true, ListKeyCapacity = DescriptorCapacity,
            TemporaryDescriptorAllocator = AllocateDescriptor, NodeAllocator = AllocateNode,
            NodeCountIncrementer = _ => ActiveNodeCount = checked(ActiveNodeCount + 1),
            NodeReturner = ReturnNodeChain, NodeCountReturner = SubtractNodeCount,
            TemporaryDescriptorReleaser = ReleaseDescriptor,
            InternalPoolLockEnterer = () => Monitor.Enter(_synchronization),
            InternalPoolLockExiter = () => Monitor.Exit(_synchronization)
        };
    }

    //Called only after complete recipe revalidation; no external callbacks or
    //allocation provider is invoked while publishing the detached owned state.
    internal void CommitArmyReturnPool(OriginalCurrentListPool draft)
    {
        if (draft.DescriptorCapacity != DescriptorCapacity || draft.NodeCapacity != NodeCapacity)
            throw new InvalidDataException("Detached pool has a different capacity owner.");
        _state.CommitArmyReturnSharedListFields(draft._state);
        _freeDescriptors.Clear(); foreach (int key in draft._freeDescriptors.Reverse()) _freeDescriptors.Push(key);
        _liveDescriptors.Clear(); _liveDescriptors.UnionWith(draft._liveDescriptors);
        _freeNodeHead = draft._freeNodeHead; _createdNodes = draft._createdNodes; ActiveNodeCount = draft.ActiveNodeCount;
    }
}
