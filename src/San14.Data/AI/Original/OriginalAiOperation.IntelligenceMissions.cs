using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus IntelligenceSpyAway(uint forceKey, uint personKey, uint durationKey)
    {
        if (!TryEntity(forceKey, out var force) || force.Kind != WorldEntityKind.Force ||
            !TryEntity(personKey, out var person) || person.Kind != WorldEntityKind.Officer ||
            !TryInteger(durationKey, out int duration)) return OriginalAiNodeStatus.Failure;
        return ApplyIntelligenceMissionPreparation(Adapter.PrepareOriginalAiSpyAwayCurrent(
            Current, InputsCurrent, force.Id, person.Id, duration));
    }

    internal OriginalAiNodeStatus IntelligenceInspect()
    {
        var district = Adapter.ReadOriginalAiMyselfDistrict(InputsCurrent, Actor);
        if (district is not { } at) return OriginalAiNodeStatus.Failure;
        var prepared = Adapter.PrepareOriginalAiInspectCurrent(Current, InputsCurrent, at.Id);
        foreach (var entry in prepared.Evaluations) candidateEvaluations[entry.Entity] = entry.Score;
        return ApplyIntelligenceMissionPreparation(prepared.Command);
    }

    private OriginalAiNodeStatus ApplyIntelligenceMissionPreparation(OriginalAiCommandPreparation prepared) =>
        !prepared.Success ? OriginalAiNodeStatus.Failure : prepared.Order is { } order
            ? ApplyNativeCommand(order) : OriginalAiNodeStatus.Success;
}
