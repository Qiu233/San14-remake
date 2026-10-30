using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus TransportSoldier(uint listKey, int consumptionPercent,
        int supplyHorizon, int incomeHorizon)
    {
        if (!TryFootholdCandidateList(listKey, out var list)) return OriginalAiNodeStatus.Failure;
        if (list.Values.Count == 0) return OriginalAiNodeStatus.Success;
        var locations = new int[list.Values.Count];
        for (int i = 0; i < locations.Length; i++)
        {
            var value = list.Values[i];
            if (value.Kind is not (WorldEntityKind.City or WorldEntityKind.Gate))
                throw new InvalidDataException("original_ai.transport_troop_ceiling.Base_list_type");
            locations[i] = value.Id;
        }
        return ApplyNativeCommand(new EstimateTransportTroopCeilingsOrder(Actor.ForceId,
            locations, consumptionPercent, supplyHorizon, incomeHorizon));
    }
}
