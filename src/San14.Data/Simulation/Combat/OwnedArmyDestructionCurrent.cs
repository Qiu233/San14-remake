using San14.Core;

namespace San14.Data;

public sealed record OwnedArmyDestructionAdmission(GameState? State,
    CombatSettlementAdmissionKind? Kind, string? UnresolvedOwner)
{
    public bool RequiresImmediateSettlement => Kind == CombatSettlementAdmissionKind.Full;
    public bool IsPending => Kind is CombatSettlementAdmissionKind.Accepted or CombatSettlementAdmissionKind.Duplicate;
}

/// <summary>Current entity views for the deferred settlement domain.</summary>
public static class OwnedArmyDestructionCurrent
{
    public static void BindOwnedCurrentContext(GameState state, SourceWorldContext current)
    {
        if (state.PendingCombatSettlements is not null) current.BindOwnedDestructionReadOnlyView(state);
    }

    public static bool? ReadArmyExcluded233560(GameState state, SourceWorldContext current, int physical)
    {
        if (state.ReadArmyLifecycle() is not { } arena || (uint)physical >= arena.ReceiverSlots().Length) return null;
        if (arena.Indexes.PendingUnitIds is not { } pending) return null;
        if ((arena.ReceiverSlots()[physical].ActiveUnitId ?? arena.ReceiverSlots()[physical].PendingImage?.Id) is int unitId && pending.Contains(unitId)) return true;
        if (state.PendingCombatSettlements is not { } queue) return null;
        return ArmyDestructionQueue.ResolveUnitId(state, physical) is int id
            ? CombatSettlementRules.ExcludesUnit(queue, id, ArmyDestructionQueue.BindCurrent(state, current, true)) : false;
    }

    public static bool? ReadBaseExcluded2334B0(GameState state, SourceWorldContext current, int location)
    {
        if (state.PendingCombatSettlements is not { } queue) return null;
        SettlementBase? target = state.Cities.ContainsKey(location) ? new(SettlementBaseKind.City, location) :
            state.Gates.ContainsKey(location) ? new(SettlementBaseKind.Gate, location) : null;
        return target is null ? null : CombatSettlementRules.ExcludesBase(queue, target, ArmyDestructionQueue.BindCurrent(state, current, true));
    }

    public static OwnedArmyDestructionAdmission AdmitOrdinaryArmy1745B0(GameState state,
        SourceWorldContext current, int victimPhysical, int? attackerPhysical, int? attackerBaseLocation)
    {
        if (state.PendingCombatSettlements is not { } queue) return new(null, null, "Combat settlements are not initialized.");
        if (state.ReadArmyLifecycle() is not { } lifecycle || (uint)victimPhysical >= lifecycle.ReceiverSlots().Length)
            return new(null, null, "Victim army registry mapping is unavailable.");
        var victimSlot = lifecycle.ReceiverSlots()[victimPhysical];
        bool? victimValid = victimSlot.Commander12 is ushort commander ? commander == 0 ? false : victimSlot.Status10 is byte status ? status != 0 : null : null;
        if (victimValid is null) return new(null, null, "Victim army validity is unavailable.");
        if (!victimValid.Value) return new(state, null, null);
        if (ArmyDestructionQueue.ResolveUnitId(state, victimPhysical) is not int victimId)
            return new(null, null, "Victim army entity mapping is unavailable.");
        var queries = ArmyDestructionQueue.BindCurrent(state, current, true);
        int? attackerId = ArmyDestructionQueue.ResolveUnitId(state, attackerPhysical);
        if (attackerPhysical is int supplied && attackerId is null && current.ReadArmyUnitNativeValidity(new(supplied)) is not false)
            return new(null, null, "Winner army entity mapping is unavailable.");
        bool? attackerValid = attackerId is int winner ? queries.IsUnitValid(winner) : false;
        if (attackerValid is null) return new(null, null, "Winner army validity is unavailable.");
        if (attackerValid.Value && attackerBaseLocation is not null)
            return new(null, null, "A settlement cannot have both an army winner and a base winner.");
        SettlementBase? winnerBase = null;
        if (!attackerValid.Value && attackerBaseLocation is int location)
        {
            winnerBase = state.Cities.ContainsKey(location) ? new(SettlementBaseKind.City, location) :
                state.Gates.ContainsKey(location) ? new(SettlementBaseKind.Gate, location) : null;
            if (winnerBase is null) return new(null, null, "Winner base entity mapping is unavailable.");
            bool? baseValid = queries.IsBaseValid(winnerBase);
            if (baseValid is null) return new(null, null, "Winner base validity is unavailable.");
            if (!baseValid.Value) winnerBase = null;
        }
        var admitted = CombatSettlementRules.Admit(queue, new UnitDefeatRequest(victimId,
            attackerValid.Value ? attackerId : null, winnerBase), queries);
        if (admitted.Kind is null) return new(null, null, admitted.MissingInput);
        return new(state with { PendingCombatSettlements = admitted.Queue }, admitted.Kind, null);
    }

}
