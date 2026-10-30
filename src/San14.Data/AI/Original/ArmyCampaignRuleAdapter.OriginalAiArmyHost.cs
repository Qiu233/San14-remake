using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private Dictionary<int, int[]>? originalAiHostAreaHexes;

    internal GameState PrepareOriginalAiArmyStrategyCurrent(GameState state, SourceWorldContext current,
        int unitId, int physicalArmy)
    {
        var unit = AiHostArmy(state, unitId, physicalArmy);
        var grouping = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
            Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        if (AiHostKnown(grouping.IsAtHome2C96E0(physicalArmy), "Army_at_home")) return state;
        if (AiHostKnown(current.ReadArmyReturnUnitStatus10(new(physicalArmy)), "Army_status") == 2)
            return TroopsInfoPurpose(state, current.ReadOriginalAiArmyTroopsCurrent(new(physicalArmy))).Kind == ArmyGroupPurposeKind.TransportToFoothold
                ? PrepareOriginalAiTransportArmyCornerStrategyCurrent(state, current, unitId, physicalArmy) : state;
        var working = RepairOriginalAiAreaRelayCurrent(state, current, unit, adjacent: true);
        unit = working.Units[unitId];
        int purpose = (int)TroopsInfoPurpose(working, current.ReadOriginalAiArmyTroopsCurrent(new(physicalArmy))).Kind;
        if (purpose == 21 || purpose is >= 0 and < 23 && ((0x41EF00U >> purpose) & 1) != 0)
            working = RepairOriginalAiAreaRelayCurrent(working, current, unit, adjacent: false);
        return working;
    }

    internal bool ShouldRunOriginalAiArmyStrategyTreeCurrent(GameState state, SourceWorldContext current,
        int unitId, int physicalArmy)
    {
        var unit = AiHostArmy(state, unitId, physicalArmy);
        if (AiHostKnown(current.ReadArmyReturnUnitStatus10(new(physicalArmy)), "Army_status") == 2) return false;
        var grouping = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
            Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        if (AiHostKnown(grouping.IsAtHome2C96E0(physicalArmy), "Army_at_home")) return false;
        return AiHostKnown(ArmyConfigurationSourceAdapter.ReadAction(state, unit), "Army_action") is not (11 or 12);
    }

    internal GameState CompleteOriginalAiArmyStrategyCurrent(GameState state, SourceWorldContext current,
        int unitId, int physicalArmy)
    {
        var unit = AiHostArmy(state, unitId, physicalArmy);
        if (AiHostKnown(current.ReadArmyReturnUnitStatus10(new(physicalArmy)), "Army_status") == 2) return state;
        var grouping = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
            Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        if (AiHostKnown(grouping.IsExcluded233560(physicalArmy), "Army_excluded") ||
            AiHostKnown(grouping.IsAtHome2C96E0(physicalArmy), "Army_at_home")) return state;
        var cleared = unit with { March = (unit.March ?? new(null, null, null)) with
            { NextStep = ArmyMarchDestination.None } };
        var working = state with { Units = new(state.Units) { [unitId] = cleared } };
        var appended = ArmyChangedListsSource.AppendCurrent(working, physicalArmy, current);
        return appended.State ?? throw AiHostMissing(appended.UnresolvedOwner ?? "changed_Army_list");
    }

    internal GameState PrepareOriginalAiTransportArmyStrategyCurrent(GameState state, SourceWorldContext current,
        int unitId, int physicalArmy, OriginalAiTroopsReference group)
    {
        return PrepareOriginalAiTransportRouteCurrent(state, current, unitId, physicalArmy, group);
    }

    private GameState RepairOriginalAiAreaRelayCurrent(GameState state, SourceWorldContext current,
        UnitState unit, bool adjacent)
    {
        int physical = unit.NativeArmyOrigin?.NativeSlotIndex ?? throw AiHostMissing("Army_receiver");
        int start = AiHostHex(current, AiHostKnown(current.ReadArmyUnitRawHex2A(new(physical)), "Army_hex"));
        int goal = ReadOriginalAiArmyStrategyGoalCurrent(state, current, unit);
        int fromArea = AiHostArea(current, start), toArea = AiHostArea(current, goal);
        if (adjacent ? fromArea == toArea : fromArea != toArea) return state;
        List<int> fromNeighbors = adjacent ? AiHostNeighbors(current, fromArea) : [];
        if (adjacent && !fromNeighbors.Contains(toArea)) return state;
        int force = AiHostKnown(current.ReadArmyUnitForceReference(new(physical))?.PhysicalSlot, "Army_Force");
        bool cornerPenalty = adjacent && start % 220 < 55 && start / 220 > 170;
        int[] Search(int destination, IReadOnlySet<int>? excluded = null, bool penalize = false)
        {
            var target = OrdinaryArmyRouteSource.ReadEffectiveSearchTarget(state, unit)
                ?? throw AiHostMissing("Army_target");
            return ArmyRouteSearch.Search(state, current, unit, start, destination, target.Kind, target.Identity,
                physical, out _, excludedHexIds: excluded, penalizeSurface7: penalize);
        }
        var initial = Search(goal, penalize: cornerPenalty);
        if (initial.Length == 0) return state;
        var foreignAreas = new List<int>();
        foreach (int hex in initial)
        {
            int area = AiHostArea(current, hex);
            if (area == fromArea || area == toArea || AiHostAreaForce(current, area) == force || foreignAreas.Contains(area)) continue;
            foreignAreas.Add(area);
        }
        if (foreignAreas.Count == 0) return state;
        var excludedAreas = new HashSet<int>(foreignAreas);
        if (adjacent)
        {
            foreach (int area in fromNeighbors)
                if (area != toArea && !foreignAreas.Contains(area)) excludedAreas.Add(area);
            if (foreignAreas.Count == 1)
                foreach (int area in AiHostNeighbors(current, toArea))
                    if (area != fromArea && !foreignAreas.Contains(area) && fromNeighbors.Contains(area)) excludedAreas.Add(area);
        }
        var excluded = new HashSet<int>();
        foreach (int area in excludedAreas)
            excluded.UnionWith(ReadOriginalAiHostAreaHexes(area));
        var detour = Search(goal, excluded, cornerPenalty);
        if (detour.Length == 0) return state;

        //The fallback follows the new route and retains the first five points
        //adjacent to a rejected Area, in the source's radius1 order.
        var border = new List<int>(5);
        foreach (int hex in detour)
        {
            if (TacticRemakeCurrentContext.NativeHexOrder(hex, 1).Skip(1)
                .Any(near => foreignAreas.Contains(AiHostArea(current, near)))) border.Add(hex);
            if (border.Count == 5) break;
        }
        var samples = new List<int>(5);
        for (int section = 1; section < 5; section++)
        {
            int index = unchecked(section * detour.Length) / 5;
            if (index >= detour.Length) break;
            int hex = detour[index];
            if (hex == start || hex == goal) continue;
            if (samples.Count > 0)
            {
                if (hex == AiHostOldWaypoint(unit, samples.Count - 1)) continue;
            }
            if (OccupationCenterEligible(state, current, unit,
                current.ReadHexTableReference(hex) ?? throw AiHostMissing("sample_Hex"), force)) samples.Add(hex);
        }
        if (samples.Count < 5 && OccupationCenterEligible(state, current, unit,
            current.ReadHexTableReference(goal) ?? throw AiHostMissing("goal_Hex"), force)) samples.Add(goal);
        if (samples.Count == 0) return state;
        var firstLeg = Search(samples[0]);
        if (firstLeg.Length == 0) return state;
        bool needsBorder = firstLeg.Any(hex =>
        {
            int area = AiHostArea(current, hex);
            return area != fromArea && area != toArea && AiHostAreaForce(current, area) != force;
        });
        if (needsBorder && border.Count == 0) return state;
        var selected = needsBorder ? border : samples;
        var points = Enumerable.Repeat((ushort)48400, 5).ToArray();
        for (int index = 0; index < selected.Count && index < 5; index++)
        {
            int hex = selected[index];
            if (hex is 0 or 48400) break;
            if (IsOriginalAiArmyRelayHexIdentityCurrent(current, hex)) points[index] = checked((ushort)hex);
        }
        if (AiHostKnown(ArmySavedCommandKnowledge.ReadControl(state, unit), "Army_control") != 0)
            Array.Fill(points, (ushort)48400);
        if (ArmyMarchSourceAdapter.ReadOrdinaryWaypoints(unit.March) is { } previous && previous.SequenceEqual(points)) return state;
        var march = unit.March ?? new(null, null, null);
        var changed = unit with { March = march with { Target = ArmyMarchSourceAdapter.DecodeDestination(points[0]),
            FollowingWaypoints = new(points.Skip(1).Select(value => (ArmyMarchDestination?)ArmyMarchSourceAdapter.DecodeDestination(value)).ToArray()) } };
        return state with { Units = new(state.Units) { [unit.Id] = changed } };
    }

    //Hex12 is a fixed normal resource field. The source iterator scans it in
    //ascending order; indexing that immutable map once avoids one full scan
    //for every excluded Area of every Army. Mutable Hex14 is never cached here.
    internal IReadOnlyList<int> ReadOriginalAiHostAreaHexes(int areaPhysical)
    {
        if (areaPhysical is < 1 or > 500) return [];
        originalAiHostAreaHexes ??= _map.Map.Cells.Where(cell => cell.AreaSlot is >= 1 and <= 500)
            .GroupBy(cell => (int)cell.AreaSlot).ToDictionary(group => group.Key, group => group.Select(cell => cell.Id).ToArray());
        return originalAiHostAreaHexes.GetValueOrDefault(areaPhysical) ?? [];
    }

    internal int ReadOriginalAiArmyStrategyGoalCurrent(GameState state, SourceWorldContext current, UnitState unit)
        => AiHostHex(current, AiHostKnown(OrdinaryArmyRouteSource.ReadCurrentTarget(state, unit, current), "Army_goal"));

    internal int ReadOriginalAiArmyFormationClassCurrent(GameState state, SourceWorldContext current, UnitState unit)
    {
        int physical = unit.NativeArmyOrigin?.NativeSlotIndex ?? throw AiHostMissing("formation_Army_receiver");
        var army = new ArmyUnitReference(physical);
        if (!AiHostKnown(current.ReadArmyUnitNativeValidity(army), "formation_Army_validity")) return 2;
        int at = AiHostHex(current, AiHostKnown(current.ReadArmyUnitRawHex2A(army), "formation_Hex"));
        int surface = AiHostKnown(current.ReadHexSurfaceCode(current.ReadHexTableReference(at)!), "formation_surface");
        var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0) ?? throw AiHostMissing("formation_terrain");
        int terrainId = AiHostKnown(terrain.PhysicalSlot, "formation_terrain_identity");
        bool naval = (AiHostKnown(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, terrainId), "formation_terrain_flags") & 2) != 0;
        int rawFormation = AiHostKnown(naval ? ArmyConfigurationSourceAdapter.ReadNaval(state, unit)
            : ArmyConfigurationSourceAdapter.ReadCurrentLand(state, unit), "formation_code");
        var definition = current.ReadStaticTableReference(0x77428, rawFormation <= 20 ? rawFormation : 0)
            ?? throw AiHostMissing("formation_pointer");
        if (definition.PhysicalSlot is null or 0) return 2;
        int raw = AiHostKnown(current.ReadArmyUnitCommanderId(army), "formation_Commander");
        int person = AiHostKnown((raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
            "formation_Commander_pointer");
        if (!AiHostKnown(current.ReadPersonNativeValidity(person), "formation_Commander_validity")) return 2;
        var result = DeploymentTroopClassificationSource.Read(state, current, person, definition.PhysicalSlot.Value,
            AiHostKnown(current.ReadArmyReturnResourceFields(army)?.Ready16, "formation_Troops"));
        return result.Marker switch
        {
            DeploymentTroopMarker.AtSecondaryThreshold => 1,
            DeploymentTroopMarker.Default => 0,
            _ => 2
        };
    }

    internal bool IsOriginalAiArmyRelayHexCurrent(SourceWorldContext current, OriginalAiHex hex)
        => AiHostRelayEligiblePointer(current, new(hex.Id));

    internal bool IsOriginalAiArmyRelayHexIdentityCurrent(SourceWorldContext current, int hex)
    {
        var at = current.ReadHexTableReference(hex is >= 0 and < 48400 ? hex : 0) ?? throw AiHostMissing("relay_Hex");
        return AiHostRelayEligiblePointer(current, at);
    }

    private static bool AiHostRelayEligiblePointer(SourceWorldContext current, HexReference at)
    {
        if (at.PhysicalSlot is null) return false;
        byte surface = AiHostKnown(current.ReadHexSurfaceCode(at), "relay_surface");
        var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0) ?? throw AiHostMissing("relay_terrain");
        if (terrain.PhysicalSlot is null || unchecked((short)AiHostKnown(current.ReadSurfaceTypeRawField2A(terrain), "relay_terrain_cost")) == 0 ||
            surface is 16 or 17) return false;
        int raw = AiHostKnown(current.ReadHexObjectLink(at), "relay_Object_link");
        var obj = current.ReadObjectTableReference(raw <= 3000 ? raw : 0) ?? throw AiHostMissing("relay_Object");
        if (AiHostKnown(obj.PhysicalSlot, "relay_Object_identity") == 0) return true;
        var fields = current.ReadPhysicalObject(obj.PhysicalSlot!.Value) ?? throw AiHostMissing("relay_Object_fields");
        var definition = current.ReadObjectDefinitionReference(fields.RawField12 <= 30 ? fields.RawField12 : 0)
            ?? throw AiHostMissing("relay_Object_definition");
        return AiHostKnown(current.ReadObjectDefinitionRaw2C(definition), "relay_Object_category") == 4;
    }

    private static ushort AiHostOldWaypoint(UnitState unit, int index)
    {
        if (index == 0) return AiHostKnown(ArmyMarchSourceAdapter.EncodeDestination(unit.March?.Target), "Army_relay_point0");
        var following = unit.March?.FollowingWaypoints ?? throw AiHostMissing("Army_relay_continuation");
        return index > following.Destinations.Count ? (ushort)48400
            : AiHostKnown(ArmyMarchSourceAdapter.EncodeDestination(following.Destinations[index - 1]), "Army_relay_point" + index);
    }

    private static UnitState AiHostArmy(GameState state, int unitId, int physical)
        => state.Units.TryGetValue(unitId, out var unit) && unit.NativeArmyOrigin?.NativeSlotIndex == physical
            ? unit : throw AiHostMissing("Army_current_receiver");
    private static int AiHostHex(SourceWorldContext current, int hex) => AiHostKnown(
        current.ReadHexTableReference(hex is >= 0 and < 48400 ? hex : 0)?.PhysicalSlot, "Hex_pointer");
    private static int AiHostArea(SourceWorldContext current, int hex)
    {
        int raw = AiHostKnown(current.ReadHexAreaSlot(current.ReadHexTableReference(AiHostHex(current, hex))!), "Hex_Area");
        return AiHostKnown(current.ReadAreaTableReference(raw <= 500 ? raw : 0)?.PhysicalSlot, "Area_pointer");
    }
    private static int AiHostAreaForce(SourceWorldContext current, int area)
    {
        int raw = AiHostKnown(current.ReadAreaCurrentForceSlot(new(area)), "Area_Force");
        return AiHostKnown(current.ReadForceTableReference(raw <= 51 ? raw : 0)?.PhysicalSlot, "Area_Force_pointer");
    }
    private static List<int> AiHostNeighbors(SourceWorldContext current, int area)
    {
        var result = new List<int>(8);
        for (int edge = 0; edge < 8; edge++)
        {
            int raw = AiHostKnown(current.ReadAreaNeighborSlot(new(area), edge), "Area_neighbor");
            if (raw == 0) continue;
            var at = current.ReadAreaTableReference(raw <= 500 ? raw : 0) ?? throw AiHostMissing("Area_neighbor_pointer");
            if (AiHostKnown(current.ReadAreaNativeValidity(at), "Area_neighbor_validity"))
                result.Add(AiHostKnown(at.PhysicalSlot, "Area_neighbor_identity"));
        }
        return result;
    }
    private static T AiHostKnown<T>(T? value, string field) where T : struct => value ?? throw AiHostMissing(field);
    private static InvalidDataException AiHostMissing(string field) => new("original_ai.Army_host." + field);
}
