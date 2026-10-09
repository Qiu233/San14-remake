namespace San14.Core;

/// <summary>Ends unavailable playable military goals at their scheduled settlement stage.</summary>
public static class PlayableMilitaryTargetLifecycle
{
    public const string PreparedTargetLostRuleId = "san14:provisional-prepared-unit-target-lost-v1";

    public static bool PreparedUnitTargetIsUnavailable(PreparedDeployment plan,
        IReadOnlyDictionary<int, UnitState> units, Func<int, int?, bool> isHostile) =>
        plan.Actions.Last() is { ActionKey: "attack", Target.Kind: DeploymentTargetKind.Unit } action &&
        (!units.TryGetValue(action.Target.Id, out UnitState? target) || !isHostile(plan.ForceId, target.ForceId));

    public static void EndUnavailableTargets(Dictionary<int, UnitState> units,
        IReadOnlyDictionary<int, CityState> cities, IReadOnlyDictionary<int, GateState> gates,
        IReadOnlyDictionary<int, OfficerState> officers,
        IReadOnlyDictionary<int, TerritoryState> territory, PlayableLandRoutes routes,
        int mapWidth, List<PeriodReportEntry> reports, Func<int, int?, bool> isHostile,
        IReadOnlyDictionary<int, StructureState> structures)
    {
        foreach (UnitState unit in units.Values.OrderBy(unit => unit.Id).ToArray())
        {
            if (unit.MilitaryPlan is not { } plan) continue;
            if (!plan.Completed && plan.Action is { ActionKey: "attack", Target.Kind: DeploymentTargetKind.Unit } action &&
                (!units.TryGetValue(action.Target.Id, out UnitState? target) || !isHostile(unit.ForceId, target.ForceId)))
            {
                units[unit.Id] = PlayableWaypointMarch.CompleteAction(unit, cities, gates,
                    officers, routes, mapWidth, reports, interrupted: true);
                continue;
            }
            if (plan.IncidentalEnemyId is not int incidental || units.TryGetValue(incidental, out UnitState? enemy) &&
                isHostile(unit.ForceId, enemy.ForceId)) continue;
            plan = plan with { IncidentalEnemyId = null };
            int destination = plan.Completed ? plan.RestingHexId!.Value :
                PlayableWaypointMarch.ResolveDestination(plan.Action, unit.ForceId, unit.HexId,
                    cities, gates, units, territory, structures, routes, isHostile, validate: false) ?? unit.HexId;
            units[unit.Id] = unit with { MilitaryPlan = plan, DestinationHexId = destination };
        }
    }

    /// <summary>The Army still leaves under the existing execution accounting,
    /// but its lost unit objective is cancelled into a safe completed rest plan.
    /// The old ID must not bind to this or another newly allocated Army.</summary>
    public static UnitState CancelPreparedUnitTarget(UnitState army,
        IReadOnlyDictionary<int, CityState> cities, IReadOnlyDictionary<int, GateState> gates,
        IReadOnlyDictionary<int, OfficerState> officers, PlayableLandRoutes routes,
        int mapWidth, List<PeriodReportEntry> reports)
    {
        UnitState resting = PlayableWaypointMarch.AttachAction(army with
        {
            DestinationHexId = army.HexId, Mission = new(UnitMissionKind.Combat)
        }, new("stay", new(DeploymentTargetKind.Hex, army.HexId)));
        int before = reports.Count;
        UnitState result = PlayableWaypointMarch.CompleteAction(resting, cities, gates,
            officers, routes, mapWidth, reports, interrupted: true);
        reports[before] = reports[before] with { RuleId = PreparedTargetLostRuleId };
        return result;
    }
}
