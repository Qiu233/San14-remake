using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The third lesson's actual short packet and its two consecutive
    /// path transfers. This is a constructor call, not a player command or an
    /// elapsed simulation update.</summary>
    internal GameState SpawnThirdTutorialEnemyCurrent(GameState state)
    {
        RequireSelectedTutorialControl(state);
        if (state.TutorialLessonProgress?.CourseIndex != 2) throw TutorialArmyMissing("third_lesson_owner");
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        int person = TutorialArmyKnown((current.ReadTransferPersonTableReference(149)
            ?? throw TutorialArmyMissing("third_Person_pointer")).PhysicalSlot, "third_Person_physical");
        int identity = TutorialArmyKnown(current.ReadPersonIdentity(person), "third_Person_identity");
        int target = TutorialAreaHex(current, 44);
        var created = CreateTutorialArmyPacketCurrent(state, identity, 7000, new(9, (ushort)target),
            new("stay", new(DeploymentTargetKind.Hex, target)), 13);
        if (created.UnitId is not int id) return created.State;
        current = CaptureOwnedArmyReturnWorld(created.State).Context;
        return MoveTutorialArmyCurrent(created.State, id, [TutorialAreaHex(current, 51), target]);
    }

    /// <summary>The common tutorial enemy wrapper. The target is independent
    /// from the commander's current Home, which supplies the actual departure.</summary>
    internal GameState CreateTutorialArmyCurrent(GameState state, int personId, int troops,
        int relocationHex, int relocationDirection, BaseReference? cityTarget = null, int? armyTargetId = null)
    {
        RequireSelectedTutorialControl(state);
        if ((cityTarget is null) == !armyTargetId.HasValue || relocationHex < 0 || relocationDirection is < 0 or > 6)
            throw TutorialArmyMissing("target_or_direction");
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        ArmyTargetSourceFields target;
        DeploymentAction action;
        byte nativeAction;
        if (cityTarget is not null)
        {
            if (cityTarget.Kind != BaseKind.City) throw TutorialArmyMissing("target_City_kind");
            int physical = TutorialArmyKnown(cityTarget.PhysicalSlot, "target_City_physical");
            target = new(5, checked((ushort)physical));
            action = new("attack", new(DeploymentTargetKind.Location,
                TutorialArmyKnown(current.ReadBaseObjectLink(cityTarget), "target_City_location")));
            nativeAction = 4;
        }
        else
        {
            var army = state.Units.GetValueOrDefault(armyTargetId!.Value) ?? throw TutorialArmyMissing("target_Army_owner");
            int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw TutorialArmyMissing("target_Army_physical");
            target = new(27, checked((ushort)physical));
            action = new("attack", new(DeploymentTargetKind.Unit, army.Id));
            nativeAction = 7;
        }
        var result = CreateTutorialArmyPacketCurrent(state, personId, troops, target, action, nativeAction);
        if (relocationHex >= 48400 || result.UnitId is not int id) return result.State;
        int destination = relocationHex;
        if (relocationDirection != 0)
        {
            int adjacent = PersonReturnPathSource.Neighbor(relocationHex, relocationDirection);
            if (adjacent < 48400) destination = adjacent;
        }
        return MoveTutorialArmyCurrent(result.State, id, [destination]);
    }

    private (GameState State, int? UnitId) CreateTutorialArmyPacketCurrent(GameState state,
        int personId, int troops, ArmyTargetSourceFields target, DeploymentAction action, byte nativeAction,
        int? sourceLandFormation = null, int sourceEnemyApproach = 2, int sourceAfterAction = 2)
    {
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        DeploymentTacticCurrentInputs.BindCurrent(state, current);
        int person = TutorialArmyKnown(current.ResolveOfficerSource(personId), "commander_physical");
        if (!TutorialArmyKnown(current.ReadPersonNativeValidity(person), "commander_validity")) return (state, null);
        var officer = state.Officers.GetValueOrDefault(personId) ?? throw TutorialArmyMissing("commander_owner");
        //The shared wrapper writes bit0 before even testing allocation.
        ushort flags = (ushort)(TutorialArmyKnown(current.ReadArmyReturnPersonFlags196(person), "commander_flags") | 1);
        current.WriteArmyReturnPersonFlags196(person, flags);
        state = state with { Officers = new(state.Officers) { [personId] = officer with
            { Behavior = OfficerBehaviorSourceAdapter.WithSourceFlags(officer.Behavior, flags) } } };
        var existing = PersonArmyLocationSource.ReadCommanderArmy(current, person) ?? throw TutorialArmyMissing("commander_Army_pointer");
        if (TutorialArmyKnown(current.ReadArmyUnitNativeValidity(existing), "commander_Army_validity"))
        {
            int physical = TutorialArmyKnown(existing.PhysicalSlot, "existing_Army_physical");
            int id = state.ReadArmyLifecycle()?.ReceiverSlots()[physical].ActiveUnitId
                ?? throw TutorialArmyMissing("existing_Army_owner");
            return (CompleteSpecialForceRaidCreationCurrent(state, current, id, person), id);
        }
        var associated = current.ReadPersonLinkedEntity(person) ?? throw TutorialArmyMissing("commander_associated_pointer");
        if (TutorialArmyKnown(current.ReadLinkedEntityNativeValidity(associated), "commander_associated_validity") ||
            OwnedArmyLifecycleCurrent.FirstCreationSlot(state) is not > 0) return (state, null);

        int formation = sourceLandFormation ?? 0;
        if (sourceLandFormation is null)
        {
            uint mask = TutorialArmyKnown(current.ReadPersonFormationMask164(person), "commander_formations");
            for (int candidate = 0; candidate <= 20; candidate++)
                if (DeploymentFormationEligibility.Allows(mask, candidate, personId)) { formation = candidate; break; }
        }
        //The source searches resolved slots by tactic identity, including0.
        var tactics = new int[10];
        for (int slot = 0; slot < tactics.Length; slot++)
            tactics[slot] = TutorialArmyKnown(DeploymentTacticSelectionSource.ReadCurrentSlot(current, personId, person, slot).PhysicalSlot,
                "commander_tactic_identity");
        int tactic = tactics.Min();
        int homeId = TutorialArmyKnown(current.ReadPersonFirstLocation(person), "commander_Home");
        var home = BaseLookup.Find(homeId, current);
        int force = TutorialArmyKnown((current.ReadPersonForce(person) ?? throw TutorialArmyMissing("commander_Force")).PhysicalSlot,
            "commander_Force_physical");
        int district = TutorialArmyKnown(current.ReadPersonDistrictSlot(person), "commander_District");
        int sourceHex = ArmyConstruction.ResolveCurrentHex2A(current, personId) ?? throw TutorialArmyMissing("source_Hex");
        int goal = target.Kind38 == 5 ? current.ReadBaseCurrentHexIndex(new(BaseKind.City, target.Identity3A))
            : target.Kind38 == 27 ? TutorialArmyKnown(current.ReadArmyUnitRawHex2A(new(target.Identity3A)), "target_Army_Hex")
            : TutorialArmyKnown(target.Identity3A, "target_Hex");
        var behavior = DeploymentDraftDefaults.Decode(sourceEnemyApproach, 0, 0, sourceAfterAction) ?? throw TutorialArmyMissing("packet_behavior");
        state = ArmyIdentityAllocation.Initialize(state);
        int unitId = state.ArmyIdentities!.Candidate;
        var packet = new PreparedDeployment(1, 1, force, district, homeId, personId, formation,
            troops, [tactic, 0, 0], [action], [], behavior, true, true);
        var army = new UnitState(unitId, force, personId, sourceHex, goal, Math.Clamp(troops, 0, ushort.MaxValue),
            0, new(0, null))
        {
            SourceLocationId = homeId, Formations = new(formation, null, null), TacticIds = [tactic, 0, 0],
            Behavior = behavior, TacticRuntime = UnitTacticRuntime.Empty(), Mission = new(UnitMissionKind.Combat)
        };
        army = PlayableWaypointMarch.AttachAction(army, action);
        var configuration = new ArmyCreationConfiguration(11, nativeAction, sourceEnemyApproach, 0, 0)
            { Choice50Input = sourceAfterAction, RouteHold40Input = 0, TargetKind38 = target.Kind38 };
        GameState? afterTroops = null;
        var receipt = CreateOrdinaryCore(state, packet, army, configuration, (before, fields) =>
        {
            if (fields.CurrentMorale is null || fields.CommanderPosition is null) throw TutorialArmyMissing("creation_morale_position");
            var inputs = CaptureOwnedArmyReturnWorld(before).Context;
            int stock = TutorialArmyKnown(inputs.ReadBaseResource(home, BaseResourceKind.Troops), "source_troops");
            int amount = OCBound(unchecked(stock - troops), BasePostLoad.ReadResourceCapacities(home, inputs).Troops);
            amount = OCBound(amount, BasePostLoad.ReadResourceCapacities(home, inputs).Troops);
            var changed = OCWithTroops(before, home, amount);
            var commander = changed.Officers[personId];
            afterTroops = changed with { Officers = new(changed.Officers) { [personId] = commander with
                { LocationState = commander.LocationState is { } location ? location with { Position = fields.CommanderPosition }
                    : new(fields.CommanderPosition, null) } } };
            return afterTroops;
        }, normalTarget: target) ?? throw TutorialArmyMissing("generic_creator_registration");
        state = PlayableArmyCreation.PublishCreated(afterTroops ?? throw TutorialArmyMissing("troop_writer"), army, receipt);
        state = OCApplyGovernorTail(state, person);
        state = OCCompleteCreationGroups(state, unitId);
        current = CaptureOwnedArmyReturnWorld(state).Context;
        state = CompleteSpecialForceRaidCreationCurrent(state, current, unitId, person);
        return (state, unitId);
    }

    /// <summary>The tutorial's instant relocation searches each authored leg
    /// and transfers its cells; it does not add marching time or movement credit.</summary>
    internal GameState MoveTutorialArmyCurrent(GameState state, int unitId, IReadOnlyList<int> pathHexes)
    {
        RequireSelectedTutorialControl(state);
        if (pathHexes.Count == 0 || pathHexes.Any(hex => hex is < 0 or >= 48400)) throw TutorialArmyMissing("path_endpoints");
        var operation = CaptureOwnedArmyReturnWorld(state);
        var current = operation.Context;
        var army = state.Units.GetValueOrDefault(unitId) ?? throw TutorialArmyMissing("moving_Army_owner");
        var departed = OrdinaryArmyAppearanceSource.Depart(state, current, army, borrowedCurrent: true, sourceRules: this);
        state = departed.State;
        army = state.Units[unitId];
        int from = army.HexId;
        var temporary = current.CreateTemporaryHexList() ?? throw TutorialArmyMissing("path_descriptor");
        try
        {
            foreach (int destination in pathHexes)
            {
                var target = OrdinaryArmyRouteSource.ReadEffectiveSearchTarget(state, army) ?? throw TutorialArmyMissing("path_target");
                int[] path = ArmyRouteSearch.Search(state, current, army, from, destination,
                    target.Kind, target.Identity, null, out _);
                int city = TutorialArmyKnown(CurrentGateMapQueries.FindCity(current.FindArmyUnitCurrentBase(
                    new(army.NativeArmyOrigin!.NativeSlotIndex)), current).PhysicalSlot, "occupation_City");
                state = new MonthlyCampaignMaintenanceSource(this, state, current, operation.Pool)
                    .ApplyTutorialTerritory(path, army.ForceId, city, showAppearance: true);
                from = destination;
            }
        }
        finally { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
        state = state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = operation.Pool.ExportOwnedState() });
        army = state.Units[unitId];
        int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw TutorialArmyMissing("moving_Army_physical");
        //282940 unconditionally clears the old cell, unlike an ordinary
        //adjacent traversal's conditional occupant test.2EE070 writes only the
        //last authored destination; path interiors never acquire occupancy.
        current.SetHexRaw1C(army.HexId, 0);
        int final = pathHexes[^1];
        current.SetArmyUnitRawHex2A(physical, (ushort)final);
        current.SetHexRaw1C(final, (ushort)physical);
        state = state with { Units = new(state.Units) { [unitId] = PlayableUnitPosition.AtHex(army, final) },
            ArmyMapOccupancy = current.ExportArmyMapOccupancy() };
        int owningCity = TutorialArmyKnown(CurrentGateMapQueries.FindCity(current.FindArmyUnitCurrentBase(new(physical)), current).PhysicalSlot,
            "final_occupation_City");
        return new MonthlyCampaignMaintenanceSource(this, state, current, operation.Pool)
            .ApplyTutorialTerritory([final], army.ForceId, owningCity, showAppearance: true);
    }

    internal GameState ApplyTutorialTerritoryCurrent(GameState state, IReadOnlyList<int> hexes,
        int forceId, int cityId, bool showAppearance = false)
    {
        RequireSelectedTutorialControl(state);
        var world = CaptureOwnedArmyReturnWorld(state);
        return new MonthlyCampaignMaintenanceSource(this, state, world.Context, world.Pool)
            .ApplyTutorialTerritory(hexes, forceId, cityId, showAppearance);
    }

    internal sealed partial class MonthlyCampaignMaintenanceSource
    {
        internal GameState ApplyTutorialTerritory(IReadOnlyList<int> hexes, int force, int city, bool showAppearance)
        {
            foreach (int identity in hexes)
            {
                if ((uint)identity >= 48400) throw TutorialArmyMissing("territory_Hex");
                var hex = current.ReadHexTableReference(identity) ?? throw TutorialArmyMissing("territory_Hex_pointer");
                RestoreMonthlyOccupation(hex, force, city, showAppearance: showAppearance);
            }
            return state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = pool.ExportOwnedState() });
        }

        //Every actual active-tutorial185D00 caller reaches this same tail,
        //including ordinary period occupation. Count once after a successful
        //wrapper, independently from its optional appearance effect.
        private static GameState IncrementTutorialTerritoryTransfer(GameState reached)
        {
            var lesson = reached.TutorialLessonProgress ?? throw TutorialArmyMissing("territory_progress");
            int count = lesson.TerritoryTransferCount ?? throw TutorialArmyMissing("territory_transfer_count");
            return reached with { TutorialLessonProgress = lesson with { TerritoryTransferCount = unchecked(count + 1) } };
        }
    }

    private static int TutorialAreaHex(SourceWorldContext current, int physical)
    {
        var area = current.ReadAreaTableReference(physical) ?? throw TutorialArmyMissing("Area_pointer");
        return TutorialArmyKnown(current.ReadAreaHexIndex(area), "Area_Hex");
    }
    private static T TutorialArmyKnown<T>(T? value, string name) where T : struct => value ?? throw TutorialArmyMissing(name);
    private static InvalidDataException TutorialArmyMissing(string name) => new("tutorial.army_setup." + name);
}
