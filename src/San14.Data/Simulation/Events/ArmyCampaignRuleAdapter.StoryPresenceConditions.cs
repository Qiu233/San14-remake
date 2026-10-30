using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal void BindStoryPresenceConditionsCurrent(GameState state, SourceWorldContext current) =>
        current.StoryPresenceConditionInputs = new(this, state, current);

    internal bool ReadStoryPersonDisappearingCurrent(GameState state, SourceWorldContext current, int person)
    {
        //232AE0 contains no location/status predicate:22AC00 finds the first
        //real associated command, then native validity and virtual98==16.
        var associated = current.ReadPersonLinkedEntity(person)
            ?? throw StoryPresenceConditions.Missing("associated_command22AC00");
        bool valid = current.ReadLinkedEntityNativeValidity(associated)
            ?? throw StoryPresenceConditions.Missing("associated_command_validity");
        if (!valid) return false;
        int kind = new PersonReturnGovernorQueries(state, _governorInputs, current)
            .ReadCurrentAssociatedVirtual98(associated)
            ?? throw StoryPresenceConditions.Missing("associated_command_virtual98");
        return kind == 16;
    }
}
