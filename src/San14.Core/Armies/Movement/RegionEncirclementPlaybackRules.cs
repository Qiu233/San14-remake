namespace San14.Core;

/// <summary>Receipt of the actual map's completed encirclement display. The
/// queue is independent of territory settlement and no render result is
/// inferred from a changed Hex, saved phase or elapsed simulation time.</summary>
public static class RegionEncirclementPlaybackRules
{
    /// <summary>The live period host owns a borrowed candidate instead of a
    /// GameSimulation. It applies the same precise ArmyMovement source FIFO
    /// after the actual 4/5 map effect or obstacle consumer completes.</summary>
    public static bool TryCompleteOccupationEffect(GameState owner, long sequence, int index,
        CampaignEffectRequest expectedRequest, out GameState next)
    {
        next = owner;
        if (owner.CampaignEffectRequests is not { } effects) return false;
        for (int batchIndex = 0; batchIndex < effects.PendingBatches.Length; batchIndex++)
        {
            var batch = effects.PendingBatches[batchIndex];
            if (!MapObjectPresentationSources.HasSource(batch.Source)) continue;
            for (int requestIndex = 0; requestIndex < batch.Requests.Length; requestIndex++)
            {
                var request = batch.Requests[requestIndex];
                if (request.HistoryNotification is not null) continue;
                if (batch.Sequence != sequence || requestIndex != index ||
                    !ReferenceEquals(request, expectedRequest)) return false;
                if (request.MonthlyMaintenance is not
                        { Operation: MonthlyMaintenanceOperation.TerritoryOccupation, EventKind: 4 or 5 } &&
                    !MapObjectPresentationSources.Supports(batch.Source, request.MapObjectPresentation)) return false;
                CampaignEffectBatch[] batches;
                if (batch.Requests.Length == 1)
                    batches = effects.PendingBatches.Where((_, at) => at != batchIndex).ToArray();
                else
                {
                    batches = (CampaignEffectBatch[])effects.PendingBatches.Clone();
                    batches[batchIndex] = batch with { Requests = batch.Requests.Where((_, at) => at != requestIndex).ToArray() };
                }
                next = owner with { CampaignEffectRequests = effects with { PendingBatches = batches } };
                return true;
            }
        }
        return false;
    }

    public static bool TryComplete(GameState owner, RegionEncirclementPresentation expectedRequest,
        out GameState next)
    {
        next = owner;
        if (owner.RegionOccupationFollowups is not { } pending ||
            pending.EncirclementPresentations.Count == 0 ||
            !ReferenceEquals(pending.EncirclementPresentations[0], expectedRequest)) return false;
        next = owner with { RegionOccupationFollowups = new(pending.EncirclementSeeds,
            pending.Announcements, pending.EncirclementPresentations.Skip(1).ToArray()) };
        return true;
    }
}
