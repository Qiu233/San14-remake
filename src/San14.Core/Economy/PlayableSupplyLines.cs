namespace San14.Core;

/// <summary>Provisional land connection from a combat unit to its current supporting base.</summary>
public static class PlayableSupplyLines
{
    /// <summary>Current playable diplomacy admits alliance land, not merely a
    /// ceasefire or a good relationship. The supporting base remains the army's
    /// own source; allied bases are not replacement food payers.</summary>
    public static int? Distance(GameState state, UnitState unit, PlayableLandRoutes routes)
    {
        ArgumentNullException.ThrowIfNull(state);
        routes.ValidateWorld(state);
        return Distance(unit, state.Cities, state.Gates, state.Territory, routes,
            (actor, owner) => PlayableDiplomacy.AreAllied(state, actor, owner));
    }

    public static int? Distance(UnitState unit, IReadOnlyDictionary<int, CityState> cities,
        IReadOnlyDictionary<int, GateState> gates,
        IReadOnlyDictionary<int, TerritoryState> territory, PlayableLandRoutes routes,
        Func<int, int, bool>? isSupplyAlly = null)
    {
        if (unit.SourceLocationId is not int sourceId) return null;
        int? sourceHex = cities.TryGetValue(sourceId, out CityState? city) &&
                         city.ForceId == unit.ForceId ? city.HexId :
            gates.TryGetValue(sourceId, out GateState? gate) && gate.ForceId == unit.ForceId
                ? gate.HexId : null;
        if (sourceHex is not int origin) return null;
        return routes.DistanceThrough(unit.HexId, origin, hex =>
            territory.TryGetValue(hex, out TerritoryState? cell) &&
            (cell.ForceId == unit.ForceId || cell.ForceId is int owner &&
                isSupplyAlly?.Invoke(unit.ForceId, owner) == true));
    }

    public static void Settle(IReadOnlyDictionary<int, CityState> cities,
        IReadOnlyDictionary<int, GateState> gates, Dictionary<int, UnitState> units,
        IReadOnlyDictionary<int, TerritoryState> territory, PlayableLandRoutes routes,
        List<PeriodReportEntry> reports, Func<int, int, bool>? isSupplyAlly = null)
    {
        foreach (UnitState unit in units.Values.OrderBy(unit => unit.Id).ToArray())
        {
            // The original transport supply rule has not been established yet.
            if (unit.SourceLocationId is null ||
                unit.Mission is null or { Kind: UnitMissionKind.Transport }) continue;
            if (Distance(unit, cities, gates, territory, routes, isSupplyAlly) is not null) continue;
            int moraleLoss = Math.Min(Math.Max(unit.Morale, 0), 10);
            if (moraleLoss > 0)
                units[unit.Id] = unit with { MoraleState = unit.MoraleState with { ProvisionalValue = unit.Morale - moraleLoss } };
            reports.Add(new PeriodReportEntry("supply_cut", unit.ForceId,
                unit.SourceLocationId is int source && cities.ContainsKey(source) ? source : null,
                unit.OfficerId, moraleLoss, unit.HexId, unit.HexId));
        }
    }
}
