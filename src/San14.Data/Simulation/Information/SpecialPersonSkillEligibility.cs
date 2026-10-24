using San14.Core;

namespace San14.Data;

internal readonly record struct SpecialPersonSkillEligibilityResult(bool? Allowed, string? UnresolvedOwner);

/// <summary>The8EA30x/8EA4xx information-list gates. Special definitions
/// require an available original-person record that actually supplies the
/// requested skill. This catalog is independent of the live campaign people.</summary>
internal static class SpecialPersonSkillEligibility
{
    internal static bool IsSpecialTactic(int id) => unchecked((uint)(id - 101)) <= 29 ||
        unchecked((uint)(id - 170)) <= 13 && ((0x380Fu >> (id - 170)) & 1) != 0;

    internal static bool IsSpecialPersonality(int id) => unchecked((uint)(id - 155)) < 56 &&
        ((0xE03C0003FFFFFFUL >> (id - 155)) & 1) != 0;

    internal static SpecialPersonSkillEligibilityResult ReadTactic(SourceWorldContext current, int physical,
        Func<ApplicationSpecialPersonAvailability?> readApplication)
    {
        if (!IsSpecialTactic(physical)) return new(true, null);
        return Read(current, readApplication, person =>
        {
            if (person.TacticIds is not { Count: 10 }) throw Missing("original_person_tactic_slots");
            var target = current.ReadSkillTacticReference(physical) ?? throw Missing("target_tactic_pointer");
            for (int index = 0; index < 10; index++)
            {
                //6D63B0→22A4B0 reads these actual original slots. It is not
                //the corresponding live Person's current tactics or a DLC ID.
                var selected = DeploymentTacticSelectionSource.ReadQualifiedSlot(current, person.PersonId,
                    person.TacticIds[index]);
                if (selected == target) return true;
            }
            return false;
        });
    }

    internal static SpecialPersonSkillEligibilityResult ReadPersonality(SourceWorldContext current, int physical,
        Func<ApplicationSpecialPersonAvailability?> readApplication)
    {
        if (!IsSpecialPersonality(physical)) return new(true, null);
        return Read(current, readApplication, person =>
        {
            if (person.PersonalityIds is not { Count: 9 }) throw Missing("original_person_personality_slots");
            var target = current.ReadPersonalityTableReference(physical) ?? throw Missing("target_personality_pointer");
            for (int index = 0; index < 9; index++)
            {
                ushort selected = TacticRemakeCurrentTraits.SelectPersonalityLookup(current,
                    checked((ushort)person.PersonId), index, () => person.PersonalityIds[index])
                    ?? throw Missing("original_person_personality_qualification");
                var candidate = current.ReadPersonalityTableReference(selected <= 355 ? selected : 0)
                    ?? throw Missing("original_person_personality_pointer");
                if (current.ReadSkillPersonalityValidity(candidate) is not bool valid)
                    throw Missing("original_person_personality_validity");
                //2307D0 only appends native-valid selected personalities.
                if (valid && candidate == target) return true;
            }
            return false;
        });
    }

    private static SpecialPersonSkillEligibilityResult Read(SourceWorldContext current,
        Func<ApplicationSpecialPersonAvailability?> readApplication,
        Func<ApplicationSpecialPersonDefinition, bool> contains)
    {
        try
        {
            var application = readApplication() ?? throw Missing("application_capability_owner");
            if (application.OriginalPersons is not { Count: 100 } people)
                throw Missing("original_person_catalog");
            //14D480 is the actual constant-zero leaf in this TC profile.
            //370170 calls36F410 with a null secondary-result pointer: this
            //caller consumes the FIRST availability only, not3701A0's pair.
            foreach (var person in people)
            {
                if (person is null || (uint)person.PersonId > ushort.MaxValue)
                    throw Missing("original_person_identity");
                if (!FirstAvailability(application, person.PersonId)) continue;
                if (contains(person)) return new(true, null);
            }
            return new(false, null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private static bool FirstAvailability(ApplicationSpecialPersonAvailability application, int person)
    {
        //36FCF0. Nonclassified original identities return true before the
        //capability bits; constructor/reset values cannot fill unknown bits.
        if (unchecked((uint)(person - 2001)) < 49) return true;
        if (unchecked((uint)(person - 5101)) >= 100 && unchecked((uint)(person - 2078)) >= 5) return true;
        if (application.FirstAvailability is null ||
            !application.FirstAvailability.TryGetValue(person, out bool? value) || value is null)
            throw Missing("first_availability_" + person);
        return value.Value;
    }

    private static InvalidDataException Missing(string field) => new("special-person-skills." + field);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal SpecialPersonSkillEligibilityResult ReadSpecialPersonTacticEligibilityCurrent(
        SourceWorldContext current, int physical) => SpecialPersonSkillEligibility.ReadTactic(
            current, physical, ReadApplicationSpecialPersons);

    internal SpecialPersonSkillEligibilityResult ReadSpecialPersonPersonalityEligibilityCurrent(
        SourceWorldContext current, int physical) => SpecialPersonSkillEligibility.ReadPersonality(
            current, physical, ReadApplicationSpecialPersons);
}
