using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus MyselfForceInformationCurrent(int mode, uint output)
    {
        var current = InputsCurrent;
        var force = Adapter.ReadOriginalAiMyselfForce(current, Actor);
        if (force is not { } at || !Adapter.ReadOriginalAiForceValidity(current, at.Id))
            return OriginalAiNodeStatus.Failure;
        return PublishForceInformation(at.Id, mode, output);
    }

    internal OriginalAiNodeStatus VariableForceInformationCurrent(uint input, int mode, uint output)
    {
        if (!TryEntity(input, out var force) || force.Kind != WorldEntityKind.Force ||
            !Adapter.ReadOriginalAiForceValidity(InputsCurrent, force.Id)) return OriginalAiNodeStatus.Failure;
        return PublishForceInformation(force.Id, mode, output);
    }

    private OriginalAiNodeStatus PublishForceInformation(int force, int mode, uint output)
    {
        if (mode == 20) return OriginalAiNodeStatus.Success; // Actual switch no-op; leave the destination intact.
        if (mode is 9 or 10 or 17 or 25 or 28 or 35)
        {
            var values = Adapter.ReadOriginalAiForceInformationList(Current, InputsCurrent, force, mode);
            if (mode is 10 or 28) ReplaceFootholdCandidates(output, values);
            else ReplaceTypedCandidates(output, mode == 9 ? WorldEntityKind.Force : mode == 17
                ? WorldEntityKind.Army : WorldEntityKind.Officer, values);
            return OriginalAiNodeStatus.Success;
        }
        return mode is 2 or 18 or 24 or 29 or 30
            ? SetAncestorEntity(output, Adapter.ReadOriginalAiForceInformationEntity(Current, InputsCurrent, force, mode))
            : SetInteger(output, Adapter.ReadOriginalAiForceInformationScalar(Current, InputsCurrent, force, mode));
    }

    internal bool ForceIsCurrentPlayer(uint key) => TryEntity(key, out var force) &&
        force.Kind == WorldEntityKind.Force && Adapter.ReadOriginalAiForceValidity(InputsCurrent, force.Id) &&
        Adapter.ReadOriginalAiForceIsCurrentPlayer(Current, InputsCurrent, force.Id);
}
