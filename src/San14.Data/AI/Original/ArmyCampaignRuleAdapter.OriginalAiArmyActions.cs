using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    CommandResult IArmyInstructionExecution.RecoverLogistics(GameState state, ForceState force,
        RecoverArmyLogisticsOrder order, CommandContext context)
        => ApplyOriginalAiArmyAction(state, order.UnitId, "recover_logistics", (current, army) =>
        {
            var runtime = army.NativeTacticRuntime ?? throw AiArmyActionMissing("Army_runtime");
            if ((runtime.Flags26 & 2) != 0) return (state, false);
            int physical = army.NativeArmyOrigin!.NativeSlotIndex!.Value;
            int raw = AiArmyKnown(current.ReadArmyUnitCommanderId(new(physical)), "recover_Commander12");
            int commander = AiArmyKnown(current.ReadPersonLookupReference(raw <= 6000 ? raw : 0)?.PhysicalSlot, "recover_Commander_pointer");
            int intelligence = 0;
            if (AiArmyKnown(current.ReadPersonNativeValidity(commander), "recover_Commander_validity"))
            {
                _baseCapacityInputs.BindCurrent(state, current);
                _personRelationInputs.BindCurrent(state, current);
                intelligence = ReadOriginalAiPersonInformationScalar(state, current, commander, 9);
            }
            int radius = Math.Max(2, intelligence / 10 - 4);
            int? target = FindOriginalAiLogisticsRecoveryHex(state, current, army, radius);
            if (target is not int destination) return (state, false);
            int origin = AiArmyKnown(current.ReadArmyUnitRawHex2A(new(physical)), "recover_Army2A");
            GameState working;
            if (ArmyRouteSearch.Distance(origin, destination) == 1)
                working = PrependOriginalAiArmyRelayCurrent(state, current, army, destination);
            else
            {
                working = SetOriginalAiArmyMoveCurrent(state, army, destination);
                army = working.Units[army.Id];
                working = WriteOriginalAiArmyRelayCurrent(working, army, Enumerable.Repeat((ushort)48400, 5).ToArray());
                army = working.Units[army.Id];
                if (runtime.Abnormal25 == 0) working = ClearTrackedArmyOverride(working, current, army);
            }
            army = working.Units[army.Id];
            if (AiArmyKnown(ArmySavedCommandKnowledge.ReadControl(working, army), "recover_control") == 0)
            {
                var behavior = army.Behavior ?? throw AiArmyActionMissing("recover_Behavior40");
                working = working with { Units = new(working.Units)
                    { [army.Id] = army with { Behavior = behavior with { AllowRetreat = true } } } };
            }
            //The node's tail is separate from CD930's internal far-route tail.
            //Both actual26CDC0 calls remain, including their abnormal25 gate.
            working = ArmyAiActionFlagsSource.Or(working, army.Id, 4);
            army = working.Units[army.Id];
            if (runtime.Abnormal25 == 0) working = ClearTrackedArmyOverride(working, current, army);
            return (working, true);
        });

    CommandResult IArmyInstructionExecution.ExcludePlayerFoothold(GameState state, ForceState force,
        ExcludeArmyFromPlayerFootholdOrder order, CommandContext context)
        => ApplyOriginalAiArmyAction(state, order.UnitId, "exclude_player_foothold", (current, army) =>
        {
            var runtime = army.NativeTacticRuntime ?? throw AiArmyActionMissing("Army_runtime");
            int physical = army.NativeArmyOrigin!.NativeSlotIndex!.Value;
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw AiArmyActionMissing("current_player_Force");
            int playerForce = AiArmyKnown(player.PhysicalSlot, "current_player_Force_identity");
            int ownForce = AiArmyKnown(current.ReadArmyUnitForceReference(new(physical))?.PhysicalSlot, "actor_Force");
            if (!AiArmyKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state, playerForce, ownForce), "player_hostility") ||
                (runtime.Flags26 & 1) == 0 || AiArmyKnown(ArmyConfigurationSourceAdapter.ReadAction(state, army), "exclude_Action37") != 4)
                return (state, false);
            var target = OwnedArmyLifecycleTargets.Read(state, state.ReadArmyLifecycle()!.Slots[physical].ReceiverId)
                ?? throw AiArmyActionMissing("exclude_target");
            if (AiArmyKnown(target.Kind38, "exclude_target38") != 5) return (state, false);
            int selected = AiArmyKnown(target.Identity3A, "exclude_target3A");
            var city = current.ReadCityTableReference(selected <= 51 ? selected : 0) ?? throw AiArmyActionMissing("exclude_City");
            int at = AiArmyKnown(current.ReadArmyUnitRawHex2A(new(physical)), "exclude_Army2A");
            foreach (int adjacent in TacticRemakeCurrentContext.NativeHexOrder(AiHostHex(current, at), 1).Skip(1))
            {
                var hex = current.ReadHexTableReference(adjacent) ?? throw AiArmyActionMissing("exclude_adjacent_Hex");
                int link = AiArmyKnown(current.ReadHexObjectLink(hex), "exclude_adjacent_City_link");
                if (current.ReadCityTableReference(link <= 51 ? link : 0) != city) continue;
                int cityObject = AiArmyKnown(current.ReadBaseObjectLink(city), "exclude_City_Object");
                var obj = current.ReadObjectTableReference(cityObject <= 3000 ? cityObject : 0) ?? throw AiArmyActionMissing("exclude_City_Object_pointer");
                int center = AiArmyKnown(current.ReadObjectNativeValidity(obj), "exclude_City_Object_validity")
                    ? AiHostHex(current, AiArmyKnown(current.ReadObjectHexIndex(obj), "exclude_City_Hex")) : AiHostHex(current, 0);
                current.BindArmyMapOccupancy(state);
                var grouping = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
                    Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
                foreach (int near in TacticRemakeCurrentContext.NativeHexOrder(center, 2).Skip(1))
                {
                    var nearHex = current.ReadHexTableReference(near) ?? throw AiArmyActionMissing("exclude_near_Hex");
                    int marker = unchecked((short)AiArmyKnown(current.ReadHexRaw1C(nearHex), "exclude_near_Army_marker"));
                    if (marker is < 1 or > 500) continue;
                    var candidate = current.ReadArmyUnitTableReference(marker) ?? throw AiArmyActionMissing("exclude_near_Army");
                    if (!AiArmyKnown(current.ReadArmyUnitNativeValidity(candidate), "exclude_near_Army_validity")) continue;
                    int actual = AiArmyKnown(candidate.PhysicalSlot, "exclude_near_Army_identity");
                    if (AiArmyKnown(grouping.IsExcluded233560(actual), "exclude_near_excluded") ||
                        AiArmyKnown(grouping.IsAtHome2C96E0(actual), "exclude_near_at_home")) continue;
                    if (playerForce == 0 || AiArmyKnown(current.ReadArmyUnitForceReference(candidate)?.PhysicalSlot, "exclude_near_Force") != playerForce)
                        continue;
                    int destination = AiArmyKnown(current.ReadArmyUnitRawHex2A(candidate), "exclude_near_Army2A");
                    var working = PrependOriginalAiArmyRelayCurrent(state, current, army, destination);
                    working = ArmyAiActionFlagsSource.Or(working, army.Id, 0x20);
                    if (runtime.Abnormal25 == 0) working = ClearTrackedArmyOverride(working, current, working.Units[army.Id]);
                    return (working, true);
                }
                //B4E50 exits after the first matching target-City neighbor.
                return (state, false);
            }
            return (state, false);
        });

    private CommandResult ApplyOriginalAiArmyAction(GameState state, int unitId, string name,
        Func<SourceWorldContext, UnitState, (GameState State, bool Applied)> apply)
    {
        bool applied = false;
        var result = ApplyAiArmyInstruction(state, unitId, (current, army) =>
        {
            var changed = apply(current, army);
            applied = changed.Applied;
            return changed.State;
        });
        return result.Accepted && !applied
            ? CommandResult.Reject("The current Army has no applicable " + name + " action.", "army.ai." + name + ".source_gate")
            : result;
    }

    private int? FindOriginalAiLogisticsRecoveryHex(GameState state, SourceWorldContext current, UnitState army, int radius)
    {
        int physical = army.NativeArmyOrigin!.NativeSlotIndex!.Value;
        int origin = AiHostHex(current, AiArmyKnown(current.ReadArmyUnitRawHex2A(new(physical)), "recover_Army_Hex"));
        var force = current.ReadArmyUnitForceReference(new(physical)) ?? throw AiArmyActionMissing("recover_Force_pointer");
        int forceId = AiArmyKnown(force.PhysicalSlot, "recover_Force_identity");
        int? home = null;
        foreach (int candidate in TacticRemakeCurrentContext.NativeHexOrder(origin, radius).Skip(1))
        {
            bool adjacentFriendly = false;
            foreach (int neighbor in TacticRemakeCurrentContext.NativeHexOrder(candidate, 1).Skip(1))
            {
                if (neighbor == origin) continue;
                var hex = current.ReadHexTableReference(neighbor) ?? throw AiArmyActionMissing("recover_neighbor_Hex");
                int owner = AiArmyKnown(current.ReadHexForceSlot(hex), "recover_neighbor_Force");
                var other = current.ReadForceTableReference(owner <= 51 ? owner : 0) ?? throw AiArmyActionMissing("recover_neighbor_Force_pointer");
                if (!AiArmyKnown(current.ReadForceNativeValidity(force), "recover_actor_Force_validity") ||
                    !AiArmyKnown(current.ReadForceNativeValidity(other), "recover_neighbor_Force_validity")) continue;
                if (AiArmyKnown(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, force, other), "recover_friendly_relation"))
                { adjacentFriendly = true; break; }
            }
            if (!adjacentFriendly) continue;
            var target = current.ReadHexTableReference(candidate) ?? throw AiArmyActionMissing("recover_candidate_Hex");
            byte originalForce = AiArmyKnown(current.ReadHexForceSlot(target), "recover_candidate_Force");
            if (originalForce != forceId && !OccupationCenterEligible(state, current, army, target, forceId, requireNoArmy: true))
                continue;
            //BCE80 only raw-assigns/restores this candidate's Hex14 for flags8.
            //An operation-local read override preserves that exact observation
            //without changing actual territory or application profile state.
            byte? ReadForce(int hex) => hex == candidate ? checked((byte)forceId)
                : current.ReadHexForceSlot(current.ReadHexTableReference(hex) ?? throw AiArmyActionMissing("recover_route_Hex"));
            home ??= ReadOriginalAiArmyHomeHexCurrent(current, new(physical));
            int destination = home.Value;
            if ((uint)destination >= 48400) throw AiArmyActionMissing("recover_home_endpoint");
            var path = ArmyRouteSearch.Search(state, current, army, candidate, destination, 0, 0, physical, out _,
                searchFlags: 8, readHexForce: originalForce != forceId ? ReadForce : null);
            if (path.Length != 0) return candidate;
        }
        return null;
    }

    private static int ReadOriginalAiArmyHomeHexCurrent(SourceWorldContext current, ArmyUnitReference army)
    {
        var home = current.FindArmyUnitCurrentBase(army) ?? throw AiArmyActionMissing("home_Base");
        if (!AiArmyKnown(current.ReadBaseNativeValidity(home), "home_Base_validity")) return 48400;
        int link = AiArmyKnown(current.ReadBaseObjectLink(home), "home_Object_link");
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw AiArmyActionMissing("home_Object");
        return AiArmyKnown(current.ReadObjectNativeValidity(obj), "home_Object_validity")
            ? AiArmyKnown(current.ReadObjectHexIndex(obj), "home_Object_Hex") : 48400;
    }

    private GameState PrependOriginalAiArmyRelayCurrent(GameState state, SourceWorldContext current, UnitState army, int hex)
    {
        if (!IsOriginalAiArmyRelayHexIdentityCurrent(current, hex)) return state;
        var points = ReadOriginalAiArmyRelayCurrent(state, army);
        if (points[0] == hex) return state;
        var prefix = new[] { checked((ushort)hex) }.Concat(points.Where(value => value < 48400)).Take(5).ToArray();
        if (AiArmyKnown(ArmySavedCommandKnowledge.ReadControl(state, army), "relay_control") != 0)
            Array.Fill(points, (ushort)48400);
        else Array.Copy(prefix, points, prefix.Length);
        return WriteOriginalAiArmyRelayCurrent(state, army, points);
    }

    private static ushort[] ReadOriginalAiArmyRelayCurrent(GameState state, UnitState army)
        => (army.NativeTacticRuntime?.Status10 == 2 ? TransportRouteSourceAdapter.ReadWaypoints(state, army)
            : ArmyMarchSourceAdapter.ReadOrdinaryWaypoints(army.March)) ?? throw AiArmyActionMissing("five_relay_owner");

    private static GameState WriteOriginalAiArmyRelayCurrent(GameState state, UnitState army, ushort[] points)
    {
        if (army.NativeTacticRuntime?.Status10 == 2)
        {
            var resource = state.ResourceState;
            if (TransportRouteSourceAdapter.ReadChoice(state, army) is byte choice)
                resource = TransportRouteSourceAdapter.Write(resource, army.Id, points, choice);
            return state with { Units = new(state.Units) { [army.Id] = army with { TransportRoute = TransportRouteSourceAdapter.Decode(points) } },
                ResourceState = resource };
        }
        return state with { Units = new(state.Units) { [army.Id] = army with
            { March = (army.March ?? new(null, null, null)) with { Target = ArmyMarchSourceAdapter.DecodeDestination(points[0]),
                FollowingWaypoints = new(points.Skip(1).Select(value => (ArmyMarchDestination?)ArmyMarchSourceAdapter.DecodeDestination(value)).ToArray()) } } } };
    }

    private static GameState SetOriginalAiArmyMoveCurrent(GameState state, UnitState army, int hex)
    {
        if (AiArmyKnown(ArmySavedCommandKnowledge.ReadControl(state, army), "move_control") != 0) return state;
        var changed = army with { FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(1) };
        var working = state with { Units = new(state.Units) { [army.Id] = changed },
            ResourceState = ArmyConfigurationSourceAdapter.WriteAction(state.ResourceState, army.Id, 1) };
        int physical = army.NativeArmyOrigin!.NativeSlotIndex!.Value;
        return OwnedArmyLifecycleTargets.Write(working, working.ReadArmyLifecycle()!.Slots[physical].ReceiverId,
            new(9, checked((ushort)hex)));
    }

    private static InvalidDataException AiArmyActionMissing(string name) => new("original_ai.Army_action." + name);
}
