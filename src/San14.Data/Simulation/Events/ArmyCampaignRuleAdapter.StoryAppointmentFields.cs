using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal (GameState State, int NativeReturn) ApplyStoryAppointmentFieldsCurrent(
        GameState state, SourceWorldContext current, int code, BaseReference? selectedBase,
        int? firstPerson, int? secondPerson)
    {
        if (code == 151)
        {
            var home = selectedBase ?? throw StoryPostMissing("selected_Base");
            if (!K(current.ReadBaseNativeValidity(home), "Base_validity") ||
                K(current.ReadBaseObjectLink(home), "Base4E") >= 52) return (state, 1);
            if (home.Kind != BaseKind.City) throw StoryPostMissing("CA_CC_nonCity_receiver");
            int city = K(home.PhysicalSlot, "City_identity");
            current.SetAssignmentCityCA(city, 0);
            current.SetAssignmentCityCC(city, 0);
            state = PersonAssignmentPrecallInputs.PublishAreaAppointments(state, current, [city], [], []);
            return (state, 1);
        }
        int first = firstPerson ?? throw StoryPostMissing("first_Person");
        if (code == 443)
        {
            if (K(current.ReadPersonNativeValidity(first), "Person_validity")) WritePost(first, 0x49);
        }
        else if (code == 320)
        {
            int second = secondPerson ?? throw StoryPostMissing("second_Person");
            if (K(current.ReadPersonNativeValidity(first), "donor_validity") &&
                K(current.ReadPersonNativeValidity(second), "recipient_validity") && first != second)
            {
                byte old = K(current.ReadMoraleWavePersonPostE7(first), "donor_PostE7");
                byte recipient = K(current.ReadMoraleWavePersonPostE7(second), "recipient_PostE7");
                int donorPost = K(current.ReadStaticTableReference(0x784F8, old)?.PhysicalSlot, "donor_Post_pointer");
                int recipientPost = K(current.ReadStaticTableReference(0x784F8, recipient)?.PhysicalSlot, "recipient_Post_pointer");
                if (K(OwnedArmyMoraleWaveInputs.ReadPost37(state, donorPost), "donor_Post37") <
                    K(OwnedArmyMoraleWaveInputs.ReadPost37(state, recipientPost), "recipient_Post37"))
                {
                    int anchor = K(current.ReadStaticTableReference(0x784F8, 0)?.PhysicalSlot, "Post_anchor");
                    WritePost(second, (byte)Math.Clamp(donorPost - anchor, 0, 255)); //22E0D0.
                    WritePost(first, 0x49);
                }
            }
        }
        else throw StoryPostMissing("opcode");
        return (state, 1);

        void WritePost(int physical, byte post)
        {
            current.SetMoraleWavePersonPostE7(physical, post);
            int id = K(current.ReadPersonIdentity(physical), "written_Person_identity");
            var person = state.Officers.GetValueOrDefault(id) ?? throw StoryPostMissing("written_Person_owner");
            state = state with { Officers = new(state.Officers)
                { [id] = person with { Appointment = OfficerAppointment.Synchronize(post) } } };
        }
        T K<T>(T? value, string field) where T : struct => value ?? throw StoryPostMissing(field);
    }

    private static InvalidDataException StoryPostMissing(string field) => new("story.appointment_fields." + field);
}
