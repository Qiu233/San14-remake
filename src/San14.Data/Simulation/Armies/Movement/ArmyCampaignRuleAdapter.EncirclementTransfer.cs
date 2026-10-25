using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState ApplyEncirclementComponent(GameState state, SourceWorldContext current, int force,
        RegionEncirclementCandidate component, TransferServiceState? service)
    {
        if (component.BoundaryAreas.Length == 0) return state;
        if (force is < 1 or > 45) throw OccupationMissing("encirclement_special_force");
        bool Relation(int other) => OccupationKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state, force, other), "encirclement_relation");
        var hexes = new List<HexReference>();
        foreach (int area in component.CapturedAreas.Concat(component.BoundaryAreas))
            for (int h = AreaHexIterator.FindNextIndex(0, area, current); h < 48400; h = AreaHexIterator.FindNextIndex(h + 1, area, current))
            {
                var hex = current.ReadHexTableReference(h) ?? throw OccupationMissing("encirclement_Hex");
                if (!hex.PhysicalSlot.HasValue || !Relation(OccupationKnown(current.ReadHexForceSlot(hex), "encirclement_Hex_force"))) continue;
                if (!GateCaptureTerritorySource.Eligible(state, current, hex, force, true) && EncirclementObjectKind(current, hex) != 3) continue;
                bool blocked = false;
                foreach (int near in TacticRemakeCurrentContext.NativeHexOrder(h, 2).Skip(1))
                {
                    var neighbor = current.ReadHexTableReference(near) ?? throw OccupationMissing("encirclement_ring_Hex");
                    int link = OccupationKnown(current.ReadHexObjectLink(neighbor), "encirclement_ring_link");
                    var city = current.ReadCityTableReference(link <= 51 ? link : 0) ?? throw OccupationMissing("encirclement_ring_City");
                    if (OccupationKnown(current.ReadBaseNativeValidity(city), "encirclement_ring_City_validity") &&
                        Relation(OccupationKnown(current.ReadBaseForceSlot(city), "encirclement_ring_City_force"))) { blocked = true; break; }
                    var gate = current.ReadGateTableReference(link is >= 52 and <= 61 ? link - 51 : 0) ?? throw OccupationMissing("encirclement_ring_Gate");
                    if (OccupationKnown(current.ReadBaseNativeValidity(gate), "encirclement_ring_Gate_validity") &&
                        Relation(OccupationKnown(current.ReadBaseForceSlot(gate), "encirclement_ring_Gate_force"))) { blocked = true; break; }
                }
                if (!blocked) hexes.Add(hex);
            }
        if (hexes.Count == 0) return state;
        var votes = new Dictionary<int, int>();
        foreach (int id in component.BoundaryAreas)
        {
            int raw = OccupationKnown(current.ReadAreaCitySlot(new(id)), "encirclement_boundary_City");
            var city = current.ReadCityTableReference(raw <= 51 ? raw : 0) ?? throw OccupationMissing("encirclement_boundary_City_pointer");
            if (!OccupationKnown(current.ReadBaseNativeValidity(city), "encirclement_boundary_City_validity")) continue;
            int physical = OccupationKnown(city.PhysicalSlot, "encirclement_boundary_City_identity");
            votes[physical] = votes.GetValueOrDefault(physical) + 1;
        }
        int selectedCity = votes.OrderByDescending(x => x.Value).ThenBy(x => x.Key).Select(x => x.Key).FirstOrDefault();
        int player = OccupationKnown(OwnedCurrentPlayerForce.ReadRawForce3A(state), "encirclement_player");
        bool show = force == player || component.CapturedAreas.Any(a => player != 0 && OccupationKnown(current.ReadAreaCurrentForceSlot(new(a)), "encirclement_movie_previous_force") == player);
        var working = state;
        if (show)
        {
            var pending = working.RegionOccupationFollowups!;
            var areas = component.BoundaryAreas.Concat(component.CapturedAreas.Where(a => Relation(
                OccupationKnown(current.ReadAreaCurrentForceSlot(new(a)), "encirclement_movie_Area_force")))).ToArray();
            var appearances = hexes.Select(h => { int f = OccupationKnown(current.ReadHexForceSlot(h), "encirclement_movie_Hex_force");
                return new EncirclementHexAppearance(h.PhysicalSlot!.Value, f == 0 ? null : f); }).ToArray();
            working = working with { RegionOccupationFollowups = new(pending.EncirclementSeeds, pending.Announcements,
                [.. pending.EncirclementPresentations, new(force, selectedCity == 0 ? null : selectedCity, areas, appearances)]) };
        }
        current.CurrentSceneryLayout = new(null);
        current.ArmyReturnTransferMode = new(false, null);
        current.TransferModeStateReader = () => current.ArmyReturnTransferMode;
        current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
        OwnedArmyReturnServiceInputs.BindCurrent(state, current);
        // No external callback may escape a transaction that can still fail.
        if (service is not null && service.PlatformPolicy != TransferServicePlatformPolicy.Absent)
            throw OccupationMissing("encirclement_service_platform_policy");
        current.TransferService = service ?? new();
        if (force == player && OccupationKnown(current.ReadTransferServiceEligibility(), "encirclement_service_eligibility"))
        {
            if (service is null) throw OccupationMissing("encirclement_service_owner");
            TransferService.SetFlag(35, current, service);
            bool complete = true;
            for (int bit = 1; bit <= 37; bit++)
                if (!OccupationKnown(service.ReadPrimaryFlag(bit), "encirclement_service_completion")) { complete = false; break; }
            if (complete) TransferService.SetFlag(0, current, service);
        }
        var territory = new Dictionary<int, TerritoryState>(working.Territory);
        var changedCenters = new List<int>();
        foreach (var hex in hexes)
        {
            int h = hex.PhysicalSlot!.Value;
            int city = selectedCity;
            if (OccupationKnown(current.ReadHexSurfaceCode(hex), "encirclement_surface") is >= 15 and <= 18)
            {
                int area = OccupationKnown(current.ReadHexAreaSlot(hex), "encirclement_center_Area");
                int raw = OccupationKnown(current.ReadAreaValidityCode(new(area)), "encirclement_original_City");
                var original = current.ReadCityTableReference(raw <= 51 ? raw : 0) ?? throw OccupationMissing("encirclement_original_City_pointer");
                if (OccupationKnown(current.ReadBaseForceSlot(original), "encirclement_original_City_force") == force)
                    city = OccupationKnown(original.PhysicalSlot, "encirclement_original_City_identity");
            }
            if ((GateCaptureTerritorySource.Eligible(working, current, hex, force, false) || EncirclementObjectKind(current, hex) == 3) &&
                OccupationKnown(current.ReadHexForceSlot(hex), "encirclement_wrapper_force") != force)
            {
                if (_occupationSceneryHexes.Contains(h)) throw OccupationMissing("encirclement_scenery_callbacks");
                if (OccupationKnown(current.ReadHexSurfaceCode(hex), "encirclement_wrapper_surface") is >= 15 and <= 18)
                {
                    if (city != 0) working = PrepareRegionCenterTransfer(working, current, hex, force, city, enqueueEncirclement: false);
                    changedCenters.Add(OccupationKnown(current.ReadHexAreaSlot(hex), "encirclement_changed_Area"));
                }
                current.AssignHexForce(hex, force);
                if (!territory.TryGetValue(h, out var old)) throw OccupationMissing("encirclement_territory_owner");
                territory[h] = old with { ForceId = force };
            }
            //429370 runs even when185D00 returned false.
            if (EncirclementObjectKind(current, hex) == 4 && Relation(OccupationKnown(
                current.ReadObjectForceSlot13(EncirclementObject(current, hex)), "encirclement_destroy_Object_force")))
                throw OccupationMissing("encirclement_hostile_object_destruction_queue");
        }
        var updatedAreas = new Dictionary<int, AreaState>(working.Areas);
        foreach (int id in changedCenters.Distinct())
        {
            if (!updatedAreas.TryGetValue(id, out var area)) throw OccupationMissing("encirclement_Area_owner");
            updatedAreas[id] = area with { CityId = AreaCityProjection.FromNative(OccupationKnown(current.ReadAreaCitySlot(new(id)), "encirclement_published_City")),
                ForceId = force };
        }
        var cities = new Dictionary<int, CityState>(working.Cities);
        foreach (var city in cities.Values.ToArray()) cities[city.Id] = city with {
            AreaIds = updatedAreas.Values.Where(a => a.CityId == city.Id).Select(a => a.Id).Order().ToArray() };
        working = working with { Territory = territory, Areas = updatedAreas, Cities = cities };
        int firstArea = component.CapturedAreas[0];
        int firstHex = OccupationKnown(current.ReadAreaHexIndex(new(firstArea)), "encirclement_report_Hex");
        var reportForce = current.ReadForceTableReference(force) ?? throw OccupationMissing("encirclement_report_Force");
        bool reportValid = OccupationKnown(current.ReadForceNativeValidity(reportForce), "encirclement_report_Force_validity");
        int? ruler = null;
        int district = 0;
        if (reportValid)
        {
            ruler = OccupationKnown(current.ReadForceRecord(force)?.RawRuler10, "encirclement_report_ruler");
            foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw OccupationMissing("encirclement_report_district_order"))
            {
                int id = OccupationKnown(entry?.PhysicalSlot, "encirclement_report_district");
                if (OccupationKnown(current.ReadDistrictForceId(id), "encirclement_report_district_force") == force &&
                    (OccupationKnown(current.ReadDistrictLeader(id), "encirclement_report_district_leader") == ruler ||
                    OccupationKnown(current.ReadDistrictNumber(id), "encirclement_report_district_number") == 1)) { district = id; break; }
            }
            reportValid = OccupationKnown(current.ReadDistrictNativeValidity(district), "encirclement_report_district_validity");
        }
        bool journal = player != 0 && force == player;
        if (!reportValid && !journal) return working;
        int day = Context(current).ReadCurrentSamplerWorld(working)?.Day450 ?? throw OccupationMissing("encirclement_report_current_day");
        //Persistent semantic category5 report; the player journal shares these references.
        var report = new PeriodReportEntry(reportValid ? "areas_encircled" : "areas_encircled_journal", force, selectedCity == 0 ? null : selectedCity, ruler, component.CapturedAreas.Length, firstHex, firstHex)
            { AreaId = firstArea, DistrictId = reportValid ? district : null, CampaignDay = day, IsPlayerJournalEntry = journal, RuleId = "campaign.region-encirclement" };
        return working with { PendingCommandReports = [.. working.PendingCommandReports, report] };
    }

    private static ObjectReference EncirclementObject(SourceWorldContext current, HexReference hex)
    {
        int raw = OccupationKnown(current.ReadHexObjectLink(hex), "encirclement_Object_link");
        return current.ReadObjectTableReference(raw <= 3000 ? raw : 0) ?? throw OccupationMissing("encirclement_Object_pointer");
    }
    private static int EncirclementObjectKind(SourceWorldContext current, HexReference hex)
    {
        var obj = EncirclementObject(current, hex);
        if (!OccupationKnown(current.ReadObjectNativeValidity(obj), "encirclement_Object_validity")) return 0;
        int kind = OccupationKnown(current.ReadPhysicalObject(OccupationKnown(obj.PhysicalSlot, "encirclement_Object_identity"))?.RawField12, "encirclement_Object_kind");
        var definition = current.ReadObjectDefinitionReference(kind <= 30 ? kind : 0) ?? throw OccupationMissing("encirclement_Object_definition");
        return OccupationKnown(current.ReadObjectDefinitionValidity(definition), "encirclement_Object_definition_validity")
            ? OccupationKnown(current.ReadObjectDefinitionRaw2C(definition), "encirclement_Object_category") : 0;
    }
}
