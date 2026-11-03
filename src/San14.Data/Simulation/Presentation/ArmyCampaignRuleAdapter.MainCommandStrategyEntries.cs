using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //8929B0 uses these seven admission/help producers, before any page is
    //constructed. Poison is kind14; kind13 is the Area pacification order.
    private MapMainCommandEntryPresentation ReadStrategyCommandEntry(GameState state,
        SourceWorldContext current, MapMenuCommand command, bool? restricted, Action bindCurrent)
    {
        int kind = command switch { MapMenuCommand.ProvokeWar => 10, MapMenuCommand.Incite => 11,
            MapMenuCommand.Alienate => 12, MapMenuCommand.PlacateArea => 13, MapMenuCommand.Poison => 14,
            MapMenuCommand.Sabotage => 20, MapMenuCommand.Rumor => 24,
            _ => throw new ArgumentOutOfRangeException(nameof(command)) };
        try
        {
            if (kind is not (13 or 20))
            {
                if (restricted is null) throw MainCommandMissing("strategy_application_selection");
                if (restricted.Value) return new(command, false, 0x11171);
            }
            var player = current.CurrentPlayerForce ?? throw MainCommandMissing("strategy_player");
            var district = ForcePrimaryDistrictSource.Resolve(current, player);
            int districtId = MainNeed(district.PhysicalSlot, "strategy_district");
            int books = MainNeed(current.ReadDistrictRawByte(districtId, 0x14), "strategy_books");
            if (books < StrategicParameter(state, $"kind-{kind}.command-books", 1)) return new(command, false, 0x57C7);
            int gold = BaseCurrencyAdditionSource.ReadCurrentGold(ReadPersonnelHireTreasuryCurrent(current, districtId), current);
            //6DDA00 receives the actual District Force pointer. Do not look
            //its physical ordinal up again and apply a second table alias.
            ForceReference? force = null;
            int cost = ReadStrategicBaseGoldCurrent(state, kind);
            if (kind != 24)
            {
                force = PersonnelMenuDistrictForce(current, districtId);
                var advantage = PersonReturnPathSource.ReadForceAdvantage(state, current, force, 11, 100);
                int multiplier = advantage.Value ?? throw MainCommandMissing(advantage.UnresolvedOwner ?? "strategy_advantage11");
                int denominator = StrategicParameter(state, "gold-cost-percent-denominator", 100);
                if (denominator == 0) throw MainCommandMissing("strategy_cost_denominator");
                cost = unchecked(multiplier * cost) / denominator;
            }
            if (gold < cost) return new(command, false, 0x5A1E);
            if (kind == 24 && ReadDiplomacyDistrictBudget(current, districtId) < 0) return new(command, false, 0x5C1D);
            bindCurrent();
            int minimum = kind == 24 ? StrategicParameter(state, "rumor.minimum-executors", 1) : 1;
            if (!MainStrategyHasExecutors(current, districtId, minimum)) return new(command, false, 0x5A1D);
            force ??= PersonnelMenuDistrictForce(current, districtId);
            int forceId = MainNeed(force.PhysicalSlot, "strategy_district_force");
            bool targets = kind is 12 or 24
                ? new StrategicPersonTargetsCurrent(this, state, current, forceId).HasMenuTarget(kind,
                    kind == 24 ? StrategicParameter(state, "rumor.minimum-targets", 1) : 1,
                    (first, second) => MainStrategyHasTreaty(state, current, first, second))
                : new MainStrategyTerritoryTargets(this, state, current, force).HasTarget(kind);
            int message = kind switch { 10 => targets ? 0x5A28 : 0x5A27, 11 => targets ? 0x5A26 : 0x5A25,
                12 => targets ? 0x5A22 : 0x5A21, 13 => targets ? 0x5A24 : 0x5A23,
                14 => targets ? 0x5A20 : 0x5A1F, 20 => targets ? 0x5A2A : 0x5A29,
                _ => targets ? 0x5C20 : 0x5C1F };
            return new(command, targets, message);
        }
        catch (InvalidDataException error) { return new(command, null, null, error.Message); }
    }

    private static bool MainStrategyHasExecutors(SourceWorldContext current, int districtId, int minimum)
    {
        int count = 0;
        foreach (int person in PersonnelMenuServingPeople(current, 0, districtId, false))
        {
            if (MainNeed(OfficerHealthSourceAdapter.ReadByte(current, person), "strategy_executor_health") >= 2 ||
                (MainNeed(current.ReadArmyReturnPersonFlags196(person), "strategy_executor_flags") & 1) != 0 ||
                PersonCurrentBusySource.Read(current, person, MainCommandMissing)) continue;
            int owner = MainNeed((current.ReadPersonForce(person) ?? throw MainCommandMissing("strategy_executor_force")).PhysicalSlot,
                "strategy_executor_force_identity");
            if (owner is >= 46 and <= 50 || DuelAftermathSpecialPerson(current, person)) continue;
            //6BE260 compares only after a qualifying receiver, even for a
            //saved nonpositive threshold.710BD0 is the same existential leaf.
            if (++count >= minimum) return true;
        }
        return false;
    }

    private static byte MainStrategyForceByte(GameState state, ForceReference force, int offset)
    {
        int id = MainNeed(force.PhysicalSlot, "strategy_force_byte_receiver");
        if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(id, out var image) == true)
            return MainNeed(image[offset], "strategy_force_reset_byte");
        var fields = state.NativeArmySimulation?.Forces.GetValueOrDefault(id) ?? throw MainCommandMissing("strategy_force_fields");
        return MainNeed(offset == 0x12 ? fields.Flags12 : fields.Flags194, "strategy_force_byte");
    }

    private static byte MainStrategyRelation(GameState state, ForceReference first, int second)
    {
        int id = MainNeed(first.PhysicalSlot, "strategy_relation_receiver");
        if ((uint)second >= 52) throw MainCommandMissing("strategy_relation_index");
        if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(id, out var image) == true)
            return MainNeed(image[0xEA + second], "strategy_relation_reset");
        return ReadReportForceRelationByte(state, first, second, MainCommandMissing);
    }

    //2C3CA0's operands are already physical receivers. Unlike a page's ID
    //overload, this must not table-lookup C0 or Person-owned pointers again.
    private static bool MainStrategyHasTreaty(GameState state, SourceWorldContext current,
        ForceReference first, ForceReference second)
    {
        if (!MainNeed(current.ReadForceNativeValidity(first), "strategy_treaty_first_validity") ||
            !MainNeed(current.ReadForceNativeValidity(second), "strategy_treaty_second_validity")) return false;
        if (first == second) return true;
        int a = MainNeed(first.PhysicalSlot, "strategy_treaty_first");
        int b = MainNeed(second.PhysicalSlot, "strategy_treaty_second");
        int selected = MainNeed(state.NativeArmySimulation?.World.SelectedForce1656, "strategy_selected_force");
        if (selected == a && (MainStrategyForceByte(state, second, 0x12) & 2) != 0 ||
            (MainStrategyForceByte(state, first, 0x12) & 2) != 0 && selected == b) return false;
        return (MainStrategyRelation(state, first, b) & 127) != 0 ||
            (MainStrategyForceByte(state, first, 0x12) & 2) != 0 && (MainStrategyForceByte(state, second, 0x12) & 2) != 0 ||
            MainStrategyForceByte(state, first, 0x194) == b || MainStrategyForceByte(state, second, 0x194) == a;
    }

    private static IEnumerable<RegistryObjectReference> MainStrategyList(SourceWorldContext current, HexListHandle list)
    {
        var node = current.ReadRegistryHead(new(list.Identity)) ?? throw MainCommandMissing("strategy_list_head");
        while (node.Identity is not null)
        {
            yield return current.ReadRegistryNodePayload(node) ?? throw MainCommandMissing("strategy_list_payload");
            node = current.ReadRegistryNodeNext(node) ?? throw MainCommandMissing("strategy_list_next");
        }
    }

    private sealed class MainStrategyTerritoryTargets(ArmyCampaignRuleAdapter package, GameState state,
        SourceWorldContext current, ForceReference force)
    {
        private readonly int own = MainNeed(force.PhysicalSlot, "strategy_target_force");
        private PersonFreedomHomeSource? neighborhoods;
        private BaseReference City(int raw) => current.ReadCityTableReference(raw < 52 ? raw : 0)
            ?? throw MainCommandMissing("strategy_target_city");
        private ForceReference Force(int raw) => current.ReadForceTableReference(raw < 52 ? raw : 0)
            ?? throw MainCommandMissing("strategy_target_force_pointer");
        private int CityOwner(BaseReference city) => MainNeed(current.ReadBaseForceSlot(city), "strategy_target_city_owner");
        private ForceReference AreaForce(AreaReference area) => Force(MainNeed(current.ReadAreaCurrentForceSlot(area), "strategy_target_area_owner"));
        private bool Unprotected(ForceReference target) => (MainStrategyRelation(state, target, own) & 127) == 0 &&
            ((MainStrategyForceByte(state, force, 0x12) & 2) == 0 || (MainStrategyForceByte(state, target, 0x12) & 2) == 0);
        private void FillNeighbors(BaseReference city, HexListHandle list) => (neighborhoods ??= new(package._scenario)).FillCities22F430(current, city, list);
        private static BaseReference ListedCity(RegistryObjectReference entry) => entry.Kind == RegistryKind.City
            ? new(BaseKind.City, MainNeed(entry.PhysicalSlot, "strategy_neighbor_city")) : throw MainCommandMissing("strategy_neighbor_kind");

        internal bool HasTarget(int kind)
        {
            if (kind == 10) return HasForces();
            if (kind == 11) return HasCities();
            if (kind == 20) return HasObjects();
            if (kind == 13) return HasPacificationArea();
            foreach (var area in current.ReadRegisteredAreaReferences() ?? throw MainCommandMissing("strategy_area_registry"))
                if (CanPoison(area ?? throw MainCommandMissing("strategy_area_receiver"))) return true;
            return false;
        }

        private bool HasForces()
        {
            //70D950 keeps this otherwise unused Force list alive during711400.
            var lease = FinalizationTemporary(current);
            try
            {
                int count = 0;
                foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw MainCommandMissing("strategy_force_registry"))
                {
                    if (entry is not { Kind: RegistryKind.Force, PhysicalSlot: int id }) throw MainCommandMissing("strategy_force_receiver");
                    var target = new ForceReference(id);
                    if (id is >= 46 and <= 50 && MainStrategyForceByte(state, target, 0x194) != 0 || id == 0 || id == own) continue;
                    if (!MainStrategyHasTreaty(state, current, target, force) && ++count > 1) return true;
                }
                return false;
            }
            finally { FinalizationRelease(current, lease); }
        }

        private bool CanPoison(AreaReference area)
        {
            var city = City(MainNeed(current.ReadAreaCitySlot(area), "strategy_poison_city35"));
            var list = FinalizationTemporary(current);
            try
            {
                FillNeighbors(city, list);
                bool neighbor = MainStrategyList(current, list).Any(entry => CityOwner(ListedCity(entry)) == own);
                if (!neighbor && CityOwner(city) != own) return false;
                var target = AreaForce(area);
                if (!MainNeed(current.ReadForceNativeValidity(target), "strategy_poison_force_validity")) return false;
                int id = MainNeed(target.PhysicalSlot, "strategy_poison_force");
                return id is not (>= 46 and <= 50) && id != own && Unprotected(target);
            }
            finally { FinalizationRelease(current, list); }
        }

        private bool HasPacificationArea()
        {
            var enemies = FinalizationTemporary(current);
            try
            {
                for (int slot = 0; slot < 52; slot++)
                {
                    var city = City(slot);
                    if (CityOwner(city) != own) continue;
                    var neighbors = FinalizationTemporary(current);
                    try
                    {
                        FillNeighbors(city, neighbors);
                        foreach (var entry in MainStrategyList(current, neighbors))
                            if (CityOwner(ListedCity(entry)) != own && !MainStrategyList(current, enemies).Contains(entry))
                                FinalizationAppend(current, enemies, entry);
                    }
                    finally { FinalizationRelease(current, neighbors); }
                }
                foreach (var area in current.ReadRegisteredAreaReferences() ?? throw MainCommandMissing("strategy_pacify_area_registry"))
                    if (CanPacify(area ?? throw MainCommandMissing("strategy_pacify_area_receiver"), enemies)) return true;
                return false;
            }
            finally { FinalizationRelease(current, enemies); }
        }

        private bool CanPacify(AreaReference area, HexListHandle enemies)
        {
            int definition = MainNeed(current.ReadAreaValidityCode(area), "strategy_pacify_city34");
            var city = City(definition);
            bool accessible = false;
            for (int index = 0; index < 8; index++)
            {
                int raw = MainNeed(current.ReadAreaNeighborSlot(area, index), "strategy_pacify_neighbor");
                var neighbor = current.ReadAreaTableReference(raw < 501 ? raw : 0) ?? throw MainCommandMissing("strategy_pacify_neighbor_pointer");
                if (!MainNeed(current.ReadAreaNativeValidity(neighbor), "strategy_pacify_neighbor_validity")) continue;
                int physical = MainNeed(area.PhysicalSlot, "strategy_pacify_physical");
                if ((package._scenario.Snapshot.Areas[physical].RegionalNeighborMask & (1 << index)) == 0 ||
                    MainNeed(current.ReadAreaCurrentForceSlot(neighbor), "strategy_pacify_neighbor_owner") != own) continue;
                if (MainStrategyList(current, enemies).Any(entry =>
                    MainNeed(entry.PhysicalSlot, "strategy_pacify_enemy_physical") -
                    MainNeed(City(0).PhysicalSlot, "strategy_pacify_city_anchor") == definition)) { accessible = true; break; }
            }
            if (!accessible && CityOwner(city) != own) return false;
            var target = AreaForce(area);
            if (!MainNeed(current.ReadForceNativeValidity(target), "strategy_pacify_force_validity")) return true;
            var assigned = City(MainNeed(current.ReadAreaCitySlot(area), "strategy_pacify_city35"));
            int hex = MainNeed(current.ReadAreaHexIndex(area), "strategy_pacify_hex");
            if (MainNeed(current.ReadBaseNativeValidity(assigned), "strategy_pacify_city_validity"))
            {
                int link = MainNeed(current.ReadBaseObjectLink(assigned), "strategy_pacify_city_object");
                var obj = current.ReadObjectTableReference(link < 3001 ? link : 0) ?? throw MainCommandMissing("strategy_pacify_object_pointer");
                int objectHex = MainNeed(current.ReadObjectNativeValidity(obj), "strategy_pacify_object_validity")
                    ? MainNeed(current.ReadObjectHexIndex(obj), "strategy_pacify_object_hex") : 48400;
                if (objectHex == hex) return false;
            }
            var at = current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw MainCommandMissing("strategy_pacify_hex_pointer");
            if (MainNeed(current.ReadHexSurfaceCode(at), "strategy_pacify_surface") == 17) return false;
            int id = MainNeed(target.PhysicalSlot, "strategy_pacify_force");
            return id is not (>= 46 and <= 50) && id != own && Unprotected(target);
        }

        private bool HasCities()
        {
            //32D1D0 returns the placeholder City for a known-null Emperor
            //Person pointer; that is distinct from missing lookup knowledge.
            int emperorId = MainNeed(CityCaptureTerritoryInputs.ReadEmperor1654(state), "strategy_emperor_identity");
            var emperorPerson = current.ReadPersonLookupReference(emperorId <= 6000 ? emperorId : 0)
                ?? throw MainCommandMissing("strategy_emperor_person_pointer");
            int emperorCity = 0;
            if (emperorPerson.PhysicalSlot is int emperorPhysical &&
                MainNeed(current.ReadPersonNativeValidity(emperorPhysical), "strategy_emperor_person_validity"))
            {
                var position = PersonPositionRepair.DecodeSecondLocation(MainNeed(current.ReadPersonSecondLocation(emperorPhysical),
                    "strategy_emperor_position"));
                if (position.Kind == 5) emperorCity = position.Index < 52 ? position.Index : 0;
            }
            var emperor = City(emperorCity);
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.City) ?? throw MainCommandMissing("strategy_incite_city_registry"))
            {
                if (entry is not { Kind: RegistryKind.City, PhysicalSlot: int id }) throw MainCommandMissing("strategy_incite_city_receiver");
                var city = new BaseReference(BaseKind.City, id);
                var target = Force(CityOwner(city));
                if (!MainNeed(current.ReadForceNativeValidity(target), "strategy_incite_force_validity")) continue;
                int owner = MainNeed(target.PhysicalSlot, "strategy_incite_force");
                if (owner is >= 46 and <= 50 || owner == own || !Unprotected(target)) continue;
                int governor = MainNeed(current.ReadArmyReturnBaseGovernor2E(city), "strategy_incite_governor");
                var person = current.ReadPersonLookupReference(governor <= 6000 ? governor : 0) ?? throw MainCommandMissing("strategy_incite_governor_pointer");
                int physical = MainNeed(person.PhysicalSlot, "strategy_incite_governor_physical");
                if (!MainNeed(current.ReadPersonNativeValidity(physical), "strategy_incite_governor_validity") ||
                    MainNeed(current.ReadPersonStatus(physical), "strategy_incite_governor_status") == 1) continue;
                int ruler = MainNeed(current.ReadForceRecord(owner)?.RawRuler10, "strategy_incite_ruler");
                var leader = current.ReadPersonLookupReference(ruler <= 6000 ? ruler : 0) ?? throw MainCommandMissing("strategy_incite_ruler_pointer");
                var capital = BaseLookup.Find(MainNeed(current.ReadPersonFirstLocation(MainNeed(leader.PhysicalSlot,
                    "strategy_incite_ruler_physical")), "strategy_incite_ruler_home"), current);
                int capitalLink = MainNeed(current.ReadBaseObjectLink(capital), "strategy_incite_capital_identity");
                int link = MainNeed(current.ReadBaseObjectLink(city), "strategy_incite_city_identity");
                if (capitalLink == link) continue;
                if (capitalLink is >= 52 and <= 61)
                {
                    var home = CurrentGateMapQueries.FindCity(capital, current);
                    if (MainNeed(current.ReadBaseNativeValidity(home), "strategy_incite_capital_city_validity") &&
                        MainNeed(current.ReadBaseObjectLink(home), "strategy_incite_capital_city_identity") == link) continue;
                }
                if (MainNeed(current.ReadBaseNativeValidity(emperor), "strategy_incite_emperor_validity") && emperor == city) continue;
                return true;
            }
            return false;
        }

        private bool HasObjects()
        {
            var areas = FinalizationTemporary(current);
            try
            {
                //6DBED0 first fills and sorts a second list, then appends its
                //distinct successful payloads to the caller's still-owned list.
                var gathered = FinalizationTemporary(current);
                try
                {
                    foreach (var entry in current.ReadRegistryReferences(RegistryKind.Object) ?? throw MainCommandMissing("strategy_object_registry"))
                    {
                        if (entry is not { Kind: RegistryKind.Object, PhysicalSlot: int physical }) throw MainCommandMissing("strategy_object_receiver");
                        var obj = current.ReadPhysicalObject(physical) ?? throw MainCommandMissing("strategy_object_record");
                        var definition = current.ReadObjectDefinitionReference(obj.RawField12 < 31 ? obj.RawField12 : 0) ?? throw MainCommandMissing("strategy_object_definition");
                        if (MainNeed(current.ReadObjectDefinitionRaw2C(definition), "strategy_object_category") != 2) continue;
                        int rawOwner = MainNeed(current.ReadObjectForceSlot13(new(physical)), "strategy_object_owner");
                        int owner = MainNeed(Force(rawOwner).PhysicalSlot, "strategy_object_force_receiver");
                        if (!MainNeed(ArmyLogisticsPreparation.ReadNativeRelationGate(state, own, owner), "strategy_object_relation")) continue;
                        int raw = MainNeed(current.ReadObjectHexIndex(new(physical)), "strategy_object_hex");
                        var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw MainCommandMissing("strategy_object_hex_pointer");
                        int area = MainNeed(current.ReadHexAreaSlot(hex), "strategy_object_hex_area");
                        var pointer = current.ReadAreaTableReference(area < 501 ? area : 0) ?? throw MainCommandMissing("strategy_object_area_pointer");
                        FinalizationAppend(current, gathered, new(RegistryKind.Area, MainNeed(pointer.PhysicalSlot, "strategy_object_area_physical")));
                    }
                    RegistryObjectReference? previous = null;
                    foreach (var item in MainStrategyList(current, gathered).OrderBy(item => item.PhysicalSlot))
                    {
                        if (item != previous) FinalizationAppend(current, areas, item);
                        previous = item;
                    }
                }
                finally { FinalizationRelease(current, gathered); }
                foreach (var item in MainStrategyList(current, areas))
                    if (CanSabotage(new(MainNeed(item.PhysicalSlot, "strategy_sabotage_area")))) return true;
                return false;
            }
            finally { FinalizationRelease(current, areas); }
        }

        private bool CanSabotage(AreaReference area)
        {
            var city = City(MainNeed(current.ReadAreaCitySlot(area), "strategy_sabotage_city"));
            var target = AreaForce(area);
            if (target == force) return true;
            var neighbors = FinalizationTemporary(current);
            try
            {
                FillNeighbors(city, neighbors);
                foreach (var entry in MainStrategyList(current, neighbors))
                {
                    if (CityOwner(ListedCity(entry)) != own) continue;
                    if (!MainNeed(current.ReadForceNativeValidity(target), "strategy_sabotage_force_validity")) return true;
                    int governor = MainNeed(current.ReadArmyReturnBaseGovernor2E(city), "strategy_sabotage_governor");
                    var person = current.ReadPersonLookupReference(governor <= 6000 ? governor : 0) ?? throw MainCommandMissing("strategy_sabotage_governor_pointer");
                    int home = MainNeed(current.ReadPersonFirstLocation(MainNeed(person.PhysicalSlot,
                        "strategy_sabotage_governor_physical")), "strategy_sabotage_governor_home");
                    return !(home is >= 1 and <= 61 && CityOwner(city) is >= 46 and <= 50) && Unprotected(target);
                }
                return false;
            }
            finally { FinalizationRelease(current, neighbors); }
        }
    }
}
