namespace San14.Core;

/// <summary>
/// Ordered intermediate destinations and action termination for the temporary
/// playable movement adapter. The official manual establishes five waypoints and
/// the four behavior choices. Route cost, encounter radius and phase timing remain
/// explicitly provisional; this class does not introduce a combat formula.
/// </summary>
public static class PlayableWaypointMarch
{
    public const int MaximumWaypoints = 5;
    public const int ProvisionalNearbyEnemyRadius = 1;

    public static bool IsSupportedAction(DeploymentAction action) => action.ActionKey switch
    {
        "move" or "stay" => action.Target.Kind == DeploymentTargetKind.Hex && action.StructureKindId is null,
        "attack" => action.Target.Kind is DeploymentTargetKind.Unit or DeploymentTargetKind.Location && action.StructureKindId is null,
        "enter" => action.Target.Kind == DeploymentTargetKind.Location && action.StructureKindId is null,
        "occupy" => action.Target.Kind == DeploymentTargetKind.Area && action.StructureKindId is null,
        "build" => action.Target.Kind == DeploymentTargetKind.Hex &&
            (PlayableConstructionRules.IsSupportedKind(action.StructureKindId) || FacilityConstructionIntent.IsDefinitionKey(action.StructureKindId)),
        "repair" or "demolish" => action.Target.Kind == DeploymentTargetKind.Structure && action.StructureKindId is null,
        _ => false
    };

    public static DeploymentEligibility ValidateDeploymentPath(GameState state, DeploymentSource source,
        DeploymentUnitRequest request, PlayableLandRoutes routes,
        Func<int, int?, bool>? isHostile = null)
    {
        routes.ValidateWorld(state);
        if (request.Actions is null || request.Actions.Count != 1 || request.Behavior is null ||
            !Enum.IsDefined(request.Behavior.AfterAction) || request.WaypointHexIds is null ||
            request.WaypointHexIds.Count > MaximumWaypoints)
            return new(DeploymentDecisionStatus.Denied, "Military action or behavior is invalid.");
        DeploymentAction action = request.Actions[0];
        if (action is null || action.Target is null || action.Target.Id < 0 || !IsSupportedAction(action))
            return new(DeploymentDecisionStatus.Denied, "This terminal military action is unsupported.");
        int cursor = source.HexId;
        foreach (int waypoint in request.WaypointHexIds)
        {
            if (!routes.CanEnter(waypoint) || cursor != waypoint && routes.Route(cursor, waypoint).Length == 0)
                return new(DeploymentDecisionStatus.Denied, "An intermediate destination is unreachable.");
            cursor = waypoint;
        }
        int? destination = ResolveDestination(action, source.ForceId, cursor, state.Cities,
            state.Gates, state.Units, state.Territory, state.Structures ?? [], routes,
            isHostile, validate: true);
        if (destination is not int hex || !routes.CanEnter(hex) ||
            cursor != hex && routes.Route(cursor, hex).Length == 0)
            return new(DeploymentDecisionStatus.Denied, "The terminal action is unavailable or unreachable.");
        if (action.ActionKey == "build" && (!state.Cities.ContainsKey(source.LocationId) ||
            !PlayableConstructionRules.CanBuildAt(state, routes, source.ForceId, hex, state.Structures ?? [])))
            return new(DeploymentDecisionStatus.Denied, "The selected construction site is unavailable.");
        return new(DeploymentDecisionStatus.Allowed);
    }

    public static UnitState AttachDeployment(UnitState unit, PreparedDeployment deployment) => unit with
    {
        MilitaryPlan = new PlayableMilitaryPlan(deployment.WaypointHexIds.ToArray(), 0,
            deployment.Actions.Single())
    };

    public static UnitState AttachAction(UnitState unit, DeploymentAction action,
        IReadOnlyList<int>? waypoints = null) => unit with
    {
        MilitaryPlan = new PlayableMilitaryPlan(waypoints?.ToArray() ?? [], 0, action)
    };

    /// <summary>Whole ordered route, excluding its start. The existing period step budget applies once to this route.</summary>
    public static int[] RouteForUnit(UnitState unit, PlayableLandRoutes routes)
    {
        PlayableMilitaryPlan? plan = unit.MilitaryPlan;
        if (plan?.IncidentalEnemyId is not null || plan is null)
            return routes.Route(unit.HexId, unit.DestinationHexId);
        if (plan.Completed) return routes.Route(unit.HexId, plan.RestingHexId!.Value);
        var result = new List<int>();
        int cursor = unit.HexId;
        for (int i = plan.NextWaypointIndex; i < plan.WaypointHexIds.Length; i++)
            if (!Append(plan.WaypointHexIds[i])) return [];
        if (!Append(unit.DestinationHexId)) return [];
        return result.ToArray();

        bool Append(int destination)
        {
            if (cursor == destination) return true;
            int[] segment = routes.Route(cursor, destination);
            if (segment.Length == 0) return false;
            result.AddRange(segment);
            cursor = destination;
            return true;
        }
    }

    /// <summary>Consumes only actually traversed cells. A blocked enemy cell must be excluded by the movement adapter.</summary>
    public static UnitState ConsumeTraversed(UnitState unit, ReadOnlySpan<int> traversed)
    {
        if (unit.MilitaryPlan is not { Completed: false, IncidentalEnemyId: null } plan) return unit;
        int index = plan.NextWaypointIndex;
        while (index < plan.WaypointHexIds.Length && plan.WaypointHexIds[index] == unit.HexId) index++;
        foreach (int hex in traversed)
            while (index < plan.WaypointHexIds.Length && plan.WaypointHexIds[index] == hex) index++;
        return index == plan.NextWaypointIndex ? unit : unit with { MilitaryPlan = plan with { NextWaypointIndex = index } };
    }

    public static UnitState RecordEncounter(UnitState unit, int enemyId) =>
        unit.MilitaryPlan is { } plan && plan.Action.ActionKey == "attack" &&
        plan.Action.Target is { Kind: DeploymentTargetKind.Unit } target && target.Id == enemyId
            ? unit with { MilitaryPlan = plan with { TargetWasEngaged = true } } : unit;

    /// <summary>Explicit attacks remain attacks even when opportunistic engagement is disabled.</summary>
    public static bool CanEngageEncounter(UnitState unit, UnitState enemy,
        Func<int, int?, bool>? isHostile = null) =>
        Hostile(unit.ForceId, enemy.ForceId, isHostile) &&
        unit.Mission?.Kind is not (UnitMissionKind.Transport or UnitMissionKind.Retreat) &&
        (unit.Behavior?.AttackNearbyEnemies != false ||
         unit.MilitaryPlan is { Completed: false, Action.ActionKey: "attack", Action.Target.Kind: DeploymentTargetKind.Unit } plan &&
         plan.Action.Target.Id == enemy.Id);

    public static bool CanEngageLocation(UnitState unit, int locationId, int? ownerForceId,
        Func<int, int?, bool>? isHostile = null) =>
        unit.Mission?.Kind is not (UnitMissionKind.Transport or UnitMissionKind.Retreat) &&
        (ownerForceId is null || Hostile(unit.ForceId, ownerForceId, isHostile) &&
        (unit.Behavior?.AttackNearbyEnemies != false ||
         unit.MilitaryPlan is { Completed: false, Action.ActionKey: "attack", Action.Target.Kind: DeploymentTargetKind.Location } plan &&
         plan.Action.Target.Id == locationId));

    public static UnitState PrepareForMovement(UnitState unit,
        Dictionary<int, CityState> cities, Dictionary<int, GateState> gates,
        Dictionary<int, OfficerState> officers, Dictionary<int, UnitState> units,
        IReadOnlyDictionary<int, TerritoryState> territory, PlayableLandRoutes routes,
        int mapWidth, List<PeriodReportEntry> reports,
        Func<int, int?, bool>? isHostile = null,
        IReadOnlyDictionary<int, StructureState>? structures = null)
    {
        if (unit.Mission?.Kind is UnitMissionKind.Retreat or UnitMissionKind.Transport)
            return unit.MilitaryPlan is null ? unit : unit with { MilitaryPlan = null };
        unit = ConsumeTraversed(unit, []);
        if (unit.MilitaryPlan is not { } plan) return unit;
        if (plan.Completed) unit = unit with { DestinationHexId = plan.RestingHexId!.Value };
        if (!plan.Completed)
        {
            int start = plan.NextWaypointIndex < plan.WaypointHexIds.Length
                ? plan.WaypointHexIds[^1] : unit.HexId;
            int? final = ResolveDestination(plan.Action, unit.ForceId, start, cities,
                gates, units, territory, structures ?? new Dictionary<int, StructureState>(),
                routes, isHostile, validate: false);
            bool fleeingTarget = plan.TargetWasEngaged && plan.Action.ActionKey == "attack" &&
                plan.Action.Target.Kind == DeploymentTargetKind.Unit &&
                units.TryGetValue(plan.Action.Target.Id, out UnitState? enemy) &&
                enemy.Mission?.Kind == UnitMissionKind.Retreat && unit.Behavior?.PursueRetreatingEnemies != true;
            // Completion and its typed report belong to the occupation module.
            // An empty unowned-cell search is successful completion after all
            // intermediate destinations, rather than an interrupted action.
            if (final is null && plan.Action.ActionKey == "occupy" &&
                plan.NextWaypointIndex >= plan.WaypointHexIds.Length)
                return unit with { DestinationHexId = unit.HexId };
            if (final is null || fleeingTarget)
                return CompleteAction(unit, cities, gates, officers, routes, mapWidth, reports, interrupted: true);
            unit = unit with { DestinationHexId = final.Value };
        }
        if (plan.IncidentalEnemyId is int incidental)
        {
            //A temporary nearby target follows the current behavior switch.
            //Disabling it resumes the existing goal and waypoint cursor; an
            //explicit attack remains independent of this switch.
            if (unit.Behavior?.AttackNearbyEnemies != false &&
                units.TryGetValue(incidental, out UnitState? enemy) &&
                Hostile(unit.ForceId, enemy.ForceId, isHostile) &&
                (enemy.Mission?.Kind != UnitMissionKind.Retreat || unit.Behavior?.PursueRetreatingEnemies == true))
                return unit with { DestinationHexId = enemy.HexId };
            plan = plan with { IncidentalEnemyId = null };
            unit = unit with { MilitaryPlan = plan };
        }
        if (unit.Behavior?.AttackNearbyEnemies == true &&
            unit.Mission?.Kind is UnitMissionKind.Combat or UnitMissionKind.OccupyArea)
        {
            UnitState? nearby = units.Values.Where(enemy => enemy.Id != unit.Id &&
                    Hostile(unit.ForceId, enemy.ForceId, isHostile) &&
                    (enemy.Mission?.Kind != UnitMissionKind.Retreat || unit.Behavior.PursueRetreatingEnemies) &&
                    routes.WithinRadius(unit.HexId, enemy.HexId, ProvisionalNearbyEnemyRadius) &&
                    (unit.HexId == enemy.HexId || routes.Route(unit.HexId, enemy.HexId).Length > 0))
                .OrderBy(enemy => enemy.Id).FirstOrDefault();
            if (nearby is not null && !(!plan.Completed && plan.Action.ActionKey == "attack" &&
                    plan.Action.Target.Kind == DeploymentTargetKind.Unit && plan.Action.Target.Id == nearby.Id))
                return unit with { DestinationHexId = nearby.HexId,
                    MilitaryPlan = plan with { IncidentalEnemyId = nearby.Id } };
        }
        return unit;
    }

    public static void AfterMovement(Dictionary<int, CityState> cities,
        Dictionary<int, GateState> gates, Dictionary<int, OfficerState> officers,
        Dictionary<int, UnitState> units, IReadOnlyDictionary<int, TerritoryState> territory,
        PlayableLandRoutes routes, int mapWidth, List<PeriodReportEntry> reports,
        Func<int, int?, bool>? isHostile = null,
        IReadOnlyDictionary<int, StructureState>? structures = null,
        Action<UnitState>? onRetired = null,
        Func<UnitState, int, PlayableCityEntryResourceQuote?>? cityEntryResources = null,
        Func<UnitState, int, string?>? completeCityEntry = null,
        Func<UnitState, int, bool>? requiresCurrentCityEntry = null)
    {
        foreach (UnitState starting in units.Values.OrderBy(unit => unit.Id).ToArray())
        {
            if (!units.TryGetValue(starting.Id, out UnitState? unit)) continue;
            if (unit.MilitaryPlan is not { } plan) continue;
            if (!plan.Completed && plan.Action.ActionKey is "attack" or "enter" &&
                ResolveDestination(plan.Action, unit.ForceId, unit.HexId, cities, gates, units,
                    territory, structures ?? new Dictionary<int, StructureState>(), routes, isHostile, false) is null)
            {
                units[unit.Id] = CompleteAction(unit, cities, gates, officers, routes, mapWidth, reports,
                    interrupted: plan.Action.ActionKey == "enter");
                continue;
            }
            if (plan.IncidentalEnemyId is int incidental &&
                (!units.TryGetValue(incidental, out UnitState? enemy) || !Hostile(unit.ForceId, enemy.ForceId, isHostile)))
            {
                plan = plan with { IncidentalEnemyId = null };
                int restoreDestination = plan.Completed ? plan.RestingHexId!.Value :
                    ResolveDestination(plan.Action, unit.ForceId, unit.HexId, cities, gates, units,
                        territory, structures ?? new Dictionary<int, StructureState>(), routes, isHostile, false)
                    ?? unit.HexId;
                unit = unit with { MilitaryPlan = plan, DestinationHexId = restoreDestination };
                units[unit.Id] = unit;
            }
            if (plan.Completed || plan.IncidentalEnemyId is not null ||
                plan.NextWaypointIndex < plan.WaypointHexIds.Length || unit.HexId != unit.DestinationHexId) continue;
            if (plan.Action.ActionKey == "enter")
            {
                int locationId = plan.Action.Target.Id;
                if (completeCityEntry?.Invoke(unit, locationId) is string ruleId)
                {
                    reports.Add(new("unit_entered", unit.ForceId, cities.ContainsKey(locationId) ? locationId : null, unit.OfficerId,
                        unit.Troops, unit.HexId, unit.HexId) { LocationId = locationId, RuleId = ruleId });
                    continue;
                }
                if (requiresCurrentCityEntry?.Invoke(unit, locationId) == true)
                {
                    reports.Add(new("unit_enter_unresolved", unit.ForceId, cities.ContainsKey(locationId) ? locationId : null, unit.OfficerId,
                        FromHexId: unit.HexId, ToHexId: unit.HexId)
                    { LocationId = locationId, RuleId = PlayableCityEntryStates.UnresolvedRuleId });
                    continue;
                }
                PlayableCityEntryResourceQuote? sourceResources = cityEntryResources?.Invoke(unit, locationId);
                if (unit.WoundedTroops is > 0 && sourceResources is null)
                {
                    reports.Add(new PeriodReportEntry("wounded_return_unresolved", unit.ForceId,
                        cities.ContainsKey(locationId) ? locationId : null, unit.OfficerId, unit.WoundedTroops.Value,
                        unit.HexId, unit.HexId) { LocationId = locationId, RuleId = "unit.wounded_base_transfer" });
                    units[unit.Id] = CompleteAction(unit, cities, gates, officers, routes, mapWidth, reports, true);
                    continue;
                }
                if (FriendlyBase(locationId, unit.ForceId, cities, gates) is not int friendlyHex || friendlyHex != unit.HexId)
                {
                    units[unit.Id] = CompleteAction(unit, cities, gates, officers, routes, mapWidth, reports, true);
                    continue;
                }
                // Combat units carry no food; use the same source-agnostic troop and
                // officer accounting as return-to-base. Distinguish the report kind.
                int reportsBefore = reports.Count;
                PlayableUnitRetreat.Complete(unit, locationId, cities, gates, officers, units, reports, onRetired, sourceResources);
                reports[reportsBefore] = reports[reportsBefore] with { Kind = "unit_entered" };
                continue;
            }
            if (plan.Action.ActionKey is "move" or "stay")
            {
                units[unit.Id] = plan.Action.ActionKey == "stay"
                    ? unit with { MilitaryPlan = plan with { Completed = true, RestingHexId = unit.HexId } }
                    : CompleteAction(unit, cities, gates, officers, routes, mapWidth, reports);
            }
            else if (plan.Action.ActionKey == "attack" && ResolveDestination(plan.Action,
                         unit.ForceId, unit.HexId, cities, gates, units, territory,
                         structures ?? new Dictionary<int, StructureState>(), routes, isHostile, false) is null)
                units[unit.Id] = CompleteAction(unit, cities, gates, officers, routes, mapWidth, reports);
            // Occupation and construction finish in their existing modules; call
            // CompleteAction at that module's successful or impossible terminal state.
        }
    }

    public static UnitState CompleteAction(UnitState unit,
        IReadOnlyDictionary<int, CityState> cities, IReadOnlyDictionary<int, GateState> gates,
        IReadOnlyDictionary<int, OfficerState> officers, PlayableLandRoutes routes,
        int mapWidth, List<PeriodReportEntry> reports, bool interrupted = false)
    {
        if (unit.MilitaryPlan is not { } plan) return unit;
        PlayableMilitaryPlan completed = plan with
        {
            NextWaypointIndex = plan.WaypointHexIds.Length, Completed = true, IncidentalEnemyId = null,
            RestingHexId = unit.HexId
        };
        reports.Add(new PeriodReportEntry(interrupted ? "unit_action_cancelled" : "unit_action_complete",
            unit.ForceId, null, unit.OfficerId, 0, unit.HexId, unit.HexId));
        if (unit.Behavior?.AfterAction == DeploymentAfterAction.Retreat &&
            PlayableUnitRetreat.Destination(unit, cities, gates, officers, routes, mapWidth) is { } home)
        {
            reports.Add(new PeriodReportEntry("retreat_started", unit.ForceId,
                cities.ContainsKey(home.LocationId) ? home.LocationId : null,
                unit.OfficerId, unit.Morale, unit.HexId, home.HexId));
            return unit with { MilitaryPlan = null, DestinationHexId = home.HexId,
                Mission = new UnitMission(UnitMissionKind.Retreat) };
        }
        if (unit.Behavior?.AfterAction == DeploymentAfterAction.Retreat)
            reports.Add(new PeriodReportEntry("retreat_blocked", unit.ForceId, null,
                unit.OfficerId, 0, unit.HexId, unit.HexId));
        return unit with { MilitaryPlan = completed, DestinationHexId = unit.HexId,
            Mission = new UnitMission(UnitMissionKind.Combat) };
    }

    internal static int? ResolveDestination(DeploymentAction action, int forceId, int startHex,
        IReadOnlyDictionary<int, CityState> cities, IReadOnlyDictionary<int, GateState> gates,
        IReadOnlyDictionary<int, UnitState> units, IReadOnlyDictionary<int, TerritoryState> territory,
        IReadOnlyDictionary<int, StructureState> structures, PlayableLandRoutes routes,
        Func<int, int?, bool>? isHostile, bool validate)
    {
        int id = action.Target.Id;
        switch (action.ActionKey)
        {
            case "move": case "stay": case "build": return id;
            case "enter": return FriendlyBase(id, forceId, cities, gates);
            case "attack" when action.Target.Kind == DeploymentTargetKind.Unit:
                return units.TryGetValue(id, out UnitState? enemy) && Hostile(forceId, enemy.ForceId, isHostile)
                    ? enemy.HexId : null;
            case "attack":
                if (cities.TryGetValue(id, out CityState? city))
                    return Hostile(forceId, city.ForceId, isHostile) ? city.HexId : null;
                if (gates.TryGetValue(id, out GateState? gate))
                    return Hostile(forceId, gate.ForceId, isHostile) ? gate.HexId : null;
                return null;
            case "occupy":
                if (territory.TryGetValue(startHex, out TerritoryState? land) && land.AreaId == id && Hostile(forceId, land.ForceId, isHostile))
                    return startHex;
                int[] route = routes.RouteToNearest(startHex, hex => territory.TryGetValue(hex, out TerritoryState? next) &&
                    next.AreaId == id && Hostile(forceId, next.ForceId, isHostile));
                return route.Length > 0 ? route[^1] : null;
            case "repair": case "demolish":
                if (action.ActionKey == "repair" && validate && structures.TryGetValue(id, out var repairTarget) &&
                    repairTarget.ForceId == forceId && PlayableConstructionRules.IsSupportedKind(repairTarget.KindId) &&
                    repairTarget.Completed && repairTarget.MaxDurability is null)
                    throw new InvalidDataException("Structure repair destination requires its current durability capacity.");
                return structures.TryGetValue(id, out StructureState? structure) && structure.ForceId == forceId &&
                    PlayableConstructionRules.IsSupportedKind(structure.KindId) &&
                    (action.ActionKey != "repair" || !validate || structure.Completed &&
                        structure.MaxDurability > structure.Durability) ? structure.HexId : null;
            default: return null;
        }
    }

    private static int? FriendlyBase(int id, int forceId,
        IReadOnlyDictionary<int, CityState> cities, IReadOnlyDictionary<int, GateState> gates) =>
        cities.TryGetValue(id, out CityState? city) ? city.ForceId == forceId ? city.HexId : null :
        gates.TryGetValue(id, out GateState? gate) && gate.ForceId == forceId ? gate.HexId : null;

    // This fallback is confined to the provisional adapter. Central integration
    // passes the diplomacy ruleset's hostility function once that state is known.
    private static bool Hostile(int forceId, int? targetForce, Func<int, int?, bool>? isHostile) =>
        isHostile?.Invoke(forceId, targetForce) ?? targetForce != forceId;
}
