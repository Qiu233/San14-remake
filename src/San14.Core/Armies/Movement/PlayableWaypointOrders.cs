namespace San14.Core;

/// <summary>Replace the terminal action and its ordered intermediate destinations.</summary>
public sealed record SetUnitActionOrder(int ForceId, int UnitId, DeploymentAction Action,
    IReadOnlyList<int> WaypointHexIds) : GameOrder(ForceId);
public sealed record IssueContinuousFacilityConstructionOrder(int ForceId, int UnitId,
    int InitialHexId, int FinalHexId, string DefinitionKey, IReadOnlyList<int> WaypointHexIds) : GameOrder(ForceId);
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
/// <summary>Change one radio pair while retaining each unit's other settings.</summary>
public sealed record UpdateUnitsBehaviorSettingOrder(int ForceId, IReadOnlyList<int> UnitIds,
    UnitBehaviorSetting Setting, bool First) : GameOrder(ForceId);
/// <summary>Withdraw each selected army under its own current return policy.
/// A failed command rejects the selection. Native footer withdrawal retains
/// the source abnormal-member behavior while finalizing all selected rows.</summary>
public sealed record WithdrawUnitsOrder(int ForceId, IReadOnlyList<int> UnitIds,
    IReadOnlyList<ArmyWithdrawalSavedGroup>? SavedGroups = null) : GameOrder(ForceId);

/// <summary>
/// Shared player/AI military command adapter. It wraps existing unit commands so
/// a replacement order cannot leave stale intermediate destinations behind.
/// Command costs and route timing retain the playable ruleset's stated policy.
/// </summary>
public sealed class PlayableWaypointOrders(IOrderRules next, PlayableLandRoutes routes,
    Func<GameState, int, int?, bool>? isHostile = null,
    IPlayableCityEntryResourceRules? cityEntryResources = null,
    IPlayableCityEntryStateRules? cityEntryState = null,
    IPlayableArmyConstructionCommandRules? construction = null,
    IPlayableUnitBehaviorRules? behaviorRules = null,
    IPlayableArmyMovementCommandRules? movementCommands = null) : IOrderRules
{
    // The selector limits additions, not the complete initial map-group vector.
    // Shared orders validate every selected unit and publish one atomic result.
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is IssueArmiesFacilityConstructionOrder group)
        {
            int hexCount = checked(state.MapWidth * state.MapHeight);
            if (group.Armies is null || group.Armies.Count < 1 ||
                group.InitialHexId < 0 || group.InitialHexId >= hexCount ||
                group.FinalHexId is int finalHex && (finalHex < 0 || finalHex >= hexCount) ||
                !FacilityConstructionIntent.IsDefinitionKey(group.DefinitionKey))
                return CommandResult.Reject("Select distinct armies and valid construction sites.", "structure.selection");
            var selected = new ArmyFacilityConstructionSelection[group.Armies.Count];
            var ids = new HashSet<int>();
            for (int index = 0; index < group.Armies.Count; index++)
            {
                var row = group.Armies[index];
                if (row is null || !ids.Add(row.UnitId) || row.WaypointHexIds is null ||
                    row.WaypointHexIds.Count > PlayableWaypointMarch.MaximumWaypoints ||
                    row.WaypointHexIds.Any(hex => hex < 0 || hex >= hexCount))
                    return CommandResult.Reject("Each selected army needs its own valid intermediate destinations.", "structure.selection");
                if (!TryUnit(state, force, row.UnitId, context, out _, out var denied)) return denied!;
                selected[index] = row with { WaypointHexIds = row.WaypointHexIds.ToArray() };
            }
            // The whole People selection supplies the construction qualification.
            // Singleton commands would incorrectly require every commander to qualify.
            return construction is IPlayableArmyGroupConstructionCommandRules commands
                ? commands.IssueArmyGroupFacilityConstruction(state, force,
                    group with { Armies = selected }, context)
                : CommandResult.RuleUnavailable("rules.army_group_construction", "Group construction rules are unavailable.");
        }
        if (order is IssueContinuousFacilityConstructionOrder continuous)
        {
            if (!TryUnit(state, force, continuous.UnitId, context, out var army, out var denied)) return denied!;
            int hexCount = checked(state.MapWidth * state.MapHeight);
            if (continuous.InitialHexId < 0 || continuous.InitialHexId >= hexCount ||
                continuous.FinalHexId < 0 || continuous.FinalHexId >= hexCount ||
                !FacilityConstructionIntent.IsDefinitionKey(continuous.DefinitionKey) ||
                continuous.WaypointHexIds is null || continuous.WaypointHexIds.Count > PlayableWaypointMarch.MaximumWaypoints ||
                continuous.WaypointHexIds.Any(hex => hex < 0 || hex >= hexCount))
                return CommandResult.Reject("The continuous construction sites or route are invalid.", "structure.target");
            return construction is IPlayableArmyContinuousConstructionCommandRules commands
                ? commands.IssueContinuousFacilityConstruction(state, army!, continuous.InitialHexId,
                    continuous.FinalHexId, continuous.DefinitionKey, continuous.WaypointHexIds)
                : CommandResult.RuleUnavailable("rules.army_continuous_construction", "Continuous construction rules are unavailable.");
        }
        if (order is SetUnitFireOrder fire)
            return SetFire(state, force, [fire.UnitId], fire.ClickedHexId, fire.TargetHexId, fire.WaypointHexIds, context);
        if (order is SetUnitsFireOrder fires)
            return SetFire(state, force, fires.UnitIds, fires.ClickedHexId, fires.TargetHexId, fires.WaypointHexIds, context);
        if (order is SetTransportArmiesActionOrder transports)
        {
            if (transports.Action is null || transports.UnitIds is null)
                return CommandResult.Reject("Select transports and an action.", "transport.action_selection");
            if (transports.Action.ActionKey == "stay" && transports.UnitIds.Count != 1)
                return CommandResult.Reject("Transport holding requires one selected army.", "transport.action_selection");
            return ApplyBatch(state, force, transports.UnitIds,
                id => new SetTransportArmyActionOrder(force.Id, id, transports.Action, transports.WaypointHexIds), context);
        }
        if (order is SetTransportArmiesOptionsOrder transportOptions)
            return ApplyBatch(state, force, transportOptions.UnitIds,
                id => new SetTransportArmyOptionsOrder(force.Id, id,
                    transportOptions.SourceBehaviorChoice, transportOptions.ReturnOfficerToSource), context);
        if (order is SetTransportArmyActionOrder transportAction)
        {
            if (!TryTransport(state, force, transportAction.UnitId, context, out var army, out var denied)) return denied!;
            if (transportAction.Action is not { ActionKey: "stay" or "move" or "enter", Target: { }, StructureKindId: null } ||
                transportAction.WaypointHexIds is null || transportAction.WaypointHexIds.Count > PlayableWaypointMarch.MaximumWaypoints ||
                transportAction.WaypointHexIds.Any(hex => hex < 0 || hex >= state.MapWidth * state.MapHeight))
                return CommandResult.Reject("Transport action or intermediate route is invalid.", "transport.action");
            var target = transportAction.Action.Target;
            if (transportAction.Action.ActionKey == "enter"
                ? target.Kind != DeploymentTargetKind.Location || target.Id is < 1 or > 61
                : target.Kind != DeploymentTargetKind.Hex || target.Id < 0 || target.Id >= state.MapWidth * state.MapHeight)
                return CommandResult.Reject("The transport target shape is invalid.", "transport.destination");
            return movementCommands is IPlayableTransportUnitCommandRules commands
                ? commands.IssueTransportArmyAction(state, army!, transportAction.Action, transportAction.WaypointHexIds)
                : CommandResult.RuleUnavailable("rules.transport_unit_command", "Current transport action rules are unavailable.");
        }
        if (order is SetTransportArmyOptionsOrder options)
        {
            if (!TryTransport(state, force, options.UnitId, context, out var army, out var denied)) return denied!;
            if (options.SourceBehaviorChoice is < 0 or > 2 ||
                options.SourceBehaviorChoice is null && options.ReturnOfficerToSource is null)
                return CommandResult.Reject("Select a valid transport option.", "transport.options");
            return movementCommands is IPlayableTransportUnitCommandRules commands
                ? commands.SetTransportArmyOptions(state, army!, options.SourceBehaviorChoice, options.ReturnOfficerToSource)
                : CommandResult.RuleUnavailable("rules.transport_unit_command", "Current transport option rules are unavailable.");
        }
        if (order is WithdrawUnitOrder transportWithdrawal &&
            state.Units.GetValueOrDefault(transportWithdrawal.UnitId)?.Mission?.Kind == UnitMissionKind.Transport)
        {
            if (!TryTransport(state, force, transportWithdrawal.UnitId, context, out var army, out var denied)) return denied!;
            return movementCommands is IPlayableArmyWithdrawalCommandRules commands
                ? commands.IssueWithdrawal(state, army!)
                : CommandResult.RuleUnavailable("rules.army_withdrawal", "Current transport withdrawal rules are unavailable.");
        }
        if (order is WithdrawUnitsOrder withdrawals)
        {
            if (withdrawals.UnitIds is null)
                return CommandResult.Reject("Select deployed units for withdrawal.", "unit.batch");
            if (withdrawals.UnitIds.Any(id => state.Units.GetValueOrDefault(id)?.Mission?.Kind == UnitMissionKind.Transport))
                return WithdrawTransports(state, force, withdrawals.UnitIds, context, withdrawals.SavedGroups);
            if (withdrawals.UnitIds.Any(id => state.Units.GetValueOrDefault(id)?.NativeArmyOrigin is not null))
                return WithdrawOrdinaryArmies(state, force, withdrawals.UnitIds, context, withdrawals.SavedGroups);
            return ApplyBatch(state, force, withdrawals.UnitIds,
                id => new WithdrawUnitOrder(force.Id, id), context);
        }
        if (order is SetUnitsActionOrder actions)
            return ApplyBatch(state, force, actions.UnitIds,
                id => new SetUnitActionOrder(force.Id, id, actions.Action, actions.WaypointHexIds), context);
        if (order is SetUnitsWaypointsOrder routesOrder)
            return ApplyBatch(state, force, routesOrder.UnitIds,
                id => new SetUnitWaypointsOrder(force.Id, id, routesOrder.WaypointHexIds), context);
        if (order is UpdateUnitsBehaviorOrder behaviors)
            return UpdateBehavior(state, force, behaviors.UnitIds, behaviors.Behavior, context);
        if (order is UpdateUnitsBehaviorSettingOrder setting)
            return UpdateBehavior(state, force, setting.UnitIds, null, context, setting.Setting, setting.First);
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
            return UpdateBehavior(state, force, [behavior.UnitId], behavior.Behavior, context);
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
        if (changed.NativeArmyOrigin is not null && (order is BuildStructureOrder or RepairStructureOrder or DemolishStructureOrder) &&
            construction is not null) return result;
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

    private CommandResult SetFire(GameState state, ForceState force, IReadOnlyList<int> ids,
        int clickedHexId, int targetHexId, IReadOnlyList<int> waypointHexIds, CommandContext context)
    {
        if (ids is null || ids.Count < 1 || ids.Distinct().Count() != ids.Count ||
            clickedHexId < 0 || clickedHexId >= state.MapWidth * state.MapHeight ||
            targetHexId < 0 || targetHexId >= state.MapWidth * state.MapHeight ||
            waypointHexIds is null || waypointHexIds.Count > PlayableWaypointMarch.MaximumWaypoints ||
            waypointHexIds.Any(hex => hex < 0 || hex >= state.MapWidth * state.MapHeight))
            return CommandResult.Reject("Select distinct ordinary armies, a Fire target and up to five intermediate points.", "unit.fire_selection");
        var selected = new List<UnitState>(ids.Count);
        // Use the same actor/district ownership boundary before invoking any
        // provider. The provider submits this selection atomically.
        foreach (int id in ids)
        {
            if (!TryUnit(state, force, id, context, out var army, out var denied)) return denied!;
            if (army!.Mission is null or { Kind: UnitMissionKind.Transport } || army.NativeArmyOrigin is null)
                return CommandResult.Reject("Only current ordinary armies can receive Fire orders.", "unit.fire_mission");
            selected.Add(army);
        }
        return movementCommands is IPlayableArmyFireCommandRules commands
            ? commands.IssueArmiesFire(state, force.Id, selected, clickedHexId, targetHexId, waypointHexIds)
            : CommandResult.RuleUnavailable("rules.army_fire_command", "Current army Fire command rules are unavailable.");
    }

    private CommandResult WithdrawOrdinaryArmies(GameState state, ForceState force, IReadOnlyList<int> ids, CommandContext context,
        IReadOnlyList<ArmyWithdrawalSavedGroup>? savedGroups)
    {
        if (ids.Count < 1 || ids.Distinct().Count() != ids.Count)
            return CommandResult.Reject("Select distinct deployed ordinary armies.", "unit.batch");
        var selected = new List<UnitState>(ids.Count);
        foreach (int id in ids)
        {
            if (!TryUnit(state, force, id, context, out var army, out var denied)) return denied!;
            if (army!.Mission is null or { Kind: UnitMissionKind.Transport } || army.NativeArmyOrigin is null)
                return CommandResult.Reject("Select current ordinary armies from the same selection partition.", "unit.withdraw_mission");
            selected.Add(army);
        }
        return movementCommands is IPlayableArmyWithdrawalCommandRules commands
            ? commands.IssueOrdinaryArmiesWithdrawal(state, selected, savedGroups)
            : CommandResult.RuleUnavailable("rules.army_withdrawal", "Current ordinary withdrawal rules are unavailable.");
    }

    private CommandResult WithdrawTransports(GameState state, ForceState force, IReadOnlyList<int> ids, CommandContext context,
        IReadOnlyList<ArmyWithdrawalSavedGroup>? savedGroups)
    {
        if (ids.Count < 1 || ids.Distinct().Count() != ids.Count)
            return CommandResult.Reject("Select distinct deployed transports.", "unit.batch");
        var selected = new List<UnitState>(ids.Count);
        foreach (int id in ids)
        {
            if (!TryTransport(state, force, id, context, out var army, out var denied)) return denied!;
            selected.Add(army!);
        }
        return movementCommands is IPlayableTransportUnitCommandRules commands
            ? commands.IssueTransportArmiesWithdrawal(state, selected, savedGroups)
            : CommandResult.RuleUnavailable("rules.army_withdrawal", "Current transport withdrawal rules are unavailable.");
    }

    private CommandResult UpdateBehavior(GameState state, ForceState force, IReadOnlyList<int> ids,
        DeploymentBehavior? behavior, CommandContext context, UnitBehaviorSetting? setting = null, bool first = false)
    {
        if (ids is null || ids.Count == 0 || ids.Any(id => id < 0) || ids.Distinct().Count() != ids.Count)
            return CommandResult.Reject("Select distinct deployed units for the behavior command.", "unit.batch");
        if (setting is { } selectedSetting && !Enum.IsDefined(selectedSetting))
            return CommandResult.Reject("Military behavior setting is invalid.", "unit.behavior");
        var selected = new List<UnitState>(ids.Count);
        var proposed = new List<DeploymentBehavior>(ids.Count);
        foreach (int id in ids)
        {
            if (!TryUnit(state, force, id, context, out var unit, out var denied)) return denied!;
            DeploymentBehavior? replacement = behavior;
            if (setting is { } field)
            {
                if (unit!.Behavior is not { } saved)
                    return CommandResult.RuleUnavailable("rules.unit_behavior_current",
                        "The unit's current behavior settings are unresolved.");
                if (!Enum.IsDefined(saved.AfterAction))
                    return CommandResult.Reject("Military behavior is invalid.", "unit.behavior");
                replacement = field switch
                {
                    UnitBehaviorSetting.AttackNearby => saved with { AttackNearbyEnemies = first },
                    UnitBehaviorSetting.AllowRetreat => saved with { AllowRetreat = first },
                    UnitBehaviorSetting.Pursue => saved with { PursueRetreatingEnemies = first },
                    UnitBehaviorSetting.AfterRetreat => saved with
                        { AfterAction = first ? DeploymentAfterAction.Retreat : DeploymentAfterAction.Stay },
                    _ => throw new InvalidOperationException("Unsupported military behavior setting.")
                };
            }
            if (replacement is null || !Enum.IsDefined(replacement.AfterAction))
                return CommandResult.Reject("Military behavior is invalid.", "unit.behavior");
            if (unit!.Mission?.Kind == UnitMissionKind.Transport)
                return CommandResult.Reject("Transport has no combat behavior settings.", "unit.behavior_mission");
            selected.Add(unit);
            proposed.Add(replacement);
        }
        if (behaviorRules is null)
        {
            if (state.NativeArmySimulation is not null)
                return CommandResult.RuleUnavailable("rules.unit_behavior", "Current commander behavior restrictions are unavailable.");
        }
        else
        {
            // One current-trait capture for the selection, including batch
            // commands. No partial member write is published on denial.
            var eligibility = setting is { } field
                ? behaviorRules.EvaluateUnitBehaviorSetting(state, selected, field, first)
                : behaviorRules.EvaluateUnitBehaviors(state, selected, behavior!);
            if (eligibility.Allowed is null)
                return CommandResult.RuleUnavailable(eligibility.MissingRuleKey ?? "rules.unit_behavior",
                    eligibility.Reason ?? "Current commander behavior restrictions are unresolved.");
            if (eligibility.Allowed == false)
                return CommandResult.Reject(eligibility.Reason ?? "A commander trait locks this behavior.", "unit.behavior_locked");
        }
        var units = new Dictionary<int, UnitState>(state.Units);
        for (int index = 0; index < selected.Count; index++)
        {
            var unit = selected[index];
            units[unit.Id] = unit with { Behavior = proposed[index] };
        }
        return CommandResult.Accept(state with { Units = units });
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
        if (action?.ActionKey == "build")
        {
            if (action.Target is not { Kind: DeploymentTargetKind.Hex, Id: >= 0 } ||
                action.StructureKindId is not string definition || waypoints is null ||
                waypoints.Count > PlayableWaypointMarch.MaximumWaypoints)
                return CommandResult.Reject("The facility construction target is invalid.", "structure.target");
            return construction?.IssueFacilityConstruction(state, unit!, action.Target.Id, definition, waypoints) ??
                CommandResult.RuleUnavailable("rules.army_construction", "Current facility construction command rules are unavailable.");
        }
        if (action is { ActionKey: "repair" or "demolish", Target.Kind: DeploymentTargetKind.Structure } &&
            construction is IPlayableArmyFacilityWorkCommandRules demolition)
        {
            if (waypoints is null || waypoints.Count > PlayableWaypointMarch.MaximumWaypoints ||
                state.Structures is not { } targetStructures ||
                !targetStructures.TryGetValue(action.Target.Id, out var target) ||
                action.ActionKey == "demolish" && target.ForceId != force.Id)
                return CommandResult.Reject("The selected facility or route is invalid.", "structure.target");
            int demolitionCursor = unit!.HexId;
            foreach (int hex in waypoints)
            {
                if (!routes.CanEnter(hex) || demolitionCursor != hex && routes.Route(demolitionCursor, hex).Length == 0)
                    return CommandResult.Reject("An intermediate destination is unreachable.", "unit.waypoint");
                demolitionCursor = hex;
            }
            int? approach = PlayableWaypointMarch.FacilityApproach(routes, demolitionCursor, target.HexId);
            return approach is int at ? action.ActionKey == "repair"
                ? demolition.IssueFacilityRepair(state, unit!, target.Id, at, waypoints)
                : demolition.IssueFacilityDemolition(state, unit!, target.Id, at, waypoints) :
                CommandResult.Reject("The facility is unreachable.", "structure.destination");
        }
        if (action is { ActionKey: "attack", Target.Kind: DeploymentTargetKind.Structure })
        {
            if (waypoints is null || waypoints.Count > PlayableWaypointMarch.MaximumWaypoints ||
                state.Structures is not { } targetStructures ||
                !targetStructures.TryGetValue(action.Target.Id, out var target) ||
                !(isHostile?.Invoke(state, force.Id, target.ForceId) ?? target.ForceId != force.Id))
                return CommandResult.Reject("The selected hostile facility or route is invalid.", "structure.target");
            int attackCursor = unit!.HexId;
            foreach (int hex in waypoints)
            {
                if (!routes.CanEnter(hex) || attackCursor != hex && routes.Route(attackCursor, hex).Length == 0)
                    return CommandResult.Reject("An intermediate destination is unreachable.", "unit.waypoint");
                attackCursor = hex;
            }
            int? approach = PlayableWaypointMarch.FacilityApproach(routes, attackCursor, target.HexId);
            if (approach is not int at)
                return CommandResult.Reject("The hostile facility is unreachable.", "structure.destination");
            return construction is IPlayableArmyStructureAttackRules attacks
                ? attacks.IssueStructureAttack(state, unit!, target.Id, at, waypoints)
                : CommandResult.RuleUnavailable("rules.army_structure_attack", "Current facility attack rules are unavailable.");
        }
        //The native general target writer preserves the row's construction
        //selection while replacing action/target. Keep the old movement-only
        //adapter for legacy callers, but do not block a closed native target.
        bool nativeTargetReplacement = (state.NativeArmySimulation is not null || unit!.NativeArmyOrigin is not null) &&
            (action?.ActionKey is "enter" or "occupy" || action is
                { ActionKey: "attack", Target.Kind: DeploymentTargetKind.Unit or DeploymentTargetKind.Location });
        if (!nativeTargetReplacement && state.ArmyLifecycle?.Receivers.FindUnit(id)?.FacilityConstruction?.DefinitionKey is not null)
        {
            if (action is not { ActionKey: "move" or "stay", Target.Kind: DeploymentTargetKind.Hex })
                return CommandResult.RuleUnavailable("rules.army_construction_replacement",
                    "Replacement of this current construction command is unresolved.");
            return construction?.IssueMovementAfterConstruction(state, unit!, action, waypoints) ??
                CommandResult.RuleUnavailable("rules.army_construction", "Current facility construction command rules are unavailable.");
        }
        if (!nativeTargetReplacement && unit!.MilitaryPlan is { Action: { ActionKey: "attack", Target.Kind: DeploymentTargetKind.Structure } })
        {
            if (action is not { ActionKey: "move" or "stay", Target.Kind: DeploymentTargetKind.Hex })
                return CommandResult.RuleUnavailable("rules.army_structure_attack_replacement",
                    "Replacement of this current object attack is unresolved.");
            return construction?.IssueMovementAfterConstruction(state, unit, action, waypoints) ??
                CommandResult.RuleUnavailable("rules.army_structure_attack", "Current object attack command rules are unavailable.");
        }
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
        // The native area command stores14/9 against the area's representative
        // Hex. Stage28 selects its later patrol points, not RouteToNearest.
        if ((state.NativeArmySimulation is not null || unit.NativeArmyOrigin is not null) && action.ActionKey == "occupy")
            return movementCommands?.IssueArmyMovementCommand(state, unit, action, waypoints) ??
                CommandResult.RuleUnavailable("rules.army_movement_command", "Current area occupation command rules are unavailable.");
        bool Hostile(int actor, int? target) => isHostile?.Invoke(state, actor, target) ?? target != actor;
        int? destination = PlayableWaypointMarch.ResolveDestination(action, force.Id, cursor,
            state.Cities, state.Gates, state.Units, state.Territory, state.Structures ?? [], routes, Hostile, true);
        if (destination is not int targetHex || !routes.CanEnter(targetHex) ||
            cursor != targetHex && routes.Route(cursor, targetHex).Length == 0)
            return CommandResult.Reject("The military objective is unavailable or unreachable.", "unit.destination");
        if ((state.NativeArmySimulation is not null || unit.NativeArmyOrigin is not null) &&
            (action.ActionKey is "move" or "stay" or "enter" || action is
                { ActionKey: "attack", Target.Kind: DeploymentTargetKind.Unit or DeploymentTargetKind.Location }))
            return movementCommands?.IssueArmyMovementCommand(state, unit, action, waypoints) ??
                CommandResult.RuleUnavailable("rules.army_movement_command",
                    "Current army movement command rules are unavailable.");
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

    private static bool TryTransport(GameState state, ForceState force, int id, CommandContext context,
        out UnitState? unit, out CommandResult? denied)
    {
        if (!TryUnit(state, force, id, context, out unit, out denied)) return false;
        if (unit!.Mission?.Kind != UnitMissionKind.Transport)
        {
            denied = CommandResult.Reject("Only an existing transport may receive this order.", "transport.unit_mission");
            return false;
        }
        if (unit.NativeArmyOrigin is null)
        {
            denied = CommandResult.RuleUnavailable("rules.transport_unit_command", "Current transport receiver identity is unavailable.");
            return false;
        }
        return true;
    }

    private static CommandResult Replace(GameState state, UnitState unit) => CommandResult.Accept(state with
    {
        Units = new Dictionary<int, UnitState>(state.Units) { [unit.Id] = unit }
    });
}
