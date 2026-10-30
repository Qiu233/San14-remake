using San14.Core;

namespace San14.Data;

/// <summary>2C3FF0 shared by annual appearance updates and cutin selection.
/// The saved current world, Person and event owners determine the predicate.</summary>
internal static class PersonPortraitTransitionPredicate
{
    internal static bool Read(GameState state, SourceWorldContext current, int physical)
    {
        if (!(current.ReadPersonNativeValidity(physical) ?? throw Missing("current_person_validity"))) return false;
        int mode = state.CampaignEventWorld?.ScenarioMode ?? throw Missing("current_world_mode");
        if (mode == 2) return false;
        if (mode == 3 && (state.CampaignEventWorld?.ScenarioId ?? throw Missing("current_scenario_id")) == 151) return false;
        short year = unchecked((short)(state.NativeMapSeason?.ThirdDate.Year ?? throw Missing("current_third_year")));
        ushort id = current.ReadPersonIdentity(physical) ?? throw Missing("person_identity");
        foreach (var rule in PersonPortraitTransitionCatalog.Rules)
        {
            if (rule.PersonId != id) continue;
            if (id != 988) return rule.Direction == 0 ? year >= rule.ThresholdYear : year < rule.ThresholdYear;
            if (rule.Direction == 0) return false;
            var occurred = state.CampaignEvents?.Occurred ?? throw Missing("current_event_occurrence");
            if (occurred.Length <= 45) throw Missing("current_event_occurrence");
            if (occurred[45] || year >= rule.ThresholdYear) return false;
            if (current.ResolveOfficerSource(id) != physical || !state.Officers.TryGetValue(id, out var person))
                throw Missing("current_person_owner");
            int intelligence = person.BaseAbilities?.Intelligence ?? throw Missing("current_base_intelligence");
            if (intelligence is < 0 or > byte.MaxValue) throw Missing("current_base_intelligence");
            return intelligence < 70;
        }
        return false;
    }
    private static InvalidDataException Missing(string field) => new("annual-person-portrait-transition." + field);
}
