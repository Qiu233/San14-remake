using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState PrepareOriginalAiForceStrategyCurrent(GameState state, SourceWorldContext current, int forceId)
    {
        if (Seed.Language != "TC") throw AiForceStrategyMissing("SC_source_contract");
        int rulerId = AiForceStrategyKnown(current.ReadForceRecord(forceId)?.RawRuler10, "ruler_identity");
        int ruler = AiForceStrategyKnown((rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "ruler_pointer");
        int homeId = AiForceStrategyKnown(current.ReadPersonFirstLocation(ruler), "ruler_home");
        var home = BaseLookup.Find(homeId, current);
        if (AiForceStrategyKnown(current.ReadBaseObjectLink(home), "ruler_home_identity") is not (>= 52 and <= 61)) return state;
        //6D9CD0 queries the Base's real serving-person iterator and then the
        //current native validity, Home, position and return bit. It imposes no
        //health, ruler exclusion or invented general movement page predicate.
        if (!AiForceStrategyKnown(current.ReadBaseNativeValidity(home), "ruler_home_validity")) return state;
        var force = current.ReadPersonForce(ruler) ?? throw AiForceStrategyMissing("ruler_force");
        if (!AiForceStrategyKnown(current.ReadForceNativeValidity(force), "ruler_force_validity")) return state;
        bool selected = false;
        foreach (int person in EnumerateBaseResidents(current, checked((ushort)homeId), AiForceStrategyMissing))
        {
            if (!AiForceStrategyKnown(current.ReadPersonNativeValidity(person), "move_person_validity") ||
                AiForceStrategyKnown(current.ReadPersonFirstLocation(person), "move_person_home") != homeId ||
                AiForceStrategyKnown(current.ReadPersonSecondLocation(person), "move_person_position") != homeId ||
                (AiForceStrategyKnown(current.ReadArmyReturnPersonFlags196(person), "move_person_flags") & 1) != 0) continue;
            if (person == ruler) { selected = true; break; }
        }
        if (!selected) return state;
        var city = CurrentGateMapQueries.FindCity(home, current);
        int target = AiForceStrategyKnown(current.ReadBaseObjectLink(city), "ruler_target_city");
        var district = current.ReadBaseDistrictReference(home) ?? throw AiForceStrategyMissing("ruler_home_district");
        int districtId = AiForceStrategyKnown(district.PhysicalSlot, "ruler_home_district_identity");
        int books = AiForceStrategyKnown(current.ReadDistrictRawByte(districtId, 0x14), "ruler_home_books");
        int cost = CityOfficerMovementSource.ReadCost(state);
        PersonReturnCommands.BindCurrent(state, current);
        var moved = PersonReturnCommands.StartReassignment(state, current, [rulerId], target);
        var working = moved.State ?? throw AiForceStrategyMissing(moved.UnresolvedOwner ?? "ruler_reassignment");
        int after = Math.Clamp(unchecked(books - cost), 0, 200);
        var saved = working.NativeForceUnregisterCurrent ?? throw AiForceStrategyMissing("ruler_payment_owner");
        var window = saved.DistrictWindows10.GetValueOrDefault(districtId)?.ToArray()
            ?? throw AiForceStrategyMissing("ruler_payment_window");
        if (window.Length <= 4) throw AiForceStrategyMissing("ruler_payment_window_format");
        window[4] = (byte)after;
        current.SetDistrictRawByte(districtId, 0x14, (byte)after);
        working = working with
        {
            Districts = new(working.Districts) { [districtId] = working.Districts[districtId] with { OrdersRemaining = after } },
            NativeForceUnregisterCurrent = saved with { DistrictWindows10 = new(saved.DistrictWindows10) { [districtId] = window } }
        };
        return OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
    }

    internal GameState CompleteOriginalAiForceStrategyCurrent(GameState state, SourceWorldContext current, int forceId)
    {
        if (Seed.Language != "TC") throw AiForceStrategyMissing("SC_source_contract");
        var emergency = new List<int>();
        foreach (var entry in current.ReadRegisteredBases() ?? throw AiForceStrategyMissing("base_registry"))
        {
            var home = entry ?? throw AiForceStrategyMissing("base_pointer");
            if (AiForceStrategyKnown(current.ReadBaseForceSlot(home), "base_force") != forceId) continue;
            int location = AiForceStrategyKnown(current.ReadBaseObjectLink(home), "base_identity");
            if (AiForceStrategyKnown(current.ReadConditionBaseSpecialGovernor(home), "base_special")) continue;
            var memory = state.OriginalAiStrategyMemory?.Footholds.GetValueOrDefault(location)
                ?? throw AiForceStrategyMissing("base_forecast_owner");
            int goldForecast = AiForceStrategyKnown(memory.GoldForecast, "gold_forecast"),
                foodForecast = AiForceStrategyKnown(memory.FoodForecast, "food_forecast");
            var stock = AiForceStrategyCurrency(current, home);
            int goldFloor = goldForecast < 1 ? 500 : 0, foodFloor = foodForecast < 1 ? 2500 : 0;
            if (!(stock.Gold <= goldFloor && goldForecast < 0 || stock.Food <= foodFloor && foodForecast < 0)) continue;
            var incoming = AiForceStrategyIncoming(state, current, forceId, location);
            if (unchecked(stock.Gold + incoming.Gold) <= goldFloor || unchecked(stock.Food + incoming.Food) <= foodFloor)
                emergency.Add(location);
        }
        var working = state;
        foreach (int targetId in emergency)
        {
            var target = BaseLookup.Find(targetId, current);
            var targetStock = AiForceStrategyCurrency(current, target);
            int goldNeed = unchecked(3000 - targetStock.Gold), foodNeed = unchecked(10000 - targetStock.Food);
            var donors = new List<BaseReference>();
            foreach (var neighbor in AiForceInformationNeighbors(current, target))
            {
                var district = current.ReadBaseDistrictReference(neighbor) ?? throw AiForceStrategyMissing("donor_district");
                int districtId = AiForceStrategyKnown(district.PhysicalSlot, "donor_district_identity");
                if (AiForceStrategyKnown(current.ReadDistrictNativeValidity(district), "donor_district_validity") &&
                    (AiForceStrategyKnown(current.ReadDistrictRawByte(districtId, 0x1B), "donor_transport_permission") & 0x10) == 0) continue;
                var query = new OriginalTransportSelection(this, working, current, districtId);
                if (query.EnemyNear(neighbor, 15)) continue;
                if (AiForceStrategyKnown(current.ReadBaseForceSlot(neighbor), "donor_force") != forceId) continue;
                int location = AiForceStrategyKnown(current.ReadBaseObjectLink(neighbor), "donor_identity");
                if (emergency.Contains(location)) continue;
                var stock = AiForceStrategyCurrency(current, neighbor);
                if (stock.Gold > 9999 && stock.Food > 19999 &&
                    AiForceStrategyKnown(current.ReadBaseResource(neighbor, BaseResourceKind.Troops), "donor_troops") > 4999)
                    donors.Add(neighbor);
            }
            //Source list sort uses its ordinary Base ordering; it is not a
            //new score or an identity tie-breaker. See bounded ASM contract.
            AiForceStrategySortDonors(current, target, donors);
            foreach (var donor in donors)
            {
                int donorId = AiForceStrategyKnown(current.ReadBaseObjectLink(donor), "donor_identity");
                int districtId = AiForceStrategyKnown(current.ReadBaseDistrictReference(donor)?.PhysicalSlot, "donor_district_identity");
                if (AiForceStrategyKnown(current.ReadDistrictRawByte(districtId, 0x14), "donor_books") == 0 || donorId == targetId) continue;
                int fromArea = AiForceStrategyKnown(CurrentGateMapQueries.FindArea(donor, current).PhysicalSlot, "donor_area"),
                    toArea = AiForceStrategyKnown(CurrentGateMapQueries.FindArea(target, current).PhysicalSlot, "target_area");
                if (fromArea != toArea)
                {
                    var route = OriginalAiTransportAreaRoute.Resolve(current, fromArea, toArea, false);
                    if (!route.Found || route.Cost > 6) continue;
                }
                if (ArmyMarchSourceAdapter.ReadBaseMapDestination(working, current, donor) is not
                        { Kind: ArmyMarchDestinationKind.Hex, HexId: int start } ||
                    ArmyMarchSourceAdapter.ReadBaseMapDestination(working, current, target) is not
                        { Kind: ArmyMarchDestinationKind.Hex, HexId: int goal } ||
                    !ArmyRouteSearch.OriginalAiEmergencyTransportCanReach(working, current, forceId, start, goal)) continue;
                var query = new OriginalTransportSelection(this, working, current, districtId);
                var people = query.People(donor);
                if (people.Count == 0) continue;
                int person = people[0];
                int id = AiForceStrategyKnown(current.ReadPersonIdentity(person), "transport_officer_identity");
                var quantities = ReadTransportQuantities(working, current, donor, id, 0, 0, 0);
                int maxTroops = quantities.MaximumTroops ?? throw AiForceStrategyMissing("transport_troop_capacity"),
                    maxGold = quantities.MaximumGold ?? throw AiForceStrategyMissing("transport_gold_capacity"),
                    maxFood = quantities.MaximumSupplies ?? throw AiForceStrategyMissing("transport_food_capacity");
                var stock = AiForceStrategyCurrency(current, donor);
                int gold = Math.Max(0, Math.Min(unchecked(stock.Gold - 3000), goldNeed));
                int food = Math.Max(0, Math.Min(unchecked(stock.Food - 10000), foodNeed));
                if (gold is > 0 and < 3000) gold = 3000;
                if (food is > 0 and < 10000) food = 10000;
                gold = Math.Min(gold, maxGold); food = Math.Min(food, maxFood);
                int troops = Math.Min(1000, maxTroops);
                var force = current.ReadPersonForce(person) ?? throw AiForceStrategyMissing("transport_force");
                int naval = TransportArmyPacketSource.ReadDefaultNaval(working, current, force)
                    ?? throw AiForceStrategyMissing("transport_naval");
                var request = new OriginalAiEmergencyTransportRequest(forceId, donorId, id, districtId, targetId,
                    troops, gold, food, naval);
                var next = CreateOriginalAiEmergencyTransportCurrent(working, current, request);
                if (!ReferenceEquals(next, working)) current = CaptureOriginalAiDomesticCurrent(next);
                working = next;
                //0B3660 leaves this destination after the first admitted
                //donor's actual creation attempt, including a native failure.
                break;
            }
        }
        return working;
    }

    private static (int Gold, int Food) AiForceStrategyCurrency(SourceWorldContext current, BaseReference home)
    {
        var city = BaseCurrencyAdditionSource.CurrencyCity(home, current);
        return (AiForceStrategyKnown(current.ReadBaseResource(city, BaseResourceKind.Gold), "base_gold"),
            AiForceStrategyKnown(current.ReadBaseResource(city, BaseResourceKind.Supplies), "base_food"));
    }

    private static (int Gold, int Food) AiForceStrategyIncoming(GameState state, SourceWorldContext current, int force, int location)
    {
        int gold = 0, food = 0;
        foreach (var row in current.ReadRegistryReferences(RegistryKind.Army) ?? throw AiForceStrategyMissing("army_registry"))
        {
            int physical = AiForceStrategyKnown(row?.PhysicalSlot, "army_pointer");
            var army = new ArmyUnitReference(physical);
            if (AiForceStrategyKnown(current.ReadArmyUnitForceReference(army).PhysicalSlot, "army_force") != force ||
                AiForceStrategyKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "army_excluded")) continue;
            if (AiForceStrategyKnown(current.ReadArmyReturnUnitStatus10(army), "army_status") != 2) continue;
            var fields = current.ReadArmyLifecycleTargetFields(physical) ?? throw AiForceStrategyMissing("army_target");
            int target = OriginalAiBaseNeedsMath.TransportTargetLocation(AiForceStrategyKnown(fields.Kind38, "army_target_kind"),
                AiForceStrategyKnown(fields.Identity3A, "army_target_identity"));
            if (target != location) continue;
            var resource = current.ReadArmyReturnResourceFields(army) ?? throw AiForceStrategyMissing("army_resources");
            gold = unchecked(gold + AiForceStrategyKnown(resource.Gold5C, "army_gold"));
            food = unchecked(food + AiForceStrategyKnown(resource.Supplies60, "army_food"));
        }
        return (gold, food);
    }

    private static void AiForceStrategySortDonors(SourceWorldContext current, BaseReference target, List<BaseReference> donors)
    {
        //22D880 sets the comparison origin in the source Base sort context.
        int origin = current.ReadBaseCurrentHexIndex(target);
        StableTransportSort(donors, (first, second) => ArmyRouteSearch.Distance(origin, current.ReadBaseCurrentHexIndex(first)) <
            ArmyRouteSearch.Distance(origin, current.ReadBaseCurrentHexIndex(second)));
    }

    private static T AiForceStrategyKnown<T>(T? value, string field) where T : struct => value ?? throw AiForceStrategyMissing(field);
    private static InvalidDataException AiForceStrategyMissing(string field) => new("original_ai.force_strategy." + field);
}
