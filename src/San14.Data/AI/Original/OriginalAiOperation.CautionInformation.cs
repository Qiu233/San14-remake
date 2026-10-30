using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus ReadCautionArmyInformation(uint input, int mode, uint output)
    {
        if (!TryEntity(input, out var relation) || relation.Kind != WorldEntityKind.ArmyCaution ||
            !Adapter.IsOriginalAiCautionInformationReceiverCurrent(InputsCurrent, relation))
            return OriginalAiNodeStatus.Failure;
        return mode switch
        {
            1 => SetInteger(output, Adapter.ReadOriginalAiCautionArmyStrengthCurrent(Current, InputsCurrent, relation.Id)),
            2 => SetEntity(output, Adapter.ReadOriginalAiCautionArmyBaseCurrent(Current, InputsCurrent, relation.Id)),
            3 => SetEntity(output, Adapter.ReadOriginalAiCautionArmyTargetCurrent(Current, relation.Id)),
            _ => throw new InvalidDataException("original_ai.caution_relation.information_selector." + mode)
        };
    }

    internal OriginalAiNodeStatus ReadCautionGroupInformation(uint input, int mode, uint output)
    {
        if (!TryEntity(input, out var group) || group.Kind != WorldEntityKind.ArmyCautionGroup ||
            !Adapter.IsOriginalAiCautionInformationReceiverCurrent(InputsCurrent, group))
            return OriginalAiNodeStatus.Failure;
        if (mode == 2)
        {
            // Read the actual ordered membership before replacing the output
            // key, including when input and output name the same blackboard key.
            var members = Adapter.ReadOriginalAiCautionGroupArmyListCurrent(Current, group.Id);
            ReplaceTypedCandidates(output, WorldEntityKind.Army, members);
            return OriginalAiNodeStatus.Success;
        }
        return mode switch
        {
            1 => CacheCautionGroupStrength(group.Id, output),
            3 => CautionGroupStatus(group.Id, output),
            4 => CautionGroupVirtualTarget(group.Id, output),
            7 => SetInteger(output, Adapter.ReadOriginalAiCautionGroupTroopsCurrent(Current, InputsCurrent, group.Id)),
            8 => SetEntity(output, Adapter.ReadOriginalAiCautionGroupForceCurrent(Current, InputsCurrent, group.Id)),
            _ => throw new InvalidDataException("original_ai.caution_group.information_selector." + mode)
        };
    }
}
