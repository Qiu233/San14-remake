namespace San14.Core;

/// <summary>
/// The terminal action and ordered intermediate destinations remain separate from
/// the currently pursued enemy. The cursor is persisted rather than rebuilt from
/// the unit's current position after a save or a temporary encounter.
/// </summary>
public sealed record PlayableMilitaryPlan(int[] WaypointHexIds, int NextWaypointIndex,
    DeploymentAction Action, bool Completed = false, int? IncidentalEnemyId = null,
    bool TargetWasEngaged = false, int? RestingHexId = null);

public sealed partial record UnitState
{
    // Null preserves legacy and source uncertainty. Preparing a new playable order
    // explicitly supplies its plan; generic save loading does not invent one.
    public PlayableMilitaryPlan? MilitaryPlan { get; init; }
}

public static class PlayableMilitaryStateValidation
{
    public static void Validate(GameState state)
    {
        int mapSize = checked(state.MapWidth * state.MapHeight);
        foreach (UnitState unit in state.Units.Values)
        {
            if (unit.MilitaryPlan is not { } plan) continue;
            if (plan.WaypointHexIds is null || plan.WaypointHexIds.Length > 5 ||
                plan.NextWaypointIndex < 0 || plan.NextWaypointIndex > plan.WaypointHexIds.Length ||
                plan.WaypointHexIds.Any(hex => hex < 0 || hex >= mapSize) ||
                plan.IncidentalEnemyId is < 0 || plan.IncidentalEnemyId == unit.Id ||
                plan.RestingHexId is int resting && (resting < 0 || resting >= mapSize) ||
                plan.Action is null || plan.Action.Target is null || plan.Action.Target.Id < 0 ||
                !Enum.IsDefined(plan.Action.Target.Kind) ||
                !PlayableWaypointMarch.IsSupportedAction(plan.Action) && !IsNativeFireProjection(state, unit, plan) && !IsNativeFalseReportProjection(unit, plan) ||
                plan.Action.Target.Kind == DeploymentTargetKind.Hex && plan.Action.Target.Id >= mapSize ||
                plan.Action.ActionKey == "capture" && plan.Action.FalseReportDestinationHexId >= mapSize ||
                plan.Action.Target.Kind == DeploymentTargetKind.Unit && plan.Action.Target.Id == unit.Id ||
                plan.Action.Target.Kind == DeploymentTargetKind.Area && !state.Areas.ContainsKey(plan.Action.Target.Id) ||
                plan.Action.Target.Kind == DeploymentTargetKind.Location &&
                    !state.Cities.ContainsKey(plan.Action.Target.Id) && !state.Gates.ContainsKey(plan.Action.Target.Id) ||
                plan.Completed && plan.NextWaypointIndex != plan.WaypointHexIds.Length ||
                plan.Completed && plan.RestingHexId is null ||
                plan.TargetWasEngaged && (plan.Action.ActionKey != "attack" || plan.Action.Target.Kind != DeploymentTargetKind.Unit) ||
                !MatchesMission(unit, plan) ||
                unit.Mission?.Kind is UnitMissionKind.Transport or UnitMissionKind.Retreat)
                throw new InvalidDataException($"Unit {unit.Id} has an invalid military plan.");
            // A target unit may disappear, and a base may change ownership, between
            // orders. Those are runtime action-termination events, not corrupt saves.
        }
    }

    // This is a saved existing-Army projection. It does not admit Fire to
    // new deployments or the provisional coarse movement consumer.
    private static bool IsNativeFireProjection(GameState state, UnitState unit, PlayableMilitaryPlan plan) =>
        unit.NativeArmyOrigin is { NativeSlotIndex: >= 1 and <= 500 } && unit.NativeTacticRuntime is { Status10: 1 } &&
        (unit.FieldAttackPolicy?.UsesConfusionGate == plan.Completed ||
            unit.FieldAttackPolicy?.UsesConfusionGate == true &&
            ArmySavedCommandKnowledge.RetainsReplacedMilitaryPlan(state, unit, plan, plan.Completed ? 13 : 16)) &&
        plan.Action is { ActionKey: "fire", Target.Kind: DeploymentTargetKind.Hex, StructureKindId: null };

    private static bool IsNativeFalseReportProjection(UnitState unit, PlayableMilitaryPlan plan) =>
        unit.NativeArmyOrigin is { NativeSlotIndex: >= 1 and <= 500 } && unit.NativeTacticRuntime is { Status10: 1 } &&
        unit.FieldAttackPolicy?.UsesConfusionGate == true &&
        plan.Action is { ActionKey: "capture", Target.Kind: DeploymentTargetKind.Unit, StructureKindId: null,
            FalseReportDestinationHexId: >= 0 };

    private static bool MatchesMission(UnitState unit, PlayableMilitaryPlan plan)
    {
        if (plan.Completed) return unit.Mission?.Kind == UnitMissionKind.Combat;
        return plan.Action.ActionKey switch
        {
            "occupy" => unit.Mission is { Kind: UnitMissionKind.OccupyArea } mission && mission.TargetAreaId == plan.Action.Target.Id,
            "build" => unit.Mission is { Kind: UnitMissionKind.BuildStructure } mission && mission.StructureKindId == plan.Action.StructureKindId,
            "repair" => unit.Mission is { Kind: UnitMissionKind.RepairStructure } mission && mission.TargetStructureId == plan.Action.Target.Id,
            "demolish" => unit.Mission is { Kind: UnitMissionKind.DemolishStructure } mission && mission.TargetStructureId == plan.Action.Target.Id,
            _ => unit.Mission?.Kind == UnitMissionKind.Combat
        };
    }
}
