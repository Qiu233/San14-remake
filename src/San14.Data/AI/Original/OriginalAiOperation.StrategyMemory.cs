using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus EstimateAllFoodNeeds(uint districtKey, int rate, int horizon, int incomeHorizon)
    {
        if (!TryEstimateForce(districtKey, out int force)) return OriginalAiNodeStatus.Failure;
        return ApplyStrategyMemoryOrder(new EstimateAiFoodNeedsOrder(force, rate, horizon, incomeHorizon));
    }

    internal OriginalAiNodeStatus EstimateAllGoldNeeds(uint districtKey, int incomeHorizon)
    {
        if (!TryEstimateForce(districtKey, out int force)) return OriginalAiNodeStatus.Failure;
        return ApplyStrategyMemoryOrder(new EstimateAiGoldNeedsOrder(force, incomeHorizon));
    }

    private bool TryEstimateForce(uint districtKey, out int force)
    {
        force = 0;
        if (!TryEntity(districtKey, out var district) || district.Kind != WorldEntityKind.District)
            return false;
        var current = InputsCurrent;
        if (!Adapter.ReadOriginalAiDistrictValidity(current, district.Id)) return false;
        int rawForce = current.ReadDistrictForceSlot(new(district.Id))
            ?? throw StrategyMemoryMissing("estimate_District10");
        force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0)?.PhysicalSlot
            ?? throw StrategyMemoryMissing("estimate_Force_pointer");
        return true;
    }

    private OriginalAiNodeStatus ApplyStrategyMemoryOrder(GameOrder order)
    {
        if (order.ForceId != Actor.ForceId) return OriginalAiNodeStatus.Failure;
        var result = planning.Apply(order, rules.OrderRules, commandContext, authority);
        LastCommand = result;
        if (result.MissingRuleKey is { } missing)
            throw new InvalidDataException($"original_ai.command.{missing}: {result.Error}");
        return result.Accepted ? OriginalAiNodeStatus.Success : OriginalAiNodeStatus.Failure;
    }

    internal OriginalAiNodeStatus FootholdSavedNeed(uint input, int field, uint output)
    {
        if (!TryEntity(input, out var foothold) || foothold.Kind is not (WorldEntityKind.City or WorldEntityKind.Gate))
            return OriginalAiNodeStatus.Failure;
        // Information queries read only the saved result after the original
        // native-validity gate. They never run an estimator or repair an owner.
        var reference = BaseLookup.Find(foothold.Id, InputsCurrent);
        if (!(InputsCurrent.ReadBaseNativeValidity(reference) ?? throw StrategyMemoryMissing("Foothold_validity")))
            return OriginalAiNodeStatus.Failure;
        var memory = Current.OriginalAiStrategyMemory?.Footholds.GetValueOrDefault(foothold.Id)
            ?? throw StrategyMemoryMissing("Foothold_owner." + foothold.Id);
        int value = (field switch { 38 => memory.FoodNeed, 39 => memory.GoldNeed, _ => null })
            ?? throw StrategyMemoryMissing($"Foothold{(field == 38 ? "94_FoodNeed" : "98_GoldNeed")}.{foothold.Id}");
        return SetInteger(output, value);
    }

    internal OriginalAiNodeStatus MyselfForceSavedFootholds(int field, uint output)
    {
        var current = InputsCurrent;
        var force = Adapter.ReadOriginalAiMyselfForce(current, Actor);
        if (force is not { } actorForce || !Adapter.ReadOriginalAiForceValidity(current, actorForce.Id))
            return OriginalAiNodeStatus.Failure;
        return CopySavedForceFootholds(actorForce.Id, field, output);
    }

    internal OriginalAiNodeStatus ForceSavedFootholds(uint input, int field, uint output)
    {
        if (!TryEntity(input, out var force) || force.Kind != WorldEntityKind.Force ||
            !Adapter.ReadOriginalAiForceValidity(InputsCurrent, force.Id)) return OriginalAiNodeStatus.Failure;
        return CopySavedForceFootholds(force.Id, field, output);
    }

    private OriginalAiNodeStatus CopySavedForceFootholds(int forceId, int field, uint output)
    {
        var memory = Current.OriginalAiStrategyMemory?.Forces.GetValueOrDefault(forceId)
            ?? throw StrategyMemoryMissing("Force_classification_owner." + forceId);
        var locations = field switch
        {
            19 => memory.FrontierLocationIds,
            21 => memory.RearLocationIds,
            _ => throw StrategyMemoryMissing("ForceInfo_" + field)
        };
        if (locations.IsDefault) throw StrategyMemoryMissing($"ForceInfo{field}_ordered_list.{forceId}");
        if (!TryFootholdCandidateList(output, out var list))
        {
            integers.Remove(output); entities.Remove(output); candidateNullEntities.Remove(output);
            candidateLists[output] = list = new(CandidateListKind.Foothold);
        }
        else list.Values.Clear();
        // Preserve source list order and duplicates. City/Gate identities stay
        // concrete, while the list itself has the common Foothold discriminator.
        foreach (int id in locations)
        {
            WorldEntityKind kind = id switch
            {
                >= 1 and <= 51 => WorldEntityKind.City,
                >= 52 and <= 61 => WorldEntityKind.Gate,
                _ => throw StrategyMemoryMissing("Force_list_location_identity." + id)
            };
            list.Values.Add(new(kind, id));
        }
        return OriginalAiNodeStatus.Success;
    }

    private bool TryFootholdCandidateList(uint key, out CandidateList list)
    {
        if (!integers.ContainsKey(key) && !entities.ContainsKey(key) &&
            candidateLists.TryGetValue(key, out list!) && list.Kind == CandidateListKind.Foothold) return true;
        list = null!; return false;
    }

    internal OriginalAiNodeStatus LoopFootholdCandidates(uint key, uint output,
        IReadOnlyList<Func<OriginalAiOperation, OriginalAiNodeStatus>> children) =>
        TryFootholdCandidateList(key, out var list)
            ? LoopCandidateValues(list.Values, output, children) : OriginalAiNodeStatus.Success;

    private static InvalidDataException StrategyMemoryMissing(string field) => new("original-ai.strategy-memory." + field);
}
