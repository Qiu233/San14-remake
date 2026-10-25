using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyNormalTurnMonthlyIncursionsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
        => new NormalTurnBanditIncursions(this, state, current, pool).Run();

    //4280E0→25C8E0. The enclosing normal-Turn table owns the calendar gate.
    //Candidates and their copies use the enclosing live pool; no world capture,
    //pool export, host answer or synthetic deployment is performed here.
    private sealed class NormalTurnBanditIncursions(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool)
    {
        private readonly List<CampaignEffectRequest> effects = [];
        private TacticSourceSamplerWorld? sampler;
        private NativeMapSeasonDate Date => state.NativeMapSeason?.ThirdDate ?? throw Missing("World34_date");

        internal GameState Run()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("shared_current_pool");
            if (N(package.ReadApplicationInformationRestriction(state), "Application_user_selection")) return state;
            if (N(package.ReadApplicationTutorialActive(), "Application_tutorial")) return state;
            if (ReadCommanderSelectionPeriod(state) <= P("start-period", 18)) return state;
            int free = 0;
            for (int identity = 4033; identity <= 4100; identity++)
            {
                var pointer = current.ReadPersonLookupReference(identity) ?? throw Missing("dynamic_Person_lookup");
                if (pointer.PhysicalSlot is not int physical) continue;
                if (!N(current.ReadPersonNativeValidity(physical), "dynamic_Person_validity") &&
                    N(current.ReadPersonIdentity(physical), "dynamic_Person_ID") != 0) free++;
            }
            if (free <= 30) return state;
            int active = 0;
            foreach (int army in Registry(RegistryKind.Army))
                if (ArmyForce(army) is >= 46 and <= 51) active++;
            if (active > P("existing-army-threshold", 7)) return state;
            var candidates = Temporary();
            HexListHandle? selected = null;
            try
            {
                FillCities(candidates);
                if (Values(candidates).Count == 0) return state;
                selected = Temporary();
                int selector = N(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_E58");
                int difficulty = N(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x134 : 0x28), "difficulty");
                int divisor = P(difficulty == 0 ? "easy-security-divisor" : difficulty == 1
                    ? "intermediate-security-divisor" : "advanced-security-divisor", difficulty == 0 ? 15 : difficulty == 1 ? 8 : 5);
                if (divisor == 0) throw Missing("security_divisor_zero");
                var scores = new Dictionary<int, int>();
                foreach (var entry in Values(candidates))
                {
                    int cityId = Physical(entry.Payload);
                    var city = City(cityId);
                    int minimum = int.MaxValue;
                    foreach (int area in CityAreas(city))
                    {
                        if (CityForce(city) != AreaForce(area) || AreaHasMarkedArmy(area)) continue;
                        if (N(current.ReadBaseNativeValidity(city), "candidate_City_validity") &&
                            CityForce(city) != 0 && CityHasTrait(city, 66)) continue;
                        minimum = Math.Min(minimum, Security(area));
                    }
                    int chance = minimum == int.MaxValue ? 0 : Math.Clamp((100 - minimum) / divisor, 2, 20);
                    if (CitySample(city) >= chance) continue;
                    Add(selected, RegistryKind.City, cityId);
                    scores[cityId] = chance;
                }
                if (scores.Count == 0) return state;
                var ordered = Values(selected).OrderByDescending(entry => scores[Physical(entry.Payload)])
                    .ThenBy(entry => Physical(entry.Payload)).ToArray();
                current.ReorderCurrentList(selected, ordered.Select(entry => entry.Node).ToArray());
                int maximum = Math.Min(2, scores.Count + 1), citiesCreated = 0;
                foreach (var entry in ordered)
                {
                    int cityId = Physical(entry.Payload);
                    var city = City(cityId);
                    var aggregate = Temporary();
                    try
                    {
                        foreach (int area in CityAreas(city))
                        {
                            var cells = Temporary();
                            try
                            {
                                var source = new StrategicIncitementArmyCurrent(package, state, current, area);
                                if (!source.PopulateSpawnCells(cells)) continue;
                                for (var node = current.ReadHexListHead(cells) ?? throw Missing("spawn_cells_head"); node.Identity.HasValue;
                                    node = current.ReadHexListNodeNext(node) ?? throw Missing("spawn_cells_next"))
                                {
                                    var hex = current.ReadHexListNodePayload(node) ?? throw Missing("spawn_cell_Hex");
                                    var copy = current.AllocateHexListNode(aggregate);
                                    if (copy.Identity is int identity)
                                        current.WriteCurrentListPayload(identity, N(hex.PhysicalSlot, "spawn_cell_pointer"));
                                }
                            }
                            finally { Release(cells); }
                        }
                        if (!ApplyCity(cityId, aggregate)) continue;
                        citiesCreated++;
                        if (citiesCreated >= maximum) break;
                    }
                    finally { Release(aggregate); }
                }
                if (effects.Count != 0)
                {
                    var pending = state.CampaignEffectRequests ?? throw Missing("pending_owner");
                    long revision = state.NativeClock?.UpdateOrdinal ?? throw Missing("source_revision");
                    state = state with { CampaignEffectRequests = pending.Append(CampaignEffectSource.MonthlyMaintenance, revision, effects) };
                }
                return state;
            }
            finally
            {
                if (selected is not null) Release(selected);
                Release(candidates);
            }
        }

        //2A8890 does not merely find a low-security Area. It also requires
        //seven unmarked Areas, actual successfully allocated spawn candidates,
        //the City's saved nearby-enemy gate, and no existing local band Army.
        private void FillCities(HexListHandle output)
        {
            foreach (int cityId in Registry(RegistryKind.City))
            {
                var city = City(cityId);
                int force = CityForce(city);
                if (force == 0 || force is >= 46 and <= 50 || NearbyEnemy(cityId)) continue;
                int unmarked = 0, spawnable = 0;
                foreach (int area in CityAreas(city))
                {
                    if (AreaHasMarkedArmy(area)) continue;
                    unmarked++;
                    if (AreaForce(area) != force || Security(area) >= P("candidate-security-threshold", 60)) continue;
                    var cells = Temporary();
                    try
                    {
                        if (new StrategicIncitementArmyCurrent(package, state, current, area).PopulateSpawnCells(cells)) spawnable++;
                    }
                    finally { Release(cells); }
                }
                if (unmarked <= 6 || spawnable == 0 || HasLocalSpecialArmy(cityId)) continue;
                Add(output, RegistryKind.City, cityId);
            }
        }

        //25D2B0 selects the first minimum-security actual owned Area. It repeats
        //the occupancy gate after earlier Cities' real Army registrations.
        private bool ApplyCity(int cityId, HexListHandle candidates)
        {
            var city = City(cityId);
            int? chosen = null;
            int minimum = int.MaxValue;
            foreach (int area in CityAreas(city))
            {
                if (CityForce(city) != AreaForce(area) || AreaHasMarkedArmy(area)) continue;
                int security = Security(area);
                if (security >= minimum) continue;
                minimum = security; chosen = area;
            }
            if (chosen is not int target || !N(current.ReadAreaNativeValidity(new(target)), "chosen_Area_validity")) return false;
            int level = minimum < 61 ? 3 : minimum < 71 ? 2 : 1;
            int district = NaturalDistrict(target), created = 0;
            for (int attempt = 0; attempt < level; attempt++)
            {
                var result = package.CreateSpecialIncursionArmyCurrent(state, current, target, level, candidates, district);
                state = result.State;
                if (!result.Created) break;
                created++;
            }
            if (created == 0) return false;
            state = package.ApplyNormalTurnIncursionReportCurrent(state, current, cityId, target, district, effects);
            return true;
        }

        //2A8E90 intentionally uses Area34, not the current parent City35.
        private int NaturalDistrict(int area)
        {
            var city = City(N(current.ReadAreaValidityCode(new(area)), "natural_Area34"));
            int cityId = N(city.PhysicalSlot, "natural_City_pointer");
            int province = N(package._growthQueries.ReadCityState(state, cityId), "natural_City_province");
            int district = province switch
            {
                1 or 3 => 46, 2 or 7 or 8 or 9 => 47, 10 or 11 => 48,
                12 or 13 => 49, 15 or 17 => 50, _ => 51
            };
            int bandForce = N(current.ReadDistrictForceId(district), "natural_District_Force");
            bandForce = bandForce < 52 ? bandForce : 0;
            int owner = CityForce(city);
            if (Relation(owner, district) >= P("friendship-threshold", 70) || Flags194(bandForce) != 0) return 51;
            if (district != 51 && CityHasTrait(city, 83 + district - 46)) return 51;
            return district;
        }

        private bool HasLocalSpecialArmy(int city)
        {
            //Original2C54B0 is completed before2C55C0; retain that lazy order
            //rather than reading an unrelated tribal Army's location first.
            return HasLocalArmy(city, false) || HasLocalArmy(city, true);
        }
        private bool HasLocalArmy(int city, bool tribal)
        {
            foreach (int physical in Registry(RegistryKind.Army))
            {
                var entry = new RegistryObjectReference(RegistryKind.Army, physical);
                if (N(current.ReadOwnedArmyExclusion233560(entry), "existing_Army_exclusion")) continue;
                int force = ArmyForce(physical);
                if (tribal ? force is not (>= 46 and <= 50) : force != 51) continue;
                ushort raw = N(current.ReadArmyUnitRawHex2A(new(physical)), "existing_Army2A");
                var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("existing_Army_Hex");
                int area = N(current.ReadHexAreaSlot(hex), "existing_Hex_Area");
                var pointer = current.ReadAreaTableReference(area < 501 ? area : 0) ?? throw Missing("existing_Area_pointer");
                if (N(current.ReadAreaNativeValidity(pointer), "existing_Area_validity") &&
                    N(current.ReadAreaCitySlot(pointer), "existing_Area35") == city) return true;
            }
            return false;
        }
        private bool AreaHasMarkedArmy(int area)
        {
            for (int index = AreaHexIterator.FindNextIndex(0, area, current); index < 48400;
                index = AreaHexIterator.FindNextIndex(index + 1, area, current))
            {
                var hex = current.ReadHexTableReference(index) ?? throw Missing("occupancy_Hex");
                int physical = unchecked((short)N(current.ReadHexRaw1C(hex), "occupancy_Hex1C"));
                if (physical is < 1 or > 500) continue;
                var army = current.ReadArmyUnitTableReference(physical) ?? throw Missing("occupancy_Army");
                if (!N(current.ReadArmyUnitNativeValidity(army), "occupancy_Army_validity")) continue;
                if ((N(NotificationArmy(state, N(army.PhysicalSlot, "occupancy_Army_pointer")).NativeTacticRuntime?.Flags26,
                    "occupancy_Army26") & 1) != 0) return true;
            }
            return false;
        }
        private bool CityHasTrait(BaseReference city, int trait)
        {
            int home = N(current.ReadBaseObjectLink(city), "trait_Base4E");
            if (home is < 1 or > 61) return false;
            foreach (int person in Registry(RegistryKind.Person))
            {
                if (N(current.ReadPersonStatus(person), "trait_Person_status") is < 1 or > 4 ||
                    N(current.ReadPersonFirstLocation(person), "trait_Person_home") != home ||
                    PersonCurrentBusySource.Read(current, person, Missing)) continue;
                if (N(TacticRemakeCurrentTraits.PersonContribution(current, person, trait), "trait_value") != 0) return true;
            }
            return false;
        }
        private int CitySample(BaseReference city)
        {
            int troops = N(current.ReadBaseResource(city, BaseResourceKind.Troops), "sample_Troops3C");
            int wounded = N(current.ReadArmyReturnBaseWounded40(city), "sample_Wounded40");
            int morale = N(current.ReadBaseMorale44(city), "sample_Morale44");
            int governor = N(current.ReadArmyReturnBaseGovernor2E(city), "sample_Governor2E");
            int gold = N(current.ReadBaseResource(city, BaseResourceKind.Gold), "sample_Gold");
            int food = N(current.ReadBaseResource(city, BaseResourceKind.Supplies), "sample_Food");
            int link = N(current.ReadBaseObjectLink(city), "sample_Base4E");
            var obj = current.ReadObjectTableReference(link < 3001 ? link : 0) ?? throw Missing("sample_Object_pointer");
            int durability = N(current.ReadObjectNativeValidity(obj), "sample_Object_validity")
                ? N(current.ReadObjectRawField14(obj), "sample_Object14") : 6000;
            sampler ??= package.ReadArmyDestructionSampler(state, current) ?? throw Missing("sample_World450_45C");
            return TacticEffectSourceSamplerMath.Sample(100, 1, unchecked((short)Date.Year + (sbyte)Date.Month),
                N(city.PhysicalSlot, "sample_City_ID"), governor, unchecked(troops + wounded + morale + food + gold + durability), sampler);
        }
        private int ArmyForce(int physical) => N(current.ReadForcePhysicalSlot(
            current.ReadArmyUnitForceReference(new(physical)) ?? throw Missing("Army_Force")), "Army_Force_pointer");
        private int CityForce(BaseReference city) => N(current.ReadBaseForceSlot(city), "City_Force");
        private int AreaForce(int area) => N(current.ReadAreaCurrentForceSlot(new(area)), "Area_Force");
        private int Security(int area) => state.Areas.GetValueOrDefault(area)?.Security ?? throw Missing("Area_security54");
        private bool NearbyEnemy(int city) => N(city == 0 ? state.UnassignedCityHasNearbyEnemyArmy
            : state.Cities.GetValueOrDefault(city)?.HasNearbyEnemyArmy, "City_nearby_enemy67");
        private byte Flags194(int force) => state.NativeForceUnregisterCurrent?.ForceResetImages.GetValueOrDefault(force) is { } reset
            ? N(reset[0x194], "reset_Force194") : N(state.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.Flags194, "Force194");
        private byte Relation(int force, int other)
        {
            if (state.NativeForceUnregisterCurrent?.ForceResetImages.GetValueOrDefault(force) is { } reset) return N(reset[0x13 + other], "reset_Force_relation");
            var values = force == 0 ? state.NativeForceUnregisterCurrent?.PlaceholderRelations13
                : state.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.NormalAftermath?.Relations13;
            return values is { Length: 52 } ? values[other] : throw Missing("Force_relation13");
        }
        private BaseReference City(int physical) => current.ReadCityTableReference(physical < 52 ? physical : 0) ?? throw Missing("City_pointer");
        private IEnumerable<int> CityAreas(BaseReference city)
        {
            int id = N(city.PhysicalSlot, "iterator_City_pointer");
            if (id is < 1 or > 51) yield break;
            foreach (int area in Registry(RegistryKind.Area))
                if (N(current.ReadAreaCitySlot(new(area)), "iterator_Area35") == id) yield return area;
        }
        private IEnumerable<int> Registry(RegistryKind kind)
        {
            foreach (var row in current.ReadRegistryReferences(kind) ?? throw Missing("registry_" + kind))
            {
                if (row is null || row.Kind != kind) throw Missing("registry_payload_" + kind);
                yield return Physical(row);
            }
        }
        private int P(string name, int original)
        {
            var bank = state.NativeArmySimulation?.Parameters ?? throw Missing("parameter_owner");
            string key = "simulation.bandit-incursion." + name;
            if (bank.Float32Values.ContainsKey(key)) throw Missing("parameter_type_" + name);
            if (bank.Int32Values.TryGetValue(key, out int saved)) return saved;
            //Matched TC immutable file-backed rule data, extracted offline.
            //These defaults are not observed BSS or a runtime EXE dependency.
            if (bank.Language != "TC") throw Missing("parameter_locale_" + name);
            return original;
        }
        private HexListHandle Temporary() => current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor");
        private void Release(HexListHandle list) { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        private void Add(HexListHandle list, RegistryKind kind, int physical)
        {
            var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(kind, physical));
        }
        private List<PersonFreedomHomeSource.Entry> Values(HexListHandle list)
        {
            var values = new List<PersonFreedomHomeSource.Entry>();
            for (var node = current.ReadHexListHead(list) ?? throw Missing("temporary_head"); node.Identity.HasValue;
                node = current.ReadHexListNodeNext(node) ?? throw Missing("temporary_next"))
                values.Add(new(node, current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("temporary_payload")));
            return values;
        }
        private static int Physical(RegistryObjectReference row) => N(row.PhysicalSlot, "registry_pointer");
        private static T N<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
        private static InvalidDataException Missing(string owner) => new("normal.turn.bandit-incursion." + owner);
    }
}
