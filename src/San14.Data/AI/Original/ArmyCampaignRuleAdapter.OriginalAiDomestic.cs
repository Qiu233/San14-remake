using San14.Core;

namespace San14.Data;

/// <summary>A source node can succeed without issuing a command. In particular,
/// an unchanged appointment is success, whereas a zero selected focus is failure.</summary>
public readonly record struct OriginalAiAreaAppointmentPreparation(bool Success,
    GameOrder? Order);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Current 079BC0/0B1750 selection. A missing or invalid scope
    /// district falls back through the area's current city, as the source does.
    /// This query neither changes an appointment nor consumes random state.</summary>
    public int ReadOriginalAiDevelopmentFocusCurrent(GameState state, int districtId, int areaId,
        int commerceWeight, int agricultureWeight, int barracksWeight)
    {
        var area = state.Areas.GetValueOrDefault(areaId) ?? throw OriginalAiDomesticMissing("area");
        if (area.Id != areaId || areaId is < 1 or > 500 || area.CityId is not (>= 1 and <= 51))
            throw OriginalAiDomesticMissing("selection_area_validity");
        var district = OriginalAiDomesticDevelopmentDistrict(state, districtId, area);
        var development = area.Development ?? throw OriginalAiDomesticMissing("area_development");
        int currentFocus = OriginalAiDomesticFocus(area);
        int priority = district.Settings?.PriorityId ?? throw OriginalAiDomesticMissing("district_priority");
        // These are the actual District1A cases, not enum ordering inferred
        // from the names in the district menu. A completed preference falls
        // through to the ordinary weighted-level choice.
        int preferred = priority switch
        { 3 when Progress(development.Commerce, "commerce") != 1000 => 1,
          2 when Progress(development.Agriculture, "agriculture") != 1000 => 2,
          4 when Progress(development.Barracks, "barracks") != 1000 => 3, _ => 0 };
        if (preferred != 0) return preferred == currentFocus ? 0 : preferred;

        int leader = ForceUnregisterInputs.ReadDistrictLeader(state, district)
            ?? throw OriginalAiDomesticMissing("district_leader");
        int physical = OriginalAiDomesticPersonPhysical(leader);
        int Score(ushort progress, int offset, int weight)
        {
            short preference = RulerCaptureReleaseInputs.ReadCurrentWord(state, physical, offset)
                ?? throw OriginalAiDomesticMissing("leader_preference_" + offset.ToString("X"));
            // 22E400/22C890/2312D0 share the existing ordinary level table.
            return unchecked(CeAreaIncomeMath.Level(progress) * (preference + 100) * weight) / 100;
        }
        int commerceScore = Score(Progress(development.Commerce, "commerce"), 0x170, commerceWeight);
        int agricultureScore = Score(Progress(development.Agriculture, "agriculture"), 0x172, agricultureWeight);
        int barracksScore = Score(Progress(development.Barracks, "barracks"), 0x174, barracksWeight);
        int minimum = Math.Min(Math.Min(commerceScore, agricultureScore), barracksScore);
        int selected = minimum == commerceScore ? 1 : minimum == agricultureScore ? 2 : 3;
        if (selected == currentFocus && OriginalAiDomesticPersonValid(state, area.Administration!.OverseerId ?? 0))
            return 0;
        return selected;

        static ushort Progress(int? value, string field) => value is >= 0 and <= ushort.MaxValue
            ? (ushort)value.Value : throw OriginalAiDomesticMissing("area_" + field);
    }

    /// <summary>Read-only preparation of 036270. The caller submits Order through
    /// the ordinary command authority and AreaAdministrationExecution provider.
    /// A prepared order is not a fabricated successful execution receipt.</summary>
    public OriginalAiAreaAppointmentPreparation CreateOriginalAiAreaAppointmentCurrent(GameState state,
        int districtId, int areaId, int officerId, int sourceFocus)
    {
        if (sourceFocus == 0) return new(false, null);
        AreaDevelopmentFocus focus = sourceFocus switch
        { 1 => AreaDevelopmentFocus.Commerce, 2 => AreaDevelopmentFocus.Agriculture,
          3 => AreaDevelopmentFocus.Barracks, _ => throw OriginalAiDomesticMissing("appointment_focus") };
        var area = state.Areas.GetValueOrDefault(areaId) ?? throw OriginalAiDomesticMissing("appointment_area");
        if ((area.Administration?.OverseerId ?? 0) == officerId && OriginalAiDomesticFocus(area) == sourceFocus)
            return new(true, null);

        int physical = OriginalAiDomesticPersonPhysical(officerId);
        var person = physical == 0 ? null : state.Officers.GetValueOrDefault(officerId)
            ?? throw OriginalAiDomesticMissing("appointment_person");
        int personDistrict;
        if (physical == 0) personDistrict = 0;
        else
        {
            byte raw = OfficerLocationSourceAdapter.EncodeMembership(person!.LocationState?.Membership,
                (state.ResourceState as CampaignSourceKnowledge)?.Find(officerId))
                ?? throw OriginalAiDomesticMissing("person_district");
            personDistrict = raw <= 51 ? raw : 0;
        }
        int books = state.Districts.GetValueOrDefault(personDistrict)?.OrdersRemaining
            ?? (personDistrict == 0 && state.NativeForceUnregisterCurrent?.DistrictWindows10.TryGetValue(0, out var zero) == true
                && zero.Length > 4 ? zero[4] : null)
            ?? throw OriginalAiDomesticMissing("appointment_command_books");
        int cost = PersonAssignmentPrecallInputs.ReadAreaAppointmentCost(state)
            ?? throw OriginalAiDomesticMissing("appointment_cost");
        if (books < cost) return new(false, null);
        // 036270 returns success after the budget check if either receiver's
        // actual vslot18 validity is false. It does not invent an order.
        if (areaId == 0 || area.CityId is not (>= 1 and <= 51) || !OriginalAiDomesticPersonValid(state, officerId))
            return new(true, null);
        var district = OriginalAiDomesticDevelopmentDistrict(state, districtId, area);
        var changes = new List<AreaAdministrationChange>();
        int count = PersonAssignmentPrecallInputs.ReadCurrentAreaAssignmentActiveCount(state, physical)
            ?? throw OriginalAiDomesticMissing("person_area_count");
        // With at most one item, capacity>1 and count>=capacity cannot both
        // hold. Only the genuinely reached multi-appointment branch needs
        // the existing operation-local source context and capacity consumer.
        if (count >= 2)
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            int capacity = unchecked(PersonAreaAppointmentSource.AdditionalCount(state, current, physical) + 1);
            if (capacity > 1 && count >= capacity)
            {
                var vector = new PersonAssignmentPrecallInputs.AreaAppointmentDraft(state);
                for (int index = 0; index < count; index++)
                {
                    int raw = vector.Entry(physical, index);
                    var previous = current.ReadAreaTableReference(raw < 501 ? raw : 0)
                        ?? throw OriginalAiDomesticMissing("previous_area_pointer");
                    if (!(current.ReadAreaNativeValidity(previous) ?? throw OriginalAiDomesticMissing("previous_area_validity"))) continue;
                    int previousId = previous.PhysicalSlot ?? throw OriginalAiDomesticMissing("previous_area_identity");
                    changes.Add(new(previousId, null, AreaDevelopmentFocus.Commerce));
                    break;
                }
            }
        }
        changes.Add(new(areaId, officerId, focus));
        if (changes.Count > 1)
        {
            //036270 only checks one appointment cost before entering1ED380.
            //The shared formal transaction retains its full reserved-budget
            //gate. Keep this proven low-budget caller difference explicit.
            var firstArea = state.Areas.GetValueOrDefault(changes[0].AreaId)
                ?? throw OriginalAiDomesticMissing("multi_appointment_first_area");
            var firstCity = state.Cities.GetValueOrDefault(firstArea.CityId ?? 0)
                ?? throw OriginalAiDomesticMissing("multi_appointment_first_city");
            int chargedDistrict = firstCity.DistrictId
                ?? throw OriginalAiDomesticMissing("multi_appointment_first_district");
            int remaining = state.Districts.GetValueOrDefault(chargedDistrict)?.OrdersRemaining
                ?? throw OriginalAiDomesticMissing("multi_appointment_books");
            int changed = 0;
            foreach (var change in changes)
            {
                var before = state.Areas.GetValueOrDefault(change.AreaId)
                    ?? throw OriginalAiDomesticMissing("multi_appointment_area");
                if (before.Administration is not { DevelopmentFocus: not null } administration)
                    throw OriginalAiDomesticMissing("multi_appointment_current");
                if (administration.OverseerId != change.OverseerId ||
                    administration.DevelopmentFocus != change.DevelopmentFocus) changed++;
            }
            int debit = unchecked(cost * changed);
            if (cost < 0 || debit < 0 || remaining - CommandBookAvailability.Reserved(state, chargedDistrict) < debit)
                throw OriginalAiDomesticMissing("multi_appointment_budget_semantics");
        }
        return new(true, new ApplyAreaAdministrationOrder(district.ForceId, changes.AsReadOnly()));
    }

    private static DistrictState OriginalAiDomesticDevelopmentDistrict(GameState state, int districtId, AreaState area)
    {
        bool Valid(DistrictState value) => NativeValidity.District(value.Id,
            value.ForceId is >= 0 and <= byte.MaxValue ? (byte)value.ForceId : null,
            value.Number is >= 0 and <= byte.MaxValue ? (byte)value.Number : null,
            ForceUnregisterInputs.ReadDistrictLeader(state, value))
            ?? throw OriginalAiDomesticMissing("district_validity");
        if (districtId >= 0)
        {
            var requested = state.Districts.GetValueOrDefault(districtId);
            if (requested is not null && Valid(requested)) return requested;
            if (requested is null && districtId != 0) throw OriginalAiDomesticMissing("scope_district");
        }
        var city = state.Cities.GetValueOrDefault(area.CityId ?? 0) ?? throw OriginalAiDomesticMissing("fallback_city");
        int fallbackId = city.DistrictId ?? throw OriginalAiDomesticMissing("fallback_city_district");
        return state.Districts.GetValueOrDefault(fallbackId) ?? throw OriginalAiDomesticMissing("fallback_district");
    }

    private int OriginalAiDomesticPersonPhysical(int identity) => identity switch
    { < 0 => throw OriginalAiDomesticMissing("person_identity"), 0 or > 6000 => 0,
      _ => _definitions.ResolveOfficerSource(identity) ?? throw OriginalAiDomesticMissing("person_lookup") };

    private bool OriginalAiDomesticPersonValid(GameState state, int identity)
    {
        int physical = OriginalAiDomesticPersonPhysical(identity);
        if (physical == 0) return false;
        var person = state.Officers.GetValueOrDefault(identity) ?? throw OriginalAiDomesticMissing("person_current");
        return NativeValidity.PersonWithBehavior(physical, checked((ushort)identity), person.Behavior,
            OfficerServiceStatusSourceAdapter.Encode(person.ServiceStatus)) ?? throw OriginalAiDomesticMissing("person_validity");
    }

    private static int OriginalAiDomesticFocus(AreaState area) => area.Administration?.DevelopmentFocus switch
    { AreaDevelopmentFocus.None => 0, AreaDevelopmentFocus.Commerce => 1,
      AreaDevelopmentFocus.Agriculture => 2, AreaDevelopmentFocus.Barracks => 3,
      _ => throw OriginalAiDomesticMissing("area_focus") };
    private static InvalidDataException OriginalAiDomesticMissing(string input) => new("original-ai.domestic." + input);
}
