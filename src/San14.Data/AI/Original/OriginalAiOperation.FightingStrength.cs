using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus FootholdFightingStrength(uint input, uint output, int radius,
        uint reservedKey, uint goalKey, uint availableKey)
    {
        if (!TryEntity(input, out var value) || value.Kind is not (WorldEntityKind.City or WorldEntityKind.Gate))
            return OriginalAiNodeStatus.Failure;
        var at = BaseLookup.Find(value.Id, InputsCurrent);
        if (InputsCurrent.ReadBaseNativeValidity(at) is not bool valid)
            throw new InvalidDataException("original_ai.fighting_strength.Base_validity");
        if (!valid) return OriginalAiNodeStatus.Failure;
        TryInteger(reservedKey, out int reserved); TryInteger(availableKey, out int available);
        //The goal selector is another optional scalar with an initialized zero.
        TryInteger(goalKey, out int goal);
        return SetInteger(output, Adapter.ReadOriginalAiFightingStrengthCurrent(Current, InputsCurrent,
            at, radius, reserved, goal != 0, available != 0));
    }

    internal OriginalAiNodeStatus MyselfForceEvaluation(int mode, uint output)
    {
        var force = Adapter.ReadOriginalAiMyselfForce(InputsCurrent, Actor);
        if (force is not { } value || !Adapter.ReadOriginalAiForceValidity(InputsCurrent, value.Id))
            return OriginalAiNodeStatus.Failure;
        return Adapter.ReadOriginalAiForceEvaluationCurrent(Current, InputsCurrent, Actor, value.Id, mode, variableNode: false) is int result
            ? SetInteger(output, result) : OriginalAiNodeStatus.Failure;
    }

    internal OriginalAiNodeStatus VariableForceEvaluation(uint input, int mode, uint output)
    {
        if (!TryEntity(input, out var value) || value.Kind != WorldEntityKind.Force ||
            !Adapter.ReadOriginalAiForceValidity(InputsCurrent, value.Id)) return OriginalAiNodeStatus.Failure;
        return Adapter.ReadOriginalAiForceEvaluationCurrent(Current, InputsCurrent, Actor, value.Id, mode, variableNode: true) is int result
            ? SetInteger(output, result) : OriginalAiNodeStatus.Failure;
    }
}
