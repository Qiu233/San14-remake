using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal float ReadOriginalAiCautionGroupStrengthCurrent(GameState state, SourceWorldContext current, int group)
    {
        float total = 0f;
        foreach (int? member in CautionInformationMembers(state, group))
        {
            int id = member ?? throw CautionGroupPlanningMissing("member_pointer");
            int physical = WorldIndexContext.FindArmyPhysical(state, id) ?? throw CautionGroupPlanningMissing("member_Army_receiver");
            total += ReadOriginalAiArmyCommonCurrent(state, current, new(physical));
        }
        return total;
    }

    internal int ReadOriginalAiCautionVirtualTargetLocationCurrent(SourceWorldContext current, OriginalAiEntity target)
    {
        if (target.Kind is not (WorldEntityKind.City or WorldEntityKind.Gate)) throw CautionGroupPlanningMissing("virtual_target_Base_type");
        var at = BaseLookup.Find(target.Id, current);
        int location = current.ReadBaseObjectLink(at) ?? throw CautionGroupPlanningMissing("virtual_target_Base4E");
        return Math.Min(location, 61);
    }

    internal OriginalAiEntity ReadOriginalAiCautionVirtualTargetBaseCurrent(SourceWorldContext current, int location)
    {
        var at = BaseLookup.Find(location, current);
        int id = current.ReadBaseObjectLink(at) ?? throw CautionGroupPlanningMissing("virtual_target_Base_identity");
        return new(at.Kind == BaseKind.City ? WorldEntityKind.City : WorldEntityKind.Gate, id);
    }

    private static InvalidDataException CautionGroupPlanningMissing(string field) => new("original_ai.caution_group_planning." + field);
}
