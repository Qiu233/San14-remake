using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class EventPersonMovementCurrent
    {
        private void StaffStoryCities(ForceReference force, DistrictReference district,
            Func<IReadOnlyList<int>> preferredPeople, IReadOnlyList<S14EventConditionCityPointer> cities)
        {
            //270 copies the invocation City list. It never sorts or replaces
            //that saved list. CPredCity/3D24E0 sorts physical identity descending.
            var destinations = cities.Select(city => new BaseReference(BaseKind.City,
                city.PhysicalSlot ?? throw StoryAppointmentMissing("staffing_City_pointer")))
                .Where(city => CountStaffingHomePeople(city) == 0)
                .OrderByDescending(city => city.PhysicalSlot).ToArray();
            if (destinations.Length == 0) return;
            int primary = Need(current.ReadDistrictPhysicalSlot(district), "story_staffing_District_pointer");
            var available = new List<int>();
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots()
                ?? throw StoryAppointmentMissing("staffing_Person_registry"))
            {
                int person = Need(entry, "story_staffing_registered_Person");
                if (Need(current.ReadPersonDistrictSlot(person), "story_staffing_PersonE0") != primary) continue;
                if (Need(current.ReadPersonStatus(person), "story_staffing_PersonE6") != 4) continue;
                int id = Need(current.ReadPersonIdentity(person), "story_staffing_Person10");
                //2C7250: five ten-person groups, not the wider special-Person gate.
                if (StaffingExcludedIdentity(id)) continue;
                available.Add(person);
            }
            var preferred = preferredPeople().Where(person => Need(current.ReadPersonNativeValidity(person),
                "story_staffing_preferred_validity")).ToArray();
            if (preferred.Length > 7) throw StoryAppointmentMissing("staffing_preferred_shape");
            int forceId = Need(current.ReadForcePhysicalSlot(force), "story_staffing_Force_pointer");
            int rulerId = Need(current.ReadForceRecord(forceId)?.RawRuler10, "story_staffing_ruler10");
            int ruler = Need((rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId)
                : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "story_staffing_ruler_pointer");
            int family = Need(current.ReadPersonNativeValidity(ruler), "story_staffing_ruler_validity")
                ? Need(current.ReadConditionPersonBloodlineId(new(ruler)), "story_staffing_ruler_B2") : 0;
            var abilities = new TacticRemakeCurrentAbilities(current, rules._scenario, rules.Seed.Language,
                SimulationRuleIdentity.ForLocale(rules.Seed.Language));
            //3D2600's packed unsigned key is descending; equal keys preserve
            //the original registered-Person order. No random draw is involved.
            var ordered = available.Select(person => (Person: person, Key: StaffingKey(person)))
                .OrderByDescending(row => row.Key).Select(row => row.Person).ToList();
            foreach (var destination in destinations)
            {
                //Re-read after earlier moves; four Home residents protect an
                //origin. A Gate Home remains a Gate here (no owning-City map).
                ordered = ordered.Where(person =>
                {
                    var home = BaseLookup.Find(Need(current.ReadPersonFirstLocation(person),
                        "story_staffing_origin_E2"), current);
                    return Need(current.ReadBaseNativeValidity(home), "story_staffing_origin_validity") &&
                        CountStaffingHomePeople(home) >= 4;
                }).ToList();
                if (ordered.Count == 0) break;
                int selected = ordered[0];
                if (Need(current.ReadPersonNativeValidity(selected), "story_staffing_selected_validity"))
                    MoveStoryDirectPeople([selected], destination); //3D7BA0 -> actual3D7A40.
                ordered.RemoveAll(person => person == selected);
            }

            ulong StaffingKey(int person)
            {
                int identity = Need(current.ReadPersonIdentity(person), "story_staffing_key_Person10");
                int position = PersonPositionRepair.DecodeSecondLocation(
                    Need(current.ReadPersonSecondLocation(person), "story_staffing_key_E4")).Kind;
                uint joined = Need(rules._governorInputs.ReadCurrentPhysicalWord166(latest, person), "story_staffing_key_166");
                int blood = Need(current.ReadConditionPersonBloodlineId(new(person)), "story_staffing_key_B2");
                int ability = abilities.ResolveDimension(latest, identity, 0)
                    ?? throw StoryAppointmentMissing("staffing_key_current_leadership");
                bool duty = HasStaffingDuty(person);
                var linked = current.ReadPersonLinkedEntity(person) ?? throw StoryAppointmentMissing("staffing_key_association");
                bool associated = Need(current.ReadLinkedEntityNativeValidity(linked), "story_staffing_key_association_validity");
                int preferredIndex = Array.IndexOf(preferred, person);
                if (preferredIndex < 0) preferredIndex = preferred.Length;
                ulong traits = ((associated ? 0UL : 2UL) | (duty ? 0UL : 1UL)) << 21;
                ulong lower = unchecked((uint)(6000 - identity)) & 0xFFFFUL;
                ulong age = unchecked((uint)(short)Date.Year - joined) & 0x1FFUL;
                ulong priority = unchecked((uint)(preferred.Length - preferredIndex)) & 7UL;
                return unchecked((lower | ((traits | ((ulong)(uint)ability & 255UL)) << 16)) << 24) |
                    ((age | (blood == family ? 0x200UL : 0UL) | (priority << 10)) << 48) |
                    (position is 5 or 6 ? 0x8000000000000000UL : 0UL);
            }
        }

        private int CountStaffingHomePeople(BaseReference home)
        {
            int location = Need(current.ReadBaseObjectLink(home), "story_staffing_count_Base4E");
            if (location is < 1 or > 61) return 0;
            int count = 0;
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots()
                ?? throw StoryAppointmentMissing("staffing_count_Person_registry"))
            {
                int person = Need(entry, "story_staffing_count_Person_pointer");
                if (Need(current.ReadPersonFirstLocation(person), "story_staffing_count_E2") != location) continue;
                if (Need(current.ReadPersonStatus(person), "story_staffing_count_E6") is >= 1 and <= 4) count++;
            }
            return count;
        }

        private bool HasStaffingDuty(int person)
        {
            if (!Need(current.ReadPersonNativeValidity(person), "story_staffing_duty_Person_validity")) return false;
            int identity = Need(current.ReadPersonIdentity(person), "story_staffing_duty_Person10");
            var home = BaseLookup.Find(Need(current.ReadPersonFirstLocation(person), "story_staffing_duty_E2"), current);
            if (Need(current.ReadBaseNativeValidity(home), "story_staffing_duty_Base_validity"))
            {
                var city = CurrentGateMapQueries.FindCity(home, current);
                if (Need(current.ReadBaseNativeValidity(city), "story_staffing_duty_City_validity"))
                {
                    int at = Need(city.PhysicalSlot, "story_staffing_duty_City_pointer");
                    if (Need(current.ReadAssignmentCityCA(at), "story_staffing_recruiter") == identity ||
                        Need(current.ReadAssignmentCityCC(at), "story_staffing_trainer") == identity) return true;
                }
            }
            var vector = current.ReadAssignmentPersonAreaVector(person)
                ?? throw StoryAppointmentMissing("staffing_Person_Area_vector");
            if (vector.ActiveCount1E8 > vector.Values.Length) throw StoryAppointmentMissing("staffing_Area_vector_length");
            for (int index = 0; index < vector.ActiveCount1E8; index++)
            {
                int raw = vector.Values[index];
                var area = current.ReadAreaTableReference(raw < 501 ? raw : 0)
                    ?? throw StoryAppointmentMissing("staffing_Area_pointer");
                if (Need(current.ReadAreaNativeValidity(area), "story_staffing_Area_validity")) return true;
            }
            return false;
        }

        private static bool StaffingExcludedIdentity(int id) =>
            unchecked((uint)(id - 4111)) <= 9 || unchecked((uint)(id - 4131)) <= 9 ||
            unchecked((uint)(id - 4151)) <= 9 || unchecked((uint)(id - 4171)) <= 9 ||
            unchecked((uint)(id - 4191)) <= 9;
    }
}
