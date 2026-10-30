using San14.Core;

namespace San14.Data.AI.Original;

/// <summary>An independent offer receiver. Its identity never resolves through
/// the world's Officer lookup even when the numeric identities overlap.</summary>
public readonly record struct OriginalAiHireOffer(int TemplateId);

public sealed partial class OriginalAiOperation
{
    private readonly Dictionary<uint, OriginalAiHireOffer> hireOffers = [];
    private Dictionary<OriginalAiHireOffer, int> hireOfferEvaluations = [];

    internal bool TryHireOffer(uint key, out OriginalAiHireOffer value)
    {
        if (integers.ContainsKey(key) || entities.ContainsKey(key) || candidateLists.ContainsKey(key) ||
            candidateNullEntities.Contains(key) || hexes.ContainsKey(key) || troops.ContainsKey(key) || tradeCountries.ContainsKey(key))
        { value = default; return false; }
        return hireOffers.TryGetValue(key, out value);
    }
    internal OriginalAiNodeStatus SetHireOffer(uint key, OriginalAiHireOffer value)
    {
        integers.Remove(key); entities.Remove(key); candidateLists.Remove(key);
        ClearCandidateNull(key); hireOffers[key] = value;
        return OriginalAiNodeStatus.Success;
    }
    internal void ClearHireOfferVariable(uint key) => hireOffers.Remove(key);
    internal bool TryCopyHireOfferVariable(uint source, uint destination)
    {
        if (!TryHireOffer(source, out var value)) return false;
        SetHireOffer(destination, value); return true;
    }
    internal bool TryCompareHireOfferVariables(uint left, uint right, out bool equal)
    {
        bool hasLeft = TryHireOffer(left, out var a), hasRight = TryHireOffer(right, out var b);
        equal = hasLeft && hasRight && a == b;
        return hasLeft || hasRight;
    }
    internal void SetHireOfferEvaluation(OriginalAiHireOffer value, int score) => hireOfferEvaluations[value] = score;
    internal int HireOfferEvaluation(OriginalAiHireOffer value) => hireOfferEvaluations.TryGetValue(value, out int score)
        ? score : throw new InvalidDataException("original_ai.hire.offer_evaluation." + value.TemplateId);

    internal OriginalAiNodeStatus HireOfferInformation(OriginalAiHireOffer offer, int field, uint output)
    {
        if (!Adapter.ReadOriginalAiHireOfferValidity(Current, offer.TemplateId)) return OriginalAiNodeStatus.Failure;
        int value = field switch
        {
            11 => Adapter.ReadOriginalAiHireOfferAbility(Current, InputsCurrent, offer.TemplateId, 3)
                ?? throw new InvalidDataException("original_ai.hire.offer_politics"),
            25 => HireOfferEvaluation(offer),
            27 => Adapter.ReadOriginalAiHireOfferAbilitySum(Current, InputsCurrent, offer.TemplateId)
                ?? throw new InvalidDataException("original_ai.hire.offer_ability_sum"),
            _ => throw new InvalidDataException("original_ai.hire.offer_information_" + field)
        };
        return SetInteger(output, value);
    }
    internal bool HireOfferCondition(OriginalAiHireOffer offer, uint query, IReadOnlyList<int> literals) => query switch
    {
        22 => Adapter.ReadOriginalAiHireOfferValidity(Current, offer.TemplateId),
        _ => throw new InvalidDataException("original_ai.hire.offer_condition_" + query)
    };

    internal OriginalAiNodeStatus MyselfHireOffers(uint output)
    {
        var force = Adapter.ReadOriginalAiMyselfForce(InputsCurrent, Actor);
        if (force is not { } selected || !Adapter.ReadOriginalAiForceValidity(InputsCurrent, selected.Id))
            return OriginalAiNodeStatus.Failure;
        ReplaceHireOfferCandidates(output, Adapter.ReadOriginalAiHireOffersCurrent(Current, selected.Id));
        return OriginalAiNodeStatus.Success;
    }
    internal OriginalAiNodeStatus VariableHireOffers(uint input, uint output)
    {
        if (!TryEntity(input, out var force) || force.Kind != WorldEntityKind.Force ||
            !Adapter.ReadOriginalAiForceValidity(InputsCurrent, force.Id)) return OriginalAiNodeStatus.Failure;
        ReplaceHireOfferCandidates(output, Adapter.ReadOriginalAiHireOffersCurrent(Current, force.Id));
        return OriginalAiNodeStatus.Success;
    }
    internal OriginalAiNodeStatus PersonAbilitySum(uint input, uint output)
    {
        int? sum;
        if (TryHireOffer(input, out var offer)) sum = Adapter.ReadOriginalAiHireOfferAbilitySum(Current, InputsCurrent, offer.TemplateId);
        else if (TryEntity(input, out var person) && person.Kind == WorldEntityKind.Officer)
            sum = Adapter.ReadOriginalAiWorldPersonAbilitySum(Current, InputsCurrent, person.Id);
        else return OriginalAiNodeStatus.Failure;
        return sum is int value ? SetInteger(output, value) : OriginalAiNodeStatus.Failure;
    }
    internal OriginalAiNodeStatus ExecuteHireTemplates(uint footholdKey, uint listKey)
    {
        if (!TryEntity(footholdKey, out var foothold) || foothold.Kind is not (WorldEntityKind.City or WorldEntityKind.Gate) ||
            !TryTypedCandidates(listKey, CandidateListKind.Officer, out var list)) return OriginalAiNodeStatus.Failure;
        //The actual node checks destination/budgets using the full list count
        //before inspecting even its first Person. Preserve those known skips
        //before reporting the unclosed main-Person clone input domain.
        int count = list.HoldsHireOffers ? list.HireOfferValues.Count : list.Values.Count;
        if (Adapter.ReadOriginalAiHireTargetCurrent(Current, InputsCurrent, foothold.Id, count) is not { } target)
            return OriginalAiNodeStatus.Failure;
        if (!TryReadHireOfferCandidates(listKey, out var selected)) return OriginalAiNodeStatus.Failure;
        var status = ApplyNativeCommand(new ExecutePersonnelHireOrder(target.ForceId, target.DistrictId,
            target.DestinationBaseId, selected.Select(value => value.TemplateId).ToArray()));
        if (status != OriginalAiNodeStatus.Success) return status;
        var receipt = LastCommand?.PersonnelHireExecution
            ?? throw new InvalidDataException("original_ai.hire.execution_receipt");
        return receipt.Completed ? OriginalAiNodeStatus.Success : OriginalAiNodeStatus.Failure;
    }
}
