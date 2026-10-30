using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The ordinary direct-merit writer shared by event handlers and
    /// editor results. Eligibility and invocation-summary occurrences belong
    /// to their callers; this changes only the actual merit value.</summary>
    private GameState ApplyDirectPersonMeritCurrent(GameState state, SourceWorldContext current,
        int physical, int addition)
    {
        int id = DirectMeritNeed(current.ReadPersonIdentity(physical), "Person_identity");
        var person = state.Officers.GetValueOrDefault(id) ?? throw DirectMeritMissing("Person_owner");
        var merit = person.MeritState ?? throw DirectMeritMissing("merit_cycle");
        int before = DirectMeritNeed(merit.Merit, "merit_value");
        var ranks = CreditRanks();
        int selector = DirectMeritNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "environment_selector");
        int option = DirectMeritNeed(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x4C4 : 0x128),
            "lower_bound_option");
        int lower = ranks[option == 1 ? 2 : 1].SignedThreshold;
        int requested = unchecked((short)before + addition);
        int after = unchecked((short)(requested < lower ? lower : Math.Min(requested, ranks[6].SignedThreshold)));
        return state with { Officers = new(state.Officers) { [id] = person with
            { MeritState = new(after, merit.PendingMerit, merit.RankStageId,
                merit.FirstSelectionCount, merit.SecondSelectionCount, merit.ResetCode) } } };
    }

    private static T DirectMeritNeed<T>(T? value, string owner) where T : struct
        => value ?? throw DirectMeritMissing(owner);
    private static InvalidDataException DirectMeritMissing(string owner) => new("person.direct_merit.current." + owner);
}
