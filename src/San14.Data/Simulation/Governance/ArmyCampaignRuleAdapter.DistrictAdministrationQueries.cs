using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal AreaAdministrationQuery<string> QueryDistrictDissolveConfirmationCurrent(GameState state,
        SourceWorldContext current, int districtId)
    {
        try
        {
            return AreaAdministrationQuery<string>.Known(FormatReportMessage(state, current, 0x5946,
                new(District0: districtId), ReportMessageOrigin.PersonnelAdministration, null).Text);
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<string>.Unknown(error.Message); }
    }

    internal DistrictAdministrationRules CaptureDistrictAdministrationRules(GameState state,
        SourceWorldContext current, int forceId)
    {
        var force = new ForceReference(forceId);
        if (!DistrictNeed(current.ReadForceNativeValidity(force), "page_Force_validity"))
            throw DistrictMissing("page_Force_inactive");
        var districts = new List<DistrictAdministrationDistrict>();
        var cities = DistrictAdministrationOwnedCities(state, current, forceId);
        int? rulerCity = DistrictAdministrationRulerCity(current, force);
        foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw DistrictMissing("page_District_registry"))
        {
            int at = DistrictNeed(entry?.PhysicalSlot, "page_District_pointer");
            if (DistrictNeed(current.ReadDistrictForceId(at), "page_District_Force") != forceId) continue;
            int number = DistrictNeed(current.ReadDistrictNumber(at), "page_District_number");
            DistrictSettings? settings = null; string? missing = null;
            try { settings = ReadDistrictAdministrationSettingsCurrent(current, at); }
            catch (InvalidDataException error) { missing = error.Message; }
            ushort leader = DistrictNeed(current.ReadDistrictLeader(at), "page_District_leader");
            districts.Add(new(at, number, leader == 0 ? null : leader, settings,
                cities.Where(city => city.DistrictId == at).Select(city => city.Id).ToArray())
            {
                Name = ReadHistoryDataText(1, 0x38C + Math.Max(1, number)), IsPrimary = number == 1,
                CanDissolve = number != 1, UnresolvedOwner = missing
            });
        }
        int available = Enumerable.Range(1, 8).FirstOrDefault(number => districts.All(row => row.Number != number));
        var primary = ForcePrimaryDistrictSource.Resolve(current, force);
        int primaryId = DistrictNeed(primary.PhysicalSlot, "page_primary");
        int stock = ReadDistrictAdministrationIntelligenceCurrent(current, primaryId);
        int capacity = DistrictAdministrationIntelligenceCapacity(state, current, primaryId);
        return new(forceId, available, districts.AsReadOnly(), cities.Select(city => city with
            { Available = city.Available == true && city.Id != rulerCity }).ToArray(), ReadDistrictAdministrationInitialSettingsCurrent())
        {
            PrimaryMilitaryIntelligence = stock, MilitaryIntelligenceBefore = 0,
            MaximumMilitaryIntelligenceTransfer = Math.Max(0, Math.Min(stock, capacity))
        };
    }

    internal DistrictAdministrationSelection QueryDistrictAdministrationSelectionCurrent(GameState state,
        SourceWorldContext current, int forceId, int? districtId, IReadOnlyList<int> cityIds, DistrictSettings settings)
    {
        var leaders = new List<DistrictAdministrationLeader>();
        var targets = new List<DistrictAdministrationTarget>();
        try
        {
            var force = new ForceReference(forceId);
            bool primary = districtId is int old && DistrictNeed(current.ReadDistrictNumber(old), "selection_number") == 1;
            var own = DistrictAdministrationOwnedCities(state, current, forceId);
            int? rulerCity = DistrictAdministrationRulerCity(current, force);
            bool scope = cityIds.Count > 0 && cityIds.Distinct().Count() == cityIds.Count &&
                cityIds.All(id => own.Any(row => row.Id == id)) &&
                (primary ? rulerCity is null || cityIds.Contains(rulerCity.Value) : !cityIds.Contains(rulerCity ?? -1));
            var bases = DistrictAdministrationBases(current, cityIds);
            foreach (var at in bases)
                foreach (int person in DistrictAdministrationHomePeople(current, at))
                {
                    bool valid = DistrictNeed(current.ReadPersonNativeValidity(person), "leader_validity");
                    if (valid && (DuelAftermathSpecialPerson(current, person) ||
                        (DistrictNeed(current.ReadArmyReturnPersonFlags196(person), "leader_flags15E") & 0x4000) != 0)) continue;
                    int id = DistrictNeed(current.ReadPersonIdentity(person), "leader_identity");
                    leaders.Add(new(id, state.Officers.GetValueOrDefault(id)?.Name ?? throw DistrictMissing("leader_domain")));
                }
            bool cityCandidate = cityIds.Any(id => DistrictAdministrationHomePeople(current, BaseLookup.Find((ushort)id, current))
                .Any(person => DistrictNeed(current.ReadPersonNativeValidity(person), "selection_Person_validity") &&
                    !DuelAftermathSpecialPerson(current, person) &&
                    (DistrictNeed(current.ReadArmyReturnPersonFlags196(person), "selection_Person_flags") & 0x4000) == 0));
            targets.AddRange(ReadDistrictAdministrationTargetsCurrent(state, current, forceId, cityIds, settings.PolicyId));
            bool targetValid = settings.PolicyId is not (2 or 3 or 5) ? settings.Target is null :
                targets.Any(row => row.Available == true && row.Target == settings.Target);
            int count = (current.ReadArmyReturnRegisteredDistricts() ?? throw DistrictMissing("selection_District_registry"))
                .Count(row => row?.PhysicalSlot is int at && DistrictNeed(current.ReadDistrictForceId(at), "selection_Force") == forceId);
            bool createRoom = districtId is not null || count < 8 && Enumerable.Range(1, 45)
                .Any(at => !DistrictNeed(current.ReadDistrictNativeValidity(at), "selection_free_District"));
            int primaryId = DistrictNeed(ForcePrimaryDistrictSource.Resolve(current, force).PhysicalSlot, "selection_primary");
            int stock = ReadDistrictAdministrationIntelligenceCurrent(current, primaryId);
            int before = districtId is int selected ? ReadDistrictAdministrationIntelligenceCurrent(current, selected) : 0;
            int cap = DistrictAdministrationIntelligenceCapacity(state, current, districtId ?? primaryId);
            bool? canContinue; string? continueMissing = null;
            try { canContinue = districtId is not null ? count >= 2 : CanCreateAnotherDistrictCurrent(state, current, forceId); }
            catch (InvalidDataException error) { canContinue = null; continueMissing = error.Message; }
            return new(leaders.AsReadOnly(), targets.AsReadOnly(), scope && cityCandidate && leaders.Count > 0 && targetValid && createRoom,
                canContinue, null)
            {
                CanConfirmCities = scope && cityCandidate && leaders.Count > 0,
                CanContinueCities = scope && cityCandidate && leaders.Count > 0,
                PrimaryMilitaryIntelligence = stock, MilitaryIntelligenceBefore = before,
                MaximumMilitaryIntelligenceTransfer = Math.Max(0, Math.Min(stock, cap - before)),
                ContinueUnresolvedOwner = continueMissing
            };
        }
        catch (InvalidDataException error) { return new(leaders.AsReadOnly(), targets.AsReadOnly(), null, null, error.Message); }
    }

    private static List<DistrictAdministrationCity> DistrictAdministrationOwnedCities(GameState state,
        SourceWorldContext current, int forceId)
    {
        var rows = new List<DistrictAdministrationCity>();
        for (int slot = 0; slot < 52; slot++)
        {
            var city = current.ReadCityTableReference(slot) ?? throw DistrictMissing("City_table");
            if (DistrictNeed(current.ReadBaseForceSlot(city), "City_Force") != forceId) continue;
            int id = DistrictNeed(current.ReadBaseObjectLink(city), "City4E");
            var saved = state.Cities.GetValueOrDefault(id) ?? throw DistrictMissing("City_domain");
            int district = DistrictNeed(current.ReadBaseDistrictSlot(city), "City_District");
            rows.Add(new(id, saved.Name, saved.HexId, district, DistrictNeed(current.ReadBaseNativeValidity(city), "City_validity"), null));
        }
        return rows;
    }

    private static int? DistrictAdministrationRulerCity(SourceWorldContext current, ForceReference force)
    {
        int at = DistrictNeed(force.PhysicalSlot, "ruler_Force_pointer");
        ushort rulerId = DistrictNeed(current.ReadForceRecord(at)?.RawRuler10, "ruler_Force10");
        var ruler = current.ReadPersonLookupReference(rulerId <= 6000 ? rulerId : 0) ?? throw DistrictMissing("ruler_pointer");
        int physical = DistrictNeed(ruler.PhysicalSlot, "ruler_physical");
        if (!DistrictNeed(current.ReadPersonNativeValidity(physical), "ruler_validity")) return null;
        var home = BaseLookup.Find(DistrictNeed(current.ReadPersonFirstLocation(physical), "ruler_E2"), current);
        if (!DistrictNeed(current.ReadBaseNativeValidity(home), "ruler_home_validity")) return null;
        var city = CurrentGateMapQueries.FindCity(home, current);
        return DistrictNeed(current.ReadBaseObjectLink(city), "ruler_City4E");
    }

    internal static IReadOnlyList<BaseReference> DistrictAdministrationBases(SourceWorldContext current, IReadOnlyList<int> cities)
    {
        var result = new List<BaseReference>();
        foreach (int id in cities)
        {
            var city = BaseLookup.Find(checked((ushort)id), current); result.Add(city);
            foreach (var gate in current.ReadArmyReturnRegisteredGates() ?? throw DistrictMissing("Gate_registry"))
            {
                var at = gate ?? throw DistrictMissing("Gate_pointer");
                if (CurrentGateMapQueries.FindCity(at, current) == city &&
                    DistrictNeed(current.ReadBaseForceSlot(at), "Gate_Force") == DistrictNeed(current.ReadBaseForceSlot(city), "City_Force")) result.Add(at);
            }
        }
        return result;
    }

    internal static IEnumerable<int> DistrictAdministrationHomePeople(SourceWorldContext current, BaseReference at)
    {
        int location = DistrictNeed(current.ReadBaseObjectLink(at), "Home4E");
        foreach (int? row in current.ReadRegisteredPersonPhysicalSlots() ?? throw DistrictMissing("Person_registry"))
        {
            int person = DistrictNeed(row, "Person_pointer");
            if (location is >= 1 and <= 61 && DistrictNeed(current.ReadPersonFirstLocation(person), "Home_E2") == location &&
                DistrictNeed(current.ReadPersonStatus(person), "Home_status") is >= 1 and <= 4) yield return person;
        }
    }

    private IEnumerable<DistrictAdministrationTarget> ReadDistrictAdministrationTargetsCurrent(GameState state,
        SourceWorldContext current, int forceId, IReadOnlyList<int> selectedCities, int policy)
    {
        if (policy is not (2 or 3 or 5)) yield break;
        var markers = new List<(BaseReference Base, int Id, int Force, int? District, string Name, int Hex)>();
        for (int id = 0; id <= 61; id++)
        {
            var at = BaseLookup.Find((ushort)id, current);
            if (!DistrictNeed(current.ReadBaseNativeValidity(at), "target_Base_validity")) continue;
            int identity = DistrictNeed(current.ReadBaseObjectLink(at), "target_Base4E");
            int owner = DistrictNeed(current.ReadBaseForceSlot(at), "target_Base_Force");
            int district = DistrictNeed(current.ReadBaseDistrictSlot(at), "target_Base_District");
            string name; int hex;
            if (identity <= 51)
            { var city = state.Cities.GetValueOrDefault(identity) ?? throw DistrictMissing("target_City_owner"); name = city.Name; hex = city.HexId ?? throw DistrictMissing("target_City_Hex"); }
            else
            { var gate = state.Gates.GetValueOrDefault(identity) ?? throw DistrictMissing("target_Gate_owner"); name = gate.Name; hex = gate.HexId ?? throw DistrictMissing("target_Gate_Hex"); }
            bool ownerValid = DistrictNeed(current.ReadForceNativeValidity(new ForceReference(owner)), "target_Force_validity");
            if (policy is 2 or 3)
            {
                if (ownerValid && (owner == forceId || !DistrictNeed(ArmyLogisticsPreparation.ReadNativeRelationGate(state, forceId, owner), "target_relation"))) continue;
            }
            else if (owner != forceId || identity > 51 || selectedCities.Contains(identity) ||
                !DistrictNeed(current.ReadDistrictNativeValidity(district), "target_District_validity")) continue;
            markers.Add((at, identity, owner, district, name, hex));
        }
        if (policy == 3)
        { foreach (var marker in markers) yield return new(new(DistrictTargetKind.Foothold, marker.Id), marker.Name, [marker.Hex], true, null); }
        else if (policy == 2)
        {
            foreach (var marker in markers.Where(row => DistrictNeed(current.ReadForceNativeValidity(new ForceReference(row.Force)), "target_Force_validity")).DistinctBy(row => row.Force))
                yield return new(new(DistrictTargetKind.Force, marker.Force), state.Forces.GetValueOrDefault(marker.Force)?.Name ?? "",
                    markers.Where(row => row.Force == marker.Force).Select(row => row.Hex).ToArray(), true, null);
        }
        else
            foreach (var marker in markers.DistinctBy(row => row.District))
                yield return new(new(DistrictTargetKind.District, marker.District!.Value), ReadHistoryDataText(1,
                    0x38C + Math.Max(1, (int)DistrictNeed(current.ReadDistrictNumber(marker.District.Value), "target_District_number"))),
                    markers.Where(row => row.District == marker.District).Select(row => row.Hex).ToArray(), true, null);
    }

    //70F030's pre-creation menu gate. The real handler repeats it after a
    //commit before continuing; this read never anticipates a fabricated world.
    private static bool CanCreateAnotherDistrictCurrent(GameState state, SourceWorldContext current, int forceId)
    {
        if (Enumerable.Range(1, 51).Count(at => !DistrictNeed(current.ReadDistrictNativeValidity(at), "continue_free_District")) < 2) return false;
        int count = (current.ReadArmyReturnRegisteredDistricts() ?? throw DistrictMissing("continue_District_registry"))
            .Count(row => row?.PhysicalSlot is int at && DistrictNeed(current.ReadDistrictForceId(at), "continue_Force") == forceId);
        int cities = DistrictAdministrationOwnedCities(state, current, forceId).Count;
        if (count > cities || count >= 8) return false;
        int ruler = DistrictNeed(current.ReadPersonLookupReference(
            DistrictNeed(current.ReadForceRecord(forceId)?.RawRuler10, "continue_ruler_id"))?.PhysicalSlot, "continue_ruler");
        var rulerHome = PersonPositionRepair.DecodeFirstLocation(DistrictNeed(current.ReadPersonFirstLocation(ruler), "continue_ruler_E2"));
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw DistrictMissing("continue_Person_registry"))
        {
            int person = DistrictNeed(entry, "continue_Person_pointer");
            if (DistrictNeed(current.ReadPersonOldForceSlot(person), "continue_person_Force") != forceId ||
                DistrictNeed(current.ReadPersonStatus(person), "continue_person_status") is < 1 or > 4) continue;
            var linked = current.ReadPersonLinkedEntity(person) ?? throw DistrictMissing("continue_associated");
            if (linked.Identity is not null) continue;
            ushort second = DistrictNeed(current.ReadPersonSecondLocation(person), "continue_E4");
            var army = current.ReadArmyUnitTableReference(second is >= 62 and <= 561 ? second - 61 : 0) ?? throw DistrictMissing("continue_Army");
            if (DistrictNeed(current.ReadArmyUnitCommanderId(army), "continue_commander") != DistrictNeed(current.ReadPersonIdentity(person), "continue_Person_identity"))
                army = current.ReadArmyUnitTableReference(0) ?? throw DistrictMissing("continue_Army0");
            if (DistrictNeed(current.ReadArmyUnitNativeValidity(army), "continue_Army_validity")) continue;
            ushort first = DistrictNeed(current.ReadPersonFirstLocation(person), "continue_E2");
            if (PersonPositionRepair.DecodeFirstLocation(first) == rulerHome) continue;
            var home = BaseLookup.Find(first, current);
            if (DistrictNeed(current.ReadBaseNativeValidity(home), "continue_home_validity"))
            {
                var district = current.ReadBaseDistrictReference(home) ?? throw DistrictMissing("continue_home_District");
                if (DistrictNeed(current.ReadDistrictNativeValidity(district), "continue_home_District_validity"))
                {
                    int chief = DistrictNeed(current.ReadPersonLookupReference(DistrictNeed(current.ReadDistrictLeader(
                        DistrictNeed(district.PhysicalSlot, "continue_District")), "continue_District12"))?.PhysicalSlot, "continue_chief");
                    if (BaseLookup.Find(DistrictNeed(current.ReadPersonFirstLocation(chief), "continue_chief_E2"), current) == home) continue;
                }
            }
            return true;
        }
        return false;
    }
}
