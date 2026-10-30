namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus DistributeSpyPoints()
    {
        var prepared = Adapter.PrepareOriginalAiSpyAllocation(Current, InputsCurrent, Actor);
        if (!prepared.Success) return OriginalAiNodeStatus.Failure;
        if (prepared.Order is not { } order) return OriginalAiNodeStatus.Success;
        //The installed occurrence belongs to the Force root. Retain the
        //actual actor's context: a delegated district cannot impersonate its
        //Force to publish a Force-wide transfer through a different authority.
        if (commandContext.DelegatedDistrictId is not null)
            throw new InvalidDataException("original-ai.spy-allocation.direct_force_scope");
        return ApplyNativeCommand(order);
    }
}
