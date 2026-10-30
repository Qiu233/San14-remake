using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableOrdinaryDeploymentCommitRules
{
    public OrdinaryDeploymentNavalFormation ResolveDefaultNavalFormation(GameState state, int officerId)
    {
        try
        {
            var current = CaptureCurrentDefinitions(state) ?? throw OCMissing("naval_current");
            int person = OCKnown(current.ReadPersonLookupReference(officerId)?.PhysicalSlot, "naval_person");
            if (!state.Officers.ContainsKey(officerId) || current.ReadPersonIdentity(person) != officerId ||
                !OCKnown(current.ReadPersonNativeValidity(person), "naval_person_validity")) throw OCMissing("naval_person_owner");
            return new(DeploymentNavalFormationSource.Resolve(state, current, officerId, person), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }
    /// <summary>Normal Target confirmation: create and register real at-home
    /// armies now. No saved deferred preparation or later resource debit.</summary>
    public CommandResult CommitOrdinaryDeployment(GameState state, DeploymentSource source,
        IReadOnlyList<DeploymentUnitRequest> units, CommandContext context)
        => CommitDeploymentUnitsCurrent(state, source, units, context, debitCommandBook: true);

    //1EC980 is shared by normal departure and Campaign launch. Its parameter2
    //controls only the per-army book debit; formation gold and every creator,
    //registration, governor, grouping and route writer retain source order.
    private CommandResult CommitDeploymentUnitsCurrent(GameState state, DeploymentSource source,
        IReadOnlyList<DeploymentUnitRequest> units, CommandContext context, bool debitCommandBook)
    {
        try
        {
            if (state.PreparedDeployments.Count != 0) throw OCMissing("legacy_unexecuted_queue");
            var current = CaptureDeploymentPersonSelectionCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            //Initial Army targets read real saved native receivers, just as
            //existing Army commands do, before any new receiver is allocated.
            current.RestoreOwnedArmyReturnSlots(state, state.ReadArmyLifecycle()!);
            _moraleWaveInputs.BindCurrent(state, current); _baseCapacityInputs.BindCurrent(state, current);
            DeploymentTacticCurrentInputs.BindCurrent(state, current);
            //Only an actual Build submission reaches site/scenery inputs.
            //The same current serves command legality before allocation.
            if (units.Any(row => row.Actions.Any(action => action.ActionKey == "build")))
                BindFacilityConstructionInputs(state, current);
            var selection = BuildDeploymentPersonSelection(state, current, source.LocationId, 0);
            var candidates = selection.Candidates ?? throw OCMissing(selection.UnresolvedInput ?? "person_candidates");
            var targets = new Dictionary<int, ArmyCommandTarget>();
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            foreach (var row in units)
            {
                if (!candidates.Any(x => x.OfficerId == row.OfficerId && x.DisabledReasons == DeploymentPersonDisabledReason.None))
                    return CommandResult.Reject("Commander is not currently eligible at this Base.", "deployment.ineligible");
                int person = OCKnown(current.ReadPersonLookupReference(row.OfficerId)?.PhysicalSlot, "person_pointer");
                if (!OCKnown(current.ReadPersonNativeValidity(person), "person_validity"))
                    return CommandResult.Reject("Commander is not valid.", "deployment.ineligible");
                uint mask = OCKnown(current.ReadPersonFormationMask164(person), "formation_mask");
                if (row.FormationId is < 0 or > 20 || !DeploymentFormationEligibility.Allows(mask, row.FormationId, row.OfficerId))
                    return CommandResult.Reject("Commander cannot use the selected formation.", "deployment.formation");
                if (OCKnown(TacticRemakeCurrentFormations.ReadRawByte(state, row.FormationId, 0x7E), "land_kind") == 2)
                    return CommandResult.Reject("A naval formation cannot be selected as land formation.", "deployment.formation");
                if (row.NavalFormationId is not int naval) throw OCMissing("explicit_naval_formation");
                if (naval is < 0 or > 20 || OCKnown(TacticRemakeCurrentFormations.ReadRawByte(state, naval, 0x7E), "naval_kind") != 2)
                    throw OCMissing("naval_fallback_branch");
                var capacity = OfficerCommandCapacity.Resolve(state, current, person);
                if (!capacity.Available) throw OCMissing(capacity.UnresolvedInput ?? "command_capacity");
                if (row.Troops > capacity.Capacity!.Value)
                    return CommandResult.Reject("Troops exceed current commander capacity.", "deployment.command_capacity");
                if (row.TacticIds.Any(x => x != 0))
                {
                    int[] allowed = DeploymentTacticSelectionSource.ReadCandidates(current, row.OfficerId, person, row.FormationId,
                        () => abilities.Resolve(state, row.OfficerId) ?? throw OCMissing("current_abilities"));
                    if (row.TacticIds.Any(x => x != 0 && !allowed.Contains(x)))
                        return CommandResult.Reject("Selected tactics are not currently available.", "deployment.tactics");
                }
                var selectionError = OCValidateInitialFacilitySelection(state,current,person,row.ConstructionSelection,row.Actions is [{ActionKey:"build"}]);
                if (selectionError is not null) return selectionError;
                var targetError = OCReadDeploymentTarget(state, current, person, row.Actions, out var target);
                if (targetError is not null) return targetError;
                targets.Add(row.OfficerId, target ?? throw OCMissing("initial_target"));
            }
            var fireError = OCValidateInitialFireTargets(state, current, source.ForceId, units);
            if (fireError is not null) return fireError;
            var occupationError = OCValidateInitialOccupationTargets(state, current, source.ForceId, units, targets);
            if (occupationError is not null) return occupationError;
            var falseReportError = OCValidateInitialFalseReportTargets(state, current, source.ForceId, units);
            if (falseReportError is not null) return falseReportError;
            // Original Make availability quotes land only. The final native
            // Gold writer also charges naval, with its own source clamp.
            var quote = QueryDeploymentDraftResources(state, source.LocationId,
                units.Select(x => new DeploymentDraftRow(x, x.NavalFormationId!.Value)).ToArray());
            if (quote.Gold is not { } gold) throw OCMissing(quote.GoldUnresolvedInput ?? "land_gold_availability");
            if (gold.RemainingGold < 0) return CommandResult.Reject("Base lacks formation funds.", "deployment.insufficient_gold");

            var working = ArmyIdentityAllocation.Initialize(state);
            if (working.ArmyIdentities!.NextId + units.Count - 1 > int.MaxValue) throw OCMissing("identity_exhausted");
            foreach (var row in units)
            {
                working = CreateOrdinaryDeploymentArmyCurrent(working, current, source, row, targets[row.OfficerId], debitCommandBook);
            }
            return CommandResult.Accept(working);
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable("rules.ordinary_deployment_commit", error.Message); }
    }

    // The actual1EC980 creation/accounting tail shared by independently
    // admitted player and source-AI callers. Player dialog eligibility remains
    // above; this method never substitutes that eligibility for a source node.
    private GameState CreateOrdinaryDeploymentArmyCurrent(GameState working, SourceWorldContext current,
        DeploymentSource source, DeploymentUnitRequest row, ArmyCommandTarget target, bool debitCommandBook,
        ArmyCreationConfiguration? directConfiguration = null)
    {
        int person = OCKnown(current.ReadPersonLookupReference(row.OfficerId)?.PhysicalSlot, "person_pointer");
        working = MarkOrdinaryDepartureCommanderCurrent(working, current, person, row.OfficerId);
        var preference = directConfiguration is { } explicitPacket
            ? new DeploymentPreferenceEncoding(explicitPacket.Category44, explicitPacket.Flag48Input, explicitPacket.Flag4CInput,
                explicitPacket.Choice50Input ?? throw OCMissing("source_creation_choice"))
            : DeploymentDraftDefaults.Encode(row.Behavior ?? throw OCMissing("behavior"));
        var configuration = directConfiguration ?? new ArmyCreationConfiguration(row.NavalFormationId!.Value,
            target.Action, preference.EnemyApproach, preference.Retreat, preference.Pursuit)
        { Choice50Input = preference.AfterAction, RouteHold40Input = 0, TargetKind38 = target.Fields.Kind38 };
        // This compatibility descriptor is a transient input adapter to
        // the existing creator. It is never inserted in the saved queue.
        var plan = new PreparedDeployment(1, 1, source.ForceId, source.DistrictId, source.LocationId,
            row.OfficerId, row.FormationId, row.Troops, Enumerable.Range(0, 3).Select(i => i < row.TacticIds.Count ? row.TacticIds[i] : 0).ToArray(), row.Actions.ToArray(),
            row.WaypointHexIds.ToArray(), row.Behavior!, true, true);
        var army = new UnitState(working.ArmyIdentities!.Candidate, source.ForceId, row.OfficerId,
            source.HexId, target.GoalHex, Math.Clamp(row.Troops, 0, ushort.MaxValue), 0,
            new(OCKnown(current.ReadBaseMorale44(BaseLookup.Find(source.LocationId, current)), "source_morale"), null))
        { SourceLocationId = source.LocationId, Formations = new(row.FormationId, null, null),
            TacticIds = plan.TacticIds.ToArray(), Behavior = row.Behavior,
            TacticRuntime = UnitTacticRuntime.Empty(), Mission = row.Actions.Count == 0 ? null : target.Action is 9 or 10
                ? new(target.Action == 9 ? UnitMissionKind.RepairStructure : UnitMissionKind.DemolishStructure,
                    TargetStructureId: row.Actions[0].Target.Id)
                : target.Action == 14 ? new(UnitMissionKind.OccupyArea, TargetAreaId: ReadInitialOccupationAreaCurrent(current,row.Actions[0],target.GoalHex))
                : target.Action == 8 ? new(UnitMissionKind.BuildStructure,
                    StructureKindId: row.Actions[0].StructureKindId)
                : new(UnitMissionKind.Combat) };
        if (row.Actions.Count != 0) army = PlayableWaypointMarch.AttachAction(army, row.Actions[0], row.WaypointHexIds);
        GameState? afterTroopWrite = null;
        var receipt = CreateOrdinaryCore(working, plan, army, configuration, (before, fields) =>
        {
            if (fields.CurrentMorale is null || fields.CommanderPosition is null) throw OCMissing("creation_morale_or_position");
            var troopCurrent = CaptureWritableTransportRouteCurrent(before);
            _baseCapacityInputs.BindCurrent(before, troopCurrent);
            // The receiver is not admitted yet: resolve its actual
            // commander E2 instead of reading an unassigned Army header.
            ushort first = OCKnown(troopCurrent.ReadPersonFirstLocation(person), "commander_home");
            var home = BaseLookup.Find(first, troopCurrent);
            int stock = OCKnown(troopCurrent.ReadBaseResource(home, BaseResourceKind.Troops), "Base_troops");
            int after = OCBound(unchecked(stock - row.Troops), BasePostLoad.ReadResourceCapacities(home, troopCurrent).Troops);
            after = OCBound(after, BasePostLoad.ReadResourceCapacities(home, troopCurrent).Troops);
            var changed = OCWithTroops(before, home, after);
            var officer = changed.Officers[row.OfficerId];
            changed = changed with { Officers = new(changed.Officers) { [officer.Id] = officer with
            { LocationState = officer.LocationState is { } location ? location with { Position = fields.CommanderPosition }
                : new(fields.CommanderPosition, null) } } };
            afterTroopWrite = changed;
            return changed;
        }, normalTarget: target.Fields) ?? throw OCMissing("creator_or_registration");
        working = PlayableArmyCreation.PublishCreated(afterTroopWrite ?? throw OCMissing("troop_write"), army, receipt);
        if (row.Actions.Count != 0)
        {
            working = OCBindInitialFacilitySelection(working, army.Id, row);
            working = OCBindInitialFalseReportDestination(working, army.Id, row);
        }
        working = OCApplyGovernorTail(working, person);
        working = OCCompleteCreationGroups(working, army.Id);
        working = OCApplyFinalDebits(working, army.Id, person, debitCommandBook);
        var routeCurrent = CaptureWritableTransportRouteCurrent(working);
        var routed = OrdinaryArmyRouteSource.PrepareCreation(working, routeCurrent, working.Units[army.Id], configuration.SourceDeparture);
        var route = routed.Receipt ?? throw OCMissing(routed.UnresolvedOwner ?? "initial_route");
        var created = working.Units[army.Id];
        working = (working with { Units = new(working.Units) { [army.Id] = created with
        { March = created.March! with { NextStep = route.NextHexWrite is ushort next
            ? ArmyMarchSourceAdapter.DecodeDestination(next) : ArmyMarchDestination.None } } } })
            .WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = routeCurrent.ReadOwnedCurrentListPool()!.ExportOwnedState() });
        return working;
    }

    private static GameState MarkOrdinaryDepartureCommanderCurrent(GameState state, SourceWorldContext current,
        int person, int officerId)
    {
        ushort previous = OCKnown(current.ReadArmyReturnPersonFlags196(person), "departure_commander15E");
        ushort flags = (ushort)(previous | 1);
        if (flags == previous) return state;
        current.WriteArmyReturnPersonFlags196(person, flags);
        var officer = state.Officers[officerId];
        return state with { Officers = new(state.Officers) { [officer.Id] = officer with
            { Behavior = OfficerBehaviorSourceAdapter.WithSourceFlags(officer.Behavior, flags) } } };
    }

    private GameState OCApplyGovernorTail(GameState state, int person)
    {
        var current = CaptureDeploymentPersonSelectionCurrent(state);
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        ushort homeId = OCKnown(current.ReadPersonFirstLocation(person), "governor_home");
        var home = BaseLookup.Find(homeId, current);
        ushort id = OCKnown(current.ReadPersonIdentity(person), "governor_person_identity");
        if (OCKnown(current.ReadArmyReturnBaseGovernor2E(home), "Base_governor") != id) return state;
        if (!OCKnown(current.ReadBaseNativeValidity(home), "governor_Base_validity")) return state;
        var queries = new PersonReturnGovernorQueries(state, _governorInputs, current);
        var selected = ArmyReturnGovernorUpdater.PrepareCurrent(home, current, queries);
        var packet = selected.Packet ?? throw OCMissing(selected.UnresolvedOwner ?? "governor_selection");
        ArmyReturnGovernorUpdater.ApplyCurrentWrites(current, packet);
        return OwnedArmyLifecycleDetachedInputs.PublishCurrent(OwnedArmyReturnCurrentFields.PublishCurrent(state, current), current);
    }

    private GameState OCCompleteCreationGroups(GameState state, int armyId)
    {
        var current = CaptureWritableTransportRouteCurrent(state);
        var arena = state.ReadArmyLifecycle()!;
        var grouped = OwnedArmyLifecycleAuxiliaries.PrepareCreation(arena, state, state.Units[armyId], current,
            current.ReadOwnedCurrentListPool() ?? throw OCMissing("group_current_pool"));
        return state.WithArmyLifecycle(grouped ?? throw OCMissing("creation_groups"));
    }

    private GameState OCApplyFinalDebits(GameState state, int armyId, int person, bool debitCommandBook)
    {
        var current = CaptureWritableTransportRouteCurrent(state);
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        _baseCapacityInputs.BindCurrent(state, current);
        int district = OCKnown(current.ReadPersonDistrictSlot(person), "commander_District");
        var districtReference = current.ReadDistrictTableReference(district <= 51 ? district : 0) ?? throw OCMissing("District_pointer");
        if (debitCommandBook && OCKnown(current.ReadDistrictNativeValidity(districtReference), "District_validity"))
        {
            if (!state.Districts.TryGetValue(district, out var owner) || owner.OrdersRemaining is not int before) throw OCMissing("District_books");
            state = state with { Districts = new(state.Districts) { [district] = owner with { OrdersRemaining = Math.Clamp(unchecked(before - 1), 0, 200) } } };
        }
        var army = state.Units[armyId];
        var home = current.FindArmyUnitCurrentBase(new(army.NativeArmyOrigin!.NativeSlotIndex!.Value));
        var currency = home.Kind == BaseKind.City ? home : CurrentGateMapQueries.FindCity(home, current);
        int land = OCKnown(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, army), "actual_land");
        int naval = OCKnown(ArmyConfigurationSourceAdapter.ReadNaval(state, army), "actual_naval");
        int cost = 0;
        foreach (int formation in new[] { land, naval })
        {
            // Formation.v3 excludes physical0 in the installed21-row profile.
            if (formation == 0) continue;
            cost = unchecked(cost + OCKnown(TacticRemakeCurrentFormations.ReadRawWord(state, formation, 0x9A), "actual_formation_cost"));
        }
        int funds = OCKnown(current.ReadBaseResource(currency, BaseResourceKind.Gold), "Home_gold");
        int after = OCBound(unchecked(funds - cost), BasePostLoad.ReadResourceCapacities(currency, current).Gold);
        after = OCBound(after, BasePostLoad.ReadResourceCapacities(currency, current).Gold);
        int cityId = OCKnown(currency.PhysicalSlot, "currency_city");
        var city = state.Cities.GetValueOrDefault(cityId) ?? throw OCMissing("currency_city_owner");
        return state with { Cities = new(state.Cities) { [cityId] = city with { Gold = after } } };
    }

    private static GameState OCWithTroops(GameState state, BaseReference home, int troops)
    {
        int physical = OCKnown(home.PhysicalSlot, "troop_Base");
        if (home.Kind == BaseKind.City)
        {
            var city = state.Cities.GetValueOrDefault(physical) ?? throw OCMissing("troop_City");
            return state with { Cities = new(state.Cities) { [physical] = city with { Troops = troops } } };
        }
        var gate = state.Gates.GetValueOrDefault(physical + 51) ?? throw OCMissing("troop_Gate");
        return state with { Gates = new(state.Gates) { [gate.Id] = gate with { Troops = troops } } };
    }
    private static int OCBound(int value, int cap) => value < 0 ? 0 : Math.Min(value, cap);
    private static T OCKnown<T>(T? value, string field) where T : struct => value ?? throw OCMissing(field);
    private static InvalidDataException OCMissing(string field) => new("ordinary.deployment." + field);
}
