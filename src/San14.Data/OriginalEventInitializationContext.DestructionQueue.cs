using San14.Core;

namespace San14.Data;

public sealed partial class OriginalEventInitializationContext
{
    /// <summary>Reference to this operation's actual current secondary owner.
    /// It is a view of the saved queue, not a rebuilt interaction directory.</summary>
    public NativeArmyDestructionQueueState? OwnedDestructionQueue { get; private set; }
    private GameState? _ownedDestructionReadOnlyState;
    /// <summary>A writable source world already owns its imported primary
    /// pool. Its exclusion queries must observe each actual local list writer.</summary>
    public void BindOwnedDestructionQueue(NativeArmyDestructionQueueState? queue)
    { OwnedDestructionQueue = queue; _ownedDestructionReadOnlyState = null; }
    /// <summary>Normal query contexts borrow the saved pool/header view; they
    /// do not import or copy the complete primary arena on each capture.</summary>
    public void BindOwnedDestructionReadOnlyView(GameState state)
    { OwnedDestructionQueue = state.NativeArmyLifecycle?.DeferredDestructionQueue; _ownedDestructionReadOnlyState = state; }

    public bool? ReadOwnedArmyExclusion233560(OriginalRegistryObjectReference target)
    {
        if (OwnedDestructionQueue is not { } queue || target.Kind != OriginalRegistryKind.Army) return null;
        if (_ownedDestructionReadOnlyState is { } saved)
            return target.PhysicalSlot is int physical ? OriginalOwnedArmyDestructionCurrent.ReadArmyExcluded233560(saved, this, physical) : null;
        if (ReadArmyReturnPending70Descriptor() is not { } descriptor) return null;
        var at = ReadRegistryHead(descriptor);
        if (at is null) return null;
        while (at.Identity is not null)
        {
            var payload = ReadRegistryNodePayload(at);
            if (payload is null) return null;
            if (payload == target) return true;
            at = ReadRegistryNodeNext(at);
            if (at is null) return null;
        }
        return OriginalArmyDestructionQueue.ReadArmyExcludedByQueue233560(queue,
            new(NativeArmyDestructionReceiverKind.Army, target.PhysicalSlot), OriginalArmyDestructionQueue.BindCurrent(this));
    }

    public bool? ReadOwnedBaseExclusion2334B0(OriginalRegistryObjectReference target)
    {
        if (OwnedDestructionQueue is not { } queue || target.Kind is not (OriginalRegistryKind.City or OriginalRegistryKind.Gate)) return null;
        return OriginalArmyDestructionQueue.ReadBaseExcluded2334B0(queue,
            new(target.Kind == OriginalRegistryKind.City ? NativeArmyDestructionReceiverKind.City : NativeArmyDestructionReceiverKind.Gate,
                target.PhysicalSlot), OriginalArmyDestructionQueue.BindCurrent(this));
    }

    internal void CopyOwnedDestructionQueueViewTo(OriginalEventInitializationContext draft)
    {
        draft.OwnedDestructionQueue = OwnedDestructionQueue;
        draft._ownedDestructionReadOnlyState = _ownedDestructionReadOnlyState;
        draft._armyReturnPending70 = _armyReturnPending70;
    }
}
