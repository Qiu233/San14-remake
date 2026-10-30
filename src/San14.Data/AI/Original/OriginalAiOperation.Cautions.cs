using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus ClearArmyCautionCandidates(uint key)
    {
        var list = GetOrCreateTypedCandidates(key, CandidateListKind.Caution);
        var inactive = new List<int>();
        foreach (var relation in list.Values)
        {
            if (relation.Kind != WorldEntityKind.ArmyCaution)
                throw new InvalidDataException("original_ai.cautions.list_payload");
            if (!Adapter.IsOriginalAiCautionArmyActiveCurrent(Current, InputsCurrent, relation.Id)) inactive.Add(relation.Id);
        }
        if (inactive.Count != 0)
        {
            int force = Adapter.ReadOriginalAiCautionActorForce(InputsCurrent, Actor);
            var status = ApplyNativeCommand(new ClearInvalidArmyCautionsOrder(force, inactive));
            if (status != OriginalAiNodeStatus.Success) return status;
        }
        //081F30 clears the container after its inactive-target cleanup. Do not
        //call the generic clear wrapper here: it delegates back to this method.
        list.Values.Clear(); list.HexValues.Clear();
        return OriginalAiNodeStatus.Success;
    }

    internal OriginalAiNodeStatus AddUniqueArmyCaution(uint armyKey, uint baseKey, uint listKey)
    {
        if (!TryEntity(armyKey, out var army) || army.Kind != WorldEntityKind.Army ||
            !TryEntity(baseKey, out var foothold) || foothold.Kind is not (WorldEntityKind.City or WorldEntityKind.Gate))
            return OriginalAiNodeStatus.Failure;
        if (!Adapter.IsOriginalAiCautionTargetCurrent(Current, InputsCurrent, army.Id, foothold.Id))
            return OriginalAiNodeStatus.Failure;
        var list = GetOrCreateTypedCandidates(listKey, CandidateListKind.Caution);
        foreach (var relation in list.Values)
        {
            if (relation.Kind != WorldEntityKind.ArmyCaution)
                throw new InvalidDataException("original_ai.cautions.list_payload");
            if (Adapter.OriginalAiCautionTargetsArmy(Current, relation.Id, army.Id))
                return OriginalAiNodeStatus.Success;
        }
        int force = Adapter.ReadOriginalAiCautionActorForce(InputsCurrent, Actor);
        var status = ApplyNativeCommand(new CreateArmyCautionOrder(force, army.Id, foothold.Id));
        if (status != OriginalAiNodeStatus.Success) return status;
        var created = LastCommand?.ArmyCautionCreation
            ?? throw new InvalidDataException("original_ai.cautions.creation_receipt");
        if (!created.TargetLinked) return OriginalAiNodeStatus.Failure;
        list.Values.Add(new(WorldEntityKind.ArmyCaution, created.RelationId));
        return OriginalAiNodeStatus.Success;
    }

    internal OriginalAiNodeStatus RemoveArmyCautionCandidate(uint armyKey, uint listKey)
    {
        if (!TryEntity(armyKey, out var army) || army.Kind != WorldEntityKind.Army ||
            !TryTypedCandidates(listKey, CandidateListKind.Caution, out var list))
            return OriginalAiNodeStatus.Failure;
        //027FF0 rebuilds this caller's pointer list. It neither clears relation
        //records nor calls the unrelated global cancellation writer32B4D0.
        var retained = new List<OriginalAiEntity>(list.Values.Count);
        foreach (var relation in list.Values)
        {
            if (relation.Kind != WorldEntityKind.ArmyCaution)
                throw new InvalidDataException("original_ai.cautions.list_payload");
            if (!Adapter.OriginalAiCautionTargetsArmy(Current, relation.Id, army.Id)) retained.Add(relation);
        }
        list.Values.Clear(); list.Values.AddRange(retained);
        return OriginalAiNodeStatus.Success;
    }
}
