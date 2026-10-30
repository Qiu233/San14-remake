using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2725E0 case14. It is independent of the generic route tail: the source
    //patrol replaces its main target and at most five points while preserving
    //action14, accumulated credit, construction selection and saved behavior.
    private GameState MaintainAreaOccupationCommand(GameState state, SourceWorldContext current,
        UnitState army, FacilityOperation operation, int force, ushort at)
    {
        int physical = army.NativeArmyOrigin!.NativeSlotIndex!.Value;
        var receiver = state.ReadArmyLifecycle()!.ReceiverSlots()[physical].ReceiverId;
        var target = OwnedArmyLifecycleTargets.Read(state, receiver)
            ?? throw MaintenanceMissing("occupation_target");
        if (target.Kind38 != 9) return state;
        ushort raw = MaintenanceKnown(target.Identity3A, "occupation_target_identity");
        var goal = OccupationHex(current, raw);
        if (goal.PhysicalSlot is null) return state;
        var area = OccupationArea(current, goal);
        ushort first = MaintenanceKnown(ArmyMarchSourceAdapter.ReadFirstWaypoint(state, army), "occupation_first_waypoint");
        if (first < 48400) return state;
        int areaForce = MaintenanceKnown(current.ReadAreaCurrentForceSlot(area), "occupation_Area_force");
        if (areaForce != force && !OccupationRelation(state, force, areaForce))
            return FinishInvalidArmyCommand(state, current, army, operation);
        if (IsOccupationCityArea(current, area) ||
            MaintenanceKnown(current.ReadHexSurfaceCode(OccupationHex(current,
                MaintenanceKnown(current.ReadAreaHexIndex(area), "occupation_Area_hex"))), "occupation_Area_surface") == 17)
        {
            //A friendly but independently-owned City/Gate area is not a
            //patrol target. Neutral centers retain the original exception.
            if (areaForce != force && areaForce != 0 && OccupationRelation(state, force, areaForce))
                return FinishInvalidArmyCommand(state, current, army, operation);
        }
        int areaPhysical = MaintenanceKnown(area.PhysicalSlot, "occupation_Area_identity");
        for (int h = AreaHexIterator.FindNextIndex(0, areaPhysical, current); h < 48400;
            h = AreaHexIterator.FindNextIndex(h + 1, areaPhysical, current))
        {
            if (OccupationRelation(state, force, MaintenanceKnown(current.ReadHexForceSlot(OccupationHex(current, h)), "occupation_member_force")))
                return RewriteAreaOccupationPatrol(state, current, army, goal, area, force, at);
        }

        var working = state;
        if (IsPlayerRulerLedDistrict(working, current, army))
        {
            //C35B binds Army0, Area0 and the actual41 choice string before
            //history. The following kind25 is a chronology record, not speech.
            byte choice = MaintenanceKnown(army.NativeCommandChoice41, "occupation_completion_choice41");
            var formatted = FormatHistoryReport(working, current, 0xC35B,
                new(Army0: new(physical), Area0: areaPhysical, String0: ReadHistoryChoiceName(working, choice)));
            working = formatted.State;
            int day = operation.ReadCampaignDay(working);
            int hex = MaintenanceKnown(current.ReadArmyUnitRawHex2A(new(physical)), "occupation_completion_hex");
            ushort commander = MaintenanceKnown(current.ReadArmyUnitCommanderId(new(physical)), "occupation_completion_commander");
            int district = ReadFacilityHistoryDistrict(current, new(physical));
            var requests = new List<CampaignEffectRequest>();
            working = WriteHistory(working, current, new ReportCommand(5, district, hex, day,
                formatted.Text, CampaignHistoryWriter.Supplemental(working), commander, MarkBit4: 1), requests);
            working = AppendAreaOccupationCompletionChronicleCurrent(working,current,new(physical),
                areaPhysical,choice,day,hex);
            working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                ?? throw MaintenanceMissing("occupation_completion_effect_owner"))
                .Append(CampaignEffectSource.ArmyCommandMaintenance, operation.Revision, requests) };
        }
        working = FinishNoPathArmyCommand(working, current, working.Units[army.Id], operation);
        if (ArmyConfigurationSourceAdapter.ReadAction(working, working.Units[army.Id]) == 13)
        {
            var held = working.Units[army.Id];
            working = working with { Units = new(working.Units) { [held.Id] = held with
                { Mission = new(UnitMissionKind.Combat), DestinationHexId = held.HexId } } };
        }
        return working;
    }

    //301FC0's gate precedes2B1AF0. A remote representative is retained until
    //the army reaches that area; arriving at any arbitrary Hex must not start
    //a patrol of its current area instead of the selected one.
    private GameState RewriteAreaOccupationPatrol(GameState state, SourceWorldContext current,
        UnitState army, HexReference goal, AreaReference area, int force, ushort at)
    {
        if (!MaintenanceKnown(current.ReadAreaNativeValidity(area), "occupation_patrol_Area_validity")) return state;
        var position = OccupationHex(current, at);
        if (position.PhysicalSlot is null) return state;
        if (position.PhysicalSlot != goal.PhysicalSlot)
        {
            int surface = MaintenanceKnown(current.ReadHexSurfaceCode(goal), "occupation_patrol_goal_surface");
            if (surface is < 15 or > 18 ||
                MaintenanceKnown(current.ReadHexAreaSlot(position), "occupation_patrol_position_Area") != area.PhysicalSlot)
                return state;
            var points = ArmyMarchSourceAdapter.ReadOrdinaryWaypoints(army.March)
                ?? throw MaintenanceMissing("occupation_patrol_waypoints");
            if (points.Any(point => point != 48400) ||
                MaintenanceKnown(current.ReadAreaCurrentForceSlot(area), "occupation_patrol_Area_force") != force)
                return state;
        }
        var patrol = SelectAreaOccupationPatrol(state, current, army, position, force);
        int physical = army.NativeArmyOrigin!.NativeSlotIndex!.Value;
        var receiver = state.ReadArmyLifecycle()!.ReceiverSlots()[physical].ReceiverId;
        var working = OwnedArmyLifecycleTargets.Write(state, receiver, new(9, checked((ushort)patrol.Goal)));
        current.SetArmyLifecycleTargetFields(physical, new(9, checked((ushort)patrol.Goal)));
        var words = ArmyMarchSourceAdapter.ReadOrdinaryWaypoints(army.March)
            ?? throw MaintenanceMissing("occupation_patrol_waypoint_words");
        //2EE4B0 overwrites only emitted positions, not the untouched suffix.
        for (int i = 0; i < patrol.Points.Length; i++) words[i] = checked((ushort)patrol.Points[i]);
        var march = army.March! with
        {
            Target = ArmyMarchSourceAdapter.DecodeDestination(words[0]),
            FollowingWaypoints = new(words.Skip(1).Select(value => (ArmyMarchDestination?)ArmyMarchSourceAdapter.DecodeDestination(value)).ToArray())
        };
        bool reroute = ArmyMarchSourceAdapter.EncodeDestination(march.NextStep) == position.PhysicalSlot;
        if (reroute) march = march with { NextStep = ArmyMarchDestination.None };
        working = working with { Units = new(working.Units) { [army.Id] = army with
            { March = march, DestinationHexId = patrol.Goal < 48400 ? patrol.Goal : army.HexId } } };
        return reroute ? FRoute(working, army.Id, true, current, false) : working;
    }

    private (int Goal, int[] Points) SelectAreaOccupationPatrol(GameState state, SourceWorldContext current,
        UnitState army, HexReference position, int force)
    {
        int start = MaintenanceKnown(position.PhysicalSlot, "occupation_select_position");
        ushort areaRaw = MaintenanceKnown(current.ReadHexAreaSlot(position), "occupation_select_area");
        int person = MaintenanceKnown(current.ResolveOfficerSource(army.OfficerId), "occupation_select_Commander");
        bool wallPermission = !MaintenanceKnown(TacticRemakeCurrentTraits.CommanderImmune(current, person, 102), "occupation_select_trait102");
        int bestCount = 0, bestCost = 0, bestGoal = 48400, firstDistance = 255;
        int[] bestPoints = [];
        //2B1AF0 evaluates six rays in direction order. It can consider six
        //eligible centers but retains only the first five as intermediate points.
        for (int direction = 1; direction <= 6; direction++)
        {
            int probe = start, covered = 0, accumulatedCost = 0;
            var points = new List<int>(6);
            while (points.Count < 6)
            {
                int next = OccupationNeighbor(probe, direction);
                if (next >= 48400) break;
                var hex = OccupationHex(current, next);
                if (hex.PhysicalSlot is not int physical || MaintenanceKnown(current.ReadHexAreaSlot(hex), "occupation_ray_area") != areaRaw) break;
                probe = physical;
                if (!OccupationCenterEligible(state, current, army, hex, force) ||
                    !OccupationRelation(state, force, MaintenanceKnown(current.ReadHexForceSlot(hex), "occupation_ray_force"))) continue;
                bool nearerFirst = false;
                int candidateDistance = firstDistance;
                if (points.Count == 0)
                {
                    int distance = ArmyRouteSearch.Distance(start, physical);
                    if (distance > firstDistance) break;
                    nearerFirst = distance < firstDistance;
                    if (nearerFirst) candidateDistance = unchecked((byte)distance);
                }
                int previous = points.Count == 0 ? start : points[^1];
                points.Add(physical);
                covered = unchecked(covered + ReadOccupationFootprint(state, current, army, hex, force,
                    ReadOccupationWidth(state, current, army), excludeSideArmies: false, directionOverride: direction).Count);
                if (covered == 0 || !nearerFirst && covered < bestCount) continue;
                int? leg = ArmyRouteSearch.Distance(previous, physical) == 1
                    ? OccupationAdjacentCost(state, current, hex, wallPermission)
                    : ArmyRouteSearch.OccupationPathCost(state, current, army, previous, physical);
                if (leg is null) continue;
                accumulatedCost = unchecked(accumulatedCost + leg.Value);
                if (!nearerFirst && covered == bestCount &&
                    (accumulatedCost > bestCost || accumulatedCost == bestCost && physical >= bestGoal)) continue;
                bestCount = covered; bestCost = accumulatedCost; bestGoal = physical;
                firstDistance = candidateDistance; bestPoints = points.Take(5).ToArray();
            }
        }
        if (bestGoal != 48400) return (bestGoal, bestPoints);

        //No ray result: same current Area, nearest reachable eligible Hex;
        //ties use path cost, then physical identity. The output list stays empty.
        var area = OccupationArea(current, position);
        if (!MaintenanceKnown(current.ReadAreaNativeValidity(area), "occupation_fallback_Area_validity")) return (48400, []);
        int key = MaintenanceKnown(area.PhysicalSlot, "occupation_fallback_Area_identity");
        int nearest = 0, cost = 0;
        for (int h = AreaHexIterator.FindNextIndex(0, key, current); h < 48400;
            h = AreaHexIterator.FindNextIndex(h + 1, key, current))
        {
            var hex = OccupationHex(current, h);
            if (!OccupationCenterEligible(state, current, army, hex, force) ||
                !OccupationRelation(state, force, MaintenanceKnown(current.ReadHexForceSlot(hex), "occupation_fallback_force"))) continue;
            int physical = MaintenanceKnown(hex.PhysicalSlot, "occupation_fallback_Hex_identity");
            if (physical == start) continue;
            int distance = ArmyRouteSearch.Distance(start, physical);
            if (nearest > 0 && distance > nearest) continue;
            int? pathCost = ArmyRouteSearch.OccupationPathCost(state, current, army, start, physical);
            if (pathCost is not int candidateCost || distance == nearest &&
                (candidateCost > cost || candidateCost == cost && physical >= bestGoal)) continue;
            bestGoal = physical; nearest = distance; cost = candidateCost;
        }
        return (bestGoal, []);
    }

    private static bool OccupationCenterEligible(GameState state, SourceWorldContext current, UnitState army, HexReference hex, int force, bool requireNoArmy = false)
    {
        //2C6A10 receives22DEF0(candidate), then performs its own table lookup.
        var selected = OccupationHex(current, MaintenanceKnown(hex.PhysicalSlot, "occupation_center_identity"));
        if (selected.PhysicalSlot is null) return false;
        byte surface = MaintenanceKnown(current.ReadHexSurfaceCode(selected), "occupation_center_surface");
        var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0)
            ?? throw MaintenanceMissing("occupation_center_Terrain_pointer");
        if (terrain.PhysicalSlot is null) return false;
        if (terrain.PhysicalSlot == 24 && MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadNaval(state, army), "occupation_center_naval") is not (12 or 13 or 17 or 18))
            return false;
        return GateCaptureTerritorySource.Eligible(state, current, hex, force, requireNoArmy: requireNoArmy);
    }

    private static int OccupationAdjacentCost(GameState state, SourceWorldContext current, HexReference hex, bool wallPermission)
    {
        //The adjacent2AEB30 caller also reindexes the candidate identity.
        hex = OccupationHex(current, MaintenanceKnown(hex.PhysicalSlot, "occupation_cost_identity"));
        if (hex.PhysicalSlot is null) return 0;
        byte surface = MaintenanceKnown(current.ReadHexSurfaceCode(hex), "occupation_cost_surface");
        var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0)
            ?? throw MaintenanceMissing("occupation_cost_Terrain_pointer");
        if (terrain.PhysicalSlot is null) return 0;
        int cost = ArmyRouteTerrainCost.Read(state, current, terrain);
        if (wallPermission)
        {
            ushort wall = MaintenanceKnown(current.ReadCaptureHexWord16(hex.PhysicalSlot!.Value), "occupation_cost_wall");
            if (wall != 0)
            {
                int percent = MaintenanceKnown(TransportArmyRouteInputs.ReadCurrent(state, SimulationParameterKeys.TransportRoute.WallPenaltyPercent), "occupation_cost_wall_scale");
                cost = unchecked(cost + unchecked((int)((uint)wall * (uint)percent)) / 100);
            }
        }
        return cost;
    }

    private static bool IsOccupationCityArea(SourceWorldContext current, AreaReference area)
    {
        byte cityRaw = MaintenanceKnown(current.ReadAreaCitySlot(area), "occupation_city_Area35");
        var city = current.ReadCityTableReference(cityRaw <= 51 ? cityRaw : 0) ?? throw MaintenanceMissing("occupation_city_pointer");
        if (!MaintenanceKnown(current.ReadBaseNativeValidity(city), "occupation_city_validity")) return false;
        ushort link = MaintenanceKnown(current.ReadBaseObjectLink(city), "occupation_city_Object4E");
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw MaintenanceMissing("occupation_city_Object_pointer");
        int hex = MaintenanceKnown(current.ReadObjectNativeValidity(obj), "occupation_city_Object_validity")
            ? MaintenanceKnown(current.ReadObjectHexIndex(obj), "occupation_city_Object_hex") : 48400;
        return hex == MaintenanceKnown(current.ReadAreaHexIndex(area), "occupation_city_Area_hex");
    }

    private static HexReference OccupationHex(SourceWorldContext current, int raw)
        => current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw MaintenanceMissing("occupation_Hex_pointer");
    private static AreaReference OccupationArea(SourceWorldContext current, HexReference hex)
    {
        ushort raw = MaintenanceKnown(current.ReadHexAreaSlot(hex), "occupation_Area_raw");
        return current.ReadAreaTableReference(raw <= 500 ? raw : 0) ?? throw MaintenanceMissing("occupation_Area_pointer");
    }
    private static bool OccupationRelation(GameState state, int force, int other)
        => MaintenanceKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state, force, other), "occupation_relation");
}
