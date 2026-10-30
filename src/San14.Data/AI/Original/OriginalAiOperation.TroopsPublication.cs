using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus MyselfForceTroops(uint destination)
    {
        var force = Adapter.ReadOriginalAiMyselfForce(InputsCurrent, Actor);
        return force is { } own && Adapter.ReadOriginalAiForceValidity(InputsCurrent, own.Id)
            ? PublishForceTroops(own.Id, destination) : OriginalAiNodeStatus.Failure;
    }

    internal OriginalAiNodeStatus VariableForceTroops(uint input, uint destination)
    {
        if (!TryEntity(input, out var force) || force.Kind != WorldEntityKind.Force ||
            !Adapter.ReadOriginalAiForceValidity(InputsCurrent, force.Id)) return OriginalAiNodeStatus.Failure;
        return PublishForceTroops(force.Id, destination);
    }

    private OriginalAiNodeStatus PublishForceTroops(int force, uint destination)
    {
        ReplaceTroopsCandidates(destination, Adapter.ReadOriginalAiForceTroopsCurrent(Current, InputsCurrent, force));
        return OriginalAiNodeStatus.Success;
    }
}
