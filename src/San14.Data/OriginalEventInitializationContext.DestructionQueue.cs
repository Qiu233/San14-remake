using San14.Core;

namespace San14.Data;

public sealed partial class OriginalEventInitializationContext
{
    public CombatSettlementQueue? OwnedDestructionQueue { get; private set; }
    private GameState? _ownedDestructionIdentityState;
    private GameState? _ownedDestructionReadOnlyState;

    public void BindOwnedDestructionQueue(CombatSettlementQueue? queue, GameState state)
    { OwnedDestructionQueue = queue; _ownedDestructionIdentityState = state; _ownedDestructionReadOnlyState = null; }
    public void BindOwnedDestructionReadOnlyView(GameState state)
    { OwnedDestructionQueue = state.PendingCombatSettlements; _ownedDestructionIdentityState = state; _ownedDestructionReadOnlyState = state; }

    public bool? ReadOwnedArmyExclusion233560(OriginalRegistryObjectReference target)
    {
        if (OwnedDestructionQueue is not { } queue || _ownedDestructionIdentityState is not { } state || target.Kind != OriginalRegistryKind.Army) return null;
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
        return OriginalArmyDestructionQueue.ResolveUnitId(state, target.PhysicalSlot) is int id
            ? CombatSettlementRules.ExcludesUnit(queue, id, OriginalArmyDestructionQueue.BindCurrent(state, this)) : false;
    }
    public bool? ReadOwnedBaseExclusion2334B0(OriginalRegistryObjectReference target)
    {
        if (OwnedDestructionQueue is not { } queue || _ownedDestructionIdentityState is not { } state || target.Kind is not (OriginalRegistryKind.City or OriginalRegistryKind.Gate)) return null;
        var entity = OriginalArmyDestructionQueue.ResolveBase(this, new(target.Kind == OriginalRegistryKind.City ? OriginalBaseKind.City : OriginalBaseKind.Gate, target.PhysicalSlot));
        return entity is null ? false : CombatSettlementRules.ExcludesBase(queue, entity, OriginalArmyDestructionQueue.BindCurrent(state, this, _ownedDestructionReadOnlyState is not null));
    }
    internal void CopyOwnedDestructionQueueViewTo(OriginalEventInitializationContext draft)
    {
        draft.OwnedDestructionQueue = OwnedDestructionQueue;
        draft._ownedDestructionIdentityState = _ownedDestructionIdentityState;
        draft._ownedDestructionReadOnlyState = _ownedDestructionReadOnlyState;
        draft._armyReturnPending70 = _armyReturnPending70;
    }
}
