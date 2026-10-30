using System.Collections.Immutable;
using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    private OriginalAiCautionGroupPlanningRuntime? cautionGroupPlanning;

    public void UseCautionGroupPlanningRuntime(OriginalAiCautionGroupPlanningRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (cautionGroupPlanning is not null && !ReferenceEquals(cautionGroupPlanning, runtime))
            throw new InvalidOperationException("An AI operation cannot replace its caution-group planning owner.");
        cautionGroupPlanning = runtime;
    }
    private OriginalAiCautionGroupPlanningRuntime CautionGroupPlanning => cautionGroupPlanning
        ?? throw new InvalidDataException("original_ai.caution_group_planning.transaction_owner");
    private ImmutableDictionary<int, ArmyCautionGroupPlanningState>? ActualCautionGroupPlanning
        => (Current.ReadArmyLifecycle()?.ArmyCautions ?? throw new InvalidDataException("original_ai.caution_group_planning.current_owner")).GroupPlanning;

    internal OriginalAiNodeStatus CacheCautionGroupStrength(int group, uint output)
    {
        float strength = Adapter.ReadOriginalAiCautionGroupStrengthCurrent(Current, InputsCurrent, group);
        CautionGroupPlanning.SetStrength(group, ActualCautionGroupPlanning, strength);
        return SetInteger(output, BattleUnitSourceMath.TruncateSingleToInt32(strength));
    }
    internal OriginalAiNodeStatus CautionGroupStatus(int group, uint output)
    {
        bool status = CautionGroupPlanning.Read(group, ActualCautionGroupPlanning).Status
            ?? throw new InvalidDataException("original_ai.caution_group_planning.actual_status");
        return SetInteger(output, status ? 1 : 0);
    }
    internal OriginalAiNodeStatus CautionGroupVirtualTarget(int group, uint output)
    {
        int location = CautionGroupPlanning.Read(group, ActualCautionGroupPlanning).VirtualTargetLocationId
            ?? throw new InvalidDataException("original_ai.caution_group_planning.actual_virtual_target");
        return SetEntity(output, Adapter.ReadOriginalAiCautionVirtualTargetBaseCurrent(InputsCurrent, location));
    }

    internal OriginalAiNodeStatus UpdateCautionGroupStatus(uint input, bool status)
    {
        if (!TryEntity(input, out var group) || group.Kind != WorldEntityKind.ArmyCautionGroup ||
            !Adapter.IsOriginalAiCautionInformationReceiverCurrent(InputsCurrent, group)) return OriginalAiNodeStatus.Failure;
        CautionGroupPlanning.SetStatus(group.Id, ActualCautionGroupPlanning, status);
        return OriginalAiNodeStatus.Success;
    }

    internal OriginalAiNodeStatus SetCautionGroupVirtualTarget(uint input, uint targetKey)
    {
        bool groupNull = candidateNullEntities.Contains(input), targetNull = candidateNullEntities.Contains(targetKey);
        bool hasGroup = TryEntity(input, out var group), hasTarget = TryEntity(targetKey, out var target);
        if ((!hasGroup && !groupNull) || (!hasTarget && !targetNull)) return OriginalAiNodeStatus.Failure;
        // The actual setter succeeds without a write for either known null
        // object. It does not impose the information getter's validity gate.
        if (groupNull || targetNull) return OriginalAiNodeStatus.Success;
        if (group.Kind != WorldEntityKind.ArmyCautionGroup || target.Kind is not (WorldEntityKind.City or WorldEntityKind.Gate))
            return OriginalAiNodeStatus.Failure;
        int location = Adapter.ReadOriginalAiCautionVirtualTargetLocationCurrent(InputsCurrent, target);
        CautionGroupPlanning.SetVirtualTarget(group.Id, ActualCautionGroupPlanning, location);
        return OriginalAiNodeStatus.Success;
    }

    internal OriginalAiNodeStatus SetCautionGroupEvaluation(uint input, uint value)
        => SetTypedCandidateEvaluation(input, value, CandidateListKind.CautionGroup);
}
