namespace San14.Core;

/// <summary>Replace the terminal action and its ordered intermediate destinations.</summary>
public sealed record SetUnitActionOrder(int ForceId, int UnitId, DeploymentAction Action,
    IReadOnlyList<int> WaypointHexIds) : GameOrder(ForceId);
public sealed record SetUnitWaypointsOrder(int ForceId, int UnitId,
    IReadOnlyList<int> WaypointHexIds) : GameOrder(ForceId);
public sealed record SetUnitsWaypointsOrder(int ForceId, IReadOnlyList<int> UnitIds,
    IReadOnlyList<int> WaypointHexIds) : GameOrder(ForceId);
public sealed record UpdateUnitBehaviorOrder(int ForceId, int UnitId,
    DeploymentBehavior Behavior) : GameOrder(ForceId);
public sealed record SetUnitsActionOrder(int ForceId, IReadOnlyList<int> UnitIds,
    DeploymentAction Action, IReadOnlyList<int> WaypointHexIds) : GameOrder(ForceId);
public sealed record UpdateUnitsBehaviorOrder(int ForceId, IReadOnlyList<int> UnitIds,
    DeploymentBehavior Behavior) : GameOrder(ForceId);
/// <summary>Withdraw each selected army under its own current return policy.
/// A failure in any member rejects the entire selection.</summary>
public sealed record WithdrawUnitsOrder(int ForceId, IReadOnlyList<int> UnitIds) : GameOrder(ForceId);

/// <summary>
/// Shared player/AI military command adapter. It wraps existing unit commands so
/// a replacement order cannot leave stale intermediate destinations behind.
/// Command costs and route timing retain the playable ruleset's stated policy.
/// </summary>
public sealed class PlayableWaypointOrders(IOrderRules next, PlayableLandRoutes routes,
    Func<GameState, int, int?, bool>? isHostile = null,
    IPlayableCityEntryResourceRules? cityEntryResources = null,
    IPlayableCityEntryStateRules? cityEntryState = null) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is WithdrawUnitsOrder withdrawals)
            return ApplyBatch(state, force, withdrawals.UnitIds,
                id => new WithdrawUnitOrder(force.Id, id), context);
        if (order is SetUnitsActionOrder actions)
            return ApplyBatch(state, force, actions.UnitIds,
                id => new SetUnitActionOrder(force.Id, id, actions.Action, actions.WaypointHexIds), context);
        if (order is SetUnitsWaypointsOrder routesOrder)
            return ApplyBatch(state, force, routesOrder.UnitIds,
                id => new SetUnitWaypointsOrder(force.Id, id, routesOrder.WaypointHexIds), context);
        if (order is UpdateUnitsBehaviorOrder behaviors)
            return ApplyBatch(state, force, behaviors.UnitIds,
                id => new UpdateUnitBehaviorOrder(force.Id, id, behaviors.Behavior), context);
        if (order is SetUnitActionOrder action)
            return SetAction(state, force, action.UnitId, action.Action, action.WaypointHexIds, context);
        if (order is SetUnitWaypointsOrder waypoints)
        {
            if (!TryUnit(state, force, waypoints.UnitId, context, out UnitState? unit, out CommandResult? denied))
                return denied!;
            if (unit!.MilitaryPlan is not { Completed: false } plan)
                return CommandResult.Reject("No active military action is available to route.", "unit.action_missing");
            return SetAction(state, force, unit.Id, plan.Action, waypoints.WaypointHexIds, context,
                plan.TargetWasEngaged);
        }
        if (order is UpdateUnitBehaviorOrder behavior)
        {
            if (!TryUnit(state, force, behavior.UnitId, context, out UnitState? unit, out CommandResult? denied))
                return denied!;
            if (behavior.Behavior is null || !Enum.IsDefined(behavior.Behavior.AfterAction))
                return CommandResult.Reject("Military behavior is invalid.", "unit.behavior");
            if (unit!.Mission?.Kind == UnitMissionKind.Transport)
                return CommandResult.Reject("Transport has no combat behavior settings.", "unit.behavior_mission");
            return Replace(state, unit with { Behavior = behavior.Behavior });
        }
        if (order is RetargetUnitOrder redirect && state.Units.TryGetValue(redirect.UnitId, out UnitState? active) &&
            active.Mission?.Kind != UnitMissionKind.Transport)
            return SetAction(state, force, redirect.UnitId,
                new DeploymentAction("move", new DeploymentTarget(DeploymentTargetKind.Hex, redirect.DestinationHexId)), [], context);

        CommandResult result = next.Apply(state, force, order, context);
        if (!result.Accepted) return result;
        int? unitId = order switch
        {
            WithdrawUnitOrder withdrawal => withdrawal.UnitId,
            BuildStructureOrder build => build.UnitId,
            RepairStructureOrder repair => repair.UnitId,
            DemolishStructureOrder demolish => demolish.UnitId,
            OccupyAreaOrder occupation => occupation.UnitId,
            _ => null
        };
        if (unitId is not int id || !result.State!.Units.TryGetValue(id, out UnitState? changed)) return result;
        if (changed.Mission?.Kind == UnitMissionKind.Retreat)
            return Replace(result.State, changed with { MilitaryPlan = null });
        DeploymentAction? mapped = changed.Mission?.Kind switch
        {
            UnitMissionKind.BuildStructure => new("build", new(DeploymentTargetKind.Hex, changed.DestinationHexId), changed.Mission.StructureKindId),
            UnitMissionKind.RepairStructure => new("repair", new(DeploymentTargetKind.Structure, changed.Mission.TargetStructureId!.Value)),
            UnitMissionKind.DemolishStructure => new("demolish", new(DeploymentTargetKind.Structure, changed.Mission.TargetStructureId!.Value)),
            UnitMissionKind.OccupyArea => new("occupy", new(DeploymentTargetKind.Area, changed.Mission.TargetAreaId!.Value)),
            _ => null
        };
        if (mapped?.ActionKey == "occupy" && isHostile is not null)
        {
            int? permittedTarget = PlayableWaypointMarch.ResolveDestination(mapped, force.Id,
                changed.HexId, result.State.Cities, result.State.Gates, result.State.Units,
                result.State.Territory, result.State.Structures ?? [], routes,
                (actor, target) => isHostile(result.State, actor, target), true);
            if (permittedTarget is null)
                return CommandResult.Reject("Occupation area has no reachable land that this force may occupy.", "unit.occupation_complete");
            changed = changed with { DestinationHexId = permittedTarget.Value };
        }
        return mapped is null ? result : Replace(result.State, PlayableWaypointMarch.AttachAction(changed, mapped));
    }

    private CommandResult ApplyBatch(GameState state, ForceState force, IReadOnlyList<int> ids,
        Func<int, GameOrder> command, CommandContext context)
    {
        if (ids is null || ids.Count == 0 || ids.Any(id => id < 0) || ids.Distinct().Count() != ids.Count)
            return CommandResult.Reject("Select distinct deployed units for the batch command.", "unit.batch");
        // Every subcommand creates a fresh unit dictionary. A failure returns no
        // intermediate result, so the caller's state remains completely unchanged.
        GameState candidate = state;
        foreach (int id in ids)
        {
            CommandResult result = Apply(candidate, force, command(id), context);
            if (!result.Accepted) return result;
            candidate = result.State!;
        }
        return CommandResult.Accept(candidate);
    }

    private CommandResult SetAction(GameState state, ForceState force, int id,
        DeploymentAction action, IReadOnlyList<int> waypoints, CommandContext context,
        bool targetWasEngaged = false)
    {
        routes.ValidateWorld(state);
        if (!TryUnit(state, force, id, context, out UnitState? unit, out CommandResult? denied)) return denied!;
        if (unit!.Mission is null or { Kind: UnitMissionKind.Transport })
            return CommandResult.Reject("This unit cannot receive a combat action.", "unit.action_mission");
        if (action?.ActionKey == "enter" && unit.WoundedTroops is > 0 &&
            (cityEntryResources is null && cityEntryState is null || !state.Cities.ContainsKey(action.Target.Id)))
            return CommandResult.RuleUnavailable("unit.wounded_base_transfer",
                "Transfer of known wounded troops into a base is unresolved.");
        if (action is null || action.Target is null || action.Target.Id < 0 ||
            !PlayableWaypointMarch.IsSupportedAction(action) || waypoints is null ||
            waypoints.Count > PlayableWaypointMarch.MaximumWaypoints)
            return CommandResult.Reject("Military action or intermediate destinations are invalid.", "unit.action");
        int cursor = unit.HexId;
        foreach (int hex in waypoints)
        {
            if (!routes.CanEnter(hex) || cursor != hex && routes.Route(cursor, hex).Length == 0)
                return CommandResult.Reject("An intermediate destination is unreachable.", "unit.waypoint");
            cursor = hex;
        }
        bool Hostile(int actor, int? target) => isHostile?.Invoke(state, actor, target) ?? target != actor;
        int? destination = PlayableWaypointMarch.ResolveDestination(action, force.Id, cursor,
            state.Cities, state.Gates, state.Units, state.Territory, state.Structures ?? [], routes, Hostile, true);
        if (destination is not int targetHex || !routes.CanEnter(targetHex) ||
            cursor != targetHex && routes.Route(cursor, targetHex).Length == 0)
            return CommandResult.Reject("The military objective is unavailable or unreachable.", "unit.destination");
        if (action.ActionKey == "build")
        {
            if (unit.SourceLocationId is not int sourceId || !state.Cities.TryGetValue(sourceId, out CityState? source) ||
                source.ForceId != force.Id || !PlayableConstructionRules.CanBuildAt(state, routes, force.Id, targetHex, state.Structures ?? []))
                return CommandResult.Reject("The construction site or source city is unavailable.", "structure.target");
        }
        UnitMission mission = action.ActionKey switch
        {
            "occupy" => new(UnitMissionKind.OccupyArea, TargetAreaId: action.Target.Id),
            "build" => new(UnitMissionKind.BuildStructure, StructureKindId: action.StructureKindId),
            "repair" => new(UnitMissionKind.RepairStructure, TargetStructureId: action.Target.Id),
            "demolish" => new(UnitMissionKind.DemolishStructure, TargetStructureId: action.Target.Id),
            _ => new(UnitMissionKind.Combat)
        };
        UnitState changed = unit with { DestinationHexId = targetHex, Mission = mission };
        changed = PlayableWaypointMarch.AttachAction(changed, action, waypoints);
        // Replacing only intermediate destinations keeps the same attack. Its
        // already-fought target must still obey the saved pursuit switch.
        if (targetWasEngaged)
            changed = changed with { MilitaryPlan = changed.MilitaryPlan! with { TargetWasEngaged = true } };
        return Replace(state, changed);
    }

    private static bool TryUnit(GameState state, ForceState force, int id, CommandContext context,
        out UnitState? unit, out CommandResult? denied) =>
        PlayableUnitControl.TryResolve(state, force, id, context, out unit, out denied);

    private static CommandResult Replace(GameState state, UnitState unit) => CommandResult.Accept(state with
    {
        Units = new Dictionary<int, UnitState>(state.Units) { [unit.Id] = unit }
    });
}
