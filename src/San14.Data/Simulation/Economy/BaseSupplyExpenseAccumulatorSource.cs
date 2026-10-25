using San14.Core;

namespace San14.Data;

/// <summary>223B70 adds the signed requested expense to Base54. The actual
/// Turn reset and ordinary food writers already own PeriodFoodExpenditure;
/// this is not a second stock debit or a separate saved counter.</summary>
internal static class BaseSupplyExpenseAccumulatorSource
{
    //CFoothold221090 initializes50/54 together;226FA0 explicitly clears54.
    //The scenario's static Base projection does not replace this runtime
    //expense. Only the ordinary allocation caller may invoke this, never Load.
    internal static GameState InstallFresh(GameState state)
    {
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.Cities.Values.Any(city => city.PeriodFoodExpenditure.HasValue) ||
            state.Gates.Values.Any(gate => gate.PeriodFoodExpenditure.HasValue))
            throw new InvalidDataException("base-supply-expense.fresh_allocation");
        return state with {
            Cities = state.Cities.ToDictionary(pair => pair.Key, pair => pair.Value with { PeriodFoodExpenditure = 0 }),
            Gates = state.Gates.ToDictionary(pair => pair.Key, pair => pair.Value with { PeriodFoodExpenditure = 0 }) };
    }

    internal static GameState AddCurrent(GameState state, BaseReference target, int amount)
    {
        int physical = target.PhysicalSlot ?? throw new InvalidDataException("base-supply-expense.Base_receiver");
        if (target.Kind == BaseKind.City)
        {
            var city = state.Cities.GetValueOrDefault(physical) ?? throw new InvalidDataException("base-supply-expense.City_owner");
            int before = city.PeriodFoodExpenditure ?? throw new InvalidDataException("base-supply-expense.City54_current");
            return state with { Cities = new(state.Cities) { [physical] = city with { PeriodFoodExpenditure = unchecked(before + amount) } } };
        }
        int location = checked(physical + 51);
        var gate = state.Gates.GetValueOrDefault(location) ?? throw new InvalidDataException("base-supply-expense.Gate_owner");
        int old = gate.PeriodFoodExpenditure ?? throw new InvalidDataException("base-supply-expense.Gate54_current");
        return state with { Gates = new(state.Gates) { [location] = gate with { PeriodFoodExpenditure = unchecked(old + amount) } } };
    }
}
