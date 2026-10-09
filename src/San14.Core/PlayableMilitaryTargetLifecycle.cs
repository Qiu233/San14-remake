namespace San14.Core;

/// <summary>Ends stale playable military references before new logical Army
/// IDs can be allocated. This is not an original native target-pointer writer.</summary>
public static class PlayableMilitaryTargetLifecycle
{
    public const string PreparedTargetLostRuleId = "san14:provisional-prepared-unit-target-lost-v1";

    /// <summary>Strategy transports allocate before the period boundary can
    /// terminate lost references. Do not let their ID revive such a reference.</summary>
    public static int NextImmediateAllocationId(GameState state)
    {
        var referenced = state.PreparedDeployments.Values
            .SelectMany(plan => plan.Actions)
            .Where(action => action.Target.Kind == DeploymentTargetKind.Unit)
            .Select(action => action.Target.Id).ToHashSet();
        foreach (UnitState unit in state.Units.Values)
        {
            if (unit.TacticRuntime?.Pending is { } tactic) referenced.Add(tactic.TargetUnitId);
            //Native natural expiry changes25 without clearing3C/3E. Preserve
            //the stored Unit identity independently of its counter or goal.
            if (unit.NativeTacticRuntime?.ProvocationOwner is { Kind: DeploymentTargetKind.Unit } owner)
                referenced.Add(owner.Id);
            if (unit.MilitaryPlan is not { } plan) continue;
            if (plan.Action.Target.Kind == DeploymentTargetKind.Unit) referenced.Add(plan.Action.Target.Id);
            if (plan.IncidentalEnemyId is int incidental) referenced.Add(incidental);
        }
        foreach (NativeArmyLifecycleSlot slot in state.NativeArmyLifecycle?.Slots ?? [])
            if (slot.PendingImage is { } pending) referenced.Add(pending.Id);
        foreach (int unitId in state.PendingCombatSettlements?.ReferencedUnitIds() ?? []) referenced.Add(unitId);
        foreach (int unitId in state.NativeArmyLifecycle?.Indexes.ReferencedUnitIds() ?? []) referenced.Add(unitId);
        foreach (int unitId in state.NativeArmyLifecycle?.ArmyCautions?.ReferencedUnitIds() ?? []) referenced.Add(unitId);
        return NextFreeId(state.Units, referenced,
            state.Units.Count == 0 ? 1 : checked(state.Units.Keys.Max() + 1));
    }

    /// <summary>Military goals have been refreshed before period allocation,
    /// but queued tactics settle afterwards. Keep their logical targets vacant
    /// until they can report target_missing rather than selecting a new Army.
    /// Stored native provocation references survive natural expiry and cannot
    /// rebind until their source writer clears/replaces them. Historical effects
    /// and reports are not active target reservations.</summary>
    public static int NextPeriodAllocationId(IReadOnlyDictionary<int, UnitState> units, int firstCandidate,
        NativeArmyLifecycleState? nativeLifecycle = null, CombatSettlementQueue? settlements = null)
    {
        var referenced = units.Values.Where(unit => unit.TacticRuntime?.Pending is not null)
            .Select(unit => unit.TacticRuntime!.Pending!.TargetUnitId).ToHashSet();
        foreach (UnitState unit in units.Values)
            if (unit.NativeTacticRuntime?.ProvocationOwner is { Kind: DeploymentTargetKind.Unit } owner)
                referenced.Add(owner.Id);
        foreach (NativeArmyLifecycleSlot slot in nativeLifecycle?.Slots ?? [])
            if (slot.PendingImage is { } pending) referenced.Add(pending.Id);
        foreach (int unitId in settlements?.ReferencedUnitIds() ?? []) referenced.Add(unitId);
        foreach (int unitId in nativeLifecycle?.Indexes.ReferencedUnitIds() ?? []) referenced.Add(unitId);
        foreach (int unitId in nativeLifecycle?.ArmyCautions?.ReferencedUnitIds() ?? []) referenced.Add(unitId);
        return NextFreeId(units, referenced, firstCandidate);
    }

    private static int NextFreeId(IReadOnlyDictionary<int, UnitState> units,
        IReadOnlySet<int> referenced, int firstCandidate)
    {
        if (firstCandidate < 0) throw new ArgumentOutOfRangeException(nameof(firstCandidate));
        int next = firstCandidate;
        while (units.ContainsKey(next) || referenced.Contains(next)) next = checked(next + 1);
        return next;
    }

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
