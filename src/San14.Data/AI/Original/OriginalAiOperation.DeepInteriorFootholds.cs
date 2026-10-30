using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus MyselfDeepInteriorFootholds(uint output)
    {
        var force = Adapter.ReadOriginalAiMyselfForce(InputsCurrent, Actor);
        if (force is not { } value || !Adapter.ReadOriginalAiForceValidity(InputsCurrent, value.Id))
            return OriginalAiNodeStatus.Failure;
        return PublishDeepInteriorFootholds(value.Id, output);
    }

    internal OriginalAiNodeStatus VariableDeepInteriorFootholds(uint input, uint output)
    {
        if (!TryEntity(input, out var force) || force.Kind != WorldEntityKind.Force ||
            !Adapter.ReadOriginalAiForceValidity(InputsCurrent, force.Id)) return OriginalAiNodeStatus.Failure;
        return PublishDeepInteriorFootholds(force.Id, output);
    }

    private OriginalAiNodeStatus PublishDeepInteriorFootholds(int force, uint output)
    {
        ReplaceFootholdCandidates(output, Adapter.ReadOriginalAiDeepInteriorFootholdsCurrent(Current, force));
        return OriginalAiNodeStatus.Success;
    }
}
