using San14.Core;

namespace San14.Data;

internal sealed record BaseCurrencyAdditionReceipt(BaseReference Home, BaseReference CapacityCity,
    BaseReference ReadCity, BaseReference CurrencyCity, int Amount, int Before,
    int CallerCapacity, int Requested, int SetterCapacity, int After);
internal sealed record BaseCurrencyAdditionResult(GameState State, BaseCurrencyAdditionReceipt Receipt);

/// <summary>The current Base currency addition shared by resource callers.
/// The caller owns Home admission and an operation-local bound source view.</summary>
internal static class BaseCurrencyAdditionSource
{
    internal static int ReadCurrentGold(BaseReference home, SourceWorldContext current) =>
        current.ReadBaseResource(CurrencyCity(home, current), BaseResourceKind.Gold) ?? throw Missing("current_gold");

    /// <summary>Virtual90 read, then the caller's fresh cost query, then98's
    /// independently routed currency City and actual setter capacity.</summary>
    internal static GameState PayConstruction(GameState state, SourceWorldContext current,
        BaseReference home, Func<int> readCurrentCost, Func<BaseReference, int?> readGoldCapacity)
    {
        int before = ReadCurrentGold(home, current);
        int requested = Math.Max(unchecked(before - readCurrentCost()), 0);
        var cityReference = CurrencyCity(home, current);
        if (cityReference.Kind != BaseKind.City || cityReference.PhysicalSlot is not int id ||
            !state.Cities.TryGetValue(id, out var city)) throw Missing("payment_currency_city_entity");
        int capacity = readGoldCapacity(cityReference) ?? throw Missing("payment_setter_capacity");
        int after = Bound(requested, capacity);
        current.SetBaseResource(cityReference, BaseResourceKind.Gold, after);
        return state with { Cities = new(state.Cities) { [id] = city with { Gold = after } } };
    }
    /// <summary>The capacity provider receives each actual currency City. Gate
    /// routing is repeated for capacity, virtual resource read and setter;
    /// the City setter then makes its own second capacity query. Unknown late
    /// inputs throw before this helper writes either owner.</summary>
    internal static BaseCurrencyAdditionResult Add(GameState state, SourceWorldContext current,
        BaseReference home, int amount, Func<BaseReference, int?> readGoldCapacity)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(readGoldCapacity);

        BaseReference capacityCity = CurrencyCity(home, current);
        int cap = readGoldCapacity(capacityCity) ?? throw Missing("caller_capacity");
        BaseReference readCity = CurrencyCity(home, current);
        int before = current.ReadBaseResource(readCity, BaseResourceKind.Gold)
            ?? throw Missing("current_gold");
        int requested = Bound(unchecked(before + amount), cap);

        BaseReference setCity = CurrencyCity(home, current);
        if (setCity.Kind != BaseKind.City || setCity.PhysicalSlot is not int cityId ||
            !state.Cities.TryGetValue(cityId, out var city))
            throw Missing("currency_city_entity");
        int setterCap = readGoldCapacity(setCity) ?? throw Missing("setter_capacity");
        int after = Bound(requested, setterCap);
        var cities = new Dictionary<int, CityState>(state.Cities) { [cityId] = city with { Gold = after } };
        current.SetBaseResource(setCity, BaseResourceKind.Gold, after);
        return new(state with { Cities = cities },
            new(home, capacityCity, readCity, setCity, amount, before, cap, requested, setterCap, after));
    }

    internal static BaseReference CurrencyCity(BaseReference home, SourceWorldContext current) => home.Kind switch
    {
        BaseKind.City => home,
        BaseKind.Gate => CurrentGateMapQueries.FindCity(home, current),
        _ => throw Missing("base_kind")
    };
    private static int Bound(int value, int capacity) => value < 0 ? 0 : Math.Min(value, capacity);
    private static InvalidDataException Missing(string input) => new("current.BaseCurrencyAddition_" + input);
}
