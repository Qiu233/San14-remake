using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus TransportDeparture(uint sourcesKey, uint targetsKey,
        uint foodKey, uint goldKey, uint troopsKey)
    {
        var district = Adapter.ReadOriginalAiMyselfDistrict(InputsCurrent, Actor);
        if (district is not { } at || !Adapter.ReadOriginalAiTransportDistrictGateCurrent(Current, InputsCurrent, at.Id, false))
            return OriginalAiNodeStatus.Failure;
        if (!TryFootholdCandidateList(sourcesKey, out var sources) || !TryFootholdCandidateList(targetsKey, out var targets))
            return OriginalAiNodeStatus.Failure;
        //032180 ignores the scalar read result; its three cells start at zero.
        TryInteger(foodKey, out int food); TryInteger(goldKey, out int gold); TryInteger(troopsKey, out int reserve);
        var prepared = Adapter.PrepareOriginalAiTransportCurrent(Current, InputsCurrent, at.Id,
            sources.Values.ToArray(), targets.Values.ToArray(), food, gold, reserve);
        return ApplyTransportProgram(prepared);
    }

    internal OriginalAiNodeStatus FrontFootholdTransportDeparture()
    {
        var district = Adapter.ReadOriginalAiMyselfDistrict(InputsCurrent, Actor);
        if (district is not { } at || !Adapter.ReadOriginalAiTransportDistrictGateCurrent(Current, InputsCurrent, at.Id, true))
            return OriginalAiNodeStatus.Failure;
        return ApplyTransportProgram(Adapter.PrepareOriginalAiFrontTransportCurrent(Current, InputsCurrent, at.Id));
    }

    private OriginalAiNodeStatus ApplyTransportProgram(OriginalAiTransportPreparation prepared)
    {
        if (!prepared.Success) return OriginalAiNodeStatus.Failure;
        foreach (var item in prepared.Commands)
        {
            if (Actor.DistrictId is int delegated && item.Command.DistrictId != delegated)
                throw new InvalidDataException("original_ai.transport.executor_outside_delegated_District_contract");
            var status = ApplyNativeCommand(item.Command);
            if (status != OriginalAiNodeStatus.Success) return status;
        }
        return OriginalAiNodeStatus.Success;
    }
}
