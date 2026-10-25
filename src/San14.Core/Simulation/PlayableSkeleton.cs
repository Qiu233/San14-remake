namespace San14.Core;

/// <summary>
/// Explicitly provisional first-playable rules. This adapter never claims to reproduce
/// original initialization, eligibility, recovery, development or movement timing.
/// </summary>
public static class PlayableSkeleton
{
    public const string StandingBaseContactRuleId = "san14:provisional-standing-base-contact-v1";
    public const string OccupationContactRuleId = "san14:provisional-occupation-contact-before-claim-v1";
    public const string RulesetId = "san14:playable-skeleton-v2";
    public const string LegacyRulesetId = "san14:playable-skeleton-v1";
    public static bool IsPlayableRuleset(string id) => id is RulesetId or LegacyRulesetId;

    /// <summary>Upgrade earlier playable sessions without changing original pending snapshots.</summary>
    public static GameState UpgradeSession(GameState saved)
    {
        StateValidator.Validate(saved);
        if (!IsPlayableRuleset(saved.RulesetId))
            throw new InvalidOperationException("Only a playable session can use this migration.");
        // All playable sessions before city duties existed had no such appointments.
        // This interpretation is confined to the provisional ruleset; the original
        // initializer and generic codec continue to preserve null as unknown.
        var cities = saved.Cities.ToDictionary(pair => pair.Key, pair =>
            pair.Value.Duties is null ? pair.Value with { Duties = new CityDutyState(null, null, 0) } : pair.Value);
        var units = saved.Units.ToDictionary(pair => pair.Key, pair =>
            pair.Value.Mission is null ? pair.Value with { Mission = new UnitMission(UnitMissionKind.Combat) } : pair.Value);
        var forces = saved.Forces.ToDictionary(pair => pair.Key, pair =>
            pair.Value.IsActive && pair.Value.Governance is null
                ? pair.Value with { Governance = EmptyPlayableGovernance() } : pair.Value);
        // Schema26 had no completion flag. A full-durability facility built by that
        // playable ruleset had necessarily completed; preserve that fact on upgrade.
        var structures = saved.Structures?.ToDictionary(pair => pair.Key, pair =>
            // Unknown native caps do not establish historical completion.
            !pair.Value.Completed && pair.Value.MaxDurability is > 0 and int maximum &&
            pair.Value.Durability == maximum
                ? pair.Value with { Completed = true } : pair.Value);
        GameState upgraded = saved with
        {
            RulesetId = RulesetId, Cities = cities, Units = units, Forces = forces,
            PreparedOfficerMoves = saved.PreparedOfficerMoves ?? [],
            PreparedTalentOrders = saved.PreparedTalentOrders ?? [],
            Structures = structures ?? [],
            Diplomacy = saved.Diplomacy ?? PlayableDiplomacy.CreateProvisional(saved),
            PersonnelReports = saved.PersonnelReports ?? [],
            MonthlyIncomeReports = saved.MonthlyIncomeReports ?? []
        };
        upgraded = PlayableTacticRuntime.InitializePlayableState(upgraded);
        upgraded = PlayableEventRuntime.InitializePlayableState(upgraded);
        StateValidator.Validate(upgraded);
        return upgraded;
    }
    private static GovernanceState EmptyPlayableGovernance() => new(
        [new GovernanceSlot(0, GovernanceSlotKind.Advisor, null),
         new GovernanceSlot(1, GovernanceSlotKind.Department, "group-0"),
         new GovernanceSlot(2, GovernanceSlotKind.Department, "group-1"),
         new GovernanceSlot(3, GovernanceSlotKind.Department, "group-2"),
         new GovernanceSlot(4, GovernanceSlotKind.Department, "group-3"),
         new GovernanceSlot(5, GovernanceSlotKind.Department, "group-4")],
        30, new GovernanceConfiguration([]), new GovernanceConfiguration([]), null);

    public static PlayableRuleSet Bind(IHexTravelMap map,
        IPlayableMonthlyIncomeRules? monthlyIncome = null,
        IPlayableTalentResolution? talent = null,
        IDiplomacyPolicy? diplomacy = null,
        IPlayableTacticRules? tacticRules = null,
        IPlayableEventRules? eventRules = null,
        IPersonnelCommandRules? personnelRules = null,
        IPlayableBattleRules? battleRules = null,
        IPlayablePeriodEconomyRules? periodEconomyRules = null,
        IPlayableNativeSubdayRules? nativeSubdayRules = null,
        IPlayableArmyCreationRules? armyCreationRules = null,
        IPlayableNativeArmyPreparationRules? nativeArmyPreparationRules = null,
        IPlayableBaseCaptureAftermathRules? baseCaptureAftermathRules = null,
        IPlayableCityEntryResourceRules? cityEntryResources = null,
        IPlayableCityEntryStateRules? cityEntryState = null,
        IPlayableSiegeSettlementRules? siegeSettlementRules = null,
        IPlayableFieldSettlementRules? fieldSettlementRules = null,
        IPlayableNativePeriodContinuationRules? nativePeriodContinuationRules = null,
        IPlayableCaptureReturningPersonRules? captureReturningPersonRules = null,
        IPlayableCaptureArmyHomeRules? captureArmyHomeRules = null,
        IPlayableLastCityPersonnelRules? lastCityPersonnelRules = null,
        IPlayableGateEntryStateRules? gateEntryState = null,
        IPlayableBaseContactPriorityRules? baseContactPriorityRules = null,
        IPlayableArmyContactRules? armyContactRules = null,
        IPlayableTransportCreationRules? transportCreationRules = null,
        IPlayableDistrictCommandBudgetRules? districtCommandBudgetRules = null,
        IPlayableArmyFalseReportCommandRules? falseReportCommandRules = null,
        IPlayableOrdinaryDeploymentCommitRules? ordinaryDeploymentCommitRules = null,
        IAreaAdministrationExecution? areaAdministrationRules = null,
        IPlayableArmyConstructionCommandRules? armyConstructionRules = null,
        ICityDomesticExecution? cityDomesticRules = null,
        IPlayableUnitBehaviorRules? unitBehaviorRules = null,
        IGovernanceRosterRules? governanceRosterRules = null,
        IPlayableArmyWithdrawalCommandRules? armyWithdrawalRules = null,
        IPlayableArmyMovementCommandRules? armyMovementCommands = null,
        ICityMerchantExecution? cityMerchantRules = null,
        ICityOfficerMovementExecution? cityOfficerMovementRules = null)
    {
        var routes = new PlayableLandRoutes(map);
        IDiplomacyPolicy diplomacyPolicy = diplomacy ?? new ProvisionalDiplomacyPolicy();
        IPlayableTacticRules tactics = tacticRules ?? UnresolvedTacticRules.Instance;
        IPlayableEventRules events = eventRules ?? UnresolvedEventRules.Instance;
        IPersonnelCommandRules personnel = personnelRules ?? UnresolvedPersonnelRules.Instance;
        IOrderRules commands = new OrderRuleDispatcher(
            new AreaAdministrationRules(areaAdministrationRules),
            new DeploymentCommandRules(new DeploymentEligibilityRules(routes), new DeploymentResourceRules(), districtCommandBudgetRules, ordinaryDeploymentCommitRules),
            new GovernanceCommandRules(new PlayableGovernanceCommandRules()), governanceRosterRules);
        commands = new PlayableUnitOrders(commands, routes, cityEntryResources, cityEntryState, transportCreationRules, armyConstructionRules, armyWithdrawalRules);
        commands = new PlayableCityDutyOrders(commands, cityDomesticRules);
        commands = new CityMerchantOrders(commands, cityMerchantRules);
        commands = new PlayableOfficerMoveOrders(commands, cityOfficerMovementRules);
        commands = new PlayableTalentOrders(commands, talent as IPlayableTalentCommandRules);
        commands = new CampaignExpeditionOrders(commands, armyCreationRules as ICampaignExpeditionExecution);
        commands = new PlayableFalseReportOrders(commands, falseReportCommandRules);
        commands = new PlayableWaypointOrders(commands, routes, PlayableDiplomacy.IsHostile, cityEntryResources, cityEntryState,
            armyConstructionRules, unitBehaviorRules, armyMovementCommands);
        commands = new PlayableDiplomacyOrders(commands, diplomacyPolicy);
        commands = new PlayableTacticOrders(commands, routes, tactics);
        commands = new PlayablePersonnelOrders(commands, personnel);
        commands = new DistrictCommandOrders(commands, armyCreationRules as IDistrictCommandRules);
        commands = new DoctrineResearchOrders(commands, armyCreationRules as IDoctrineResearchCommandRules);
        commands = new StrategicCommandOrders(commands, armyCreationRules as IStrategicCommandRules);
        commands = new PlayableDiplomaticOfficerGuard(commands);
        if (armyCreationRules is IPlayableArmyCreationPreparationRules preparationRules)
            commands = new PlayableArmyCreationPreparationGuard(commands, preparationRules);
        return new PlayableRuleSet(commands,
            new Advancement(routes, monthlyIncome ?? new ProvisionalAreaMonthlyIncomeRules(),
                talent ?? new ProvisionalTalentResolution(), diplomacyPolicy, tactics, events,
                battleRules ?? ProvisionalPlayableBattleRules.Instance,
                periodEconomyRules ?? ProvisionalPlayablePeriodEconomyRules.Instance,
                nativeSubdayRules ?? tactics as IPlayableNativeSubdayRules, armyCreationRules, nativeArmyPreparationRules,
                baseCaptureAftermathRules, cityEntryResources, cityEntryState, siegeSettlementRules, fieldSettlementRules,
                nativePeriodContinuationRules, captureReturningPersonRules, captureArmyHomeRules, lastCityPersonnelRules, gateEntryState,
                baseContactPriorityRules, armyContactRules, districtCommandBudgetRules, armyWithdrawalRules),
            new Reporting(districtCommandBudgetRules), routes)
        { TacticRules = tactics, OrdinaryDeploymentRules = ordinaryDeploymentCommitRules, PersonnelRules = personnel,
            GovernanceRosterRules = governanceRosterRules };
    }

    public static GameState Start(GameState initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        StateValidator.Validate(initial);
        if (initial.Phase != TurnPhase.Initializing || initial.RulesetId != "san14:original-pending")
            throw new InvalidOperationException("The playable skeleton requires an original pending scenario snapshot.");
        // A known empty appointment is needed to use the existing typed command path.
        // Area ownership comes exclusively from the source and is not inferred from its city.
        var areas = initial.Areas.ToDictionary(pair => pair.Key, pair =>
            pair.Value.Administration is null
                ? pair.Value with { Administration = new AreaAdministrationState(null, AreaDevelopmentFocus.None) }
                : pair.Value);
        var cities = initial.Cities.ToDictionary(pair => pair.Key, pair =>
            pair.Value.Duties is null ? pair.Value with { Duties = new CityDutyState(null, null, 0) } : pair.Value);
        var forces = initial.Forces.ToDictionary(pair => pair.Key, pair =>
            pair.Value.IsActive && pair.Value.Governance is null
                ? pair.Value with { Governance = EmptyPlayableGovernance() } : pair.Value);
        GameState next = initial with
        {
            RulesetId = RulesetId, Phase = TurnPhase.Strategy, Forces = forces,
            Cities = cities, Areas = areas, PeriodReports = [], PendingCommandReports = [], PreparedOfficerMoves = [],
            PreparedTalentOrders = [], Structures = initial.Structures ?? [],
            Diplomacy = initial.Diplomacy ?? PlayableDiplomacy.CreateProvisional(initial),
            PersonnelReports = initial.PersonnelReports ?? [],
            MonthlyIncomeReports = initial.MonthlyIncomeReports ?? []
        };
        next = PlayableTacticRuntime.InitializePlayableState(next);
        next = PlayableEventRuntime.InitializePlayableState(next);
        StateValidator.Validate(next);
        return next;
    }

    private sealed class DeploymentEligibilityRules(PlayableLandRoutes routes) : IDeploymentEligibility
    {
        private static bool TacticsAllowed(GameState state, OfficerState officer,
            DeploymentUnitRequest unit)
        {
            if (unit.TacticIds.Count == 0) return true;
            if (unit.TacticIds.Count > 3 ||
                unit.TacticIds.Distinct().Count() != unit.TacticIds.Count ||
                officer.BaseTacticIds is not { } known) return false;
            return unit.TacticIds.All(id => id > 0 && known.Contains(id) &&
                state.Tactics.TryGetValue(id, out TacticDefinition? tactic) &&
                tactic.FormationIds?.Contains(unit.FormationId) == true);
        }

        public DeploymentEligibility Evaluate(GameState state, DeploymentSource source,
            IReadOnlyList<DeploymentUnitRequest> units, CommandContext context)
        {
            routes.ValidateWorld(state);
            foreach (DeploymentUnitRequest unit in units)
            {
                OfficerState officer = state.Officers[unit.OfficerId];
                //Catalogue identity is required even when legacy officer
                //capabilities are unknown/empty. Preserve those separate
                //capability semantics and the old pending cancellation path.
                if (!state.Formations.ContainsKey(unit.FormationId) || PlayableOfficerAvailability.IsSourceBusy(state, officer) ||
                    officer.Presence != OfficerPresence.Present ||
                    state.PreparedOfficerMoves?.ContainsKey(officer.Id) == true ||
                    state.PreparedTalentOrders?.ContainsKey(officer.Id) == true ||
                    (officer.BaseFormationIds is { Length: > 0 } formations && !formations.Contains(unit.FormationId)) ||
                    PlayableDiplomacy.IsOfficerOnMission(state, officer.Id) ||
                    !TacticsAllowed(state, officer, unit) ||
                    PlayableWaypointMarch.ValidateDeploymentPath(state, source, unit, routes,
                        (actor, target) => PlayableDiplomacy.IsHostile(state, actor, target)).Status !=
                        DeploymentDecisionStatus.Allowed)
                    return new(DeploymentDecisionStatus.Denied, "Officer, formation or destination is unavailable.");
            }
            return new(DeploymentDecisionStatus.Allowed);
        }

        public DeploymentEligibility EvaluateCancellation(GameState state, PreparedDeployment deployment,
            CommandContext context) => new(DeploymentDecisionStatus.Allowed);
    }

    private sealed class DeploymentResourceRules : IDeploymentResourceRules
    {
        public DeploymentPreparationResources Prepare(GameState state, DeploymentSource source,
            IReadOnlyList<DeploymentUnitRequest> units, CommandContext context) =>
            new(DeploymentDebitTiming.Execution, DeploymentDebitTiming.Execution);

        public DeploymentCancellationResources Cancel(GameState state, PreparedDeployment deployment,
            CommandContext context) => new(deployment.TroopsDebited ? deployment.Troops : 0,
                deployment.CommandBookDebited ? 1 : 0);
    }

    private sealed class Advancement(PlayableLandRoutes routes,
        IPlayableMonthlyIncomeRules monthlyIncome,
        IPlayableTalentResolution talent, IDiplomacyPolicy diplomacy,
        IPlayableTacticRules tactics, IPlayableEventRules events,
        IPlayableBattleRules battle, IPlayablePeriodEconomyRules periodEconomy,
        IPlayableNativeSubdayRules? nativeSubdayRules, IPlayableArmyCreationRules? armyCreationRules,
        IPlayableNativeArmyPreparationRules? nativeArmyPreparationRules,
        IPlayableBaseCaptureAftermathRules? baseCaptureAftermathRules,
        IPlayableCityEntryResourceRules? cityEntryResources,
        IPlayableCityEntryStateRules? cityEntryState,
        IPlayableSiegeSettlementRules? siegeSettlementRules,
        IPlayableFieldSettlementRules? fieldSettlementRules,
        IPlayableNativePeriodContinuationRules? nativePeriodContinuationRules,
        IPlayableCaptureReturningPersonRules? captureReturningPersonRules,
        IPlayableCaptureArmyHomeRules? captureArmyHomeRules,
        IPlayableLastCityPersonnelRules? lastCityPersonnelRules,
        IPlayableGateEntryStateRules? gateEntryState,
        IPlayableBaseContactPriorityRules? baseContactPriorityRules,
        IPlayableArmyContactRules? armyContactRules,
        IPlayableDistrictCommandBudgetRules? districtCommandBudgetRules,
        IPlayableArmyWithdrawalCommandRules? armyWithdrawalRules) : IAdvancementRules
    {
        public GameState AdvanceTenDays(GameState state)
        {
            RequireSkeleton(state);
            if (state.NativePlayableTerminal is not null || state.CampaignTurnStop is not null)
                throw new InvalidOperationException("The campaign has ended.");
            var resumedPeriod = state.NativePeriodContinuation;
            if (resumedPeriod is null) routes.ValidateWorld(state);
            // A report belongs to this period only. Diplomacy can retain missions
            // across periods, while its completed reports are appended once.
            PersonnelReportEntry[] personnelReports = resumedPeriod?.Preparation.PersonnelReports ?? (state.PersonnelReports ?? [])
                .Where(report => report.ReportedInPeriod is null)
                .Select(report => report with { ReportedInPeriod = checked(state.PeriodNumber + 1) }).ToArray();
            PeriodReportEntry[] personnelSummaries = personnelReports.Select(report =>
                new PeriodReportEntry(report.Kind switch
                {
                    PersonnelActionKind.GrantItem => "item_granted",
                    PersonnelActionKind.RevokeItem => "item_revoked",
                    _ => "office_changed"
                }, report.ForceId, OfficerId: report.OfficerId,
                    Amount: report.ItemId ?? report.NextOfficeId!.Value)).ToArray();
            if (resumedPeriod is null) state = PlayableDiplomacy.Advance(state with
            {
                PeriodReports = state.PendingCommandReports.Concat(personnelSummaries).ToArray(),
                PendingCommandReports = []
            },
                checked(state.PeriodNumber + 1), diplomacy);
            var cities = new Dictionary<int, CityState>(state.Cities);
            var gates = new Dictionary<int, GateState>(state.Gates);
            var districts = new Dictionary<int, DistrictState>(state.Districts);
            var officers = new Dictionary<int, OfficerState>(state.Officers);
            var units = new Dictionary<int, UnitState>(state.Units);
            var structures = new Dictionary<int, StructureState>(state.Structures ?? []);
            var areas = new Dictionary<int, AreaState>(state.Areas);
            Dictionary<int, TerritoryState>? territory = null;
            var reports = (resumedPeriod?.Preparation.Reports ?? state.PeriodReports).ToList();
            //Only actual field/pending commanders and completed officer moves
            //can arrive through this transaction. Do not normalize unrelated
            //initial source roles whose appointment projection may be unknown.
            int[] possibleArrivalOfficers = resumedPeriod?.Preparation.PossibleArrivalOfficers ?? state.Units.Values.Select(unit => unit.OfficerId)
                .Concat(state.PreparedDeployments.Values.Select(plan => plan.OfficerId))
                .Concat(state.PreparedOfficerMoves?.Keys ?? Enumerable.Empty<int>())
                .Distinct().ToArray();
            Func<int, PlayableBaseContactPriority?>? currentBasePriority = null;
            Func<int, PlayableArmyContact?>? currentArmyContact = null;
            bool HasHostileArmyContactAt(int forceId, int hexId)
            {
                if (currentArmyContact is null)
                    return units.Values.Any(other => IsHostile(forceId, other.ForceId) && other.HexId == hexId);
                var selected = currentArmyContact(hexId);
                if (selected is null) return true;
                return selected.Value.ArmyId is int id &&
                    (!units.TryGetValue(id, out var target) || IsHostile(forceId, target.ForceId));
            }
            bool HasHostileContactAt(int forceId, int hexId)
            {
                if (currentBasePriority is not null)
                {
                    var selected = currentBasePriority(hexId);
                    if (selected is null) return true; //Reach and report the missing selector before this cell.
                    if (selected.Value.LocationId is int location)
                    {
                        if (cities.TryGetValue(location, out var currentCity)) return IsHostile(forceId, currentCity.ForceId);
                        if (gates.TryGetValue(location, out var currentGate)) return IsHostile(forceId, currentGate.ForceId);
                        return true; //A reached source Base needs its Core projection.
                    }
                    return HasHostileArmyContactAt(forceId, hexId);
                }
                return HasHostileArmyContactAt(forceId, hexId) ||
                    cities.Values.Any(city => city.HexId == hexId && IsHostile(forceId, city.ForceId)) ||
                    gates.Values.Any(gate => gate.HexId == hexId && IsHostile(forceId, gate.ForceId));
            }
            UnitState FinishMilitaryAction(UnitState unit, bool interrupted = false) =>
                PlayableWaypointMarch.CompleteAction(unit with
                {
                    Mission = new UnitMission(UnitMissionKind.Combat)
                }, cities, gates, officers, routes, state.MapWidth, reports, interrupted);

            ulong randomState = state.RandomState;
            // Source preparation and waiting can publish independent native
            // owners. Later operations and the final snapshot must continue
            // from that current basis, rather than the period-entry owners.
            GameState periodBasis = state;
            //Last-city withdrawal can end treaties during this same movement
            //pass. Later contacts and land permissions use those current rights.
            bool IsHostile(int actor, int? target) => PlayableDiplomacy.IsHostile(periodBasis, actor, target);
            GameState CurrentWorld() => periodBasis with
            {
                Cities = cities, Gates = gates, Districts = districts, Officers = officers,
                Units = units, Structures = structures, Areas = areas,
                Territory = territory ?? state.Territory, RandomState = randomState
            };
            UnitState MoveAlong(UnitState actor, ReadOnlySpan<int> traversed)
            {
                foreach (int hex in traversed)
                {
                    byte? direction = (armyCreationRules as IPlayableArmyPositionRules)?.ReadDirection36(
                        CurrentWorld(), actor, actor.HexId, hex);
                    actor = PlayableUnitPosition.AtHex(actor with
                        { NativeDirection36 = direction ?? actor.NativeDirection36 }, hex);
                }
                return actor;
            }
            bool TryStageAlong(UnitState actor, ReadOnlySpan<int> traversed,
                out UnitState positioned, out GameState positionedWorld)
            {
                positionedWorld = CurrentWorld(); positioned = actor;
                if (!traversed.IsEmpty && actor.NativeArmyOrigin is not null &&
                    armyCreationRules is IPlayableArmyHexOccupancyRules occupancy)
                {
                    var written = occupancy.TryApplyTraversal(positionedWorld, actor, traversed);
                    if (written.State is null)
                    {
                        reports.Add(new("movement_unresolved", actor.ForceId, OfficerId: actor.OfficerId,
                            FromHexId: actor.HexId, ToHexId: traversed[0])
                            { MissingRuleKeys = written.UnresolvedOwner is null ? null : [written.UnresolvedOwner] });
                        return false;
                    }
                    positionedWorld = written.State;
                }
                positioned = MoveAlong(actor, traversed);
                positionedWorld = positionedWorld with { Units = new(positionedWorld.Units) { [actor.Id] = positioned } };
                return true;
            }
            bool TryCommitAlong(UnitState actor, ReadOnlySpan<int> traversed, out UnitState positioned)
            {
                if (!TryStageAlong(actor, traversed, out positioned, out var positionedWorld)) return false;
                periodBasis = positionedWorld;
                units[actor.Id] = positioned;
                return true;
            }
            PlayableCityEntryResourceQuote? EntryResources(UnitState actor, int locationId) =>
                cities.TryGetValue(locationId, out CityState? city)
                    ? PlayableCityEntryResources.Quote(CurrentWorld(), actor, city, cityEntryResources) : null;
            string? EntryState(UnitState actor, int locationId)
            {
                GameState entryWorld = CurrentWorld() with
                { PreparedDeployments = [], PreparedOfficerMoves = [], PreparedTalentOrders = [] };
                PlayableCityEntryStateResolution? returned = cities.TryGetValue(locationId, out CityState? city)
                    ? PlayableCityEntryStates.Return(entryWorld, actor, city, cityEntryState)
                    : gates.TryGetValue(locationId, out GateState? gate)
                        ? PlayableGateEntryStates.Return(entryWorld, actor, gate, gateEntryState) : null;
                if (returned is null) return null;
                CopyInto(cities, returned.State.Cities); CopyInto(gates, returned.State.Gates);
                CopyInto(districts, returned.State.Districts); CopyInto(officers, returned.State.Officers);
                CopyInto(units, returned.State.Units);
                periodBasis = returned.State;
                return returned.RuleId;
            }
            bool RequiresEntryState(UnitState actor, int locationId) =>
                cities.TryGetValue(locationId, out CityState? city)
                    ? PlayableCityEntryStates.RequiresCurrentState(CurrentWorld(), actor, city, cityEntryState)
                    : gates.TryGetValue(locationId, out GateState? gate) &&
                        PlayableGateEntryStates.RequiresCurrentState(CurrentWorld(), actor, gate, gateEntryState);
            static void CopyInto<T>(Dictionary<int, T> destination, Dictionary<int, T> source)
            {
                if (ReferenceEquals(destination, source)) return;
                destination.Clear();
                foreach (var pair in source) destination.Add(pair.Key, pair.Value);
            }
            //The provisional return/destruction may retire a projection before
            //the source manager cleanup is available. Keep that actual current
            //receiver independently, without freeing or resetting native owners.
            Action<UnitState>? onArmyRetired = state.ArmyLifecycle is null ? null :
                image => periodBasis = ArmyRetirement.PreservePendingImage(CurrentWorld(), image);
            void CompleteRetreat(UnitState actor, int locationId)
            {
                if (EntryState(actor, locationId) is string ruleId)
                    reports.Add(new("retreat_arrived", actor.ForceId, cities.ContainsKey(locationId) ? locationId : null, actor.OfficerId,
                        actor.Troops, actor.HexId, actor.HexId) { LocationId = locationId, RuleId = ruleId });
                else if (RequiresEntryState(actor, locationId))
                    reports.Add(new("unit_enter_unresolved", actor.ForceId, cities.ContainsKey(locationId) ? locationId : null, actor.OfficerId,
                        FromHexId: actor.HexId, ToHexId: actor.HexId)
                    { LocationId = locationId, RuleId = PlayableCityEntryStates.UnresolvedRuleId });
                else PlayableUnitRetreat.Complete(actor, locationId, cities, gates, officers, units,
                    reports, onArmyRetired, EntryResources(actor, locationId));
            }
            (int LocationId, int HexId)? RetreatDestination(UnitState actor)
            {
                if (actor.NativeArmyOrigin is not null && armyWithdrawalRules is not null &&
                    armyWithdrawalRules.ReadWithdrawalDestination(CurrentWorld(), actor) is { UsesCurrentCommand: true } selected)
                    return selected.LocationId is int location && selected.HexId is int hex ? (location, hex) : null;
                return PlayableUnitRetreat.Destination(actor, cities, gates, officers, routes, state.MapWidth);
            }
            var deploymentCancellation = new DeploymentCommandRules(new DeploymentEligibilityRules(routes),
                new DeploymentResourceRules(), districtCommandBudgetRules);
            var sourceArmyPeriod = nativePeriodContinuationRules as IPlayableNativeArmyPeriodRules;
            GameState tacticState;
            TacticReportEntry[] tacticReports;
            if (resumedPeriod is null)
            {
            PreparedOrderStageResult preparedOrders = PeriodOrderPreparation.ExecuteOwned(state,
                CurrentWorld(), routes, talent, deploymentCancellation, armyCreationRules);
            periodBasis = preparedOrders.State;
            cities = periodBasis.Cities; gates = periodBasis.Gates; districts = periodBasis.Districts;
            officers = periodBasis.Officers; units = periodBasis.Units;
            randomState = periodBasis.RandomState;
            reports.AddRange(preparedOrders.Reports);

            // CTurn36/37 follow Progress. The compatibility provider keeps
            // its old pre-movement boundary; the source path reaches them once
            // after its complete calendar, before Turn income and expenses.
            GameState preparedArmyState = sourceArmyPeriod is null
                ? NativeArmyPreparation.PrepareAfterDeployment(CurrentWorld(), nativeArmyPreparationRules)
                : CurrentWorld();
            tacticState = PlayableTacticRuntime.SettleBeforeMovement(preparedArmyState,
                checked(state.PeriodNumber + 1), routes, tactics,
                out tacticReports, out PeriodReportEntry[] tacticSummaries);
            units = tacticState.Units;
            officers = tacticState.Officers;
            randomState = tacticState.RandomState;
            reports.AddRange(tacticSummaries);
            }
            else
            {
                //AI, diplomacy Turn, prepared commands and tactic settlement
                //already belong to this exact suspended transaction.
                tacticState = CurrentWorld();
                tacticReports = resumedPeriod.Preparation.TacticReports;
            }
            PlayableSupplyUpkeepSnapshot? frozenPeriodFood = null;
            GameState nativeState;
            if (sourceArmyPeriod is not null)
            {
                // The source Turn bills current rosters after Progress. A
                // provisional start quote must not decide later admission.
                tacticState = tacticState with { NativePeriodContinuation = resumedPeriod ?? new(
                    new(personnelReports, reports.ToArray(), tacticReports, possibleArrivalOfficers), null) };
                nativeState = sourceArmyPeriod.CompleteArmyPeriod(tacticState with
                {
                    PreparedDeployments = [], PreparedOfficerMoves = [], PreparedTalentOrders = [],
                    PeriodReports = resumedPeriod is null ? [] : tacticState.PeriodReports
                }, nativeSubdayRules ?? throw new InvalidDataException("Native Army waiting rules are unavailable."));
                if (nativeState.NativePeriodContinuation?.Cursor is not null) return nativeState;
                nativeState = nativeState with { NativePeriodContinuation = null };
                var normalTurn = nativePeriodContinuationRules as IPlayableNativeNormalTurnRules
                    ?? throw new InvalidDataException("Native normal Turn rules are unavailable after completed Army Progress.");
                nativeState = normalTurn.CompleteNormalTurn(nativeState);
                if (nativeState.CampaignTurnStop is not null || nativeState.NativePlayableTerminal is not null)
                {
                    reports.AddRange(nativeState.PeriodReports);
                    reports.AddRange(nativeState.PendingCommandReports);
                    return TerminalReport(nativeState);
                }
            }
            else nativeState = NativeSubdayRuntime.CompletePeriodOwned(tacticState, nativeSubdayRules);
            if (nativePeriodContinuationRules is not null)
            {
                //All prepared orders were consumed above. Calendar/Return
                //must run before any period-end movement/capture, including a
                //period with no source combat sink at all.
                if (sourceArmyPeriod is null)
                    nativeState = nativePeriodContinuationRules.CompleteWaiting(nativeState with
                    {
                        PreparedDeployments = [], PreparedOfficerMoves = [], PreparedTalentOrders = [],
                        PeriodReports = []
                    });
                reports.AddRange(nativeState.PeriodReports);
                if (sourceArmyPeriod is not null) reports.AddRange(nativeState.PendingCommandReports);
                cities = nativeState.Cities; gates = nativeState.Gates; districts = nativeState.Districts;
                areas = nativeState.Areas; structures = nativeState.Structures!;
                territory = new(nativeState.Territory);
            }
            periodBasis = nativeState;
            units = nativeState.Units;
            officers = nativeState.Officers;
            randomState = nativeState.RandomState;

            IReadOnlySet<int>? awaitingAppearance = sourceArmyPeriod is null
                ? nativePeriodContinuationRules?.CaptureArmiesAwaitingAppearance(nativeState) : null;
            if (sourceArmyPeriod is null && nativePeriodContinuationRules is not null &&
                nativeState.NativeArmySimulation is not null && awaitingAppearance is null)
                throw new InvalidDataException("Native Army appearance qualification is unavailable.");

            //Keep the compatibility path's old start quantity cut. The source
            //Turn bills only its actual current roster at the reached callback.
            if (sourceArmyPeriod is null && frozenPeriodFood is null)
            {
                var foodCut = FreezePeriodFood(CurrentWorld());
                frozenPeriodFood = foodCut.Food;
                periodBasis = foodCut.State;
            }
            PlayableSupplyUpkeepSnapshot? periodFood = frozenPeriodFood;
            (GameState State, PlayableSupplyUpkeepSnapshot Food) FreezePeriodFood(GameState basis)
            {
                var food = armyCreationRules is IPlayableFoodOwnerRules foodOwners
                    ? PlayableSupplyUpkeepSnapshot.Capture(basis, foodOwners)
                    : PlayableSupplyUpkeepSnapshot.Capture(basis.Cities, basis.Units.Values);
                if (armyCreationRules is IPlayableArmyFoodExpenseRules armyFood)
                {
                    var expenseBatch = armyFood.CaptureArmyFoodExpenses(basis);
                    basis = expenseBatch.State;
                    food.AttachArmyExpenses(basis, expenseBatch.Expenses, expenseBatch.OrderedExpenses);
                }
                return (basis, food);
            }
            ILookup<int, int> prefecturesByHex = areas.Values
                .Where(area => area.HexId is not null)
                .ToLookup(area => area.HexId!.Value, area => area.Id);
            foreach (UnitState startingUnit in units.Values.OrderBy(unit => unit.Id).ToArray())
            {
                // An earlier field battle may have damaged or destroyed this unit.
                if (!units.TryGetValue(startingUnit.Id, out UnitState? unit)) continue;
                //The real period already moved, fought and returned this
                //native receiver. Only legacy Armies enter the coarse pass.
                if (sourceArmyPeriod is not null && unit.NativeArmyOrigin is not null) continue;
                currentBasePriority = baseContactPriorityRules?.CaptureCurrentBasePriority(CurrentWorld());
                currentArmyContact = armyContactRules?.CaptureCurrentArmyContact(CurrentWorld());
                if (unit.Morale <= 0)
                {
                    PlayableUnitAftermath.Destroy(unit, cities, gates, officers,
                        units, reports, state.MapWidth, onArmyRetired);
                    continue;
                }
                if (awaitingAppearance?.Contains(unit.Id) == true)
                {
                    continue;
                }
                unit = PlayableWaypointMarch.PrepareForMovement(unit, cities, gates,
                    officers, units, territory ?? state.Territory, routes, state.MapWidth,
                    reports, IsHostile, structures);
                units[unit.Id] = unit;
                void CapturePrefectureAt(int hexId)
                {
                    foreach (int areaId in prefecturesByHex[hexId])
                    {
                        if (!IsHostile(unit.ForceId, areas[areaId].ForceId)) continue;
                        int? receivingCity = PlayablePrefectureControl.ReceivingCity(cities, officers,
                            unit, hexId, state.MapWidth);
                        if (receivingCity is not int cityId)
                        {
                            reports.Add(new PeriodReportEntry("area_capture_unassigned", unit.ForceId,
                                null, unit.OfficerId, areaId, hexId, hexId));
                            continue;
                        }
                        PlayablePrefectureControl.Transfer(cities, areas, areaId, unit.ForceId, cityId);
                        reports.Add(new PeriodReportEntry("area_capture", unit.ForceId,
                            cityId, unit.OfficerId, areaId, hexId, hexId));
                    }
                }
                if (unit.Mission is { Kind: UnitMissionKind.Transport } mission)
                {
                    //These targets belong to the native command and waypoint
                    //owners. The City-only coarse mover cannot consume them.
                    if (unit.NativeArmyOrigin is not null && (mission.DestinationCityId is null ||
                        unit.TransportRoute?.Waypoints.Any(point => point?.Kind == ArmyMarchDestinationKind.Hex) == true))
                    {
                        reports.Add(new("transport_unresolved", unit.ForceId, mission.DestinationCityId, unit.OfficerId,
                            FromHexId: unit.HexId, ToHexId: unit.DestinationHexId)
                        { RuleId = mission.DestinationCityId is null ? "rules.transport.native_non_city_progress" :
                            "rules.transport.native_waypoint_progress" });
                        continue;
                    }
                    UnitState transportUnit = unit;
                    CityState destination = cities[mission.DestinationCityId!.Value];
                    bool? CanEnterTransportCity(UnitState current) => PlayableCityEntryStates.IsEntryPositionAllowed(
                        CurrentWorld(), current, destination, cityEntryState);
                    if (destination.ForceId != unit.ForceId)
                    {
                        reports.Add(new PeriodReportEntry("transport_blocked", unit.ForceId,
                            destination.Id, unit.OfficerId, 0, unit.HexId, unit.DestinationHexId));
                        continue;
                    }
                    bool? entryPosition = CanEnterTransportCity(transportUnit);
                    if (entryPosition == false && unit.HexId != unit.DestinationHexId)
                    {
                        int[] transportRoute = routes.Route(unit.HexId, unit.DestinationHexId);
                        int transportSteps = Math.Min(PlayableLandRoutes.StepsPerPeriod, transportRoute.Length);
                        for (int step = 0; step < transportSteps; step++)
                            if (cities.Values.Any(city => city.HexId == transportRoute[step] &&
                                    IsHostile(unit.ForceId, city.ForceId)) ||
                                gates.Values.Any(gate => gate.HexId == transportRoute[step] &&
                                    IsHostile(unit.ForceId, gate.ForceId)) ||
                                units.Values.Any(other => other.Id != unit.Id &&
                                    IsHostile(unit.ForceId, other.ForceId) &&
                                    other.HexId == transportRoute[step]))
                            {
                                transportSteps = step;
                                break;
                            }
                        if (transportSteps == 0)
                        {
                            reports.Add(new PeriodReportEntry("transport_blocked", unit.ForceId,
                                destination.Id, unit.OfficerId, 0, unit.HexId, unit.DestinationHexId));
                            continue;
                        }
                        int committedSteps = 0;
                        for (int step = 0; step < transportSteps; step++)
                        {
                            if (!TryCommitAlong(transportUnit, transportRoute.AsSpan(step, 1), out var positioned)) break;
                            transportUnit = positioned; committedSteps++;
                            entryPosition = CanEnterTransportCity(transportUnit);
                            if (entryPosition != false) break;
                        }
                        if (committedSteps == 0) continue;
                        reports.Add(new PeriodReportEntry("move", unit.ForceId, null,
                            unit.OfficerId, committedSteps, unit.HexId, transportUnit.HexId));
                    }
                    if (entryPosition is null)
                    {
                        reports.Add(new("unit_enter_unresolved", unit.ForceId, destination.Id, unit.OfficerId,
                            FromHexId: transportUnit.HexId, ToHexId: transportUnit.HexId)
                        { LocationId = destination.Id, RuleId = "unit.current_city_entry_position" });
                        continue;
                    }
                    if (transportUnit.HexId == transportUnit.DestinationHexId || entryPosition == true)
                    {
                        if (EntryState(transportUnit, destination.Id) is string transportRule)
                        {
                            CityState received = cities[destination.Id];
                            reports.Add(new("transport_troops", unit.ForceId, destination.Id, unit.OfficerId,
                                received.Troops - destination.Troops) { RuleId = transportRule });
                            reports.Add(new("transport_supplies", unit.ForceId, destination.Id, unit.OfficerId,
                                received.Supplies - destination.Supplies) { RuleId = transportRule });
                            reports.Add(new("transport_gold", unit.ForceId, destination.Id, unit.OfficerId,
                                received.Gold - destination.Gold) { RuleId = transportRule });
                            if (mission.Arrival == TransportArrivalDisposition.ReturnToSource &&
                                PlayableOfficerAvailability.IsSourceBusy(CurrentWorld(), officers[unit.OfficerId]))
                                reports.Add(new("transport_returning", unit.ForceId, destination.Id, unit.OfficerId)
                                    { RuleId = transportRule });
                            continue;
                        }
                        if (RequiresEntryState(transportUnit, destination.Id))
                        {
                            reports.Add(new("unit_enter_unresolved", unit.ForceId, destination.Id, unit.OfficerId,
                                FromHexId: transportUnit.HexId, ToHexId: transportUnit.HexId)
                            { LocationId = destination.Id, RuleId = PlayableCityEntryStates.UnresolvedRuleId });
                            continue;
                        }
                        if (transportUnit.WoundedTroops is > 0)
                            throw new InvalidDataException("Wounded transport arrival requires an explicit base transfer rule.");
                        if (mission.Arrival == TransportArrivalDisposition.ReturnToSource && !mission.Returning)
                        {
                            cities[destination.Id] = destination with
                            {
                                Supplies = checked(destination.Supplies + transportUnit.Supplies),
                                Gold = checked(destination.Gold + mission.CargoGold)
                            };
                            reports.Add(new PeriodReportEntry("transport_supplies", unit.ForceId,
                                destination.Id, unit.OfficerId, transportUnit.Supplies));
                            reports.Add(new PeriodReportEntry("transport_gold", unit.ForceId,
                                destination.Id, unit.OfficerId, mission.CargoGold));
                            CityState sourceCity = cities[unit.SourceLocationId!.Value];
                            units[unit.Id] = transportUnit with
                            {
                                Supplies = 0, DestinationHexId = sourceCity.HexId!.Value,
                                Mission = mission with
                                {
                                    DestinationCityId = sourceCity.Id, CargoGold = 0, Returning = true
                                }
                            };
                            reports.Add(new PeriodReportEntry("transport_returning", unit.ForceId,
                                destination.Id, unit.OfficerId));
                            continue;
                        }
                        cities[destination.Id] = destination with
                        {
                            Troops = checked(destination.Troops + transportUnit.Troops),
                            Supplies = checked(destination.Supplies + transportUnit.Supplies),
                            Gold = checked(destination.Gold + mission.CargoGold)
                        };
                        officers[unit.OfficerId] = officers[unit.OfficerId] with
                        {
                            Deployed = false, LocationId = destination.Id,
                            DistrictId = destination.DistrictId,
                            LocationState = officers[unit.OfficerId].LocationState is { } location
                                ? location with { Position = null } : null
                        };
                        units.Remove(unit.Id);
                        reports.Add(new PeriodReportEntry("transport_troops", unit.ForceId,
                            destination.Id, unit.OfficerId, transportUnit.Troops));
                        if (!mission.Returning)
                        {
                            reports.Add(new PeriodReportEntry("transport_supplies", unit.ForceId,
                                destination.Id, unit.OfficerId, transportUnit.Supplies));
                            reports.Add(new PeriodReportEntry("transport_gold", unit.ForceId,
                                destination.Id, unit.OfficerId, mission.CargoGold));
                        }
                        else reports.Add(new PeriodReportEntry("transport_returned", unit.ForceId,
                            destination.Id, unit.OfficerId));
                    }
                    continue;
                }
                if (unit.Mission is { Kind: UnitMissionKind.Combat or UnitMissionKind.OccupyArea } &&
                    unit.Morale < 40 && unit.Behavior?.AllowRetreat == true)
                {
                    if (PlayableUnitRetreat.Destination(unit, cities, gates, officers,
                            routes, state.MapWidth) is not { } automaticHome)
                    {
                        reports.Add(new PeriodReportEntry("retreat_blocked", unit.ForceId,
                            null, unit.OfficerId, 0, unit.HexId, unit.DestinationHexId));
                        continue;
                    }
                    unit = unit with
                    {
                        DestinationHexId = automaticHome.HexId,
                        MilitaryPlan = null,
                        Mission = new UnitMission(UnitMissionKind.Retreat)
                    };
                    units[unit.Id] = unit;
                    reports.Add(new PeriodReportEntry("retreat_started", unit.ForceId,
                        cities.ContainsKey(automaticHome.LocationId) ? automaticHome.LocationId : null,
                        unit.OfficerId, unit.Morale, unit.HexId, automaticHome.HexId));
                }
                if (unit.Mission is { Kind: UnitMissionKind.Retreat })
                {
                    if (RetreatDestination(unit) is not { } home)
                    {
                        reports.Add(new PeriodReportEntry("retreat_blocked", unit.ForceId,
                            null, unit.OfficerId, 0, unit.HexId, unit.DestinationHexId));
                        continue;
                    }
                    if (unit.DestinationHexId != home.HexId)
                    {
                        unit = unit with { DestinationHexId = home.HexId };
                        units[unit.Id] = unit;
                    }
                    if (unit.HexId == home.HexId)
                    {
                        CompleteRetreat(unit, home.LocationId);
                        continue;
                    }
                }
                bool currentOccupationContact = false;
                if (unit.Mission is { Kind: UnitMissionKind.OccupyArea, TargetAreaId: int targetArea } &&
                    (unit.MilitaryPlan is null || unit.MilitaryPlan.NextWaypointIndex >=
                        unit.MilitaryPlan.WaypointHexIds.Length))
                {
                    //A protected arrival can leave opposing projections on the
                    //same cell. Occupation must settle that existing contact
                    //before painting or transferring its prefecture. Keep the
                    //area goal pending and use the ordinary encounter/siege gates.
                    TerritoryState currentPlot = (territory ?? state.Territory)[unit.HexId];
                    bool wouldClaimCurrent = currentPlot.AreaId == targetArea &&
                        IsHostile(unit.ForceId, currentPlot.ForceId) ||
                        prefecturesByHex[unit.HexId].Any(areaId => IsHostile(unit.ForceId, areas[areaId].ForceId));
                    bool temporaryEnemyOwnsMovement = unit.MilitaryPlan?.IncidentalEnemyId is not null;
                    currentOccupationContact = wouldClaimCurrent && HasHostileContactAt(unit.ForceId, unit.HexId) &&
                        (!temporaryEnemyOwnsMovement || unit.DestinationHexId == unit.HexId);
                    //A nearby-enemy diversion suspends the area patrol. Its saved
                    //terminal area and waypoint cursor still belong to the
                    //original plan; they resume when the diversion ends. Do
                    //not paint or transfer a prefecture in this area stage,
                    //or overwrite the enemy destination with the next area cell.
                    if (!temporaryEnemyOwnsMovement && currentOccupationContact)
                    {
                        unit = unit with { DestinationHexId = unit.HexId };
                        units[unit.Id] = unit;
                    }
                    else if (!temporaryEnemyOwnsMovement)
                    {
                        // The cell occupied at the start of the period is handled separately;
                        // RouteToNearest excludes its start cell.
                        if ((territory ?? state.Territory)[unit.HexId].AreaId == targetArea &&
                            IsHostile(unit.ForceId, (territory ?? state.Territory)[unit.HexId].ForceId))
                        {
                            territory = PlayableTerritoryPainting.PaintTraversed(state.Territory,
                                territory, [unit.HexId], unit.ForceId, out int currentPainted);
                            if (currentPainted > 0)
                                reports.Add(new PeriodReportEntry("territory", unit.ForceId, null,
                                    unit.OfficerId, currentPainted, unit.HexId, unit.HexId));
                        }
                        CapturePrefectureAt(unit.HexId);
                        int[] occupationRoute = routes.RouteToNearest(unit.HexId, hex =>
                            (territory ?? state.Territory)[hex].AreaId == targetArea &&
                            IsHostile(unit.ForceId, (territory ?? state.Territory)[hex].ForceId));
                        if (occupationRoute.Length == 0)
                        {
                            unit = unit with
                            {
                                DestinationHexId = unit.HexId,
                                Mission = new UnitMission(UnitMissionKind.Combat)
                            };
                            units[unit.Id] = PlayableWaypointMarch.CompleteAction(unit, cities,
                                gates, officers, routes, state.MapWidth, reports);
                            reports.Add(new PeriodReportEntry("occupy_complete", unit.ForceId, null,
                                unit.OfficerId, targetArea, unit.HexId, unit.HexId));
                            continue;
                        }
                        unit = unit with { DestinationHexId = occupationRoute[^1] };
                        units[unit.Id] = unit;
                    }
                }
                bool movementBlocked = !PlayableTacticCombat.CanMove(unit, checked(state.PeriodNumber + 1));
                if (movementBlocked)
                {
                    reports.Add(new PeriodReportEntry("tactic_move_blocked", unit.ForceId,
                        OfficerId: unit.OfficerId, FromHexId: unit.HexId, ToHexId: unit.HexId)
                    { RuleId = PlayableTacticCombat.NativeFootstopBlocksMovement(unit)
                        ? PlayableTacticCombat.NativeFootstopMovementRuleId : null });
                    //Footstop prevents travel. It does not by itself prevent
                    //an independently permitted battle already at this cell.
                    //Keep distant targets and generic confusion behind the
                    //existing veto; no new engagement radius is introduced.
                    if (unit.HexId != unit.DestinationHexId ||
                        unit.Mission?.Kind is UnitMissionKind.Transport or UnitMissionKind.Retreat ||
                        !PlayableTacticCombat.CanAttack(unit, checked(state.PeriodNumber + 1))) continue;
                }
                int[] route = PlayableWaypointMarch.RouteForUnit(unit, routes);
                //A valid military objective can already share this cell, for
                //example after protected armies overlap and a treaty expires.
                //An empty travel route must not silently skip that encounter
                //or an explicitly permitted assault on the current base.
                //This remains the provisional collision policy; no native
                //movement credit, crossing or daily battle timing is inferred.
                bool currentCellContact = route.Length == 0 && unit.HexId == unit.DestinationHexId &&
                    unit.Mission?.Kind is not (UnitMissionKind.Transport or UnitMissionKind.Retreat) &&
                    HasHostileContactAt(unit.ForceId, unit.HexId);
                //An untraversed intermediate leg is still movement even if
                //the terminal target shares the army's current cell.
                if (movementBlocked && !currentCellContact) continue;
                if (route.Length == 0 && !currentCellContact) continue;
                int steps = Math.Min(PlayableLandRoutes.StepsPerPeriod, route.Length);
                int nextHex = steps > 0 ? route[steps - 1] : unit.HexId;
                for (int step = 0; step < steps; step++)
                    if (HasHostileContactAt(unit.ForceId, route[step]))
                    {
                        steps = step + 1;
                        nextHex = route[step];
                        break;
                    }
                if (unit.Mission is { Kind: UnitMissionKind.Retreat })
                {
                    bool hostileEndpoint = HasHostileContactAt(unit.ForceId, nextHex);
                    int travelSteps = hostileEndpoint ? steps - 1 : steps;
                    int retreatHex = travelSteps > 0 ? route[travelSteps - 1] : unit.HexId;
                    int fromHex = unit.HexId;
                    if (!TryCommitAlong(unit, route.AsSpan(0, travelSteps), out unit)) continue;
                    if (travelSteps > 0)
                        reports.Add(new PeriodReportEntry(retreatHex == unit.DestinationHexId
                                ? "arrive" : "move", unit.ForceId, null, unit.OfficerId,
                            travelSteps, fromHex, retreatHex));
                    if (hostileEndpoint)
                        reports.Add(new PeriodReportEntry("retreat_blocked", unit.ForceId,
                            null, unit.OfficerId, 0, retreatHex, nextHex));
                    else if (retreatHex == unit.DestinationHexId &&
                             RetreatDestination(unit) is { } arrival)
                        CompleteRetreat(unit, arrival.LocationId);
                    continue;
                }
                int routeOriginHex = unit.HexId;
                void PaintPath(int traversed, bool deferFinalPrefecture = false)
                {
                    if (traversed == 0) return;
                    int[] claimable = route.Take(traversed).Where(hex =>
                        IsHostile(unit.ForceId, (territory ?? state.Territory)[hex].ForceId)).ToArray();
                    territory = PlayableTerritoryPainting.PaintTraversed(state.Territory,
                        territory, claimable, unit.ForceId, out int painted);
                    if (painted > 0)
                        reports.Add(new PeriodReportEntry("territory", unit.ForceId, null,
                            unit.OfficerId, painted, routeOriginHex, route[traversed - 1]));
                    for (int step = 0; step < traversed - (deferFinalPrefecture ? 1 : 0); step++)
                        CapturePrefectureAt(route[step]);
                }
                UnitState moved = MoveAlong(unit, route.AsSpan(0, steps));
                void StopBeforeUnresolvedBattle(int? locationId = null, string? unresolvedOwner = null)
                {
                    int travelled = Math.Max(0, steps - 1);
                    int stoppedAt = travelled > 0 ? route[travelled - 1] : unit.HexId;
                    if (!TryCommitAlong(PlayableWaypointMarch.ConsumeTraversed(unit,
                        route.AsSpan(0, travelled)), route.AsSpan(0, travelled), out _)) return;
                    PaintPath(travelled);
                    if (travelled > 0)
                        reports.Add(new PeriodReportEntry("move", unit.ForceId, null,
                            unit.OfficerId, travelled, unit.HexId, stoppedAt));
                    reports.Add(new PeriodReportEntry("combat_unresolved", unit.ForceId,
                        cities.ContainsKey(locationId ?? -1) ? locationId : null,
                        unit.OfficerId, 0, stoppedAt, nextHex) { LocationId = locationId,
                        RuleId = currentOccupationContact ? OccupationContactRuleId : null,
                        MissingRuleKeys = unresolvedOwner is null ? null : [unresolvedOwner] });
                }
                PlayableBaseContactPriority? basePriority = currentBasePriority?.Invoke(nextHex);
                if (baseContactPriorityRules is not null && basePriority is null)
                {
                    StopBeforeUnresolvedBattle(unresolvedOwner: "playable.current_Base_contact_priority");
                    continue;
                }
                int? preferredBase = basePriority?.LocationId;
                if (preferredBase is int selectedBase && !cities.ContainsKey(selectedBase) && !gates.ContainsKey(selectedBase))
                {
                    StopBeforeUnresolvedBattle(unresolvedOwner: "playable.current_Base_projection_at_Hex");
                    continue;
                }
                UnitState? selectedFieldArmy = null;
                if (preferredBase is null && currentArmyContact is not null)
                {
                    var selectedArmy = currentArmyContact(nextHex);
                    if (selectedArmy is null || selectedArmy.Value.ArmyId is int at && !units.ContainsKey(at))
                    {
                        StopBeforeUnresolvedBattle(unresolvedOwner: "playable.current_Hex_Army_contact");
                        continue;
                    }
                    if (selectedArmy.Value.ArmyId is int selectedId && units[selectedId] is { } target &&
                        target.Id != unit.Id && IsHostile(unit.ForceId, target.ForceId)) selectedFieldArmy = target;
                }
                UnitState[] hostileUnits = preferredBase is not null ? [] : currentArmyContact is not null
                    ? selectedFieldArmy is null ? [] : [selectedFieldArmy]
                    : units.Values.Where(other => other.Id != unit.Id &&
                        IsHostile(unit.ForceId, other.ForceId) && other.HexId == nextHex)
                    .OrderBy(other => other.Id).ToArray();
                //A distinct enemy sharing the destination must not permanently
                //veto an explicitly permitted target merely by its lower ID.
                //Keep stable ID ordering among permitted provisional contacts.
                UnitState? hostileUnit = hostileUnits.FirstOrDefault(other =>
                    PlayableWaypointMarch.CanEngageEncounter(unit, other, IsHostile)) ?? hostileUnits.FirstOrDefault();
                if (hostileUnit is not null)
                {
                    if (!PlayableWaypointMarch.CanEngageEncounter(unit, hostileUnit, IsHostile))
                    {
                        int travelled = Math.Max(0, steps - 1);
                        int stoppedAt = travelled > 0 ? route[travelled - 1] : unit.HexId;
                        if (!TryCommitAlong(PlayableWaypointMarch.ConsumeTraversed(unit,
                            route.AsSpan(0, travelled)), route.AsSpan(0, travelled), out _)) continue;
                        PaintPath(travelled);
                        if (travelled > 0)
                            reports.Add(new PeriodReportEntry("move", unit.ForceId, null,
                                unit.OfficerId, travelled, unit.HexId, stoppedAt));
                        reports.Add(new PeriodReportEntry("unit_encounter_avoided", unit.ForceId,
                            OfficerId: unit.OfficerId, FromHexId: stoppedAt, ToHexId: nextHex)
                    { RuleId = currentOccupationContact ? OccupationContactRuleId : null });
                        continue;
                    }
                    int fieldApproachSteps = Math.Max(0, steps - 1);
                    if (!TryStageAlong(PlayableWaypointMarch.ConsumeTraversed(unit, route.AsSpan(0, fieldApproachSteps)),
                        route.AsSpan(0, fieldApproachSteps), out var fieldActor, out var fieldWorld)) continue;
                    if (armyContactRules is not null)
                    {
                        bool? permitted = armyContactRules.CanAttackFromCurrent(fieldWorld, fieldActor);
                        if (permitted is null) { StopBeforeUnresolvedBattle(unresolvedOwner: "playable.current_Army_field_actor_admission"); continue; }
                        if (!permitted.Value)
                        {
                            periodBasis = fieldWorld; units[unit.Id] = fieldActor;
                            PaintPath(fieldApproachSteps);
                            if (fieldApproachSteps > 0)
                                reports.Add(new("move", unit.ForceId, OfficerId: unit.OfficerId, Amount: fieldApproachSteps,
                                    FromHexId: routeOriginHex, ToHexId: fieldActor.HexId));
                            reports.Add(new("unit_encounter_avoided", unit.ForceId, OfficerId: unit.OfficerId,
                                FromHexId: fieldActor.HexId, ToHexId: nextHex)
                                { RuleId = "ce-Army-170240-2C96-233560-field-admission-v1" });
                            continue;
                        }
                    }
                    PlayableBattleResolution<PlayableFieldCombatResult>? field = battle.Field(
                        fieldWorld, fieldActor, hostileUnit, checked(state.PeriodNumber + 1));
                    if (field is null)
                    {
                        StopBeforeUnresolvedBattle();
                        continue;
                    }
                    PlayableFieldCombatResult fieldDamage = field.Value.Damage;
                    PlayableBattleValidation.Validate(unit.Troops, hostileUnit.Troops, fieldDamage);
                    ulong nextFieldRandomState = field.Value.RandomState;
                    bool sourceFieldSettlement = false;
                    string? sourceFieldRuleId = null;
                    if (fieldSettlementRules is not null)
                    {
                        GameState settlementWorld = fieldWorld with
                        {
                            PreparedDeployments = [], PreparedOfficerMoves = [], PreparedTalentOrders = [],
                            RandomState = nextFieldRandomState
                        };
                        PlayableFieldSettlementResult settled = fieldSettlementRules.Apply(settlementWorld,
                            fieldActor, hostileUnit, fieldDamage, checked(state.PeriodNumber + 1));
                        if (settled.Outcome == PlayableSiegeSettlementOutcome.Unresolved)
                        {
                            StopBeforeUnresolvedBattle(unresolvedOwner: settled.RuleId);
                            continue;
                        }
                        if (settled.Outcome == PlayableSiegeSettlementOutcome.Committed)
                        {
                            PlayableBattleValidation.Validate(unit.Troops, hostileUnit.Troops, settled.Damage);
                            fieldDamage = settled.Damage;
                            nextFieldRandomState = settled.State.RandomState;
                            CopyInto(cities, settled.State.Cities); CopyInto(gates, settled.State.Gates);
                            CopyInto(districts, settled.State.Districts); CopyInto(officers, settled.State.Officers);
                            CopyInto(units, settled.State.Units);
                            CopyInto(areas, settled.State.Areas);
                            periodBasis = settled.State;
                            reports.AddRange(settled.Reports);
                            sourceFieldSettlement = true;
                            sourceFieldRuleId = settled.RuleId;
                            //259360 can write the surviving winner's wounds,
                            //morale and traits. Continue from that current Army.
                            if (units.TryGetValue(unit.Id, out var currentAttacker)) unit = currentAttacker;
                            if (units.TryGetValue(hostileUnit.Id, out var currentDefender)) hostileUnit = currentDefender;
                        }
                    }
                    if (!sourceFieldSettlement)
                    {
                        periodBasis = fieldWorld;
                        unit = fieldActor; units[unit.Id] = unit;
                    }
                    randomState = nextFieldRandomState;
                    unit = PlayableWaypointMarch.RecordEncounter(unit, hostileUnit.Id);
                    //Both participants have now resolved this encounter. The
                    //defender's explicit target must retain the same pursuit
                    //history even when its own turn follows another waypoint.
                    hostileUnit = PlayableWaypointMarch.RecordEncounter(hostileUnit, unit.Id);
                    int fieldDefenderLoss = fieldDamage.DefenderLoss;
                    int fieldAttackerLoss = fieldDamage.AttackerLoss;
                    int attackerTroops = fieldDamage.RemainingAttackers;
                    int defenderTroops = fieldDamage.RemainingDefenders;
                    reports.Add(new PeriodReportEntry("field_attack", unit.ForceId, null,
                        unit.OfficerId, fieldDefenderLoss, routeOriginHex, nextHex)
                    { RuleId = fieldDamage.AttackerStrikeVetoRuleId ?? sourceFieldRuleId });
                    reports.Add(new PeriodReportEntry("field_defense", hostileUnit.ForceId, null,
                        hostileUnit.OfficerId, fieldAttackerLoss, nextHex, nextHex)
                    { RuleId = fieldDamage.DefenderStrikeVetoRuleId ?? sourceFieldRuleId });
                    if (defenderTroops == 0)
                    {
                        if (!sourceFieldSettlement || units.ContainsKey(hostileUnit.Id))
                            PlayableUnitAftermath.Destroy(hostileUnit with { Troops = defenderTroops }, cities, gates, officers,
                                units, reports, state.MapWidth, onArmyRetired);
                    }
                    else units[hostileUnit.Id] = hostileUnit with { Troops = defenderTroops };
                    if (attackerTroops == 0)
                    {
                        PaintPath(Math.Max(0, steps - 1));
                        if (!sourceFieldSettlement || units.ContainsKey(unit.Id))
                            PlayableUnitAftermath.Destroy(unit with { Troops = attackerTroops }, cities, gates, officers,
                                units, reports, state.MapWidth, onArmyRetired);
                        continue;
                    }
                    bool blocked;
                    if (armyContactRules is not null || baseContactPriorityRules is not null)
                    {
                        currentBasePriority = baseContactPriorityRules?.CaptureCurrentBasePriority(CurrentWorld());
                        currentArmyContact = armyContactRules?.CaptureCurrentArmyContact(CurrentWorld());
                        blocked = defenderTroops > 0 || HasHostileContactAt(unit.ForceId, nextHex);
                    }
                    else blocked = defenderTroops > 0 ||
                        units.Values.Any(other => IsHostile(unit.ForceId, other.ForceId) &&
                            other.HexId == nextHex) ||
                        cities.Values.Any(city => IsHostile(unit.ForceId, city.ForceId) &&
                            city.HexId == nextHex) ||
                        gates.Values.Any(gate => IsHostile(unit.ForceId, gate.ForceId) &&
                            gate.HexId == nextHex);
                    int actualSteps = blocked ? Math.Max(0, steps - 1) : steps;
                    int actualHex = actualSteps > 0 ? route[actualSteps - 1] : unit.HexId;
                    units[unit.Id] = unit with { Troops = attackerTroops };
                    var remainingFieldRoute = route.AsSpan(fieldApproachSteps, actualSteps - fieldApproachSteps);
                    if (!TryCommitAlong(PlayableWaypointMarch.ConsumeTraversed(units[unit.Id], remainingFieldRoute),
                        remainingFieldRoute, out _))
                    {
                        PaintPath(fieldApproachSteps);
                        if (fieldApproachSteps > 0)
                            reports.Add(new("move", unit.ForceId, OfficerId: unit.OfficerId, Amount: fieldApproachSteps,
                                FromHexId: routeOriginHex, ToHexId: unit.HexId));
                        continue;
                    }
                    PaintPath(actualSteps);
                    if (actualSteps > 0)
                        reports.Add(new PeriodReportEntry(actualHex == unit.DestinationHexId
                                ? "arrive" : "move", unit.ForceId, null, unit.OfficerId,
                            actualSteps, routeOriginHex, actualHex));
                    continue;
                }
                CityState? hostileCity = null; GateState? hostileGate = null;
                if (baseContactPriorityRules is not null)
                {
                    if (preferredBase is int selected)
                    {
                        if (cities.GetValueOrDefault(selected) is { } currentCity && IsHostile(moved.ForceId, currentCity.ForceId))
                            hostileCity = currentCity;
                        else if (gates.GetValueOrDefault(selected) is { } currentGate && IsHostile(moved.ForceId, currentGate.ForceId))
                            hostileGate = currentGate;
                    }
                }
                else
                {
                    hostileCity = cities.Values.FirstOrDefault(city => city.HexId == nextHex && IsHostile(moved.ForceId, city.ForceId));
                    if (hostileCity is null) hostileGate = gates.Values.FirstOrDefault(gate => gate.HexId == nextHex && IsHostile(moved.ForceId, gate.ForceId));
                }
                if (hostileCity is null && hostileGate is null)
                {
                    if (!TryCommitAlong(PlayableWaypointMarch.ConsumeTraversed(unit,
                        route.AsSpan(0, steps)), route.AsSpan(0, steps), out _)) continue;
                    reports.Add(new PeriodReportEntry(nextHex == unit.DestinationHexId ? "arrive" : "move",
                        unit.ForceId, null, unit.OfficerId, steps, unit.HexId, nextHex));
                    PaintPath(steps);
                    continue;
                }
                int hostileLocationId = hostileCity?.Id ?? hostileGate!.Id;
                int? hostileLocationOwner = hostileCity?.ForceId ?? hostileGate?.ForceId;
                if (!PlayableWaypointMarch.CanEngageLocation(unit, hostileLocationId,
                        hostileLocationOwner, IsHostile))
                {
                    int travelled = Math.Max(0, steps - 1);
                    int stoppedAt = travelled > 0 ? route[travelled - 1] : unit.HexId;
                    if (!TryCommitAlong(PlayableWaypointMarch.ConsumeTraversed(unit,
                        route.AsSpan(0, travelled)), route.AsSpan(0, travelled), out _)) continue;
                    PaintPath(travelled);
                    if (travelled > 0)
                        reports.Add(new PeriodReportEntry("move", unit.ForceId, null,
                            unit.OfficerId, travelled, unit.HexId, stoppedAt));
                    reports.Add(new PeriodReportEntry("unit_encounter_avoided", unit.ForceId,
                        OfficerId: unit.OfficerId, FromHexId: stoppedAt, ToHexId: nextHex)
                    { LocationId = hostileLocationId,
                        RuleId = currentOccupationContact ? OccupationContactRuleId : null });
                    continue;
                }
                int defenders = hostileCity?.Troops ?? hostileGate!.Troops;
                //Battle and source callbacks see the actual approach cell,
                //not the period's route origin or an unentered hostile cell.
                //Keep this detached until the callback commits, so a reached
                //unknown still uses the ordinary stop-before-contact path.
                int siegeApproachSteps = Math.Max(0, steps - 1);
                if (!TryStageAlong(PlayableWaypointMarch.ConsumeTraversed(unit, route.AsSpan(0, siegeApproachSteps)),
                    route.AsSpan(0, siegeApproachSteps), out var siegeActor, out var siegeWorld)) continue;
                PlayableBattleResolution<PlayableSiegeResult>? siegeResolution = battle.Siege(
                    siegeWorld, siegeActor, hostileLocationId, checked(state.PeriodNumber + 1));
                if (siegeResolution is null)
                {
                    StopBeforeUnresolvedBattle(hostileLocationId);
                    continue;
                }
                PlayableSiegeResult siege = siegeResolution.Value.Damage;
                PlayableBattleValidation.Validate(moved.Troops, defenders,
                    hostileCity?.Durability ?? hostileGate?.Durability, siege);
                string? settlementRuleId = null;
                bool sourceSiegeSettlement = false;
                bool sourceBaseCaptureHandled = false;
                ulong nextSiegeRandomState = siegeResolution.Value.RandomState;
                if (siegeSettlementRules is not null)
                {
                    //These preparation queues have already been consumed in
                    //this period. Current native operations must not see them
                    //as a second pending deployment or repeat their resources.
                    GameState settlementWorld = siegeWorld with
                    {
                        PreparedDeployments = [], PreparedOfficerMoves = [], PreparedTalentOrders = [],
                        RandomState = nextSiegeRandomState
                    };
                    PlayableSiegeSettlementResult settled = siegeSettlementRules.Apply(settlementWorld,
                        siegeActor, hostileLocationId, siege, checked(state.PeriodNumber + 1));
                    if (settled.Outcome == PlayableSiegeSettlementOutcome.Unresolved)
                    {
                        StopBeforeUnresolvedBattle(hostileLocationId, settled.UnresolvedOwner);
                        continue;
                    }
                    if (settled.Outcome == PlayableSiegeSettlementOutcome.Committed)
                    {
                        PlayableBattleValidation.Validate(moved.Troops, defenders,
                            hostileCity?.Durability ?? hostileGate?.Durability, settled.Damage);
                        siege = settled.Damage;
                        settlementRuleId = settled.RuleId;
                        nextSiegeRandomState = settled.State.RandomState;
                        //Keep the existing operation's dictionaries alive for
                        //closures, and carry the saved native owners forward.
                        CopyInto(cities, settled.State.Cities); CopyInto(gates, settled.State.Gates);
                        CopyInto(districts, settled.State.Districts); CopyInto(officers, settled.State.Officers);
                        CopyInto(units, settled.State.Units);
                        CopyInto(areas, settled.State.Areas);
                        periodBasis = settled.State;
                        reports.AddRange(settled.Reports);
                        sourceSiegeSettlement = true;
                        sourceBaseCaptureHandled = settled.BaseCaptureHandled;
                        if (sourceBaseCaptureHandled)
                        {
                            territory = settled.State.Territory;
                            CopyInto(structures, settled.State.Structures!);
                        }
                        if (units.TryGetValue(unit.Id, out var currentAttacker))
                        {
                            unit = currentAttacker;
                            moved = MoveAlong(unit, route.AsSpan(siegeApproachSteps, steps - siegeApproachSteps));
                        }
                    }
                }
                if (!sourceSiegeSettlement)
                {
                    periodBasis = siegeWorld;
                    unit = siegeActor; units[unit.Id] = unit;
                }
                randomState = nextSiegeRandomState;
                int remainingDefenders = siege.RemainingDefenders;
                int remainingAttackers = siege.RemainingAttackers;
                bool baseCaptured = siege.Captured;
                if (sourceBaseCaptureHandled)
                {
                    //2851B0 has already performed26DA10 and true29F220.
                    //Its current garrison can include the winning Army, and
                    //its selected land writes must not be repainted by Core.
                    reports.Add(new("assault", moved.ForceId, hostileCity?.Id,
                        moved.OfficerId, siege.DefenderLoss, nextHex, nextHex)
                    { LocationId = hostileLocationId, RuleId = settlementRuleId });
                    if (siege.AttackerLoss > 0)
                        reports.Add(new("casualties", moved.ForceId, hostileCity?.Id,
                            moved.OfficerId, siege.AttackerLoss, nextHex, nextHex) { LocationId = hostileLocationId });
                    if (periodBasis.NativePlayableTerminal is not null || periodBasis.CampaignTurnStop is not null)
                        return TerminalReport(CurrentWorld());
                    continue;
                }
                reports.Add(new PeriodReportEntry("assault", moved.ForceId, hostileCity?.Id,
                    moved.OfficerId, siege.DefenderLoss, nextHex, nextHex)
                { LocationId = hostileLocationId, RuleId = settlementRuleId ?? (steps == 0 ? StandingBaseContactRuleId : null) });
                if (siege.DurabilityLoss > 0)
                    reports.Add(new PeriodReportEntry("durability_damage", moved.ForceId,
                        hostileCity?.Id, moved.OfficerId, siege.DurabilityLoss, nextHex, nextHex)
                    { LocationId = hostileLocationId });
                if (siege.AttackerLoss > 0)
                    reports.Add(new PeriodReportEntry("casualties", moved.ForceId, hostileCity?.Id,
                        moved.OfficerId, siege.AttackerLoss, nextHex, nextHex) { LocationId = hostileLocationId });
                if (hostileCity is not null)
                    cities[hostileCity.Id] = cities[hostileCity.Id] with
                    {
                        Troops = remainingDefenders, Durability = siege.RemainingDurability
                    };
                else gates[hostileGate!.Id] = gates[hostileGate.Id] with
                { Troops = remainingDefenders, Durability = siege.RemainingDurability };
                if (remainingAttackers == 0)
                {
                    PaintPath(siegeApproachSteps);
                    if (!sourceSiegeSettlement || units.ContainsKey(moved.Id))
                        PlayableUnitAftermath.Destroy(moved with { Troops = remainingAttackers }, cities, gates, officers,
                            units, reports, state.MapWidth, onArmyRetired);
                    continue;
                }
                if (!baseCaptured)
                {
                    // A surviving attacker waits next to the defended location and retries next period.
                    int stagingHex = steps > 1 ? route[steps - 2] : unit.HexId;
                    units[moved.Id] = unit with { Troops = remainingAttackers };
                    PaintPath(siegeApproachSteps);
                    if (steps > 1)
                        reports.Add(new PeriodReportEntry("move", unit.ForceId, null,
                            unit.OfficerId, steps - 1, routeOriginHex, stagingHex));
                    continue;
                }
                DistrictState attackerDistrict = state.Districts.Values
                    .OrderBy(district => district.Id)
                    .First(district => district.ForceId == moved.ForceId && district.Number == 1);
                units[moved.Id] = unit with { Troops = remainingAttackers };
                var remainingSiegeRoute = route.AsSpan(siegeApproachSteps, steps - siegeApproachSteps);
                if (!TryCommitAlong(PlayableWaypointMarch.ConsumeTraversed(units[moved.Id], remainingSiegeRoute),
                    remainingSiegeRoute, out _))
                {
                    PaintPath(siegeApproachSteps);
                    if (siegeApproachSteps > 0)
                        reports.Add(new("move", unit.ForceId, OfficerId: unit.OfficerId, Amount: siegeApproachSteps,
                            FromHexId: routeOriginHex, ToHexId: unit.HexId));
                    continue;
                }
                PaintPath(steps, baseCaptured);
                if (steps > 0)
                    reports.Add(new PeriodReportEntry(nextHex == unit.DestinationHexId ? "arrive" : "move",
                        unit.ForceId, null, unit.OfficerId, steps, routeOriginHex, nextHex));
                if (hostileCity is not null)
                {
                    if (remainingDefenders > 0)
                        reports.Add(new PeriodReportEntry("garrison_scattered", moved.ForceId,
                            hostileCity.Id, moved.OfficerId, remainingDefenders, nextHex, nextHex)
                        { LocationId = hostileLocationId });
                    cities[hostileCity.Id] = cities[hostileCity.Id] with
                    {
                        ForceId = moved.ForceId, DistrictId = attackerDistrict.Id, GovernorId = null,
                        Troops = 0,
                        Duties = new CityDutyState(null, null, 0)
                    };
                    var capturedAreas = cities[hostileCity.Id].AreaIds.ToHashSet();
                    foreach (int areaId in capturedAreas)
                        areas[areaId] = areas[areaId] with
                        {
                            ForceId = moved.ForceId,
                            Administration = new AreaAdministrationState(null, AreaDevelopmentFocus.None)
                        };
                    territory ??= new Dictionary<int, TerritoryState>(state.Territory);
                    foreach ((int hexId, TerritoryState cell) in state.Territory)
                        if (cell.AreaId is int areaId && capturedAreas.Contains(areaId))
                            territory[hexId] = cell with { ForceId = moved.ForceId };
                }
                else
                {
                    if (remainingDefenders > 0)
                        reports.Add(new PeriodReportEntry("garrison_scattered", moved.ForceId,
                            null, moved.OfficerId, remainingDefenders, nextHex, nextHex)
                        { LocationId = hostileLocationId });
                    gates[hostileGate!.Id] = gates[hostileGate.Id] with
                    {
                        ForceId = moved.ForceId, DistrictId = attackerDistrict.Id, GovernorId = null,
                        Troops = 0
                    };
                }
                CapturePrefectureAt(nextHex);
                reports.Add(new PeriodReportEntry("capture", moved.ForceId, hostileCity?.Id,
                    moved.OfficerId, 0, nextHex, nextHex) { LocationId = hostileLocationId });
                GameState postCapture = CurrentWorld() with
                { PreparedDeployments = [], PreparedOfficerMoves = [], PreparedTalentOrders = [] };
                PlayableForceDefeatObservation? beforeDispositions = hostileCity is not null &&
                    hostileLocationOwner is int previousForce && PlayableForceDefeat.IsOrdinaryForce(previousForce) &&
                    !PlayableForceDefeat.OwnsCity(postCapture, previousForce)
                    ? PlayableForceDefeat.ObserveBeforeDispositions(postCapture, previousForce) : null;
                PlayableLastCityPersonnelResolution? personnel = beforeDispositions is { WasActive: true } &&
                    lastCityPersonnelRules is not null
                    ? lastCityPersonnelRules.Settle(postCapture, hostileCity!, beforeDispositions.ForceId, moved.ForceId) : null;
                if (personnel is not null) postCapture = personnel.State;
                //27A6B0's player exit is after the reached capture Person
                //prefix, but before ordinary293BD0 and provisional withdrawal.
                //Keep the Force/District/Army owners that this branch preserves.
                if (postCapture.NativePlayableTerminal is { Kind: NativePlayableTerminalKind.PlayerDefeated } ||
                    postCapture.CampaignTurnStop is not null)
                {
                    if (personnel is not null) reports.AddRange(personnel.Reports);
                    return TerminalReport(postCapture);
                }
                // The former residents belong to a separate aftermath policy.
                // Consumed preparation queues are removed from this operation's
                // snapshot; deployed commanders retain their independent homes.
                PlayableBaseCaptureAftermathResult aftermath = PlayableBaseCaptureAftermath.Settle(
                    postCapture, hostileLocationId, hostileLocationOwner, routes, baseCaptureAftermathRules, captureReturningPersonRules,
                    hostileCity, captureArmyHomeRules);
                PlayableForceDefeatResult? defeat = hostileCity is not null
                    ? PlayableForceDefeat.AfterCityCapture(aftermath.State, hostileCity.Id,
                        hostileLocationOwner, moved.ForceId, checked(state.PeriodNumber + 1), beforeDispositions,
                        personnel?.ResolvedOfficerIds, personnel?.NativeUnregisterRuleId, personnel?.ResourceDisposedArmyIds) : null;
                if (defeat is not null) aftermath = aftermath with { State = defeat.State };
                periodBasis = aftermath.State;
                cities = aftermath.State.Cities;
                gates = aftermath.State.Gates;
                districts = aftermath.State.Districts;
                officers = aftermath.State.Officers;
                units = aftermath.State.Units;
                areas = aftermath.State.Areas;
                structures = aftermath.State.Structures!;
                territory = aftermath.State.Territory;
                randomState = aftermath.State.RandomState;
                if (personnel is not null) reports.AddRange(personnel.Reports);
                reports.AddRange(aftermath.Reports);
                if (defeat is not null) reports.AddRange(defeat.Reports);
                if (periodBasis.NativePlayableTerminal is not null || periodBasis.CampaignTurnStop is not null)
                    return TerminalReport(CurrentWorld());
            }

            GameState TerminalReport(GameState stopped) => stopped with
            {
                PreparedDeployments = [], PreparedOfficerMoves = [], PreparedTalentOrders = [],
                TacticReports = tacticReports, PersonnelReports = personnelReports,
                MonthlyIncomeReports = [], PeriodReports = reports.ToArray(), PendingCommandReports = [],
                PeriodNumber = checked(state.PeriodNumber + 1), Phase = TurnPhase.Reporting
            };

            // Construction is settled after movement: reaching land is required before
            // its source city pays and before any durability can be added.
            foreach (UnitState builder in units.Values.OrderBy(unit => unit.Id).ToArray())
            {
                if (nativePeriodContinuationRules?.UsesSourceFacilityWork == true && builder.NativeArmyOrigin is not null) continue;
                if (builder.Mission is not { Kind: UnitMissionKind.BuildStructure } mission ||
                    builder.HexId != builder.DestinationHexId ||
                    builder.MilitaryPlan is { } buildingPlan &&
                    buildingPlan.NextWaypointIndex < buildingPlan.WaypointHexIds.Length) continue;
                PlayableConstructionRules.Parameters buildParameters =
                    PlayableConstructionRules.ForKind(mission.StructureKindId!);
                bool owned = (territory ?? state.Territory).TryGetValue(builder.HexId,
                    out TerritoryState? plot) && plot.ForceId == builder.ForceId;
                if (!owned || builder.SourceLocationId is not int sourceId ||
                    !cities.TryGetValue(sourceId, out CityState? source) ||
                    source.ForceId != builder.ForceId)
                {
                    units[builder.Id] = FinishMilitaryAction(builder, interrupted: true);
                    reports.Add(new PeriodReportEntry("structure_blocked", builder.ForceId,
                        null, builder.OfficerId, 0, builder.HexId, builder.HexId));
                    continue;
                }
                StructureState? current = structures.Values.FirstOrDefault(item =>
                    item.HexId == builder.HexId);
                if (current is null)
                {
                    GameState currentWorld = state with
                    {
                        Cities = cities, Gates = gates, Territory = territory ?? state.Territory,
                        Structures = structures
                    };
                    if (!PlayableConstructionRules.CanBuildAt(currentWorld, routes,
                            builder.ForceId, builder.HexId, structures) ||
                        source.Gold < buildParameters.GoldCost)
                    {
                        units[builder.Id] = FinishMilitaryAction(builder, interrupted: true);
                        reports.Add(new PeriodReportEntry("structure_blocked", builder.ForceId,
                            source.Id, builder.OfficerId, 0, builder.HexId, builder.HexId));
                        continue;
                    }
                    cities[source.Id] = source with
                    {
                        Gold = source.Gold - buildParameters.GoldCost
                    };
                    int structureId = structures.Count == 0 ? 1 : checked(structures.Keys.Max() + 1);
                    current = new StructureState(structureId, mission.StructureKindId!,
                        builder.HexId, builder.ForceId, 0,
                        buildParameters.MaxDurability, source.Id);
                    structures.Add(structureId, current);
                    reports.Add(new PeriodReportEntry("structure_start", builder.ForceId,
                        source.Id, builder.OfficerId,
                        buildParameters.GoldCost, builder.HexId, builder.HexId));
                }
                if (current.ForceId != builder.ForceId ||
                    current.KindId != mission.StructureKindId ||
                    current.Durability >= (current.MaxDurability ?? throw new InvalidDataException("Structure construction requires its current durability capacity.")))
                {
                    units[builder.Id] = FinishMilitaryAction(builder, interrupted: true);
                    reports.Add(new PeriodReportEntry("structure_blocked", builder.ForceId,
                        null, builder.OfficerId, 0, builder.HexId, builder.HexId));
                    continue;
                }
                int maximumDurability = current.MaxDurability!.Value;
                int nextDurability = Math.Min(maximumDurability,
                    current.Durability + buildParameters.ProgressPerPeriod);
                structures[current.Id] = current with
                {
                    Durability = nextDurability,
                    Completed = nextDurability == current.MaxDurability
                };
                reports.Add(new PeriodReportEntry(nextDurability == current.MaxDurability
                        ? "structure_complete" : "structure_progress", builder.ForceId,
                    source.Id, builder.OfficerId, nextDurability, builder.HexId, builder.HexId));
                if (nextDurability == current.MaxDurability)
                    units[builder.Id] = FinishMilitaryAction(builder);
            }

            foreach (UnitState worker in units.Values.OrderBy(unit => unit.Id).ToArray())
            {
                if (nativePeriodContinuationRules?.UsesSourceFacilityWork == true && worker.NativeArmyOrigin is not null) continue;
                if (worker.Mission is not { Kind: UnitMissionKind.RepairStructure or
                        UnitMissionKind.DemolishStructure, TargetStructureId: int targetId } task ||
                    worker.HexId != worker.DestinationHexId ||
                    worker.MilitaryPlan is { } workerPlan &&
                    workerPlan.NextWaypointIndex < workerPlan.WaypointHexIds.Length) continue;
                if (!structures.TryGetValue(targetId, out StructureState? target) ||
                    target.HexId != worker.HexId || target.ForceId != worker.ForceId ||
                    (task.Kind == UnitMissionKind.RepairStructure && !target.Completed) ||
                    (territory ?? state.Territory)[worker.HexId].ForceId != worker.ForceId)
                {
                    units[worker.Id] = FinishMilitaryAction(worker, interrupted: true);
                    reports.Add(new PeriodReportEntry("structure_blocked", worker.ForceId,
                        null, worker.OfficerId, 0, worker.HexId, worker.HexId));
                    continue;
                }
                if (task.Kind == UnitMissionKind.DemolishStructure)
                {
                    structures.Remove(target.Id);
                    units[worker.Id] = FinishMilitaryAction(worker);
                    reports.Add(new PeriodReportEntry("structure_demolished", worker.ForceId,
                        null, worker.OfficerId, 0, worker.HexId, worker.HexId));
                    continue;
                }
                int maximumDurability = target.MaxDurability ?? throw new InvalidDataException("Structure repair requires its current durability capacity.");
                int nextDurability = Math.Min(maximumDurability,
                    target.Durability +
                    PlayableConstructionRules.ForKind(target.KindId).ProgressPerPeriod);
                structures[target.Id] = target with { Durability = nextDurability };
                reports.Add(new PeriodReportEntry(nextDurability == target.MaxDurability
                        ? "structure_repaired" : "structure_repair_progress", worker.ForceId,
                    null, worker.OfficerId, nextDurability, worker.HexId, worker.HexId));
                if (nextDurability == target.MaxDurability)
                    units[worker.Id] = FinishMilitaryAction(worker);
            }

            PlayableWaypointMarch.AfterMovement(cities, gates, officers, units,
                territory ?? state.Territory, routes, state.MapWidth, reports,
                IsHostile, structures, onArmyRetired, EntryResources, EntryState, RequiresEntryState,
                sourceArmyTimelineCompleted: sourceArmyPeriod is not null);

            territory = PlayableAreaOfficerOccupation.Settle(CurrentWorld(), cities, areas, officers,
                units, territory, routes, reports);
            if (sourceArmyPeriod is not null)
                AdoptFoodSettlement(NativeArmyPreparation.PrepareAfterDeploymentOwned(
                    CurrentWorld() with { PeriodReports = reports.ToArray() }, nativeArmyPreparationRules));
            if (sourceArmyPeriod is null)
                PlayablePeriodEconomySettlement.Settle(CurrentWorld(), cities, areas, officers,
                    periodEconomy, reports);
            MonthlyIncomeReportEntry[] incomeReports = [];
            if (state.Date.Day == 21)
            {
                int[] incomeCities = cities.Values.Where(city => city.ForceId is not null)
                    .OrderBy(city => city.Id).Select(city => city.Id).ToArray();
                MonthlyIncomeQuote[] quotes = PlayableMonthlyIncome.Settle(CurrentWorld(),
                    cities, areas, monthlyIncome, reports);
                incomeReports = quotes.Select((quote, index) => new MonthlyIncomeReportEntry(
                    incomeCities[index], cities[incomeCities[index]].ForceId!.Value,
                    checked(state.PeriodNumber + 1), quote)).ToArray();
            }
            PlayableSupplyLines.Settle(cities, gates, units, territory ?? state.Territory,
                routes, reports, (actor, owner) => PlayableDiplomacy.AreAllied(periodBasis, actor, owner),
                sourceArmyTimelineCompleted: sourceArmyPeriod is not null);
            if (sourceArmyPeriod is not null)
            {
                AdoptFoodSettlement(sourceArmyPeriod.CompleteArmyFoodTurn(
                    CurrentWorld() with { PeriodReports = reports.ToArray() }));
                PlayablePeriodEconomySettlement.Settle(CurrentWorld(), cities, areas, officers,
                    periodEconomy, reports);
            }
            else
            {
                var foodShortages = new List<PlayableArmyFoodShortage>();
                var garrisonSettlement = (armyCreationRules as IPlayableGarrisonFoodShortageRules)?
                    .BeginGarrisonFoodShortageSettlement();
                PlayableSupplyUpkeep.Settle(cities, units, periodFood!, reports,
                    armyCreationRules is IPlayableArmyFoodShortageRules ? foodShortages : null,
                    garrisonSettlement is not null
                        ? shortage => AdoptFoodSettlement(garrisonSettlement(
                            CurrentWorld() with { PeriodReports = reports.ToArray() }, shortage)) : null);
                if (foodShortages.Count != 0)
                {
                    var settled = ((IPlayableArmyFoodShortageRules)armyCreationRules!).ApplyArmyFoodShortages(
                        CurrentWorld() with { PeriodReports = reports.ToArray() }, foodShortages);
                    AdoptFoodSettlement(settled);
                }
            }
            void AdoptFoodSettlement(GameState settled)
            {
                CopyInto(cities, settled.Cities); CopyInto(gates, settled.Gates);
                CopyInto(districts, settled.Districts); CopyInto(officers, settled.Officers);
                CopyInto(units, settled.Units); CopyInto(structures, settled.Structures
                    ?? throw new InvalidDataException("Current food settlement lost its Structure owner."));
                CopyInto(areas, settled.Areas);
                territory = settled.Territory; randomState = settled.RandomState;
                reports.Clear(); reports.AddRange(settled.PeriodReports);
                periodBasis = settled;
            }

            PlayableUnitAftermath.RemoveZeroMorale(cities, gates, officers, units, reports,
                state.MapWidth, onArmyRetired, sourceArmyTimelineCompleted: sourceArmyPeriod is not null);
            GameState next = periodBasis with
            {
                Cities = cities, Gates = gates, Districts = districts, Officers = officers, Units = units,
                Structures = structures,
                Areas = areas, Territory = territory ?? state.Territory,
                PreparedDeployments = [], PreparedOfficerMoves = [],
                PreparedTalentOrders = [], RandomState = randomState,
                TacticReports = tacticReports,
                PersonnelReports = personnelReports,
                MonthlyIncomeReports = incomeReports,
                NativeClock = nativeState.NativeClock is { } completedClock ? completedClock with
                    { AnchorDate = state.Date.Next(), UpdatesIntoPeriod = 0 } : null,
                NativeSubdayReports = nativeState.NativeSubdayReports,
                PeriodReports = reports.ToArray(),
                PendingCommandReports = [],
                Date = state.Date.Next(), PeriodNumber = checked(state.PeriodNumber + 1),
                Phase = TurnPhase.Reporting
            };
            int[] arrivedOfficers = possibleArrivalOfficers.Where(id =>
                next.Officers.TryGetValue(id, out OfficerState? person) && !person.Deployed &&
                !next.Units.Values.Any(army => army.OfficerId == id)).ToArray();
            if (arrivedOfficers.Length != 0)
            {
                PlayableOfficerArrivalAftermathResult arrivals = PlayableOfficerArrivalAftermath.Settle(next,
                    arrivedOfficers);
                next = arrivals.State;
                reports.AddRange(arrivals.Reports);
            }
            int[] dueForces = next.Forces.Values.Where(force =>
                force.Governance?.Pending?.ActivationPeriod == next.PeriodNumber)
                .Select(force => force.Id).Order().ToArray();
            if (dueForces.Length != 0)
            {
                CommandResult activated = GovernanceActivation.ApplyDueChanges(next,
                    new PlayableGovernanceActivationRules());
                if (!activated.Accepted)
                    throw new InvalidDataException($"Playable governance activation failed: {activated.Error}");
                next = activated.State!;
                foreach (int forceId in dueForces)
                    reports.Add(new PeriodReportEntry("governance_active", forceId,
                        Amount: next.Forces[forceId].Governance!.Active.Assignments.Length));
            }
            // Stage26's Base-neighbor writer must see the positions and
            // retirements completed above, not the pre-movement waiting image.
            if (nativePeriodContinuationRules is not null)
            {
                int retainedReports = next.PeriodReports.Length;
                next = nativePeriodContinuationRules.CompleteMovement(next);
                // Continuations append source diagnostics to their state. The
                // period's local report accumulator must retain those entries
                // before it becomes the final reporting snapshot.
                reports.AddRange(next.PeriodReports.Skip(retainedReports));
            }
            if (reports.Count == 0) reports.Add(new PeriodReportEntry("period", 0));
            return PlayableEventRuntime.SettleCompletedPeriod(next with
                { PeriodReports = reports.ToArray() }, events);
        }
    }

    private sealed class Reporting(IPlayableDistrictCommandBudgetRules? districtCommandBudgetRules) : IReportingRules
    {
        public GameState CompleteReport(GameState state)
        {
            RequireSkeleton(state);
            if (state.NativePlayableTerminal is not null || state.CampaignTurnStop is not null)
                return state with { Phase = TurnPhase.Strategy };
            if (districtCommandBudgetRules is not null)
                return districtCommandBudgetRules.RefreshDistrictCommandBudgets(state) with { Phase = TurnPhase.Strategy };
            if (state.NativeArmySimulation is not null)
                throw new InvalidDataException("The owned campaign needs current district command budget rules.");
            // Legacy skeletons retain their known quantities. No inferred
            // replenishment is installed when the current rule owner is absent.
            return state with { Phase = TurnPhase.Strategy };
        }
    }

    private static void RequireSkeleton(GameState state)
    {
        if (state.RulesetId != RulesetId)
            throw new InvalidOperationException("A different ruleset requires its own period rules.");
    }
}

public sealed record PlayableRuleSet(IOrderRules OrderRules, IAdvancementRules AdvancementRules,
    IReportingRules ReportingRules, PlayableLandRoutes Routes)
{
    /// <summary>The same tactic provider used by command validation and period settlement.</summary>
    public IPlayableTacticRules TacticRules { get; init; } = UnresolvedTacticRules.Instance;
    public IPlayableOrdinaryDeploymentCommitRules? OrdinaryDeploymentRules { get; init; }
    public IPersonnelCommandRules PersonnelRules { get; init; } = UnresolvedPersonnelRules.Instance;
    public IGovernanceRosterRules? GovernanceRosterRules { get; init; }
}

/// <summary>A local player can command only the chosen force, even while browsing others.</summary>
public sealed class SingleForceAuthority(int forceId, string actorId,
    IEnumerable<int>? delegatedDistrictIds = null) : IOrderAuthority
{
    private readonly HashSet<int> _delegatedDistrictIds = (delegatedDistrictIds ?? []).ToHashSet();
    public bool CanControl(CommandContext context, int requestedForceId) =>
        requestedForceId == forceId && context.ActorId == actorId &&
        (context.DelegatedDistrictId is not int districtId || _delegatedDistrictIds.Contains(districtId));
}
