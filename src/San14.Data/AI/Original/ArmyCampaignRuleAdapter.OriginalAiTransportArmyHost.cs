using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //43F9D0/440120 call098790 before exclusion/home admission. Its failure
    //return changes the actual destination to home; the host ignores that return.
    private GameState PrepareOriginalAiTransportRouteCurrent(GameState state, SourceWorldContext current,
        int unitId, int physicalArmy, OriginalAiTroopsReference group)
    {
        var army = AiHostArmy(state, unitId, physicalArmy);
        if (AiHostKnown(current.ReadArmyReturnUnitStatus10(new(physicalArmy)), "Army_status") != 2 ||
            TroopsInfoPurpose(state, group).Kind != ArmyGroupPurposeKind.TransportToFoothold) return state;
        var stops = AiTransportStops(state, current, army);
        int start = AiTransportCurrentHex(current, physicalArmy);
        int force = AiHostKnown(current.ReadArmyUnitForceReference(new(physicalArmy))?.PhysicalSlot, "transport_Force");
        int budget = AiTransportRouteBudget(state, current, army, physicalArmy);
        var threats = AiTransportThreats(state, current, army, start, stops, force, budget);
        if (threats.Count == 0) return state;
        var danger = new HashSet<int>();
        foreach (int physical in threats)
        {
            var obj = new ObjectReference(physical);
            if (!AiHostKnown(current.ReadObjectNativeValidity(obj), "transport_threat_validity")) continue;
            var fields = current.ReadPhysicalObject(physical) ?? throw AiHostMissing("transport_threat_fields");
            int center = AiHostHex(current, fields.RawField10);
            foreach (int near in TacticRemakeCurrentContext.NativeHexOrder(center, fields.RawField12 == 6 ? 3 : 2).Skip(1))
                danger.Add(near);
        }
        var path = new List<int>();
        int at = start;
        foreach (int destination in stops)
        {
            if (at == destination) continue;
            var leg = AiTransportSearch(state, current, army, at, destination, actualArmy: false, force,
                excluded: danger);
            if (leg.Path.Length == 0) return AiTransportReturnHome(state, current, army, physicalArmy);
            path.AddRange(leg.Path);
            budget = unchecked(budget - leg.Cost);
            at = destination;
            if (budget < 0) break;
        }
        return AiTransportCompress(state, current, army, physicalArmy, start, path,
            segment => segment.All(hex => !danger.Contains(hex)), period: 6, skipWhenClear: true,
            actualArmy: false, descriptorForce: force, avoidHostile: true);
    }

    //B51F0 calls09C5C0 only after its normal manager/Force/home gates.
    //09C5C0 ignores09AEC0's return. This automatic B51F0 caller then clears48
    //and appends when the corner/trait gate matched, even without a rewrite.
    internal GameState PrepareOriginalAiTransportArmyCornerStrategyCurrent(GameState state,
        SourceWorldContext current, int unitId, int physicalArmy)
    {
        var working = TryPrepareOriginalAiTransportArmyCornerStrategyCurrent(state, current, unitId, physicalArmy, out bool prepared);
        if (!prepared) return working;
        var army = working.Units[unitId];
        var changed = army with { March = (army.March ?? new(null, null, null)) with { NextStep = ArmyMarchDestination.None } };
        working = working with { Units = new(working.Units) { [unitId] = changed } };
        return ArmyChangedListsSource.AppendCurrent(working, physicalArmy, current).State
            ?? throw AiHostMissing("transport_changed_Army_list");
    }

    private GameState TryPrepareOriginalAiTransportArmyCornerStrategyCurrent(GameState state,
        SourceWorldContext current, int unitId, int physicalArmy, out bool prepared)
    {
        prepared = false;
        var army = AiHostArmy(state, unitId, physicalArmy);
        int start = AiTransportCurrentHex(current, physicalArmy);
        if (start % 220 >= 55 || start / 220 <= 170) return state;
        int commanderId = AiHostKnown(current.ReadArmyUnitCommanderId(new(physicalArmy)), "transport_Commander");
        int commander = AiHostKnown(current.ReadPersonLookupReference(commanderId <= 6000 ? commanderId : 0)?.PhysicalSlot,
            "transport_Commander_pointer");
        for (int index = 0; index < 9; index++)
            if (AiHostKnown(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, commander, index),
                "transport_Commander_personality") == 77) return state;
        prepared = true;
        var stops = AiTransportStops(state, current, army);
        int force = AiHostKnown(current.ReadArmyUnitForceReference(new(physicalArmy))?.PhysicalSlot, "transport_Force");
        int budget = AiTransportRouteBudget(state, current, army, physicalArmy);
        bool hasSurfaceSeven = false;
        bool actualArmy = AiHostKnown(current.ReadArmyReturnUnitStatus10(new(physicalArmy)), "poison_Army_status") == 2;
        int descriptorForce = actualArmy ? force : 0;
        int at = start, remaining = budget;
        foreach (int destination in stops)
        {
            if (at == destination) continue;
            var leg = AiTransportSearch(state, current, army, at, destination, actualArmy, descriptorForce, avoidHostile: actualArmy);
            if (leg.Path.Any(hex => AiTransportSurface(current, hex) == 7)) hasSurfaceSeven = true;
            remaining = unchecked(remaining - leg.Cost);
            if (remaining < 0) break;
            at = destination;
        }
        var working = state;
        if (hasSurfaceSeven)
        {
            var path = new List<int>(); at = start;
            foreach (int destination in stops)
            {
                if (at == destination) continue;
                var leg = AiTransportSearch(state, current, army, at, destination, actualArmy, descriptorForce,
                    penalizeSurface7: true, avoidHostile: actualArmy);
                path.AddRange(leg.Path);
                budget = unchecked(budget - leg.Cost);
                at = destination;
                if (budget < 0) break;
            }
            working = AiTransportCompress(state, current, army, physicalArmy, start, path,
                segment => segment.All(hex => AiTransportSurface(current, hex) != 7), period: 3, skipWhenClear: false,
                actualArmy, descriptorForce, avoidHostile: actualArmy);
        }
        return working;
    }

    private List<int> AiTransportThreats(GameState state, SourceWorldContext current, UnitState army,
        int start, IReadOnlyList<int> stops, int force, int budget)
    {
        //09FB90 visits the actual ordered path. Its Hex set is only an
        //invocation-local membership set; object discovery order is retained.
        var visited = new HashSet<int>();
        var objects = new List<int>();
        int at = start;
        foreach (int destination in stops)
        {
            if (at == destination) continue;
            var leg = AiTransportSearch(state, current, army, at, destination, actualArmy: false, force);
            foreach (int center in leg.Path)
            {
                //2A7EE0 excludes its center; this caller appends it afterwards.
                var nearHexes = TacticRemakeCurrentContext.NativeHexOrder(center, 3).Skip(1).Append(center);
                foreach (int near in nearHexes)
                {
                    if (visited.Contains(near)) continue;
                    int distance = ArmyRouteSearch.Distance(center, near);
                    if (distance < 3) visited.Add(near);
                    var hex = current.ReadHexTableReference(near) ?? throw AiHostMissing("transport_near_Hex");
                    int link = AiHostKnown(current.ReadHexObjectLink(hex), "transport_near_Object");
                    var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0)
                        ?? throw AiHostMissing("transport_near_Object_pointer");
                    if (!AiHostKnown(current.ReadObjectNativeValidity(obj), "transport_near_Object_validity")) continue;
                    int physical = AiHostKnown(obj.PhysicalSlot, "transport_near_Object_identity");
                    var fields = current.ReadPhysicalObject(physical) ?? throw AiHostMissing("transport_near_Object_fields");
                    if (!(fields.RawField12 == 5 && distance <= 2 || fields.RawField12 == 6 && distance <= 3)) continue;
                    int owner = AiHostKnown(current.ReadObjectForceSlot13(obj), "transport_near_Object_Force");
                    if (!AiHostKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state, owner, force), "transport_threat_relation") ||
                        objects.Contains(physical)) continue;
                    objects.Add(physical);
                }
            }
            budget = unchecked(budget - leg.Cost);
            if (budget < 0) break;
            at = destination;
        }
        return objects;
    }

    private GameState AiTransportCompress(GameState state, SourceWorldContext current, UnitState army,
        int physical, int start, IReadOnlyList<int> path, Func<IReadOnlyList<int>, bool> clear,
        int period, bool skipWhenClear, bool actualArmy, int descriptorForce, bool avoidHostile)
    {
        if (path.Count == 0) throw AiHostMissing("transport_prepared_path_tail");
        bool transport = AiHostKnown(current.ReadArmyReturnUnitStatus10(new(physical)), "relay_Army_status") == 2;
        var points = (transport ? TransportRouteSourceAdapter.ReadWaypoints(state, army)
            : ArmyMarchSourceAdapter.ReadOrdinaryWaypoints(army.March)) ?? throw AiHostMissing("poison_five_relay_owner");
        var old = (ushort[])points.Clone();
        int writes = 0, iterations = 0, anchor = start;
        int? previous = null, newTarget = null;
        bool controlled = AiHostKnown(ArmySavedCommandKnowledge.ReadControl(state, army), "transport_control") != 0;
        bool actionMove = AiHostKnown(ArmyConfigurationSourceAdapter.ReadAction(state, army), "transport_action") == 1;
        for (int index = 0; index < path.Count; index++)
        {
            int destination = path[index];
            if (anchor == destination) continue;
            var leg = AiTransportSearch(state, current, army, anchor, destination, actualArmy, descriptorForce, avoidHostile: avoidHostile);
            int selected = destination;
            if (leg.Path.Length != 0)
            {
                bool safe = clear(leg.Path.Where(hex => hex != anchor).ToArray());
                iterations++;
                bool retain = skipWhenClear ? safe || iterations % period == 0 : safe && iterations % period == 0;
                if (retain) { previous = destination; continue; }
                if (previous is int known) { selected = known; anchor = known; }
            }
            previous = destination;
            if (selected == AiHostHex(current, points[writes])) continue;
            if (IsOriginalAiArmyRelayHexIdentityCurrent(current, selected))
            {
                if (controlled) Array.Fill(points, (ushort)48400);
                else points[writes] = checked((ushort)selected);
                writes++;
            }
            if (writes != 5) continue;
            if (index + 1 < path.Count && actionMove) newTarget = path[index + 1];
            break;
        }
        if (writes < 4 && IsOriginalAiArmyRelayHexIdentityCurrent(current, path[^1]))
        {
            if (controlled) Array.Fill(points, (ushort)48400);
            else points[writes] = checked((ushort)path[^1]);
        }
        if (actionMove && newTarget is null) newTarget = path[^1];
        var working = state;
        if (!old.SequenceEqual(points))
        {
            var changed = transport ? army with { TransportRoute = TransportRouteSourceAdapter.Decode(points) }
                : army with { March = (army.March ?? new(null, null, null)) with
                    { Target = ArmyMarchSourceAdapter.DecodeDestination(points[0]),
                      FollowingWaypoints = new(points.Skip(1).Select(value => (ArmyMarchDestination?)ArmyMarchSourceAdapter.DecodeDestination(value)).ToArray()) } };
            var resource = state.ResourceState;
            if (transport && TransportRouteSourceAdapter.ReadChoice(state, army) is byte choice)
                resource = TransportRouteSourceAdapter.Write(resource, army.Id, points, choice);
            working = state with { Units = new(state.Units) { [army.Id] = changed }, ResourceState = resource };
        }
        if (newTarget is int target && !controlled)
            working = OwnedArmyLifecycleTargets.Write(working, working.ReadArmyLifecycle()!.Slots[physical].ReceiverId,
                new(9, checked((ushort)target)));
        return working;
    }

    private (int[] Path, int Cost) AiTransportSearch(GameState state, SourceWorldContext current, UnitState army,
        int start, int destination, bool actualArmy, int force, IReadOnlySet<int>? excluded = null, bool penalizeSurface7 = false, bool avoidHostile = true)
    {
        if (actualArmy)
        {
            var target = OrdinaryArmyRouteSource.ReadEffectiveSearchTarget(state, army) ?? throw AiHostMissing("transport_target");
            return ArmyRouteSearch.SearchAiTransportHost(state, current, army, start, destination,
                target.Kind, target.Identity, excluded, penalizeSurface7);
        }
        var withoutArmy = new ArmyRouteSearch.Queries(state, current, new AiTransportForceMovement(force, avoidHostile),
            start, destination, 0, 0, null, 1, hasArmyDescriptor: false, excludedHexIds: excluded, penalizeSurface7: penalizeSurface7);
        var result = ArmyRouteSearch.Search(withoutArmy, out _, out int total, hasArmyDescriptor: false);
        return (result, total);
    }

    private static List<int> AiTransportStops(GameState state, SourceWorldContext current, UnitState army)
    {
        int physical = army.NativeArmyOrigin!.NativeSlotIndex!.Value;
        bool transport = AiHostKnown(current.ReadArmyReturnUnitStatus10(new(physical)), "relay_Army_status") == 2;
        var points = (transport ? TransportRouteSourceAdapter.ReadWaypoints(state, army)
            : ArmyMarchSourceAdapter.ReadOrdinaryWaypoints(army.March)) ?? throw AiHostMissing("poison_five_relay_owner");
        var result = new List<int>(6);
        foreach (int point in points)
        {
            var reference = current.ReadHexTableReference(point < 48400 ? point : 0) ?? throw AiHostMissing("transport_relay_Hex");
            if (point is > 0 and < 48400 && reference.PhysicalSlot is int hex) result.Add(hex);
        }
        result.Add(AiHostHex(current, AiTransportMainGoal(state, current, army)));
        return result;
    }

    private static int AiTransportMainGoal(GameState state, SourceWorldContext current, UnitState army)
    {
        int physical = army.NativeArmyOrigin!.NativeSlotIndex!.Value;
        var target = OwnedArmyLifecycleTargets.Read(state, state.ReadArmyLifecycle()!.Slots[physical].ReceiverId)
            ?? throw AiHostMissing("transport_main_target");
        int id = AiHostKnown(target.Identity3A, "transport_main_target_identity");
        switch (AiHostKnown(target.Kind38, "transport_main_target_kind"))
        {
            case 9: return id;
            case 27: return AiHostKnown(current.ReadArmyUnitRawHex2A(current.ReadArmyUnitTableReference(id <= 500 ? id : 0)
                ?? throw AiHostMissing("transport_target_Army")), "transport_target_Army_Hex");
            case 5: return ObjectHex(current.ReadCityTableReference(id <= 51 ? id : 0));
            case 6: return ObjectHex(current.ReadGateTableReference(id <= 10 ? id : 0));
            case 11: return AiHostKnown(current.ReadObjectHexIndex(current.ReadObjectTableReference(id <= 3000 ? id : 0)
                ?? throw AiHostMissing("transport_target_Object")), "transport_target_Object_Hex");
            default: return 48400;
        }
        int ObjectHex(BaseReference? selected)
        {
            int link = AiHostKnown(current.ReadBaseObjectLink(selected ?? throw AiHostMissing("transport_target_Base")), "transport_target_Base_Object");
            return AiHostKnown(current.ReadObjectHexIndex(current.ReadObjectTableReference(link <= 3000 ? link : 0)
                ?? throw AiHostMissing("transport_target_Base_Object_pointer")), "transport_target_Base_Object_Hex");
        }
    }

    private GameState AiTransportReturnHome(GameState state, SourceWorldContext current, UnitState army, int physical)
    {
        if (AiHostKnown(ArmySavedCommandKnowledge.ReadControl(state, army), "transport_control") != 0) return state;
        var home = current.FindArmyUnitCurrentBase(new(physical)) ?? throw AiHostMissing("transport_home_Base");
        int location = AiHostKnown(current.ReadBaseObjectLink(home), "transport_home_Base_identity");
        byte action = location is >= 52 and <= 61 ? (byte)12 : (byte)11;
        byte kind = action == 12 ? (byte)6 : (byte)5;
        ushort identity = checked((ushort)(action == 12 ? location - 51 : location <= 51 ? location : 0));
        var changed = army with { FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(action) };
        var working = state with { Units = new(state.Units) { [army.Id] = changed },
            ResourceState = ArmyConfigurationSourceAdapter.WriteAction(state.ResourceState, army.Id, action) };
        return OwnedArmyLifecycleTargets.Write(working, working.ReadArmyLifecycle()!.Slots[physical].ReceiverId, new(kind, identity));
    }

    private static int AiTransportCurrentHex(SourceWorldContext current, int physical)
        => AiHostHex(current, AiHostKnown(current.ReadArmyUnitRawHex2A(new(physical)), "transport_Hex"));
    private static byte AiTransportSurface(SourceWorldContext current, int hex)
        => AiHostKnown(current.ReadHexSurfaceCode(current.ReadHexTableReference(hex) ?? throw AiHostMissing("transport_Hex_pointer")), "transport_surface");
    private static int AiTransportRouteBudget(GameState state, SourceWorldContext current, UnitState army, int physical)
    {
        byte surface = AiTransportSurface(current, AiTransportCurrentHex(current, physical));
        var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0) ?? throw AiHostMissing("transport_Terrain");
        bool naval = terrain.PhysicalSlot is int terrainId &&
            (AiHostKnown(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, terrainId), "transport_Terrain_flags") & 2) != 0;
        int formation = AiHostKnown(naval ? ArmyConfigurationSourceAdapter.ReadNaval(state, army)
            : ArmyConfigurationSourceAdapter.ReadCurrentLand(state, army), "transport_formation");
        var mobility = ArmyRouteMobilitySource.Read(state, current, army, formation <= 20 ? formation : 0, surface);
        return unchecked(BattleUnitSourceMath.TruncateSingleToInt32(mobility.Value
            ?? throw AiHostMissing(mobility.UnresolvedOwner ?? "transport_mobility")) * 120);
    }

    private sealed class AiTransportForceMovement(int force, bool avoidHostile) : ArmyRouteSearch.Movement
    {
        internal override bool AvoidHostile => avoidHostile;
        internal override byte LandFormation => 0;
        internal override byte NavalFormation => 0;
        internal override int Force => force;
        internal override int HomeHex => 0;
        internal override bool WallPermission => true;
        internal override byte? Action => null;
        internal override bool HasNoDestination => true;
        internal override float ReadMobility(int formation, byte surface) => throw AiHostMissing("transport_null_Army_mobility");
    }

}

internal static partial class ArmyRouteSearch
{
    //The transport host needs the source path cost as well as its points.
    //Retain the existing actual-Army Movement producer rather than duplicating
    //its Force, home, trait or mobility rules in the AI adapter.
    internal static (int[] Path, int Cost) SearchAiTransportHost(GameState state, SourceWorldContext current,
        UnitState army, int start, int goal, byte kind, ushort identity,
        IReadOnlySet<int>? excluded, bool penalizeSurface7)
    {
        var query = new Queries(state, current, new LiveMovement(state, current, army), start, goal,
            kind, identity, army.NativeArmyOrigin!.NativeSlotIndex, 1,
            excludedHexIds: excluded, penalizeSurface7: penalizeSurface7);
        var path = Search(query, out _, out int cost);
        return (path, cost);
    }
}
