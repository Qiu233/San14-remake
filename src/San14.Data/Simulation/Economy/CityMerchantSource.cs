using San14.Core;

namespace San14.Data;

/// <summary>The ordinary CE merchant page and amount readers. One captured
/// current is borrowed for the page; a formal command constructs its own.</summary>
internal sealed class CityMerchantSource
{
    internal const int MaximumOfficers = 10, CommandCost = 1, SellPricePercent = 130, MeritRate = 2;
    internal readonly GameState State;
    internal readonly SourceWorldContext Current;
    internal readonly BaseReference City;
    internal readonly DistrictReference District;
    private readonly Func<GameState, int, int, int?> ability;
    private readonly Func<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>> card;
    private readonly Func<AreaAdministrationQuery<string>> hint;
    private readonly Func<IReadOnlyList<int>, CityMerchantMode, int, string, Func<int, int>, AreaAdministrationQuery<CityDomesticNarration>> narration;
    private readonly Dictionary<int, CityMerchantCandidate> candidates = [];
    internal int CityId { get; }
    internal IReadOnlyList<CityMerchantCandidate> Candidates { get; }

    internal CityMerchantSource(GameState state, SourceWorldContext current, int cityId,
        Func<GameState, int, int, int?> readAbility,
        Func<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>> officerCard,
        Func<AreaAdministrationQuery<string>> defaultHint,
        Func<IReadOnlyList<int>, CityMerchantMode, int, string, Func<int, int>, AreaAdministrationQuery<CityDomesticNarration>> prepareNarration)
    {
        State = state; Current = current; CityId = cityId; ability = readAbility;
        card = officerCard; hint = defaultHint; narration = prepareNarration;
        if (state.NativeArmySimulation?.Parameters.Language != "TC") throw Missing("SC_rule_profile");
        if (!state.Cities.ContainsKey(cityId) || cityId is < 1 or > 51) throw Missing("city_identity");
        City = current.ReadCityTableReference(cityId) ?? throw Missing("city_pointer");
        if (!K(current.ReadBaseNativeValidity(City), "city_validity")) throw Missing("city_invalid");
        District = current.ReadCityDistrictReference(City) ?? throw Missing("district_pointer");
        if (!K(current.ReadDistrictNativeValidity(District), "district_validity")) throw Missing("district_invalid");
        int district = K(District.PhysicalSlot, "district_identity");
        var result = new List<CityMerchantCandidate>();
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100"))
        {
            int physical = K(entry, "Person_pointer");
            if (K(current.ReadPersonDistrictSlot(physical), "Person_E0") != district ||
                K(current.ReadPersonStatus(physical), "Person_E6") is < 1 or > 4) continue;
            if (!K(current.ReadPersonNativeValidity(physical), "Person_validity")) continue;
            int id = K(current.ReadPersonIdentity(physical), "Person_identity");
            if (Special(physical, id) || (K(current.ReadArmyReturnPersonFlags196(physical), "Person_flags15E") & 1) != 0) continue;
            var linked = current.ReadPersonLinkedEntity(physical) ?? throw Missing("Person_association");
            if (K(current.ReadLinkedEntityNativeValidity(linked), "Person_association_validity")) continue;
            if (K(PersonArmyLocationSource.ReadCommanderArmyValidity(state, current, physical), "Person_Army_validity")) continue;
            byte health = K(OfficerHealthSourceAdapter.ReadByte(current, physical), "Person_health161");
            if (health is 2 or 3) continue;
            int home = K(current.ReadPersonFirstLocation(physical), "Person_E2");
            if (home != cityId && !(home is >= 52 and <= 61 &&
                CurrentGateMapQueries.FindCity(new(BaseKind.Gate, home - 51), current) == City)) continue;
            var person = state.Officers.GetValueOrDefault(id) ?? throw Missing("Person_domain_identity");
            int? intelligence = readAbility(state, id, 2), politics = readAbility(state, id, 3);
            int? weight = intelligence is int a && politics is int b ? unchecked((a + b) * 100) : null;
            var candidate = new CityMerchantCandidate(id, physical, person.Name, intelligence, politics, weight);
            result.Add(candidate); candidates.Add(id, candidate);
        }
        Candidates = result.AsReadOnly();
    }

    internal bool IsCandidate(int id) => candidates.ContainsKey(id);
    internal AreaAdministrationQuery<AreaAdministrationOfficerCard> OfficerCard(int id) => card(id);
    internal AreaAdministrationQuery<string> DefaultHint() => hint();
    internal AreaAdministrationQuery<CityDomesticNarration> PrepareNarration(IReadOnlyList<int> selected,
        CityMerchantMode mode, int amount, string locale, Func<int, int> random)
        => narration(selected, mode, amount, locale, random);
    internal CityMerchantCandidate Candidate(int id) => candidates.GetValueOrDefault(id) ?? throw Missing("selected_candidate");
    internal bool CanOpen()
    {
        int books = State.Districts.GetValueOrDefault(K(District.PhysicalSlot, "admission_District"))?.OrdersRemaining
            ?? throw Missing("admission_Books");
        if (books < CommandCost) return false;
        byte flags = State.NativeBaseCaptureCurrent?.Cities.GetValueOrDefault(CityId)?.C9 ?? throw Missing("admission_C9");
        //A legacy/fresh world without the actual3035A0 merchant producer
        //cannot use a static byte as a completed merchant publication.
        int price = State.Cities[CityId].MerchantFoodPerTenGold ?? throw Missing("appearance_300FE0_and_price_302F20");
        if ((flags & 1) == 0) return false;
        int gold = K(Current.ReadBaseResource(City, BaseResourceKind.Gold), "admission_Gold");
        int food = K(Current.ReadBaseResource(City, BaseResourceKind.Supplies), "admission_Food");
        bool buy = unchecked(gold * price) / 10 > 0 && food < BasePostLoad.ReadSuppliesCapacity(City, Current);
        int sellPrice = unchecked(price * SellPricePercent) / 100;
        bool sell = gold < BasePostLoad.ReadGoldCapacity(City, Current) && unchecked(food * 10) / Math.Max(1, sellPrice) > 0;
        return (buy || sell) && Candidates.Count != 0 && (flags & 4) == 0;
    }
    internal int Weight(GameState state, CityMerchantCandidate person)
    {
        if (!K(Current.ReadPersonNativeValidity(person.PhysicalSlot), "weight_Person_validity")) return 0;
        int intelligence = K(ability(state, person.OfficerId, 2), "weight_intelligence");
        int politics = K(ability(state, person.OfficerId, 3), "weight_politics");
        return unchecked((intelligence + politics) * 100);
    }

    internal CityMerchantPage Page(IReadOnlyList<int> selected, CityMerchantMode mode, int amount)
    {
        var image = State.Cities[CityId];
        int? gold = null, food = null, books = null, buy = null, sell = null, goldCap = null, foodCap = null;
        int? maximum = null, goldDelta = null, foodDelta = null, afterGold = null, afterFood = null, afterBooks = null;
        bool? used = null, confirm = null, canBuy = null, canSell = null;
        string? missing = null;
        try
        {
            gold = K(Current.ReadBaseResource(City, BaseResourceKind.Gold), "Gold");
            food = K(Current.ReadBaseResource(City, BaseResourceKind.Supplies), "Food");
            int district = K(District.PhysicalSlot, "page_District");
            books = State.Districts.GetValueOrDefault(district)?.OrdersRemaining ?? throw Missing("Books");
            afterBooks = unchecked(books.Value - CommandCost);
            used = (State.NativeBaseCaptureCurrent?.Cities.GetValueOrDefault(CityId)?.C9
                ?? throw Missing("trade_current_C9")) is byte flags && (flags & 4) != 0;
            buy = image.MerchantFoodPerTenGold ?? throw Missing("price_current_C8");
            sell = unchecked(buy.Value * SellPricePercent) / 100;
            goldCap = BasePostLoad.ReadGoldCapacity(City, Current);
            foodCap = BasePostLoad.ReadSuppliesCapacity(City, Current);
            bool representativeValid = selected.Count != 0 && K(Current.ReadPersonNativeValidity(Candidate(selected[0]).PhysicalSlot), "representative_validity");
            canBuy = representativeValid && unchecked(gold.Value * buy.Value) / 10 > 0 && food.Value < foodCap.Value;
            canSell = representativeValid && gold.Value < goldCap.Value && unchecked(food.Value * 10) / Math.Max(1, sell.Value) > 0;
            int sum = 0;
            foreach (int id in selected) sum = unchecked(sum + Weight(State, Candidate(id)));
            if (buy <= 0 || sell <= 0) throw Missing("price_nonpositive_divisor");
            int stockLimit;
            if (mode == CityMerchantMode.Buy)
                stockLimit = Math.Min(unchecked(foodCap.Value - food.Value), unchecked(gold.Value * buy.Value) / 10);
            else
            {
                stockLimit = food.Value;
                int remainingGold = unchecked(goldCap.Value - gold.Value);
                if (remainingGold < unchecked(food.Value * 10) / sell.Value)
                    stockLimit = unchecked(sell.Value * remainingGold) / 10;
            }
            maximum = Math.Min(stockLimit, sum > 0 ? sum : int.MaxValue);
            goldDelta = mode == CityMerchantMode.Sell ? unchecked(amount * 10) / sell.Value
                : unchecked(-(unchecked(buy.Value + unchecked((amount - 1) * 10)) / buy.Value));
            //At zero the page displays unchanged quantities, independently of
            //the positive-only formal1F10A0 command guard.
            if (amount == 0) goldDelta = 0;
            foodDelta = mode == CityMerchantMode.Buy ? amount : -amount;
            afterGold = Math.Clamp(unchecked(gold.Value + goldDelta.Value), 0, goldCap.Value);
            afterFood = Math.Clamp(unchecked(food.Value + foodDelta.Value), 0, foodCap.Value);
            confirm = selected.Count is > 0 and <= MaximumOfficers && sum > 0 && amount > 0 &&
                amount <= maximum && used == false && CanOpen() && books.Value - CommandBookAvailability.Reserved(State, district) >= CommandCost;
        }
        catch (InvalidDataException error) { missing = error.Message; }
        return new(CityId, image.Name, Candidates, Array.AsReadOnly(selected.ToArray()), mode, amount,
            gold, food, books, buy, sell, goldCap, foodCap, maximum, goldDelta, foodDelta,
            afterGold, afterFood, afterBooks, used, confirm, missing) { CanBuy = canBuy, CanSell = canSell };
    }

    private bool Special(int physical, int id)
    {
        if (!new[] { 4111, 4131, 4151, 4171, 4191 }.Any(first => unchecked((uint)(id - first)) <= 9)) return false;
        int first = K(Current.ReadPersonFirstLocation(physical), "special_E2");
        if (first is < 1 or > 61) return false;
        var force = Current.ReadPersonForce(physical) ?? throw Missing("special_Force");
        return K(force.PhysicalSlot, "special_Force_identity") is not (>= 46 and <= 50);
    }
    internal static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    internal static InvalidDataException Missing(string field) => new("city-merchant." + field);
}
