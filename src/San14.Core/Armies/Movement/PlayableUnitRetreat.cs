namespace San14.Core;

/// <summary>Provisional return route and arrival accounting for combat units.</summary>
public static class PlayableUnitRetreat
{
    public static (int LocationId, int HexId)? Destination(UnitState unit,
        IReadOnlyDictionary<int, CityState> cities,
        IReadOnlyDictionary<int, GateState> gates,
        IReadOnlyDictionary<int, OfficerState> officers, PlayableLandRoutes routes,
        int mapWidth)
    {
        OfficerState officer = officers[unit.OfficerId];
        int Distance(int hex) => Math.Abs(hex % mapWidth - unit.HexId % mapWidth) +
                                 Math.Abs(hex / mapWidth - unit.HexId / mapWidth);
        var candidates = cities.Values.Where(city => city.ForceId == unit.ForceId &&
                city.HexId is not null)
            .Select(city => (LocationId: city.Id, HexId: city.HexId!.Value,
                Priority: unit.SourceLocationId == city.Id ? 0
                    : officer.LocationId == city.Id ? 1 : 2));
        candidates = candidates.Concat(gates.Values.Where(gate => gate.ForceId == unit.ForceId &&
                gate.HexId is not null)
            .Select(gate => (LocationId: gate.Id, HexId: gate.HexId!.Value,
                Priority: unit.SourceLocationId == gate.Id ? 3
                    : officer.LocationId == gate.Id ? 4 : 5)));
        foreach (var candidate in candidates.OrderBy(candidate => candidate.Priority)
                     .ThenBy(candidate => Distance(candidate.HexId))
                     .ThenBy(candidate => candidate.LocationId))
            if (candidate.HexId == unit.HexId ||
                routes.Route(unit.HexId, candidate.HexId).Length > 0)
                return (candidate.LocationId, candidate.HexId);
        return null;
    }

    public static void Complete(UnitState unit, int locationId,
        Dictionary<int, CityState> cities, Dictionary<int, GateState> gates,
        Dictionary<int, OfficerState> officers, Dictionary<int, UnitState> units,
        List<PeriodReportEntry> reports, Action<UnitState>? onRetired = null,
        PlayableCityEntryResourceQuote? cityResources = null)
    {
        if (unit.WoundedTroops is > 0 && cityResources is null)
        {
            reports.Add(new PeriodReportEntry("wounded_return_unresolved", unit.ForceId,
                cities.ContainsKey(locationId) ? locationId : null, unit.OfficerId, unit.WoundedTroops.Value,
                unit.HexId, unit.HexId) { LocationId = locationId, RuleId = "unit.wounded_base_transfer" });
            return;
        }
        int? districtId;
        int? reportCityId;
        if (cities.TryGetValue(locationId, out CityState? city))
        {
            if (city.ForceId != unit.ForceId || city.HexId != unit.HexId)
                throw new InvalidDataException("Retreating unit has not reached its friendly city.");
            cities[city.Id] = cityResources?.City ?? city with
            {
                Troops = checked(city.Troops + unit.Troops)
            };
            districtId = city.DistrictId;
            reportCityId = city.Id;
        }
        else
        {
            GateState gate = gates[locationId];
            if (gate.ForceId != unit.ForceId || gate.HexId != unit.HexId)
                throw new InvalidDataException("Retreating unit has not reached its friendly gate.");
            gates[gate.Id] = gate with { Troops = checked(gate.Troops + unit.Troops) };
            districtId = gate.DistrictId;
            reportCityId = null;
        }
        OfficerState officer = officers[unit.OfficerId];
        officers[officer.Id] = officer with
        {
            Deployed = false, LocationId = locationId, DistrictId = districtId,
            LocationState = officer.LocationState is { } location
                ? location with { Position = null } : null
        };
        units.Remove(unit.Id);
        onRetired?.Invoke(unit);
        reports.Add(new PeriodReportEntry("retreat_arrived", unit.ForceId,
            reportCityId, unit.OfficerId, unit.Troops, unit.HexId, unit.HexId)
        { RuleId = cityResources?.RuleId ?? (onRetired is not null && unit.NativeArmyOrigin is not null
            ? ArmyRetirement.RuleId : null) });
    }
}
