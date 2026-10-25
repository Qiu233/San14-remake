namespace San14.Core;

/// <summary>Provisional area-officer land occupation, separate from city development.</summary>
public static class PlayableAreaOfficerOccupation
{
    public static Dictionary<int, TerritoryState>? Settle(GameState original,
        IReadOnlyDictionary<int, CityState> cities, IReadOnlyDictionary<int, AreaState> areas,
        IReadOnlyDictionary<int, OfficerState> officers, IReadOnlyDictionary<int, UnitState> units,
        Dictionary<int, TerritoryState>? changed, PlayableLandRoutes routes,
        List<PeriodReportEntry> reports)
    {
        foreach (AreaState area in areas.Values.OrderBy(area => area.Id))
        {
            if (area.ForceId is not int forceId || area.HexId is not int center ||
                area.Administration?.OverseerId is not int officerId ||
                area.CityId is not int cityId || !cities.TryGetValue(cityId, out CityState? city) || city.ForceId != forceId ||
                !officers.TryGetValue(officerId, out OfficerState? officer) ||
                officer.ForceId != forceId || officer.LocationId != city.Id ||
                officer.Presence != OfficerPresence.Present || officer.Deployed ||
                PlayableOfficerAvailability.IsSourceBusy(original, officer))
                continue;
            if (units.Values.Any(unit => PlayableDiplomacy.IsHostile(original, forceId, unit.ForceId) &&
                    (changed ?? original.Territory)[unit.HexId].AreaId == area.Id))
                continue;
            if (officer.BaseAbilities is not OfficerAbilities abilities)
            {
                reports.Add(new PeriodReportEntry("officer_territory_unknown", forceId,
                    city.Id, officerId, area.Id, center, center));
                continue;
            }
            // Only the dependency on charm is known. This small quota is a replaceable
            // playable-rule placeholder, not a claim about the original formula.
            int maximum = Math.Clamp((abilities.Charm + 19) / 20, 1, 5);
            int[] selected = routes.NearestAreaCells(center, area.Id, hex =>
                PlayableDiplomacy.IsHostile(original, forceId,
                    (changed ?? original.Territory)[hex].ForceId), maximum);
            if (selected.Length == 0) continue;
            changed = PlayableTerritoryPainting.PaintTraversed(original.Territory,
                changed, selected, forceId, out int painted);
            if (painted > 0)
                reports.Add(new PeriodReportEntry("officer_territory", forceId,
                    city.Id, officerId, painted, center, selected[^1]));
        }
        return changed;
    }
}
