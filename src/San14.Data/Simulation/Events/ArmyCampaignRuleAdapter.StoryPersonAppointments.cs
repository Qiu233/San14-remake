using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal (GameState State, int NativeReturn) ApplyStoryPersonAppointmentCurrent(
        GameState state, SourceWorldContext current, int code, IReadOnlyList<int> arguments,
        int? selectedPerson = null, ForceReference? selectedForce = null,
        IReadOnlyList<int>? people = null, Func<IReadOnlyList<int>>? preferredPeople = null,
        IReadOnlyList<S14EventConditionCityPointer>? cities = null,
        Action<int>? recordFreed = null, Action<ForceReference, int>? recordForceChanged = null)
    {
        if (arguments.Count != 8) throw StoryAppointmentMissing("operands");
        var operation = new EventPersonMovementCurrent(this, state, current,
            new EditedEventResultSelectors(current, null, () => BindCampaignStoryInputs(state, current, false)));
        return (operation.ApplyStoryAppointments(code, selectedPerson, selectedForce,
            people, preferredPeople, cities, recordFreed, recordForceChanged), 1);
    }

    private sealed partial class EventPersonMovementCurrent
    {
        internal GameState ApplyStoryAppointments(int code, int? selectedPerson, ForceReference? selectedForce,
            IReadOnlyList<int>? people, Func<IReadOnlyList<int>>? preferredPeople,
            IReadOnlyList<S14EventConditionCityPointer>? cities,
            Action<int>? recordFreed, Action<ForceReference, int>? recordForceChanged)
        {
            storyImmediateRetirement = true;
            var oldAdvisor = current.AssignmentAdvisorResetCallbacks;
            var oldArea = current.AssignmentAreaOfficerRemoval;
            var oldPolicy = current.BaseForcePolicyEffectReader;
            try
            {
                if (code == 163)
                {
                    int person = selectedPerson ?? throw StoryAppointmentMissing("free_Person");
                    if (!Need(current.ReadPersonNativeValidity(person), "story_163_Person_validity")) return latest;
                    BindInputs();
                    SplitForeignTravelGroup(person); //26C580 only, not26CAF0.
                    if ((Need(current.ReadArmyReturnPersonFlags196(person), "story_163_Person15E") & 0x4000) == 0 ||
                        Need(current.ReadPersonStatus(person), "story_163_PersonE6") != 5)
                    {
                        FreeStoryPerson(person);
                        if (Need(current.ReadEventManagerPresent(), "story_163_manager_presence"))
                            (recordFreed ?? throw StoryAppointmentMissing("free_occurrence_recorder"))(person);
                    }
                }
                else
                {
                    var force = selectedForce ?? throw StoryAppointmentMissing("selected_Force");
                    if (!Need(current.ReadForceNativeValidity(force), "story_appointment_Force_validity")) return latest;
                    if (code == 287) { CheckStoryPosts(force); return latest; }
                    else if (code is 256 or 270)
                    {
                        var district = ForcePrimaryDistrictSource.Resolve(current, force);
                        if (!Need(current.ReadDistrictNativeValidity(district), "story_primary_District_validity")) return latest;
                        BindInputs();
                        if (code == 256) ChangeStoryDistrict(force, district,
                            people ?? throw StoryAppointmentMissing("Person_list"), recordForceChanged);
                        else StaffStoryCities(force, district,
                            preferredPeople ?? throw StoryAppointmentMissing("staffing_preferred_People"),
                            cities ?? throw StoryAppointmentMissing("staffing_City_list"));
                    }
                    else throw StoryAppointmentMissing("opcode");
                }
                Publish();
                if (effects.Count != 0)
                    latest = latest with { CampaignEffectRequests = (latest.CampaignEffectRequests
                        ?? throw StoryAppointmentMissing("effect_owner")).Append(CampaignEffectSource.ForceAssociationRemoval,
                            latest.NativeMapSeason?.AdvancedCalendarSteps ?? throw StoryAppointmentMissing("effect_calendar"), effects.ToArray()) };
                return latest;
            }
            finally
            {
                current.AssignmentAdvisorResetCallbacks = oldAdvisor;
                current.AssignmentAreaOfficerRemoval = oldArea;
                current.BaseForcePolicyEffectReader = oldPolicy;
            }
        }

        private void ChangeStoryDistrict(ForceReference force, DistrictReference district, IReadOnlyList<int> people,
            Action<ForceReference, int>? recordForceChanged)
        {
            int requested = Need(current.ReadDistrictPhysicalSlot(district), "story_256_District_pointer");
            foreach (int person in people)
            {
                //There is no Person-validity gate around this list loop.
                SplitForeignTravelGroup(person);
                ushort flags = Need(current.ReadArmyReturnPersonFlags196(person), "story_256_Person15E");
                if ((flags & 0x4000) == 0)
                {
                    var assignments = new List<PersonAssignmentPrecallResult>();
                    PersonDistrictTransfer.Apply(person, requested,
                        ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, assignments,
                            assignment => ReceiveAssignment(person, assignment)));
                    latest = PersonAssignmentPrecallInputs.PublishDeathTouched(latest, current, person, assignments);
                    var native = latest.NativeArmySimulation ?? throw StoryAppointmentMissing("service_year_owner");
                    latest = latest with { NativeArmySimulation = native with { Parameters = native.Parameters with
                        { Int32Values = new(native.Parameters.Int32Values)
                            { [OwnedArmyGovernorComparatorInputs.PersonWord166Key(person)] = Math.Clamp((int)Date.Year, 0, 350) } } } };
                    if (Need(current.ReadEventManagerPresent(), "story_256_manager_presence"))
                        (recordForceChanged ?? throw StoryAppointmentMissing("Force_change_occurrence_recorder"))(force, person);
                }
                else if (Need(current.ReadPersonStatus(person), "story_256_PersonE6") != 5)
                    FreeStoryPerson(person); //This branch has no3D5730 or3D6030 notification.
            }
        }

        private void CheckStoryPosts(ForceReference force)
        {
            int forceId = Need(current.ReadForcePhysicalSlot(force), "story_287_Force_pointer");
            int rank = Need(current.ReadForceRankSlot47(forceId), "story_287_Force_rank");
            var definition = current.ReadStaticTableReference(0x78490, rank < 13 ? rank : 0)
                ?? throw StoryAppointmentMissing("rank_pointer");
            if (definition.PhysicalSlot is null) throw StoryAppointmentMissing("rank_identity");
            if (definition.PhysicalSlot == 0) return;
            int minimum = Need(OwnedArmyMoraleWaveInputs.ReadRankAC(latest, rank < 13 ? rank : 0), "story_287_RankAC");
            foreach (int person in StoryForcePeople(forceId))
            {
                int post = Need(current.ReadMoraleWavePersonPostE7(person), "story_287_Person_post");
                if (Need(OwnedArmyMoraleWaveInputs.ReadPost37(latest, post), "story_287_Post37") >= minimum) continue;
                current.SetMoraleWavePersonPostE7(person, 0x49);
                int id = Need(current.ReadPersonIdentity(person), "story_287_Person_identity");
                var officer = latest.Officers.GetValueOrDefault(id) ?? throw StoryAppointmentMissing("post_Person_owner");
                latest = latest with { Officers = new(latest.Officers)
                    { [id] = officer with { Appointment = OfficerAppointment.Synchronize(0x49) } } };
            }
        }

        private IEnumerable<int> StoryForcePeople(int forceId)
        {
            if (forceId is < 1 or > 51) yield break;
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw StoryAppointmentMissing("Person_registry"))
            {
                int person = Need(entry, "story_appointment_registered_Person");
                if (Need(current.ReadPersonOldForceSlot(person), "story_appointment_iterator_Force") != forceId) continue;
                if (Need(current.ReadPersonStatus(person), "story_appointment_iterator_status") is >= 1 and <= 4) yield return person;
            }
        }
    }
}
