using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2B7500 supplies a real generic2D2CA0 packet, not a player deployment
    //order. Reuse the generic numeric constructor on the borrowed current;
    //registration and the wrapper's side effects retain their source order.
    private GameState CreateSpecialForceRaidArmyCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int person, BaseReference source, BaseReference target,
        int troops, int landFormation, int navalFormation, int tactic)
        => CreateSpecialForceRaidArmyCurrent(state, current, pool, person, source,
            new SpecialForceBaseTarget(target), troops, landFormation, navalFormation, tactic);

    private GameState CreateSpecialForceRaidArmyCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int person, BaseReference source, SpecialForceCreationTarget target,
        int troops, int landFormation, int navalFormation, int tactic)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw RaidCreationMissing("same_current_pool");
        if (!RaidCreationKnown(current.ReadPersonNativeValidity(person), "commander_validity")) return state;
        int commander = RaidCreationKnown(current.ReadPersonIdentity(person), "commander10");
        var officer = state.Officers.GetValueOrDefault(commander) ?? throw RaidCreationMissing("commander_owner");
        ushort flags = (ushort)(RaidCreationKnown(current.ReadArmyReturnPersonFlags196(person), "commander15E") | 1);
        current.WriteArmyReturnPersonFlags196(person, flags);
        state = state with { Officers = new(state.Officers) { [commander] = officer with
            { Behavior = OfficerBehaviorSourceAdapter.WithSourceFlags(officer.Behavior, flags) } } };
        //The wrapper's bit0 write survives every subsequent allocation failure.
        var existing = PersonArmyLocationSource.ReadCommanderArmy(current, person) ?? throw RaidCreationMissing("commander_Army");
        if (RaidCreationKnown(current.ReadArmyUnitNativeValidity(existing), "commander_Army_validity")) return state;
        var linked = current.ReadPersonLinkedEntity(person) ?? throw RaidCreationMissing("commander_associated");
        if (RaidCreationKnown(current.ReadLinkedEntityNativeValidity(linked), "commander_associated_validity")) return state;
        var arena = state.ReadArmyLifecycle() ?? throw RaidCreationMissing("Army_lifecycle");
        int physical = OwnedArmyLifecycleCurrent.FirstCreationSlot(state) ?? 0;
        if (physical == 0) return state;
        var before = arena.ReceiverSlots()[physical];
        if (before.GroupAssignmentReady != true) throw RaidCreationMissing("free_receiver_group_reset");
        int homeId = RaidCreationKnown(current.ReadPersonFirstLocation(person), "commanderE2");
        var home = BaseLookup.Find(homeId, current); //2D2CA0 repeats the lookup.
        if (home != source) throw RaidCreationMissing("selected_commander_Home_changed");
        var force = current.ReadPersonForce(person) ?? throw RaidCreationMissing("commander_Force");
        int forceId = RaidCreationKnown(current.ReadForcePhysicalSlot(force), "commander_Force_ordinal");
        int district = RaidCreationKnown(current.ReadPersonDistrictSlot(person), "commanderE0");
        int sourceHex = ArmyConstruction.ResolveCurrentHex2A(current, commander) ?? throw RaidCreationMissing("Home_Hex");
        //Resolve only here, after the original commander/receiver gates.
        //The old Base wrapper does not pre-read an otherwise skipped target.
        var objective = ResolveSpecialForceCreationTarget(state, current, target);
        byte action = objective.Action;
        var goal = objective.Target;
        int targetHex = objective.Hex;
        if (targetHex >= 48400) throw RaidCreationMissing("target_Base_Hex");
        var behavior = DeploymentDraftDefaults.Decode(2, 0, 1, 1)
            ?? throw RaidCreationMissing("source_deployment_behavior");
        var attack = objective.Command;
        state = ArmyIdentityAllocation.Initialize(state);
        int id = state.ArmyIdentities!.Candidate;
        //This transient descriptor adapts the already produced packet to the
        //shared pure constructor. It is never queued as a player command.
        var packet = new PreparedDeployment(1, 1, forceId, district, homeId, commander,
            landFormation, troops, [tactic, 0, 0], [attack], [], behavior, true, true);
        var requested = new UnitState(id, forceId, commander, sourceHex, targetHex, troops, 0, new(0, null))
        {
            SourceLocationId = homeId, Formations = new(landFormation, null, null),
            TacticIds = [tactic, 0, 0], Behavior = behavior, TacticRuntime = UnitTacticRuntime.Empty(),
            Mission = new(UnitMissionKind.Combat)
        };
        requested = PlayableWaypointMarch.AttachAction(requested, attack);
        var configuration = new ArmyCreationConfiguration(navalFormation, action, 2, 0, 1)
            { Choice50Input = 1, RouteHold40Input = 0, TargetKind38 = goal.Kind38 };
        var receipt = _creation.Create(state, packet, requested, configuration,
            formation => TacticRemakeCurrentFormations.ReadRawByte(state, formation, 0x7E))
            ?? throw RaidCreationMissing("generic_packet_current_numeric_inputs");
        if (receipt.Origin.NativeSlotIndex != physical) throw RaidCreationMissing("generic_receiver_identity");
        ushort sequence = ArmyHomeSequenceSource.ReadNextOrdinary(state, current, commander)
            ?? throw RaidCreationMissing("Home_sequence28");
        int morale = RaidCreationKnown(current.ReadBaseNativeValidity(home), "Home_validity")
            ? ArmyMoraleSource.SetByte2419A0(RaidCreationKnown(current.ReadBaseMorale44(home), "Home44"),
                BasePostLoad.ReadMoraleUpper(force, current)) : 0;
        int afterTroops = Math.Clamp(unchecked(RaidCreationKnown(current.ReadBaseResource(home, BaseResourceKind.Troops), "Home3C") - troops),
            0, BasePostLoad.ReadResourceCapacities(home, current).Troops);
        afterTroops = Math.Clamp(afterTroops, 0, BasePostLoad.ReadResourceCapacities(home, current).Troops);
        current.SetBaseResource(home, BaseResourceKind.Troops, afterTroops);
        state = OCWithTroops(state, home, afterTroops);
        var unit = requested with
        {
            NativeArmyOrigin = receipt.Origin, NativeTacticRuntime = receipt.Runtime,
            Formations = receipt.Formations, FieldAttackPolicy = receipt.FieldAttackPolicy, TraitRoster = receipt.TraitRoster,
            MoraleState = new(morale, morale), WoundedTroops = receipt.WoundedTroops,
            CarriedStores = receipt.CarriedStores, OverrideTarget = receipt.OverrideTarget,
            NativeDirection36 = before.Direction36, NativeMapUiByte13C = before.MapUiByte13C,
            NativeCommandControl140 = before.CommandControl140,
            NativeRouteHold56 = 0, NativeRouteGoal1F8 = before.RouteGoal1F8, AiActionFlags = before.AiActionFlags,
            NativeCommandChoice41 = 1, NativeHomeSequence28 = sequence, NativeSavedRegionHex152 = (ushort)sourceHex,
            PositionKnowledge = ArmyPositionKnowledge.Confirmed,
            March = new(0, ArmyMarchDestination.None, ArmyMarchDestination.None) { FollowingWaypoints = new([]) }
        };
        state = state with { ResourceState = receipt.ResourceState };
        pool.BindArmyIdentity(physical, id);
        var handles = pool.TraitHandles(physical);
        current.SetArmyUnitCommanderId(physical, (ushort)commander);
        current.SetArmyUnitStatusFlag10(physical, 1);
        current.SetArmyUnitRawHex2A(physical, (ushort)sourceHex);
        current.BindArmyReturnResourceFields(physical, new((ushort)troops, 0, (byte)morale,
            before.StoredResources.Gold, before.StoredResources.Food));
        current.BindArmyReturnTraitFields(physical, new(new ushort[36],
            before.AuxiliaryAC ?? throw RaidCreationMissing("receiver_AC"),
            before.AuxiliaryF4 ?? throw RaidCreationMissing("receiver_F4"), handles));
        current.SetArmyLifecycleTargetFields(physical, goal);
        ushort second = PersonArmyLocationSource.EncodeArmySecondLocation(physical);
        current.WritePersonSecondLocation(person, second);
        officer = state.Officers[commander];
        var position = OfficerLocationSourceAdapter.DecodePosition(second, new(arena))
            ?? throw RaidCreationMissing("commander_position_projection");
        var residue = CampaignSourceKnowledge.From(state.ResourceState);
        var oldResidue = residue.Find(commander);
        state = state with
        {
            Units = new(state.Units) { [id] = unit },
            Officers = new(state.Officers) { [commander] = officer with { Deployed = true,
                LocationState = officer.LocationState is { } location ? location with { Position = position } : new(position, null) } },
            ResourceState = residue.With(new(commander, position.Kind == OfficerPositionKind.Unresolved ? second : null,
                oldResidue?.DistrictCode))
        };
        var slots = arena.ReceiverSlots().ToArray();
        slots[physical] = before with { ActiveUnitId = id, PendingImage = null, Commander12 = (ushort)commander,
            Status10 = 1, Priority14 = 0, GroupAssignmentReady = false };
        state = OwnedArmyLifecycleTargets.PublishCurrent(state.WithArmyLifecycle(arena.WithReceiverSlots(slots)), current);
        if (!ArmyMovementManagerLifecycle.Register(new(RegistryKind.Army, physical), current))
        {
            //2269E0 does not restore the troop debit, PersonE4 or bit0.
            current.ResetArmyUnitPositionFields(physical);
            current.SetArmyUnitRawHex2A(physical, 48400);
            current.SetArmyLifecycleTargetFields(physical, new(255, 0));
            current.BindArmyReturnResourceFields(physical, new(0, 0, 0, 0, 0));
            pool.RemoveArmyIdentity(physical);
            var units = new Dictionary<int, UnitState>(state.Units); units.Remove(id);
            state = state with { Units = units, Officers = new(state.Officers)
                { [commander] = state.Officers[commander] with { Deployed = false } },
                ResourceState = CampaignSourceKnowledge.From(state.ResourceState)
                    .WithoutArmyConfiguration(id).WithoutArmyTraits(id).WithoutArmyPosition(id) };
            return OwnedArmyLifecycleTargets.PublishCurrent(state.WithArmyLifecycle(arena with { Indexes = pool.ProjectOwnedRoles() }), current);
        }
        //A true31F430 return includes a dropped manager node. Preserve that
        //actual chain rather than fabricating successful container membership.
        current.AssignArmyOccupancyReceiver(physical, id);
        state = state with { ArmyMapOccupancy = current.ExportArmyMapOccupancy() };
        if (RaidCreationKnown(current.ReadArmyReturnBaseGovernor2E(home), "Home_governor2E") == commander &&
            RaidCreationKnown(current.ReadBaseNativeValidity(home), "governor_Home_validity"))
        {
            var update = ApplyPersonFreedomGovernorCurrent(state, current, home);
            state = ArmyReturnCommanderHomeUpdater.PublishPersonCurrent(state, current, new(person, home, false, [update], null));
        }
        state = AssignSpecialForceRaidGroupCurrent(state, current, pool, physical, id, commander);
        state = ArmyIdentityAllocation.Commit(state, id);
        return CompleteSpecialForceRaidCreationCurrent(state, current, id, person);
    }

    private static GameState AssignSpecialForceRaidGroupCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int physical, int id, int commander)
    {
        var input = current.CreateTemporaryHexList() ?? throw RaidCreationMissing("group_input_descriptor");
        try
        {
            var node = current.AllocateRegistryNode(new(input.Identity)) ?? throw RaidCreationMissing("group_input_node");
            bool appended = node.Identity.HasValue;
            if (appended) current.WriteRegistryNodePayload(node, new(RegistryKind.Army, physical));
            var arena = state.ReadArmyLifecycle() ?? throw RaidCreationMissing("group_Army_lifecycle");
            var groups = (arena.ArmyGroups ?? throw RaidCreationMissing("group_owner")).Copy();
            groups.UnitAssignments[id] = new(true, null); //The receiver's actual reset58.
            int[] roster = ArmyGroupSourceAdapter.Roster(arena, current);
            var excluded = ArmyGroupSourceAdapter.Excluded(state, arena, current, roster);
            int selected = 0;
            for (int group = 1; group <= 500; group++)
            {
                var at = ArmyGroupRules.GetDistrict(groups, group, roster, excluded,
                    member => ArmyGroupSourceAdapter.Commander(arena, current, member)) ?? throw RaidCreationMissing("group_District_owner");
                if (!at.IsRecognized) { selected = group; break; }
            }
            if (selected == 0) throw RaidCreationMissing("group_free_receiver");
            if (appended) groups.UnitAssignments[id] = new(true, selected);
            current.SetStartupArmyTroopsSlot58(physical, appended ? (ushort)selected : (ushort)0);
            var selectedDistrict = ArmyGroupRules.GetDistrict(groups, selected, roster, excluded,
                member => ArmyGroupSourceAdapter.Commander(arena, current, member)) ?? throw RaidCreationMissing("selected_group_District");
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw RaidCreationMissing("group_player");
            int playerDistrict = RaidCreationKnown(ForcePrimaryDistrictSource.Resolve(current, player).PhysicalSlot, "group_player_District");
            int actualDistrict = selectedDistrict.IsRecognized ? selectedDistrict.DistrictId ?? 0 : 0;
            if (actualDistrict != playerDistrict && commander < 6000)
                groups.Leaders[selected] = new(true, ArmyGroupSourceAdapter.Officer(current, (ushort)commander));
            groups = groups.RegisterOriginalAiContext(selected); // Actual2D9630 -> D2ED0 registration tail.
            return state.WithArmyLifecycle(arena with { ArmyGroups = groups, Indexes = pool.ProjectOwnedRoles() });
        }
        finally { current.ClearTemporaryHexList(input); current.ReleaseTemporaryHexList(input); }
    }

    private static T RaidCreationKnown<T>(T? value, string field) where T : struct => value ?? throw RaidCreationMissing(field);
    private static InvalidDataException RaidCreationMissing(string field) => new("normal-turn.special-force-raids.creation." + field);
}
