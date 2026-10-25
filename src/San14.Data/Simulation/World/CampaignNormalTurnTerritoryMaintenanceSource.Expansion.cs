using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class NormalTurnTerritoryMaintenanceSource
    {
        private void ExpandAdministration()
        {
            var seeds = current.CreateNearbyTemporary();
            HexListHandle? candidates = null;
            try
            {
                candidates = current.CreateNearbyTemporary();
                //25E520 clears the previous expansion map unconditionally.
                //This real constructor operation may establish known-empty
                //knowledge even when an older save had no expansion owner.
                var assignments = ImmutableDictionary.CreateBuilder<int, int>();
                Dictionary<int, TerritoryState>? territory = null;
                state = state with { TerritoryExpansion = new(assignments.ToImmutable()) };
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("expansion_player");
                var primary = ForcePrimaryDistrictSource.Resolve(current, player);
                var abilities = new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                    SimulationRuleIdentity.ForLocale(package.Seed.Language));
                //The selected result has no consumer, but an invalid advisor
                //really builds/sorts/releases its shared-pool temporary list.
                _ = new PersonDeathHistorySelection(state, current, pool, abilities, package._governorInputs,
                    (force, effect) => package._baseCapacityInputs.ReadPanelModifier(state, force, effect)).Select(primary);
                for (int slot = 0; slot < 501; slot++)
                {
                    var area = current.ReadAreaTableReference(slot) ?? throw Missing("expansion_Area_table");
                    if (!N(current.ReadAreaNativeValidity(area), "expansion_Area_validity")) continue;
                    int areaId = N(area.PhysicalSlot, "expansion_Area_physical");
                    int representative = N(current.ReadAreaHexIndex(area), "expansion_Area3A");
                    int person = Person(N(current.ReadAssignmentArea36(areaId), "expansion_Area36"));
                    var owner = Force(N(current.ReadAreaCurrentForceSlot(area), "expansion_Area_Force"));
                    int force = ForceId(owner);
                    bool assigned = N(current.ReadPersonNativeValidity(person), "expansion_assignee_validity") &&
                        N(current.ReadForceNativeValidity(owner), "expansion_Force_validity") && current.ReadPersonForce(person) == owner;
                    int occupyingArmy = unchecked((short)N(current.ReadHexRaw1C(Hex(representative)), "expansion_representative_Army"));
                    bool special = false;
                    if (!assigned && occupyingArmy is not (>= 1 and <= 500) && force is >= 46 and <= 50)
                    {
                        int ruler = Person(N(current.ReadForceRecord(force)?.RawRuler10, "expansion_special_ruler"));
                        special = N(current.ReadPersonFirstLocation(ruler), "expansion_special_home") is >= 1 and <= 61;
                    }
                    if (occupyingArmy is not (>= 1 and <= 500) && !assigned && !special) continue;
                    if (EnemyArmyInArea(areaId, force)) continue;
                    var preview = new AreaAdministrationPreviewSource(state, current, package._scenario,
                        package.Seed.Language, package._baseCapacityInputs, capacityInputsBound: true);
                    var extent = preview.Occupation(areaId, person, p =>
                        (current.ReadAssignmentPersonAreaVector(p) ?? throw Missing("expansion_Person1E8")).ActiveCount1E8);
                    if (!extent.Available || extent.Value is not int maximum)
                        throw Missing(extent.UnresolvedInput ?? "expansion_266390");
                    if (maximum == 0) continue;
                    current.ClearNearbyTemporary(seeds, false);
                    for (int hex = AreaHexIterator.FindNextIndex(0, areaId, current); hex < 48400;
                        hex = AreaHexIterator.FindNextIndex(hex + 1, areaId, current))
                        if (Force(N(current.ReadHexForceSlot(Hex(hex)), "expansion_seed_Hex14")) == owner)
                            current.AppendNearbyHexTemporary(seeds, Hex(hex));
                    if (!Hexes(seeds).Any()) continue;
                    current.ClearNearbyTemporary(candidates, false);
                    foreach (var seed in Hexes(seeds)) AddExpansionNeighbors(seed.Hex, owner, candidates);
                    var nodes = Hexes(candidates).ToList();
                    if (nodes.Count == 0) continue;
                    var distances = new Dictionary<int, int>();
                    //The native loop constructs/releases an empty Hex list
                    //for each distance-map entry before sorting the nodes.
                    foreach (var entry in nodes)
                    {
                        var unused = current.CreateNearbyTemporary();
                        try { distances[N(entry.Hex.PhysicalSlot, "expansion_candidate_Hex")] =
                            ArmyRouteSearch.Distance(representative, N(entry.Hex.PhysicalSlot, "expansion_distance_Hex")); }
                        finally { current.ClearNearbyTemporary(unused, true); }
                    }
                    nodes.Sort((a, b) =>
                    {
                        int first = N(a.Hex.PhysicalSlot, "expansion_sort_first"), second = N(b.Hex.PhysicalSlot, "expansion_sort_second");
                        int distance = distances[first].CompareTo(distances[second]);
                        return distance != 0 ? distance : first.CompareTo(second);
                    });
                    current.ReorderCurrentList(candidates, nodes.Select(x => x.Node).ToArray());
                    int count = 0;
                    foreach (var entry in nodes)
                    {
                        SceneryLossReport(entry.Hex, person, primary);
                        int physical = N(entry.Hex.PhysicalSlot, "expansion_selected_Hex");
                        current.AssignHexForce(entry.Hex, force);
                        assignments[physical] = force;
                        territory ??= new(state.Territory);
                        var before = territory.GetValueOrDefault(physical) ?? throw Missing("expansion_Territory_owner");
                        territory[physical] = before with { ForceId = force == 0 ? null : force };
                        if (++count >= maximum) break;
                    }
                    RewardAdministrator(person, count);
                }
                state = state with { TerritoryExpansion = new(assignments.ToImmutable()), Territory = territory ?? state.Territory };
            }
            finally
            {
                if (candidates is not null) current.ClearNearbyTemporary(candidates, true);
                current.ClearNearbyTemporary(seeds, true);
            }
        }

        private bool EnemyArmyInArea(int area, int force)
        {
            foreach (int physical in ArmyOrder())
            {
                var army = new ArmyUnitReference(physical);
                var at = Hex(N(current.ReadArmyUnitRawHex2A(army), "expansion_Army2A"));
                if (N(current.ReadHexAreaSlot(at), "expansion_Army_Area") != area) continue;
                if (N(ArmyLogisticsPreparation.ReadNativeRelationGate(state, current.ReadArmyUnitRawForceSlot(army), force), "expansion_enemy_relation")) return true;
            }
            return false;
        }

        private void AddExpansionNeighbors(HexReference seed, ForceReference force, HexListHandle output)
        {
            int rawArea = N(current.ReadHexAreaSlot(seed), "expansion_seed_Area12");
            var area = current.ReadAreaTableReference(rawArea <= 500 ? rawArea : 0) ?? throw Missing("expansion_seed_Area_pointer");
            if (!N(current.ReadAreaNativeValidity(area), "expansion_seed_Area_validity")) return;
            var nearby = current.CreateNearbyTemporary();
            try
            {
                NearbyArmyTemporarySource.FillNearbyHexTemporary(current, seed, 1, nearby);
                foreach (var entry in Hexes(nearby))
                {
                    var hex = entry.Hex;
                    int surface = N(current.ReadHexSurfaceCode(hex), "expansion_neighbor_surface");
                    var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0) ?? throw Missing("expansion_neighbor_terrain");
                    if (N(current.ReadSurfaceTypeRawField2A(terrain), "expansion_neighbor_Terrain2A") == 0) continue;
                    int nextArea = N(current.ReadHexAreaSlot(hex), "expansion_neighbor_Area");
                    if (current.ReadAreaTableReference(nextArea <= 500 ? nextArea : 0) != area) continue;
                    if (!N(ArmyLogisticsPreparation.ReadNativeRelationGate(state, ForceId(force),
                        N(current.ReadHexForceSlot(hex), "expansion_neighbor_Force")), "expansion_neighbor_relation")) continue;
                    int armySlot = unchecked((short)N(current.ReadHexRaw1C(hex), "expansion_neighbor_Army1C"));
                    if (armySlot is >= 1 and <= 500)
                    {
                        var army = current.ReadArmyUnitTableReference(armySlot) ?? throw Missing("expansion_neighbor_Army");
                        if (N(current.ReadArmyUnitNativeValidity(army), "expansion_neighbor_Army_validity") && current.ReadArmyUnitForceReference(army) != force) continue;
                    }
                    int objectId = N(current.ReadHexObjectLink(hex), "expansion_neighbor_Object1A");
                    if (objectId != 0)
                    {
                        var obj = current.ReadObjectTableReference(objectId <= 3000 ? objectId : 0) ?? throw Missing("expansion_neighbor_Object");
                        if (N(current.ReadObjectNativeValidity(obj), "expansion_neighbor_Object_validity"))
                        {
                            int kind = current.ReadPhysicalObject(N(obj.PhysicalSlot, "expansion_neighbor_Object_physical"))?.RawField12
                                ?? throw Missing("expansion_neighbor_Object12");
                            var definition = current.ReadObjectDefinitionReference(kind <= 30 ? kind : 0) ?? throw Missing("expansion_neighbor_Object_definition");
                            if (N(current.ReadObjectDefinitionValidity(definition), "expansion_neighbor_definition_validity") &&
                                N(current.ReadObjectDefinitionRaw2C(definition), "expansion_neighbor_definition2C") is not (3 or 4)) continue;
                        }
                    }
                    if (!Hexes(output).Any(existing => existing.Hex == hex)) current.AppendNearbyHexTemporary(output, hex);
                }
            }
            finally { current.ClearNearbyTemporary(nearby, true); }
        }

        private void SceneryLossReport(HexReference hex, int administrator, DistrictReference primary)
        {
            int raw = N(current.ReadHexScenerySlot(hex), "expansion_Scenery1E");
            var scenery = current.ReadSceneryTableReference(raw <= 100 ? raw : 0) ?? throw Missing("expansion_Scenery");
            if (!N(current.ReadSceneryNativeValidity(scenery), "expansion_Scenery_validity") ||
                (N(current.ReadSceneryFlags47(scenery), "expansion_Scenery47") & 1) == 0) return;
            if (package.TalentForceIsPlayerCurrent(state, current, Force(N(current.ReadHexForceSlot(hex), "expansion_old_Hex14")))) return;
            int area = N(current.ReadHexAreaSlot(hex), "expansion_log_Area");
            int rawCity = N(current.ReadAreaCitySlot(new(area)), "expansion_log_City35");
            var city = current.ReadCityTableReference(rawCity <= 51 ? rawCity : 0) ?? throw Missing("expansion_log_City");
            if (!N(current.ReadBaseNativeValidity(city), "expansion_log_City_validity") ||
                !package.TalentForceIsPlayerCurrent(state, current, Force(N(current.ReadBaseForceSlot(city), "expansion_log_City_Force")))) return;
            string name = current.SceneryDefinitions?.Sceneries[N(scenery.PhysicalSlot, "expansion_log_Scenery")].Name ?? throw Missing("expansion_log_Scenery_name");
            var text = package.FormatNormalTurnReport(state, current, 0x349E, new(String0: name));
            state = package.WriteHistory(text.State, current, new(5, N(primary.PhysicalSlot, "expansion_log_District"),
                N(hex.PhysicalSlot, "expansion_log_Hex"), Day(), text.Text, CampaignHistoryWriter.Supplemental(text.State),
                PersonId(administrator), MarkBit4: 1), effects);
        }

        private void RewardAdministrator(int person, int count)
        {
            if (count <= 0 || !N(current.ReadPersonNativeValidity(person), "expansion_reward_validity")) return;
            int first = N(current.ReadPersonFirstLocation(person), "expansion_reward_home");
            int force = ForceId(current.ReadPersonForce(person) ?? throw Missing("expansion_reward_Force"));
            if (first is >= 1 and <= 61 && (force is >= 46 and <= 50 || BattleRemakeCurrentRegionFactor.IsNativeBandPerson((ushort)PersonId(person)))) return;
            var army = PersonArmyLocationSource.ReadCommanderArmy(current, person) ?? throw Missing("expansion_reward_Army");
            bool qualifies = N(current.ReadArmyUnitNativeValidity(army), "expansion_reward_Army_validity");
            if (!qualifies)
            {
                var vector = current.ReadAssignmentPersonAreaVector(person) ?? throw Missing("expansion_reward_areas");
                for (int i = 0; i < vector.ActiveCount1E8; i++)
                {
                    if ((uint)i >= (uint)vector.Values.Length) throw Missing("expansion_reward_area_index");
                    int raw = vector.Values[i];
                    var area = current.ReadAreaTableReference(raw is >= 0 and <= 500 ? raw : 0) ?? throw Missing("expansion_reward_Area_pointer");
                    if (N(current.ReadAreaNativeValidity(area), "expansion_reward_Area_validity")) { qualifies = true; break; }
                }
            }
            if (!qualifies) return;
            var grown = package._growthSource.TryApplyWithinArmyOperation(state, current, package._growthQueries, PersonId(person), 4,
                unchecked(OccupationParameter(state, SimulationParameterKeys.ArmyMarch.OccupationGrowthScale) * count), 0);
            if (grown.Outcome == PersonGrowthOutcome.CurrentInputsUnresolved) throw Missing("expansion_reward_growth");
            state = grown.State;
            var merit = package._pendingMerit.TryApplyWithinArmyOperation(state, current, PersonId(person),
                unchecked(OccupationParameter(state, SimulationParameterKeys.ArmyMarch.OccupationMeritScale) * count * 10));
            if (merit.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved) throw Missing("expansion_reward_merit");
            state = merit.State;
        }
    }
}
