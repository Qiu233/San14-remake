using San14.Core;

namespace San14.Data;

/// <summary>The setup-only before/after projection over the draft's captured
/// registered footholds. Counts include associated gates, not just cities.</summary>
public sealed record DistrictSetupDistrictComparison(int Number, int? FootholdsBefore,
    int? FootholdsAfter, int? OfficersBefore, int? OfficersAfter, string? UnresolvedOwner);

public sealed record DistrictSetupPresentation(AreaAdministrationOfficerCard? Leader,
    IReadOnlyList<DistrictSetupDistrictComparison> Districts, string? UnresolvedOwner);

public sealed partial class DistrictAdministrationDraft
{
    private sealed record SetupFootholdFact(int DistrictId, int Number, int CityId, int Officers);
    private IReadOnlyList<SetupFootholdFact>? setupFootholdFacts;
    private string? setupFootholdFactsMissing;

    public AreaAdministrationQuery<DistrictSetupPresentation> QuerySetupPresentation()
    {
        AreaAdministrationOfficerCard? leader = null;
        string? missing = null;
        try
        {
            if (LeaderId is int id && readLeader is not null) leader = readLeader(id).OfficerCard;
            if (leader is null) throw SetupMissing("leader_card");
        }
        catch (InvalidDataException error) { missing = error.Message; }
        if (rules is null || SelectionSource is null)
            return AreaAdministrationQuery<DistrictSetupPresentation>.Unknown(UnresolvedOwner ?? "district.setup.presentation.owner");
        if (setupFootholdFacts is null && setupFootholdFactsMissing is null)
        {
            try { setupFootholdFacts = CaptureSetupFootholds(SelectionSource.Current); }
            catch (InvalidDataException error) { setupFootholdFactsMissing = error.Message; }
        }
        missing ??= setupFootholdFactsMissing;
        if (rules.Districts.Count > 8)
            return AreaAdministrationQuery<DistrictSetupPresentation>.Unknown("district.setup.presentation.District_registry_count");
        var rows = new List<DistrictSetupDistrictComparison>(rules.Districts.Count);
        if (setupFootholdFacts is { } facts)
        {
            if (Number is < 0 or > 8) throw SetupMissing("number_range");
            var chosen = cities.ToHashSet();
            foreach (var district in rules.Districts)
            {
                int before = 0, after = 0, peopleBefore = 0, peopleAfter = 0;
                foreach (var fact in facts)
                {
                    if (fact.DistrictId == district.Id)
                    { before++; peopleBefore = unchecked(peopleBefore + fact.Officers); }
                    int temporary = chosen.Contains(fact.CityId) ? Number : fact.Number == Number ? 1 : fact.Number;
                    if (temporary == district.Number)
                    { after++; peopleAfter = unchecked(peopleAfter + fact.Officers); }
                }
                rows.Add(new(district.Number, before, after, peopleBefore, peopleAfter, null));
            }
        }
        else foreach (var district in rules.Districts)
            rows.Add(new(district.Number, null, null, null, null, setupFootholdFactsMissing));
        return AreaAdministrationQuery<DistrictSetupPresentation>.Known(new(leader, rows, missing));
    }

    private IReadOnlyList<SetupFootholdFact> CaptureSetupFootholds(SourceWorldContext current)
    {
        //The source CFootholdPersonIterator(mode0) counts registered serving
        //statuses1..4 at first location. Prepare it once for this captured draft.
        var counts = new Dictionary<ushort, int>();
        foreach (var entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw SetupMissing("Person_registry"))
        {
            int person = entry ?? throw SetupMissing("Person_pointer");
            byte status = SetupNeed(current.ReadPersonStatus(person), "Person_status");
            if (status is < 1 or > 4) continue;
            ushort home = SetupNeed(current.ReadPersonFirstLocation(person), "Person_first_location");
            counts[home] = unchecked(counts.GetValueOrDefault(home) + 1);
        }
        var force = current.ReadForceTableReference(ForceId) ?? throw SetupMissing("Force_pointer");
        var facts = new List<SetupFootholdFact>();
        foreach (var entry in current.ReadRegisteredBases() ?? throw SetupMissing("Foothold_registry"))
        {
            var home = entry ?? throw SetupMissing("Foothold_pointer");
            if (!SetupNeed(current.ReadBaseNativeValidity(home), "Foothold_validity")) continue;
            int owner = SetupNeed(current.ReadBaseForceSlot(home), "Foothold_Force");
            var ownerReference = current.ReadForceTableReference(owner is >= 0 and <= 51 ? owner : 0)
                ?? throw SetupMissing("Foothold_Force_pointer");
            if (ownerReference != force) continue;
            int districtId = SetupNeed(current.ReadBaseDistrictSlot(home), "Foothold_District");
            var district = current.ReadDistrictTableReference(districtId is >= 0 and <= 51 ? districtId : 0)
                ?? throw SetupMissing("Foothold_District_pointer");
            int physical = SetupNeed(district.PhysicalSlot, "Foothold_District_receiver");
            int number = SetupNeed(current.ReadDistrictNumber(physical), "District_number");
            if (number is < 0 or > 8) throw SetupMissing("District_number_range");
            var parent = CurrentGateMapQueries.FindCity(home, current);
            int city = SetupNeed(current.ReadBaseObjectLink(parent), "parent_City_link");
            ushort link = SetupNeed(current.ReadBaseObjectLink(home), "Foothold_link");
            facts.Add(new(physical, number, city, counts.GetValueOrDefault(link)));
        }
        return facts;
    }

    private static T SetupNeed<T>(T? value, string field) where T : struct => value ?? throw SetupMissing(field);
    private static InvalidDataException SetupMissing(string field) => new("district.setup.presentation." + field);
}
