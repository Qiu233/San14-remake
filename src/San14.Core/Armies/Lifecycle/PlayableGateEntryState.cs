namespace San14.Core;

/// <summary>Complete current Gate return. A missing native leaf keeps the
/// active Army and its Person location; it cannot be replaced by troop-only retirement.</summary>
public interface IPlayableGateEntryStateRules
{
    PlayableCityEntryStateResolution? Return(GameState state, UnitState unit, GateState gate);
    bool RequiresCurrentState(GameState state, UnitState unit, GateState gate) => false;
}

public static class PlayableGateEntryStates
{
    public static bool RequiresCurrentState(GameState state, UnitState unit, GateState gate,
        IPlayableGateEntryStateRules? rules) => rules?.RequiresCurrentState(state, unit, gate) ??
            (unit.NativeArmyOrigin is not null && unit.Mission?.Kind != UnitMissionKind.Transport);

    public static PlayableCityEntryStateResolution? Return(GameState state, UnitState unit, GateState gate,
        IPlayableGateEntryStateRules? rules)
    {
        if (rules is null || unit.Mission?.Kind == UnitMissionKind.Transport ||
            gate.ForceId != unit.ForceId || gate.HexId != unit.HexId) return null;
        var result = rules.Return(state, unit, gate);
        if (result is null) return null;
        GameState next = result.State;
        bool suspended = next?.ArmyReturnCaptiveContinuation is { Caller: ArmyReturnCaptiveCaller.OrdinaryEntry } captive &&
            captive.ArmyUnitId == unit.Id && captive.ArmyPhysicalSlot == unit.NativeArmyOrigin?.NativeSlotIndex &&
            captive.Destination == new SettlementBase(SettlementBaseKind.Gate, gate.Id) &&
            next.Units.ContainsKey(unit.Id);
        if (next is null || string.IsNullOrWhiteSpace(result.RuleId) || next.Date != state.Date ||
            next.NativeClock != state.NativeClock || next.Phase != state.Phase ||
            !next.Gates.TryGetValue(gate.Id, out GateState? receiving) ||
            receiving.ForceId != gate.ForceId || receiving.HexId != gate.HexId ||
            !suspended && (next.Units.ContainsKey(unit.Id) ||
                !next.Officers.TryGetValue(unit.OfficerId, out OfficerState? commander) || commander.Deployed))
            throw new InvalidDataException("Gate entry did not preserve its boundary or its active captive continuation.");
        return result;
    }
}
