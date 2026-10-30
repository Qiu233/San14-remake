using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    private bool CityGenerateTrade(ProposalKind kind, int proposerPersonId, out ProposalArguments arguments)
    {
        arguments = default;
        bool buying = kind == ProposalKind.BuyFood;
        int politics = PersonAbility(proposerPersonId, 3);
        int amount = Math.Min(unchecked((PersonAbility(proposerPersonId, 2) + politics) * 100), Rule(buying ? "buy-food-maximum" : "sell-food-maximum", 10000));
        if (politics <= Rule(buying ? "buy-food-politics" : "sell-food-politics", 50) || amount < Rule(buying ? "buy-food-minimum" : "sell-food-minimum", buying ? 2000 : 5000)) return false;
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(State, Current) ?? throw Missing("trade_player_force");
        int playerId = K(player.PhysicalSlot, "trade_player_force_identity");
        int ruler = K(Current.ReadForceRecord(playerId)?.RawRuler10, "trade_ruler");
        var capital = CityPersonCity(PersonPhysical(ruler));
        int capitalId = K(capital.PhysicalSlot, "trade_capital");
        int district = K(Current.ReadPersonDistrictSlot(PersonPhysical(proposerPersonId)), "trade_proposer_district");
        var candidates = new List<BaseReference>();
        foreach (var entry in Current.ReadRegistryReferences(RegistryKind.City) ?? throw Missing("trade_city_registry"))
        {
            int id = K(entry?.PhysicalSlot, "trade_city_pointer");
            var city = CityReference(id);
            if (id != capitalId || !K(Current.ReadBaseNativeValidity(city), "trade_city_validity") ||
                K(Current.ReadBaseDistrictSlot(city), "trade_city_district") != district || CityDuplicate(kind, id)) continue;
            int price = CityTradePrice(city, !buying);
            int gold = CityResource(city, BaseResourceKind.Gold), food = CityResource(city, BaseResourceKind.Supplies);
            if (price == 0) throw Missing("trade_price_divisor");
            if (buying)
            {
                int troops = CityResource(city, BaseResourceKind.Troops), wounded = K(Current.ReadArmyReturnBaseWounded40(city), "buy_food_wounded");
                if (unchecked(amount * 10) / price + Rule("buy-food-reserved-gold", 2000) > gold ||
                    amount > CityCapacity(city, BaseResourceKind.Supplies) - food ||
                    unchecked(troops + wounded) < Rule("buy-food-minimum-army", 2000) || food >= unchecked(troops + wounded)) continue;
            }
            else
            {
                if (gold >= Rule("sell-food-poor-gold", 1000) || unchecked(amount * 10) / price > CityCapacity(city, BaseResourceKind.Gold) - gold) continue;
                int expense = ReadProposalCityFoodExpense(city);
                if (food < unchecked(expense * Rule("sell-food-reserve-periods", 9) + Rule("sell-food-reserved-food", 10000))) continue;
            }
            if (price < Rule("trade-minimum-price", 40) || price > Rule("trade-maximum-price", 60)) continue;
            candidates.Add(city);
        }
        if (candidates.Count == 0) return false;
        int[] ranks = CreateRanks(52);
        if (buying)
            candidates = candidates.OrderByDescending(city => unchecked(CityResource(city, BaseResourceKind.Troops) +
                K(Current.ReadArmyReturnBaseWounded40(city), "buy_sort_wounded") - CityResource(city, BaseResourceKind.Supplies)))
                .ThenBy(city => ranks[K(city.PhysicalSlot, "trade_sort_identity")]).ToList();
        else candidates = candidates.OrderBy(city => CityResource(city, BaseResourceKind.Gold))
            .ThenBy(city => ranks[K(city.PhysicalSlot, "trade_sort_identity")]).ToList();
        var receiver = candidates[0];
        int improvement = Math.Max(1, politics / Rule("trade-politics-divisor", 12));
        int adjusted = unchecked(CityTradePrice(receiver, !buying) + (buying ? improvement : -improvement));
        if (adjusted == 0) throw Missing("trade_adjusted_price_divisor");
        int exchange = unchecked(amount * 10) / adjusted;
        //13/14 use vslot10=0 as their actual cost, independently of the
        //general generation caller's incoming cost argument.
        arguments = new(0, 0, K(receiver.PhysicalSlot, "trade_target"), Math.Max(0, amount), Math.Max(0, exchange));
        return true;
    }

    private int CityTradePrice(BaseReference city, bool selling)
    {
        int raw = State.Cities.GetValueOrDefault(K(city.PhysicalSlot, "trade_price_city"))?.MerchantFoodPerTenGold
            ?? throw Missing("trade_current_price");
        return selling ? unchecked(raw * CityMerchantSource.SellPricePercent) / 100 : raw;
    }
}
