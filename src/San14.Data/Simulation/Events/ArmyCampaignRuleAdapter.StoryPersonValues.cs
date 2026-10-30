using San14.Core;

namespace San14.Data;

internal sealed record StoryPersonValuesMutationResult(GameState State, int NativeReturn);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The complete ordinary158/159/160 value family. Each selector
    /// is resolved by the real invocation before entry, including its dynamic
    /// selectors. No editor eligibility or summary notification is implied.</summary>
    internal StoryPersonValuesMutationResult ApplyStoryPersonValuesCurrent(GameState state,
        SourceWorldContext current, int code, IReadOnlyList<int> arguments, int targetPerson)
    {
        if (arguments.Count != 8) throw StoryPersonValueMissing("arguments");
        if (code == 160)
            return new(WriteDirectPersonLoyaltyCurrent(state, current, targetPerson, arguments[1]), 1);
        if (code is not (158 or 159)) throw StoryPersonValueMissing("opcode");
        if (!(current.ReadPersonNativeValidity(targetPerson) ?? throw StoryPersonValueMissing("Person_validity")))
            return new(state, 1);
        int kind = arguments[1];
        if (code == 158)
        {
            if (kind is >= 1 and <= 6)
                state = ApplyDirectPersonParameterCurrent(state, current, targetPerson, kind, arguments[2]);
        }
        else
        {
            int before = ReadDirectPersonParameterCurrent(state, current, targetPerson, kind);
            int requested = arguments[2] switch
            {
                1 => arguments[3], 2 => unchecked(before + arguments[3]),
                3 => unchecked(before - arguments[3]), _ => before
            };
            state = ApplyDirectPersonParameterCurrent(state, current, targetPerson, kind, requested);
        }
        return new(state, 1);
    }

    private static InvalidDataException StoryPersonValueMissing(string owner) => new("story.person_value.current." + owner);
}
