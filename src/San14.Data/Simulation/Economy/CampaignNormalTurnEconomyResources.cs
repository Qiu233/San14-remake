using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState AddNormalTurnBaseResourceCurrent(GameState state, SourceWorldContext current,
        BaseReference home, BaseResourceKind resource, int amount, bool recordIncome = false, bool recordExpense = false)
    {
        if (resource is BaseResourceKind.Gold or BaseResourceKind.Supplies)
            state = DAddDiplomacyResource(state, current, home, resource, amount);
        else
        {
            int before = EconomyK(current.ReadBaseResource(home, resource), "resource_before");
            int cap = BasePostLoad.ReadResourceCapacities(home, current).Troops;
            int after = Math.Max(0, Math.Min(unchecked(before + amount), cap));
            current.SetBaseResource(home, resource, after);
            int location = EconomyK(current.ReadBaseObjectLink(home), "resource_location");
            if (home.Kind == BaseKind.City && state.Cities.TryGetValue(location, out var city))
                state = state with { Cities = new(state.Cities) { [location] = city with { Troops = after } } };
            else if (home.Kind == BaseKind.Gate && state.Gates.TryGetValue(location, out var gate))
                state = state with { Gates = new(state.Gates) { [location] = gate with { Troops = after } } };
            else throw EconomyMissing("resource_domain_receiver");
        }
        if (recordIncome)
        {
            int id = EconomyK(home.PhysicalSlot, "income_city_pointer");
            var city = home.Kind == BaseKind.City ? state.Cities.GetValueOrDefault(id) : null;
            if (city is null) throw EconomyMissing("income_city_owner");
            var totals = city.TurnIncomeTotals ?? throw EconomyMissing("income_reset_order");
            totals = resource switch
            {
                BaseResourceKind.Gold => totals with { Gold = unchecked(totals.Gold + amount) },
                BaseResourceKind.Supplies => totals with { Food = unchecked(totals.Food + amount) },
                _ => totals with { RecruitedTroops = unchecked(totals.RecruitedTroops + amount) }
            };
            state = state with { Cities = new(state.Cities) { [id] = city with { TurnIncomeTotals = totals } } };
        }
        if (recordExpense)
        {
            var payer = BaseCurrencyAdditionSource.CurrencyCity(home, current);
            int id = EconomyK(payer.PhysicalSlot, "expense_city_pointer");
            var city = state.Cities.GetValueOrDefault(id) ?? throw EconomyMissing("expense_city_owner");
            int paid = unchecked(-amount);
            state = state with { Cities = new(state.Cities) { [id] = resource == BaseResourceKind.Gold
                ? city with { PeriodGoldExpenditure = unchecked(EconomyK(city.PeriodGoldExpenditure, "gold_expense_reset") + paid) }
                : city with { PeriodFoodExpenditure = unchecked(EconomyK(city.PeriodFoodExpenditure, "food_expense_reset") + paid) } } };
        }
        return state;
    }

    private static T EconomyK<T>(T? value, string field) where T : struct => value ?? throw EconomyMissing(field);
    private static InvalidDataException EconomyMissing(string field) => new("normal.turn.economy." + field);
}
