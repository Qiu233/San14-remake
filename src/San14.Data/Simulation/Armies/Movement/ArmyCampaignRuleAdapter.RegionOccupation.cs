using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //185D00's ordinary center branch. The later encirclement transaction and
    //commander balloon admission are saved requests, not silently completed.
    private static GameState PrepareRegionCenterTransfer(GameState state, SourceWorldContext current,
        HexReference hex, int force, int selectedCity, bool enqueueEncirclement)
    {
        if (selectedCity is < 1 or > 51) throw OccupationMissing("center_target_City_scope");
        var pending = state.RegionOccupationFollowups ?? throw OccupationMissing("center_followup_owner");
        bool encirclement = OccupationKnown(BattleRemakeCurrentSettings.ReadEncirclementEnabled(state), "center_encirclement_setting") && enqueueEncirclement;
        int areaId = OccupationKnown(current.ReadHexAreaSlot(hex), "center_Area");
        var area = current.ReadAreaTableReference(areaId <= 500 ? areaId : 0)
            ?? throw OccupationMissing("center_Area_pointer");
        if (!OccupationKnown(current.ReadAreaNativeValidity(area), "center_Area_validity"))
            throw OccupationMissing("center_invalid_Area");
        int oldCity = OccupationKnown(current.ReadAreaCitySlot(area), "center_current_City");
        var owner = current.ReadCityTableReference(oldCity <= 51 ? oldCity : 0)
            ?? throw OccupationMissing("center_current_City_pointer");
        AreaCityAssignmentResult? assignment = null;
        if (OccupationKnown(current.ReadBaseForceSlot(owner), "center_current_City_force") != force)
            assignment = current.AssignAreaCity(area, selectedCity);

        var notices = new List<RegionOccupationAnnouncement>();
        var city = current.ReadCityTableReference(selectedCity) ?? throw OccupationMissing("center_target_City");
        if (OccupationKnown(current.ReadBaseNativeValidity(city), "center_target_City_validity"))
        {
            int home = OccupationKnown(current.ReadBaseObjectLink(city), "center_target_City_home");
            if (home is < 1 or > 61) throw OccupationMissing("center_target_City_home_scope");
            foreach (var entry in ArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current))
            {
                if (entry.PhysicalSlot is not int physical ||
                    OccupationKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "center_Army_excluded")) continue;
                var receiver = new ArmyUnitReference(physical);
                int personId = OccupationKnown(current.ReadArmyUnitCommanderId(receiver), "center_Army_commander");
                int person = OccupationKnown(current.ReadPersonLookupReference(personId <= 6000 ? personId : 0)?.PhysicalSlot, "center_Commander_pointer");
                int actualHome = OccupationKnown(current.ReadPersonNativeValidity(person), "center_Commander_validity")
                    ? OccupationKnown(current.ReadPersonFirstLocation(person), "center_Commander_home") : 0;
                if (actualHome != home || !OccupationKnown(current.ReadArmyUnitNativeValidity(receiver), "center_Army_validity") ||
                    OccupationKnown(current.ReadArmyUnitRawHex2A(receiver), "center_Army_Hex") != hex.PhysicalSlot) continue;
                int unitId = OccupationKnown(state.ReadArmyLifecycle()!.ReceiverSlots()[physical].ActiveUnitId, "center_Army_owner");
                notices.Add(new(unitId, personId, areaId, hex.PhysicalSlot!.Value));
            }
        }

        //240AA0 changes only first-location for the registered status5 people.
        //Do not replay unrelated Base capture writers to publish that small set.
        var officers = new Dictionary<int, OfficerState>(state.Officers);
        foreach (int physical in assignment?.PersonPhysicalSlotsWritten ?? [])
        {
            int id = OccupationKnown(current.ReadPersonIdentity(physical), "center_changed_Person_identity");
            if (!officers.TryGetValue(id, out var person)) throw OccupationMissing("center_changed_Person_owner");
            int home = OccupationKnown(current.ReadPersonFirstLocation(physical), "center_changed_Person_home");
            officers[id] = person with { LocationId = home == 0 ? null : home };
        }
        return state with
        {
            Officers = assignment?.PersonPhysicalSlotsWritten.Count > 0 ? officers : state.Officers,
            RegionOccupationFollowups = new(encirclement ? [.. pending.EncirclementSeeds, new(force, areaId)] : pending.EncirclementSeeds,
                [.. pending.Announcements, .. notices], pending.EncirclementPresentations)
        };
    }

    private static (GameState State, PeriodReportEntry[] Logs) PublishOccupiedRegions(GameState state, SourceWorldContext current,
        UnitState army, IReadOnlyList<int> occupiedCenters)
    {
        if (occupiedCenters.Count == 0) return (state, []);
        var areas = new Dictionary<int, AreaState>(state.Areas);
        var logs = new List<PeriodReportEntry>();
        foreach (int hex in occupiedCenters)
        {
            int id = OccupationKnown(current.ReadHexAreaSlot(new(hex)), "published_center_Area");
            if (!areas.TryGetValue(id, out var before)) throw OccupationMissing("published_Area_owner");
            int city = OccupationKnown(current.ReadAreaCitySlot(new(id)), "published_Area_City");
            int force = OccupationKnown(current.ReadAreaCurrentForceSlot(new(id)), "published_Area_force");
            if (!state.Cities.ContainsKey(city)) throw OccupationMissing("published_City_owner");
            areas[id] = before with { CityId = city, ForceId = force == 0 ? null : force };
            //2FFC30's player log (5365, optional local-district title5F6B).
            //Store semantic references; localization is a presentation concern.
            logs.Add(new("area_occupied", army.ForceId, city, army.OfficerId, 0, hex, hex)
                { AreaId = id, RuleId = "army.progress.region-center-occupation" });
        }
        var cities = new Dictionary<int, CityState>(state.Cities);
        foreach (var city in cities.Values.ToArray())
            cities[city.Id] = city with { AreaIds = areas.Values.Where(a => a.CityId == city.Id).Select(a => a.Id).Order().ToArray() };
        return (state with { Areas = areas, Cities = cities }, logs.ToArray());
    }
}
