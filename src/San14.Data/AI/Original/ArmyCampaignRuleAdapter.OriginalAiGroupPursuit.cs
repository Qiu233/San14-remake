using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // CD400 calls09E820(candidateEnemy,currentPursuer). The candidate's
    // possible advance is tested against the pursuer's route back to Home.
    internal bool ReadOriginalAiGroupPursuitAdmissionCurrent(GameState state, SourceWorldContext current,
        UnitState candidateEnemy, UnitState pursuingArmy, OriginalAiPursuitProfileScope profile) =>
        new OriginalAiGroupPursuitQuery(this, state, current, candidateEnemy, pursuingArmy, profile).Read();

    private sealed class OriginalAiGroupPursuitQuery
    {
        private const int HexCount = 48400;
        private readonly GameState state;
        private readonly SourceWorldContext current;
        private readonly UnitState enemy, pursuer;
        private readonly ArmyUnitReference enemyReference, pursuerReference;
        private readonly ArmyMapGroupingCurrentQueries grouping;
        private readonly ArmyMobilityCurrentProducer mobility;
        private readonly ArmyMarchPenaltyCurrentProducer penalty;
        private readonly IHexOwnershipState ownership;

        internal OriginalAiGroupPursuitQuery(ArmyCampaignRuleAdapter package, GameState state,
            SourceWorldContext current, UnitState enemy, UnitState pursuer, OriginalAiPursuitProfileScope profile)
        {
            this.state = state; this.current = current;
            this.enemy = enemy; this.pursuer = pursuer;
            enemyReference = new(K(enemy.NativeArmyOrigin?.NativeSlotIndex, "enemy_receiver"));
            pursuerReference = new(K(pursuer.NativeArmyOrigin?.NativeSlotIndex, "pursuer_receiver"));
            grouping = new(state, current, package._scenario, package._governorInputs,
                package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language));
            mobility = new(package, package._mobilitySeed, package._map, package._terrain, package._catalog);
            penalty = new(package, package._map, package._terrain, package._catalog, package._marchPenaltySeed);
            ownership = package.BindOriginalAiPursuitHexOwnershipCurrent(state, current, profile);
        }

        internal bool Read()
        {
            int center = K(current.ReadArmyUnitRawHex2A(enemyReference), "enemy_Hex2A");
            int formation = EffectiveFormation(center);
            int truncatedMobility = NativeInt(ReadUnmodifiedMobility(formation));
            int radius = unchecked(truncatedMobility * 3) / 4;
            if (radius < 0) throw Missing("negative_ring_radius");
            var ring = current.CreateNearbyTemporary();
            try
            {
                foreach (int key in OuterRing(center, radius)) current.AppendNearbyHexTemporary(ring, Hex(key));
                var pursued = current.CreateNearbyTemporary();
                try
                {
                    current.AppendNearbyArmyTemporary(pursued, pursuerReference);
                    var visited = current.CreateNearbyTemporary();
                    try
                    {
                        foreach (var edge in ReadHexes(ring))
                        {
                            var line = current.CreateNearbyTemporary();
                            try
                            {
                                foreach (int key in OriginalAiHexLineSource.EnumerateCandidates(center, Physical(edge.Hex)))
                                    AppendUnique(line, Hex(key));
                                Remove(line, center);
                                var points = ReadHexes(line);
                                bool passable = true;
                                for (int i = 1; i < points.Count; i++)
                                    if (!CanCrossStraightLine(Physical(points[i].Hex))) { passable = false; break; }
                                if (!passable) continue;
                                var samples = current.CreateNearbyTemporary();
                                try
                                {
                                    if (points.Count < 6)
                                        foreach (var point in points) current.AppendNearbyHexTemporary(samples, point.Hex);
                                    else
                                    {
                                        int quantile = 1;
                                        for (int i = 0; i < points.Count; i++)
                                            if (i + 1 == (long)points.Count * quantile / 5)
                                            { current.AppendNearbyHexTemporary(samples, points[i].Hex); quantile++; }
                                    }
                                    var route = current.CreateNearbyTemporary();
                                    try
                                    {
                                        int? reached = ForecastAdvance(samples, edge.Hex, route);
                                        if (reached is not int destination) continue;
                                        if (destination != Physical(edge.Hex) && Contains(visited, destination)) continue;
                                        current.AppendNearbyHexTemporary(visited, Hex(destination));
                                        if (HasHostileRouteNeighbor(route)) continue;
                                        if (CutsPursuerHomeRoute(route, pursued)) return true;
                                    }
                                    finally { current.ClearNearbyTemporary(route, true); }
                                }
                                finally { current.ClearNearbyTemporary(samples, true); }
                            }
                            finally { current.ClearNearbyTemporary(line, true); }
                        }
                        return false;
                    }
                    finally { current.ClearNearbyTemporary(visited, true); }
                }
                finally { current.ClearNearbyTemporary(pursued, true); }
            }
            finally { current.ClearNearbyTemporary(ring, true); }
        }

        // CCA80 builds the candidate's real multi-segment route and predicts
        // at most120 source movement updates. Its caller uses the reached Hex
        // even when the prediction did not finish the entire candidate route.
        private int? ForecastAdvance(HexListHandle samples, HexReference final, HexListHandle output)
        {
            current.ClearNearbyTemporary(output, false);
            int start = K(current.ReadArmyUnitRawHex2A(enemyReference), "forecast_Hex2A");
            int segmentStart = start;
            var joined = current.CreateNearbyTemporary();
            try
            {
                int limit = NativeInt(ReadUnmodifiedMobility(EffectiveFormation(start)));
                current.AppendNearbyHexTemporary(samples, final);
                var targets = ReadHexes(samples);
                int targetIndex = 0;
                for (int segment = 0; segment < targets.Count; segment++)
                {
                    int goal = Physical(targets[targetIndex].Hex);
                    // The source's default search descriptor owns one empty
                    // temporary list independently of its returned path vector.
                    var searchTemporary = current.CreateNearbyTemporary();
                    try
                    {
                        var target = OrdinaryArmyRouteSource.ReadEffectiveSearchTarget(state, enemy)
                            ?? throw Missing("forecast_search_target");
                        int[] path = ArmyRouteSearch.Search(state, current, enemy, segmentStart, goal,
                            target.Kind, target.Identity, enemyReference.PhysicalSlot, out _, searchFlags: 1);
                        if (path.Length == 0) continue;
                        foreach (int key in path)
                        {
                            AppendUnique(joined, Hex(key));
                            if (limit < Count(joined)) break;
                        }
                        segmentStart = goal;
                        targetIndex++;
                    }
                    finally { current.ClearNearbyTemporary(searchTemporary, true); }
                }
                Remove(joined, start);
                var pathHexes = ReadHexes(joined);
                if (pathHexes.Count == 0) return null;
                int land = K(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, enemy), "forecast_land1B");
                int naval = K(ArmyConfigurationSourceAdapter.ReadNaval(state, enemy), "forecast_naval1C");
                float landMobility = ModifiedMobility(land), navalMobility = ModifiedMobility(naval);
                // Preserve the source order: both mobility reads precede the
                // naval penalty read and then the independent land penalty read.
                int navalSpeed = CycleSpeed(navalMobility, start), landSpeed = CycleSpeed(landMobility, start);
                int credit = K(ArmyMarchSourceAdapter.EncodeCredit(enemy.March), "forecast_credit14");
                int previous = start, nextIndex = 0;
                int? reached = null;
                for (int update = 0; update < 120; update++)
                {
                    int next = Physical(pathHexes[nextIndex].Hex);
                    credit = unchecked(credit + (IsWater(previous) ? navalSpeed : landSpeed));
                    int cost = TerrainCost(next);
                    if (cost >= credit) continue; // Source comparison is strict.
                    AppendUnique(output, pathHexes[nextIndex].Hex);
                    reached = next;
                    credit = unchecked(credit - cost);
                    nextIndex++;
                    if (nextIndex == pathHexes.Count) return reached;
                    // The source reads formation/mobility at the actual Army
                    // position; only the march-penalty Hex is overridden.
                    if (IsWater(next)) navalSpeed = CycleSpeed(ModifiedMobility(naval), next);
                    else landSpeed = CycleSpeed(ModifiedMobility(land), next);
                    previous = next;
                }
                return reached;
            }
            finally { current.ClearNearbyTemporary(joined, true); }
        }

        private float ReadUnmodifiedMobility(int formation)
        {
            int commanderId = K(current.ReadArmyUnitCommanderId(enemyReference), "mobility_commander12");
            int commander = K((current.ReadPersonLookupReference(commanderId <= 6000 ? commanderId : 0)
                ?? throw Missing("mobility_Person_pointer")).PhysicalSlot, "mobility_Person");
            int force = Force(enemyReference);
            byte status = K(current.ReadArmyReturnUnitStatus10(enemyReference), "mobility_status10");
            var source = ArmyRouteMobilityEvaluation.Read(state, current,
                new(commander, force, status, K(current.ReadArmyUnitRawHex2A(enemyReference), "mobility_Hex2A"),
                    K(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, enemy), "mobility_land1B"),
                    K(ArmyConfigurationSourceAdapter.ReadNaval(state, enemy), "mobility_naval1C"), enemy.Troops,
                    false, () => ArmySavedCommandKnowledge.ReadControl(state, enemy), enemyReference.PhysicalSlot),
                formation, null, () => ArmyConfigurationSourceAdapter.ReadCurrentLand(state, enemy));
            return source.Value ?? throw Missing(source.UnresolvedOwner ?? "mobility_flags0");
        }

        private float ModifiedMobility(int formation)
        {
            var moving = mobility.ResolveCurrent(state, enemy, current, formation);
            return moving.Receipt?.Value ?? throw Missing(moving.UnresolvedOwner ?? "mobility_flags1");
        }

        private int CycleSpeed(float value, int penaltyHex)
        {
            var nearby = penalty.ResolveCurrent(state, enemy, current, penaltyHex);
            int penaltyValue = nearby.Receipt?.Value ?? throw Missing(nearby.UnresolvedOwner ?? "march_penalty");
            var bank = state.NativeArmySimulation?.Parameters ?? throw Missing("speed_parameter_bank");
            int scale = bank.Int32Values.TryGetValue(SimulationParameterKeys.ArmyMarch.SpeedScale, out int scalar)
                ? scalar : throw Missing("speed_scale");
            return ArmyMarchSourceMath.ResolveCycleSpeed(value, scale, penaltyValue)
                ?? throw Missing("forecast_cycle_speed");
        }

        private bool HasHostileRouteNeighbor(HexListHandle route)
        {
            bool blocked = false;
            foreach (var point in ReadHexes(route))
            {
                var neighbors = current.CreateNearbyTemporary();
                try
                {
                    var ring = current.CreateNearbyTemporary();
                    try
                    {
                        NearbyArmyTemporarySource.FillNearbyHexTemporary(current, point.Hex, 1, ring);
                        foreach (var candidate in ReadHexes(ring))
                        {
                            if (Physical(candidate.Hex) == Physical(point.Hex)) continue;
                            int marker = unchecked((short)K(current.ReadHexRaw1C(candidate.Hex), "neighbor_Hex1C"));
                            if (marker is < 1 or > 500) continue;
                            var receiver = current.ReadArmyUnitTableReference(marker) ?? throw Missing("neighbor_Army_pointer");
                            if (K(current.ReadArmyUnitNativeValidity(receiver), "neighbor_Army_validity"))
                                current.AppendNearbyArmyTemporary(neighbors, receiver);
                        }
                    }
                    finally { current.ClearNearbyTemporary(ring, true); }
                    foreach (int physical in ReadArmies(neighbors))
                    {
                        if (K(grouping.IsAtHome2C96E0(physical), "neighbor_at_Home") ||
                            K(grouping.IsExcluded233560(physical), "neighbor_excluded")) continue;
                        // The nearby actor's Force is the first2CA650 operand.
                        if (!K(ArmyLogisticsPreparation.ReadNativeRelationGate(state,
                            Force(new(physical)), Force(enemyReference)), "neighbor_relation")) continue;
                        blocked = true;
                        break;
                    }
                }
                finally { current.ClearNearbyTemporary(neighbors, true); }
            }
            return blocked;
        }

        // C0930 temporarily paints the proposed advance with the candidate's
        // Force, tests each real Army's route to its current Home, and restores
        // every original owner. Its source-time local profile writes survive.
        private bool CutsPursuerHomeRoute(HexListHandle route, HexListHandle armies)
        {
            var points = ReadHexes(route);
            if (points.Count == 0) return false;
            var formerForces = current.CreateNearbyTemporary();
            try
            {
                var originals = new List<(HexReference Hex, int Force)>();
                int enemyForce = Force(enemyReference);
                try
                {
                    foreach (var point in points)
                    {
                        byte raw = K(current.ReadHexForceSlot(point.Hex), "paint_Hex14");
                        var old = current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("paint_old_Force");
                        int restored = K(old.PhysicalSlot, "paint_old_Force_identity");
                        if (restored != raw) throw Missing("paint_noncanonical_restore_requires_World_writer");
                        current.AppendPursuitForceTemporary(formerForces, old);
                        originals.Add((point.Hex, restored));
                        HexOwnership.Apply(point.Hex, enemyForce, ownership);
                    }
                    foreach (int physical in ReadArmies(armies))
                    {
                        var receiver = new ArmyUnitReference(physical);
                        int start = K(current.ReadArmyUnitRawHex2A(receiver), "home_route_start");
                        int goal = HomeHex(receiver);
                        if (start == HexCount || goal == HexCount) continue;
                        var searchTemporary = current.CreateNearbyTemporary();
                        try
                        {
                            var army = physical == pursuerReference.PhysicalSlot ? pursuer : FindArmy(physical);
                            int[] path = start >= HexCount ? [] : ArmyRouteSearch.Search(state, current, army, start, goal,
                                0, 0, physical, out _, searchFlags: 8,
                                readHexForce: key => current.ReadHexForceSlot(Hex(key)));
                            if (path.Length == 0 && K(current.ReadArmyUnitNativeValidity(receiver), "cut_Army_validity")) return true;
                        }
                        finally { current.ClearNearbyTemporary(searchTemporary, true); }
                    }
                    return false;
                }
                finally
                {
                    // Restore in the original forward order. The restoration's
                    // own local profile calls are real source effects as well.
                    foreach (var previous in originals) HexOwnership.Apply(previous.Hex, previous.Force, ownership);
                }
            }
            finally { current.ClearNearbyTemporary(formerForces, true); }
        }

        private int HomeHex(ArmyUnitReference army)
        {
            var home = current.FindArmyUnitCurrentBase(army);
            if (!K(current.ReadBaseNativeValidity(home), "home_Base_validity")) return HexCount;
            ushort link = K(current.ReadBaseObjectLink(home), "home_Base4E");
            var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw Missing("home_Object_pointer");
            return K(current.ReadObjectNativeValidity(obj), "home_Object_validity")
                ? K(current.ReadObjectHexIndex(obj), "home_Object10") : HexCount;
        }

        private bool CanCrossStraightLine(int key)
        {
            var hex = Hex(key);
            byte surface = K(current.ReadHexSurfaceCode(hex), "line_Hex10");
            var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0) ?? throw Missing("line_Terrain_pointer");
            int terrainPhysical = K(terrain.PhysicalSlot, "line_Terrain");
            int naval = K(ArmyConfigurationSourceAdapter.ReadNaval(state, enemy), "line_naval1C");
            if (terrainPhysical == 24 && naval is not (12 or 13 or 17 or 18)) return false;
            if (unchecked((short)K(current.ReadSurfaceTypeRawField2A(terrain), "line_Terrain2A")) == 0) return false;
            int marker = unchecked((short)K(current.ReadHexRaw1C(hex), "line_Hex1C"));
            if (marker is >= 1 and <= 500) return false;
            ushort link = K(current.ReadHexObjectLink(hex), "line_Hex1A");
            var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw Missing("line_Object_pointer");
            if (obj.PhysicalSlot is >= 52 and <= 61)
                return K(current.ReadObjectHexIndex(obj), "line_Gate10") == key &&
                    !K(ArmyLogisticsPreparation.ReadNativeRelationGate(state, Force(enemyReference),
                        K(current.ReadHexForceSlot(hex), "line_Hex14")), "line_Gate_relation");
            var data = current.ReadPhysicalObject(K(obj.PhysicalSlot, "line_Object")) ?? throw Missing("line_Object_fields");
            var definition = current.ReadObjectDefinitionReference(data.RawField12 <= 30 ? data.RawField12 : 0)
                ?? throw Missing("line_Object_definition");
            return !K(current.ReadObjectDefinitionValidity(definition), "line_definition_validity") ||
                K(current.ReadObjectDefinitionRaw2C(definition), "line_definition2C") == 4;
        }

        private int EffectiveFormation(int key) => IsWater(key)
            ? K(ArmyConfigurationSourceAdapter.ReadNaval(state, enemy), "effective_naval1C")
            : K(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, enemy), "effective_land1B");
        private bool IsWater(int key)
        {
            byte raw = K(current.ReadHexSurfaceCode(Hex(key)), "Terrain_Hex10");
            return (K(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, raw <= 30 ? raw : 0), "Terrain_flags2C") & 2) != 0;
        }
        private int TerrainCost(int key)
        {
            byte raw = K(current.ReadHexSurfaceCode(Hex(key)), "cost_Hex10");
            var terrain = current.ReadSurfaceTypeTableReference(raw <= 30 ? raw : 0) ?? throw Missing("cost_Terrain_pointer");
            return ArmyRouteTerrainCost.Read(state, current, terrain);
        }
        private int Force(ArmyUnitReference army) => K(grouping.ReadForce(K(army.PhysicalSlot, "Force_Army"))?.PhysicalSlot, "Army_Force");
        private UnitState FindArmy(int physical)
        {
            var arena = state.ReadArmyLifecycle() ?? throw Missing("Army_arena");
            if ((uint)physical >= (uint)arena.ReceiverSlots().Length) throw Missing("Army_receiver_index");
            var slot = arena.ReceiverSlots()[physical];
            return slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) ?? throw Missing("Army_projection")
                : slot.PendingImage ?? throw Missing("Army_retained_image");
        }
        private HexReference Hex(int key) => current.ReadHexTableReference((uint)key < HexCount ? key : 0) ?? throw Missing("Hex_pointer");
        private int Physical(HexReference hex) => K(hex.PhysicalSlot, "Hex_identity");
        private int Count(HexListHandle list) => K(current.ReadTemporaryHexListCount(list), "Hex_list_count");
        private bool Contains(HexListHandle list, int key) => ReadHexes(list).Any(row => Physical(row.Hex) == key);
        private void AppendUnique(HexListHandle list, HexReference hex)
        { if (!Contains(list, Physical(hex))) current.AppendNearbyHexTemporary(list, hex); }
        private void Remove(HexListHandle list, int key)
        {
            foreach (var row in ReadHexes(list))
                if (Physical(row.Hex) == key) current.RemoveFacilityHexTemporary(list, row.Node);
        }
        private List<(HexListNodeReference Node, HexReference Hex)> ReadHexes(HexListHandle list)
        {
            var result = new List<(HexListNodeReference, HexReference)>();
            var node = current.ReadHexListHead(list) ?? throw Missing("Hex_list_head");
            while (node.Identity.HasValue)
            {
                result.Add((node, current.ReadHexListNodePayload(node) ?? throw Missing("Hex_list_payload")));
                node = current.ReadHexListNodeNext(node) ?? throw Missing("Hex_list_next");
            }
            return result;
        }
        private IEnumerable<int> ReadArmies(HexListHandle list)
        {
            foreach (var value in current.ReadCurrentListValues(list))
                yield return value is RegistryObjectReference { Kind: RegistryKind.Army, PhysicalSlot: int physical }
                    ? physical : throw Missing("Army_list_payload");
        }
        private static IEnumerable<int> OuterRing(int center, int radius)
        {
            if (radius == 0) yield break;
            if (radius <= 255)
            {
                foreach (int key in TacticRemakeCurrentContext.NativeHexOrder(center, radius).Skip(1))
                    if (ArmyRouteSearch.Distance(center, key) == radius) yield return key;
                yield break;
            }
            (int X, int Y)[][] directions =
            [ [(-1,0),(0,-1),(1,0),(-1,1),(0,1),(1,1)], [(-1,-1),(0,-1),(1,-1),(-1,0),(0,1),(1,0)] ];
            int x = center % 220, y = center / 220;
            for (int step = 0; step < radius; step++) Step(4);
            foreach (int direction in new[] { 2, 3, 6, 5, 4, 1 })
                for (int step = 0; step < radius; step++)
                { Step(direction); if ((uint)x < 220 && (uint)y < 220) yield return y * 220 + x; }
            void Step(int direction) { var delta = directions[Math.Abs(x) % 2][direction - 1]; x += delta.X; y += delta.Y; }
        }
        private static int NativeInt(float value) => value is >= int.MinValue and < 2147483648f ? (int)value : int.MinValue;
        private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("original-ai.group-pursuit." + field);
    }
}

public sealed partial class SourceWorldContext
{
    internal void AppendPursuitForceTemporary(HexListHandle list, ForceReference force)
    {
        ConditionalRegistryEnter();
        try
        {
            var node = AllocateRegistryNode(new(list.Identity))
                ?? throw new InvalidDataException("original-ai.group-pursuit.Force_node_allocator");
            if (node.Identity.HasValue) WriteRegistryNodePayload(node, new(RegistryKind.Force, force.PhysicalSlot));
        }
        finally { ConditionalRegistryExit(); }
    }
}
