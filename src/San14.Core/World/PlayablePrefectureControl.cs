namespace San14.Core;

/// <summary>Provisional transfer of a prefecture and its city-circle membership.</summary>
public static class PlayablePrefectureControl
{
    public static int? ReceivingCity(IReadOnlyDictionary<int, CityState> cities,
        IReadOnlyDictionary<int, OfficerState> officers, UnitState unit, int prefectureHex,
        int mapWidth)
    {
        if (unit.SourceLocationId is int sourceId && cities.TryGetValue(sourceId, out CityState? source) &&
            source.ForceId == unit.ForceId)
            return sourceId;
        if (officers.TryGetValue(unit.OfficerId, out OfficerState? officer) &&
            officer.LocationId is int officerCityId &&
            cities.TryGetValue(officerCityId, out CityState? officerCity) &&
            officerCity.ForceId == unit.ForceId)
            return officerCityId;
        CityState? nearest = cities.Values.Where(city => city.ForceId == unit.ForceId &&
                city.HexId is not null)
            .OrderBy(city => Math.Abs(city.HexId!.Value % mapWidth - prefectureHex % mapWidth) +
                             Math.Abs(city.HexId.Value / mapWidth - prefectureHex / mapWidth))
            .ThenBy(city => city.Id).FirstOrDefault();
        return nearest?.Id;
    }

    public static void Transfer(Dictionary<int, CityState> cities,
        Dictionary<int, AreaState> areas, int areaId, int forceId, int cityId)
    {
        AreaState area = areas[areaId];
        CityState target = cities[cityId];
        if (target.ForceId != forceId)
            throw new InvalidDataException("Prefecture destination city belongs to another force.");
        if (area.CityId != cityId)
        {
            if (area.CityId is int previousCityId)
            {
                CityState previous = cities[previousCityId];
                cities[previous.Id] = previous with
                {
                    AreaIds = previous.AreaIds.Where(id => id != areaId).ToArray()
                };
            }
            cities[target.Id] = target with
            {
                AreaIds = target.AreaIds.Contains(areaId)
                    ? target.AreaIds.ToArray() : [.. target.AreaIds, areaId]
            };
        }
        areas[areaId] = area with
        {
            CityId = cityId, ForceId = forceId,
            Administration = new AreaAdministrationState(null, AreaDevelopmentFocus.None)
        };
    }
}
