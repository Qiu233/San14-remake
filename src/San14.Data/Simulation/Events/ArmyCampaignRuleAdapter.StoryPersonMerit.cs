using San14.Core;

namespace San14.Data;

internal sealed record StoryPersonMeritMutationResult(GameState State, int NativeReturn,
    IReadOnlyList<int> OrderedMeritPersonSlots);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Ordinary428 is a selected Person;430 is the actual ordered
    /// invocation list. Only428 owns the source merit-summary notification.</summary>
    internal StoryPersonMeritMutationResult ApplyStoryPersonMeritCurrent(GameState state,
        SourceWorldContext current, int code, IReadOnlyList<int> arguments,
        int? targetPerson, IReadOnlyList<int> people)
    {
        if (arguments.Count != 8) throw StoryMeritMissing("arguments");
        int amount = code switch { 428 => arguments[1], 430 => arguments[0],
            _ => throw StoryMeritMissing("opcode") };
        //Both outer handlers stop before even resolving/traversing recipients.
        if (amount <= 0) return new(state, 0, []);
        if (code == 428)
        {
            int person = targetPerson ?? throw StoryMeritMissing("selected_Person");
            if (!Eligible(person)) return new(state, 0, []);
            state = ApplyDirectPersonMeritCurrent(state, current, person, amount);
            //The append is a reached occurrence even when the value is at its
            //upper bound.3D5060 preserves duplicates and does not rank up.
            bool manager = StoryMeritNeed(current.ReadEventManagerPresent(), "event_manager_presence");
            return new(state, 1, manager ? new[] { person } : []);
        }
        //The empty/all-skipped list still returns1. Source430 has no3D5060
        //call, so this branch must never manufacture a merit-summary row.
        foreach (int person in people)
            if (Eligible(person)) state = ApplyDirectPersonMeritCurrent(state, current, person, amount);
        return new(state, 1, []);

        bool Eligible(int physical)
        {
            if (!StoryMeritNeed(current.ReadPersonNativeValidity(physical), "Person_validity")) return false;
            if (StoryMeritNeed(current.ReadPersonStatus(physical), "Person_status") is 0 or 1 or 5 or 7 or 8 or 9)
                return false;
            if (DuelAftermathSpecialPerson(current, physical)) return false;
            return (StoryMeritNeed(current.ReadOwnedReturnPersonFlags(physical), "Person_behavior_flags") & 0x4000) == 0;
        }
    }

    private static T StoryMeritNeed<T>(T? value, string owner) where T : struct
        => value ?? throw StoryMeritMissing(owner);
    private static InvalidDataException StoryMeritMissing(string owner) => new("story.person_merit.current." + owner);
}
