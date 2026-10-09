namespace San14.Core;

/// <summary>Provisional removal and officer return after a unit is destroyed.</summary>
public static class PlayableUnitAftermath
{
    public static void Destroy(UnitState unit, IReadOnlyDictionary<int, CityState> cities,
        IReadOnlyDictionary<int, GateState> gates,
        Dictionary<int, OfficerState> officers, Dictionary<int, UnitState> units,
        List<PeriodReportEntry> reports, int mapWidth, Action<UnitState>? onRetired = null)
    {
        if (unit.WoundedTroops is > 0)
            throw new InvalidDataException("Wounded Army destruction requires an explicit aftermath transfer rule.");
        units.Remove(unit.Id);
        OfficerState officer = officers[unit.OfficerId];
        int? home = ReturnLocation(unit, officer, cities, gates, officers, mapWidth);
        int? district = home is int locationId
            ? cities.TryGetValue(locationId, out CityState? city) ? city.DistrictId
                : gates[locationId].DistrictId
            : officer.DistrictId;
        officers[officer.Id] = officer with
        {
            Deployed = false,
            LocationId = home ?? officer.LocationId,
            DistrictId = district,
            LocationState = officer.LocationState is { } location
                ? location with { Position = null } : null
        };
        onRetired?.Invoke(unit);
        reports.Add(new PeriodReportEntry("field_destroyed", unit.ForceId,
            home is int cityId && cities.ContainsKey(cityId) ? cityId : null,
            unit.OfficerId, 0, unit.HexId, unit.HexId)
        { RuleId = onRetired is not null && unit.NativeArmyOrigin is not null
            ? PlayableNativeArmyRetirement.RuleId : null });
    }

    public static void RemoveZeroMorale(IReadOnlyDictionary<int, CityState> cities,
        IReadOnlyDictionary<int, GateState> gates,
        Dictionary<int, OfficerState> officers, Dictionary<int, UnitState> units,
        List<PeriodReportEntry> reports, int mapWidth, Action<UnitState>? onRetired = null)
    {
        foreach (UnitState unit in units.Values.Where(unit => unit.Morale <= 0)
                     .OrderBy(unit => unit.Id).ToArray())
            Destroy(unit, cities, gates, officers, units, reports, mapWidth, onRetired);
    }

    public static int? ReturnLocation(UnitState unit, OfficerState officer,
        IReadOnlyDictionary<int, CityState> cities,
        IReadOnlyDictionary<int, GateState> gates,
        IReadOnlyDictionary<int, OfficerState> officers, int mapWidth)
    {
        bool Owned(int id) => cities.TryGetValue(id, out CityState? city) &&
                                  city.ForceId == unit.ForceId ||
                              gates.TryGetValue(id, out GateState? gate) &&
                                  gate.ForceId == unit.ForceId;
        if (unit.SourceLocationId is int sourceId && Owned(sourceId)) return sourceId;
        if (officer.LocationId is int locationId && Owned(locationId)) return locationId;
        int? nearestCity = PlayablePrefectureControl.ReceivingCity(cities, officers,
            unit, unit.HexId, mapWidth);
        if (nearestCity is not null) return nearestCity;
        GateState? nearestGate = gates.Values.Where(gate => gate.ForceId == unit.ForceId &&
                gate.HexId is not null)
            .OrderBy(gate => Math.Abs(gate.HexId!.Value % mapWidth - unit.HexId % mapWidth) +
                             Math.Abs(gate.HexId.Value / mapWidth - unit.HexId / mapWidth))
            .ThenBy(gate => gate.Id).FirstOrDefault();
        return nearestGate?.Id;
    }
}
