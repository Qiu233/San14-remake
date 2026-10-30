using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private (GameState State, bool Created) CreateStrategicIncitementArmyCurrent(GameState state, SourceWorldContext current,
        int physical, int person, int spawn, int target, int bandForce, int level = 1)
    {
        var arena = state.ReadArmyLifecycle() ?? throw StrategyMissing("incitement_creation_lifecycle");
        var freeArena = arena;
        var before = arena.ReceiverSlots()[physical];
        if (before.GroupAssignmentReady != true)
            throw StrategyMissing("incitement_creation_receiver_reset");
        var pool = current.ReadOwnedCurrentListPool() ?? throw StrategyMissing("incitement_creation_pool");
        state = ArmyIdentityAllocation.Initialize(state);
        int id = state.ArmyIdentities!.Candidate;
        int commander = StrategyNeed(current.ReadPersonIdentity(person), "incitement_creation_Person10");
        var random = new SharedSourceRandom32((state.CampaignAppRandom32 ?? throw StrategyMissing("incitement_creation_App32")).State);
        //2D3410: strategic incitement passes one; monthly incursions pass the
        //actual security tier. Both consume the same App stream in source order.
        int troopUnits = Math.Clamp(unchecked(random.Next(unchecked(level + 10)) + 2), 2, 12);
        int troops = Math.Clamp(unchecked(troopUnits * StrategicParameter(state, "incitement.troop-unit", 250)), 0, ushort.MaxValue);
        int naval = 0;
        for (int formation = 0; formation <= 20; formation++)
            if (StrategyNeed(TacticRemakeCurrentFormations.ReadRawByte(state, formation, 0x7E), "incitement_naval_kind") == 2)
            { naval = formation; break; }
        int land = random.Next(3) + 1;
        if (StrategyNeed(TacticRemakeCurrentFormations.ReadRawByte(state, land, 0x7E), "incitement_land_kind") == 2)
            land = 0; //241650 leaves the reset land field unchanged.
        var force = current.ReadForceTableReference(bandForce < 52 ? bandForce : 0)
            ?? throw StrategyMissing("incitement_creation_Force");
        int morale = Math.Min(100, BasePostLoad.ReadMoraleUpper(force, current));
        var targetHex = new HexReference(target);
        var obj = FacilityConstructionLegality.ObjectAt(current, targetHex);
        bool validObject = StrategyNeed(current.ReadObjectNativeValidity(obj), "incitement_creation_target_validity");
        int objectPhysical = StrategyNeed(obj.PhysicalSlot, "incitement_creation_target_physical");
        byte action; byte targetKind; ushort targetIdentity;
        DeploymentAction? planAction;
        UnitMission mission;
        if (!validObject)
        {
            action = 14; targetKind = 9; targetIdentity = (ushort)target;
            int area = StrategyNeed(current.ReadHexAreaSlot(targetHex), "incitement_creation_target_Area");
            planAction = new("occupy", new(DeploymentTargetKind.Area, area));
            mission = new(UnitMissionKind.OccupyArea, TargetAreaId: area);
        }
        else if (objectPhysical is >= 1 and <= 51)
        {
            action = 4; targetKind = 5; targetIdentity = (ushort)objectPhysical;
            planAction = new("attack", new(DeploymentTargetKind.Location, objectPhysical));
            mission = new(UnitMissionKind.Combat);
        }
        else if (objectPhysical is >= 52 and <= 61)
        {
            action = 5; targetKind = 6; targetIdentity = (ushort)(objectPhysical - 51);
            planAction = new("attack", new(DeploymentTargetKind.Location, objectPhysical));
            mission = new(UnitMissionKind.Combat);
        }
        else
        {
            action = 6; targetKind = 11; targetIdentity = (ushort)objectPhysical;
            // The source target remains an Object receiver. The legacy plan
            // has no attack-Object action; do not invent a Structure identity.
            planAction = null; mission = new(UnitMissionKind.Combat);
        }
        var clock = state.NativeClock ?? throw StrategyMissing("incitement_creation_clock");
        var unit = new UnitState(id, bandForce, commander, spawn, target, troops, 0, new(morale, morale))
        {
            SourceLocationId = StrategyNeed(current.ReadPersonFirstLocation(person), "incitement_creation_PersonE2"),
            NativeArmyOrigin = new("san14:ce-special-army-creation", Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language))
                { NativeSlotIndex = physical },
            NativeTacticRuntime = new(1, 0x0C, 0, 0, 0, 0, 0, 0, new sbyte[6], new byte[6], null, clock.UpdateOrdinal),
            WoundedTroops = 0, TacticRuntime = UnitTacticRuntime.Empty(), TacticIds = [0, 0, 0],
            Formations = ArmyConfigurationSourceAdapter.DecodeFormations(land, (byte)land, (byte)naval),
            FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(action),
            TraitRoster = ArmyTraitSourceAdapter.Decode(new ushort[36]),
            CarriedStores = new(StrategyNeed(before.StoredResources.Gold, "incitement_receiver_gold"),
                StrategyNeed(before.StoredResources.Food, "incitement_receiver_food")),
            OverrideTarget = before.OverrideTarget,
            NativeDirection36 = before.Direction36, NativeMapUiByte13C = before.MapUiByte13C,
            NativeCommandControl140 = before.CommandControl140,
            NativeRouteHold56 = before.RouteHold56, NativeRouteGoal1F8 = before.RouteGoal1F8, AiActionFlags = before.AiActionFlags,
            NativeHomeSequence28 = 501, NativeSavedRegionHex152 = 48400,
            NativeCommandChoice41 = 2, PositionKnowledge = ArmyPositionKnowledge.Confirmed,
            March = new(0, ArmyMarchDestination.None, ArmyMarchDestination.None)
                { FollowingWaypoints = new([]) },
            Mission = mission, Behavior = DeploymentDraftDefaults.Decode(1, 1, 1, 2)
        };
        if (planAction is not null) unit = PlayableWaypointMarch.AttachAction(unit, planAction);
        state = state with { CampaignAppRandom32 = new(random.ReadCurrentState()),
            ResourceState = ArmyConfigurationSourceAdapter.Write(ArmyTraitSourceAdapter.Write(state.ResourceState, id, new ushort[36]),
                id, (byte)land, (byte)naval, action, 1) };
        pool.BindArmyIdentity(physical, id);
        var traits = pool.TraitHandles(physical);
        current.SetArmyUnitCommanderId(physical, (ushort)commander);
        current.SetArmyUnitStatusFlag10(physical, 1);
        current.SetArmyUnitRawHex2A(physical, (ushort)spawn);
        current.BindArmyReturnResourceFields(physical, new((ushort)troops, 0, (byte)morale,
            before.StoredResources.Gold, before.StoredResources.Food));
        current.BindArmyReturnTraitFields(physical, new(new ushort[36],
            before.AuxiliaryAC ?? throw StrategyMissing("incitement_receiver_AC"),
            before.AuxiliaryF4 ?? throw StrategyMissing("incitement_receiver_F4"), traits));
        //241F20 changes only E4, preserving E2. The generated Person may have
        //retained a nonzero Home on its previously invalid receiver.
        ushort secondLocation = PersonArmyLocationSource.EncodeArmySecondLocation(physical);
        current.WritePersonSecondLocation(person, secondLocation);
        var officer = state.Officers[commander];
        var position = OfficerLocationSourceAdapter.DecodePosition(secondLocation, new(arena));
        officer = officer with { Deployed = true, LocationState = officer.LocationState is { } location
            ? location with { Position = position } : new(position, null) };
        state = state with { Units = new(state.Units) { [id] = unit }, Officers = new(state.Officers) { [commander] = officer } };
        var slots = arena.ReceiverSlots().ToArray();
        slots[physical] = before with { ActiveUnitId = id, Commander12 = (ushort)commander, Status10 = 1,
            Priority14 = 0, PendingImage = null, GroupAssignmentReady = false };
        arena = arena.WithReceiverSlots(slots);
        current.SetArmyLifecycleTargetFields(physical, new(targetKind, targetIdentity));
        state = OwnedArmyLifecycleTargets.PublishCurrent(state.WithArmyLifecycle(arena), current);
        //31F430's native return includes a known allocator failure. Preserve
        //that branch rather than declaring registration from mere Unit presence.
        if (!ArmyMovementManagerLifecycle.Register(new(RegistryKind.Army, physical), current))
            throw StrategyMissing("incitement_creation_registration_rejected");
        current.AssignArmyOccupancyReceiver(physical, id);
        var activated = ActivateStrategicIncitementArmyCurrent(state, current, id, physical, person, bandForce);
        if (activated is null)
        {
            //25C4F0 can genuinely fail when the generated receiver retained a
            //Home. Its caller then resets Army and Person and unlinks manager60.
            current.SetArmyUnitCommanderId(physical, 0); current.SetArmyUnitStatusFlag10(physical, 0);
            current.SetArmyUnitRawHex2A(physical, 48400);
            current.SetArmyLifecycleTargetFields(physical, new(255, 0));
            current.BindArmyReturnResourceFields(physical, new(0, 0, 0, 0, 0));
            state = ResetStrategicIncitementPersonCurrent(state, current, person);
            current.ReleaseArmyOccupancyReceiver(physical);
            ArmyMovementManagerLifecycle.Remove(new(RegistryKind.Army, physical), current);
            pool.RemoveArmyIdentity(physical);
            var units = new Dictionary<int, UnitState>(state.Units); units.Remove(id);
            state = OwnedArmyLifecycleTargets.PublishCurrent((state with { Units = units,
                ResourceState = CampaignSourceKnowledge.From(state.ResourceState)
                    .WithoutArmyConfiguration(id).WithoutArmyTraits(id).WithoutArmyPosition(id),
                ArmyMapOccupancy = current.ExportArmyMapOccupancy() })
                .WithArmyLifecycle(freeArena with { Indexes = pool.ProjectOwnedRoles() }), current);
            return (state, false);
        }
        state = ArmyIdentityAllocation.Commit(activated, id);
        arena = state.ReadArmyLifecycle()!;
        var groups = arena.ArmyGroups ?? throw StrategyMissing("incitement_creation_groups");
        int[] roster = ArmyGroupSourceAdapter.Roster(arena, current);
        var excluded = ArmyGroupSourceAdapter.Excluded(state, arena, current, roster);
        var grouped = ArmyGroupRules.AdmitCreation(groups, id, roster, excluded,
            member => ArmyGroupSourceAdapter.Commander(arena, current, member), null,
            Enumerable.Range(1, 500), ArmyGroupLeaderPolicy.AssignCommander)
            ?? throw StrategyMissing("incitement_creation_group_admission");
        int group = grouped.UnitAssignments[id].GroupId ?? throw StrategyMissing("incitement_creation_group");
        current.SetStartupArmyTroopsSlot58(physical, (ushort)group);
        return (state.WithArmyLifecycle(arena with { ArmyGroups = grouped, Indexes = pool.ProjectOwnedRoles() }), true);
    }
}
