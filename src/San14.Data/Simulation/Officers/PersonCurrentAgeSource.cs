using San14.Core;

namespace San14.Data;

/// <summary>The current person-age query shared by ordinary story text and
/// personnel rules. Mode three requires its independent current age policy;
/// opening settings and scenario bytes cannot replace a missing saved owner.</summary>
internal static class PersonCurrentAgeSource
{
    internal static int Read(GameState state, SourceWorldContext current, int personPhysical, int? currentYear = null, int? scenarioMode = null, int? historicalEventControl = null)
    {
        if (scenarioMode is int mode ? UsesTimelessAges(mode, historicalEventControl) : UsesTimelessAges(state))
        {
            ushort identity = current.ReadPersonIdentity(personPhysical) ?? throw Missing("person_identity");
            //The finite normal source table contains these five identities.
            int age = identity is 231 or 247 or 271 or 631 or 5121 ? 80 : 20;
            for (int index = 0; index < 9; index++)
                if ((TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, personPhysical, index)
                    ?? throw Missing("personality_slot")) == 0x90) return 80;
            return age;
        }
        ushort id = current.ReadPersonIdentity(personPhysical) ?? throw Missing("person_identity");
        if (current.ResolveOfficerSource(id) != personPhysical || !state.Officers.TryGetValue(id, out var person))
            throw Missing("person_owner");
        int birth = person.BirthYear ?? throw Missing("birth_year");
        int year = currentYear ?? state.NativeMapSeason?.ThirdDate.Year ?? throw Missing("current_year");
        return Math.Max(0, unchecked((short)year - birth + 1));
    }

    //334460 tests the current signed World50 scalar, not the environment
    //settings radio value. Keep this read lazy: ordinary modes do not need it.
    internal static bool UsesTimelessAges(GameState state)
    {
        var world = state.CampaignEventWorld ?? throw Missing("scenario_mode");
        return UsesTimelessAges(world.ScenarioMode, world.HistoricalEventControl);
    }

    internal static bool UsesTimelessAges(int mode, int? historicalEventControl) =>
        mode == 2 || mode == 3 &&
            (historicalEventControl ?? throw Missing("mode3_current_age_policy")) == 0;

    private static InvalidDataException Missing(string input) => new("person.current-age." + input);
}
