namespace San14.Core;

public sealed record PlayableCityEntryStateResolution(GameState State, string RuleId);

/// <summary>One complete city entry, including its current native
/// people, pool and receiver writes. Unknown leaves the caller's state intact.</summary>
public interface IPlayableCityEntryStateRules
{
    PlayableCityEntryStateResolution? Return(GameState state, UnitState unit, CityState city);
    bool? IsEntryPositionAllowed(GameState state, UnitState unit, CityState city) => city.HexId == unit.HexId;
    /// <summary>When true, missing current entry inputs leave the active Army
    /// in place; resource-only retirement cannot replace its native lifecycle.</summary>
    bool RequiresCurrentState(GameState state, UnitState unit, CityState city) => false;
}

public static class PlayableCityEntryStates
{
    public const string UnresolvedRuleId = "unit.current_city_return";
    public static bool RequiresCurrentState(GameState state, UnitState unit, CityState city,
        IPlayableCityEntryStateRules? rules) => rules?.RequiresCurrentState(state, unit, city) == true;

    public static bool? IsEntryPositionAllowed(GameState state, UnitState unit, CityState city,
        IPlayableCityEntryStateRules? rules) => rules is null ? city.HexId == unit.HexId : rules.IsEntryPositionAllowed(state, unit, city);

    public static PlayableCityEntryStateResolution? Return(GameState state, UnitState unit, CityState city,
        IPlayableCityEntryStateRules? rules)
    {
        if (rules is null ||
            city.ForceId != unit.ForceId || rules.IsEntryPositionAllowed(state, unit, city) != true) return null;
        var result = rules.Return(state, unit, city);
        if (result is null) return null;
        GameState next = result.State;
        if (next is null || string.IsNullOrWhiteSpace(result.RuleId) || next.Date != state.Date ||
            next.NativeClock != state.NativeClock || next.Phase != state.Phase || next.Units.ContainsKey(unit.Id) ||
            !next.Officers.TryGetValue(unit.OfficerId, out OfficerState? commander) || commander.Deployed ||
            !next.Cities.TryGetValue(city.Id, out CityState? receiving) || receiving.ForceId != city.ForceId || receiving.HexId != city.HexId)
            throw new InvalidDataException("Complete city entry did not preserve its boundary and retire the current receiver.");
        return result;
    }
}
