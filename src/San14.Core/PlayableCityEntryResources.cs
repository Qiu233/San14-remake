namespace San14.Core;

/// <summary>A resource-stage result, independent of commander transfer and
/// Army retirement. A missing quote leaves known wounded troops unresolved.</summary>
public sealed record PlayableCityEntryResourceQuote(CityState City, string RuleId);

public interface IPlayableCityEntryResourceRules
{
    PlayableCityEntryResourceQuote? Quote(GameState state, UnitState unit, CityState city);
}

public static class PlayableCityEntryResources
{
    public static PlayableCityEntryResourceQuote? Quote(GameState state, UnitState unit,
        CityState city, IPlayableCityEntryResourceRules? rules)
    {
        if (rules is null || unit.Mission?.Kind == UnitMissionKind.Transport) return null;
        PlayableCityEntryResourceQuote? result = rules.Quote(state, unit, city);
        if (result is null) return null;
        if (result.City is not { } next || string.IsNullOrWhiteSpace(result.RuleId) ||
            next.Id != city.Id || next.ForceId != city.ForceId || next.HexId != city.HexId ||
            next.DistrictId != city.DistrictId || next.Troops < 0 || next.Supplies < 0 || next.Gold < 0 ||
            next.NativeReturnRuntime?.Wounded40 is < 0 ||
            unit.WoundedTroops is > 0 && (city.NativeReturnRuntime?.Wounded40 is null || next.NativeReturnRuntime?.Wounded40 is null))
            throw new InvalidDataException("City entry resource result changed the receiving base or has invalid quantities.");
        // Only this stage's numeric writes are published; appointments and the
        // independent retirement/header owners belong to their own operations.
        return result with { City = city with { Troops = next.Troops, Supplies = next.Supplies,
            Gold = next.Gold, Morale = next.Morale,
            NativeReturnRuntime = city.NativeReturnRuntime is { } previous && next.NativeReturnRuntime is { } current
                ? previous with { Wounded40 = current.Wounded40, Morale44 = current.Morale44 } : city.NativeReturnRuntime } };
    }
}
