namespace San14.Core;

/// <summary>A reached source command, independently of modal narration from
/// other sources. Source-head and first-trait ordering match normal receipts.</summary>
public sealed record ArmyViewPresentationHead(CampaignEffectSource Source, long BatchSequence,
    int RequestIndex, CampaignEffectRequest Request);

public static class ArmyViewPresentationPlaybackRules
{
    public static ArmyViewPresentationHead? Next(GameState owner)
    {
        if (owner.CampaignEffectRequests is not { } effects) return null;
        var firstTrait = FirstTrait(effects);
        var sources = new HashSet<CampaignEffectSource>();
        foreach (var batch in effects.PendingBatches)
        {
            for (int index = 0; index < batch.Requests.Length; index++)
            {
                var request = batch.Requests[index];
                if (request.HistoryNotification is not null) continue;
                bool sourceHead = sources.Add(batch.Source);
                if (batch.Source == CampaignEffectSource.ArmyMovement && sourceHead && request.ArmyDeparture is not null)
                    return new(batch.Source, batch.Sequence, index, request);
                if (request.ArmyTraitViewRefresh is not null && firstTrait is { } trait &&
                    trait.BatchSequence == batch.Sequence && trait.RequestIndex == index &&
                    TraitSource(batch.Source) &&
                    (batch.Source is not (CampaignEffectSource.FireDamage or CampaignEffectSource.TerrainDamage) || sourceHead))
                    return trait;
            }
        }
        return null;
    }

    public static bool TryCompleteDeparture(GameState owner, long batchSequence, int requestIndex, out GameState next)
    {
        next = owner;
        if (owner.CampaignEffectRequests is not { } effects ||
            SourceHead(effects, CampaignEffectSource.ArmyMovement) is not { } head ||
            head.BatchSequence != batchSequence || head.RequestIndex != requestIndex ||
            head.Request.ArmyDeparture is not { RebuildInfoLayout: not null, SourceSnapshot: not null }) return false;
        next = Remove(owner, effects, head);
        return true;
    }

    public static bool TryCompleteTrait(GameState owner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, out GameState next)
    {
        next = owner;
        if (!TraitSource(expectedSource) || owner.CampaignEffectRequests is not { } effects ||
            FirstTrait(effects) is not { } head || head.Source != expectedSource ||
            head.BatchSequence != batchSequence || head.RequestIndex != requestIndex) return false;
        if (expectedSource is CampaignEffectSource.FireDamage or CampaignEffectSource.TerrainDamage)
        {
            if (SourceHead(effects, expectedSource) is not { } first ||
                first.BatchSequence != batchSequence || first.RequestIndex != requestIndex) return false;
        }
        next = Remove(owner, effects, head);
        return true;
    }

    private static bool TraitSource(CampaignEffectSource source) => source is
        CampaignEffectSource.ArmyFalseReport or CampaignEffectSource.ArmyTraitMaintenance or
        CampaignEffectSource.FacilityTraps or CampaignEffectSource.FireDamage or CampaignEffectSource.TerrainDamage or
        CampaignEffectSource.NormalCombatApplication or CampaignEffectSource.FacilityEffects or
        CampaignEffectSource.ArmyBattleDefeat or CampaignEffectSource.ArmyMovement;

    private static ArmyViewPresentationHead? SourceHead(CampaignEffectRequests effects, CampaignEffectSource source)
    {
        foreach (var batch in effects.PendingBatches)
            if (batch.Source == source)
                for (int index = 0; index < batch.Requests.Length; index++)
                    if (batch.Requests[index].HistoryNotification is null)
                        return new(source, batch.Sequence, index, batch.Requests[index]);
        return null;
    }
    private static ArmyViewPresentationHead? FirstTrait(CampaignEffectRequests effects)
    {
        foreach (var batch in effects.PendingBatches)
            for (int index = 0; index < batch.Requests.Length; index++)
                if (batch.Requests[index].ArmyTraitViewRefresh is not null)
                    return new(batch.Source, batch.Sequence, index, batch.Requests[index]);
        return null;
    }
    private static GameState Remove(GameState owner, CampaignEffectRequests effects, ArmyViewPresentationHead head)
    {
        int at = Array.FindIndex(effects.PendingBatches, batch => batch.Sequence == head.BatchSequence);
        var batch = effects.PendingBatches[at];
        CampaignEffectBatch[] batches;
        if (batch.Requests.Length == 1)
        {
            batches = new CampaignEffectBatch[effects.PendingBatches.Length - 1];
            Array.Copy(effects.PendingBatches, 0, batches, 0, at);
            Array.Copy(effects.PendingBatches, at + 1, batches, at, batches.Length - at);
        }
        else
        {
            var requests = new CampaignEffectRequest[batch.Requests.Length - 1];
            Array.Copy(batch.Requests, 0, requests, 0, head.RequestIndex);
            Array.Copy(batch.Requests, head.RequestIndex + 1, requests, head.RequestIndex, requests.Length - head.RequestIndex);
            batches = (CampaignEffectBatch[])effects.PendingBatches.Clone();
            batches[at] = batch with { Requests = requests };
        }
        return owner with { CampaignEffectRequests = effects with { PendingBatches = batches } };
    }
}
