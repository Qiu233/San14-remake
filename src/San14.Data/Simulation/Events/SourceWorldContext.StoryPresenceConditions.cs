using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class SourceWorldContext : IS14StoryPresenceConditionQueries
{
    internal StoryPresenceConditionCurrent? StoryPresenceConditionInputs { get; set; }

    public bool? ReadConditionPersonDisappearing(S14EventConditionPersonReference person) =>
        StoryPresenceConditionInputs?.ReadDisappearing(person.PhysicalSlot);

    public CampaignEventExecutionInputs ReadStoryPresenceInvocationInputs() =>
        (StoryPresenceConditionInputs ?? throw StoryPresenceConditions.Missing("current_invocation_inputs"))
            .ReadInvocationInputs();

    public int? ReadConditionFootholdEndurance(BaseReference receiver)
    {
        if (ReadBaseObjectLink(receiver) is not ushort raw) return null;
        var obj = ReadObjectTableReference(raw < 3001 ? raw : 0);
        if (obj is null || ReadObjectNativeValidity(obj) is not bool valid) return null;
        //22C800's observed invalid Object fallback is the literal6000.
        //An unknown Object pointer or validity cannot take that branch.
        return valid ? ReadObjectRawField14(obj) : 6000;
    }
}

internal sealed class StoryPresenceConditionCurrent(ArmyCampaignRuleAdapter owner,
    GameState state, SourceWorldContext current)
{
    internal bool ReadDisappearing(int person) => owner.ReadStoryPersonDisappearingCurrent(state, current, person);
    internal CampaignEventExecutionInputs ReadInvocationInputs() => owner.BindCampaignStoryInputs(state, current, false);
}
