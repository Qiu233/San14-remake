namespace San14.Core;

/// <summary>The controller's ordered Base-damage movies are independent of
/// model reaction and narration channels. Completion never writes Base damage.</summary>
public static class BaseDamageTierCutscenePlaybackRules
{
    private static (int BatchIndex, int RequestIndex, BaseDamageTierCutsceneRequest Request)?
        First(CampaignEffectRequests effects)
    {
        for (int batchIndex = 0; batchIndex < effects.PendingBatches.Length; batchIndex++)
        {
            var batch = effects.PendingBatches[batchIndex];
            if (batch.Source != CampaignEffectSource.NormalCombatApplication) continue;
            for (int index = 0; index < batch.Requests.Length; index++)
                if (batch.Requests[index].BaseDamageTierCutscene is { } request)
                    return (batchIndex, index, request);
        }
        return null;
    }

    /// <summary>Consumes only the first reached movie request after its actual
    /// controller and camera have completed, or reached the genuine disabled
    /// branch. The immutable source request is the admission identity.</summary>
    public static bool TryComplete(GameState owner, long batchSequence, int requestIndex,
        BaseDamageTierCutsceneRequest expected, out GameState next)
    {
        next = owner;
        if (expected is null || owner.CampaignEffectRequests is not { } effects ||
            First(effects) is not { } head || head.Request != expected ||
            head.RequestIndex != requestIndex ||
            effects.PendingBatches[head.BatchIndex].Sequence != batchSequence) return false;
        int at = head.BatchIndex;
        var batch = effects.PendingBatches[at];
        var batches = new List<CampaignEffectBatch>(effects.PendingBatches.Length);
        for (int index = 0; index < effects.PendingBatches.Length; index++)
        {
            if (index != at) { batches.Add(effects.PendingBatches[index]); continue; }
            if (batch.Requests.Length == 1) continue;
            var requests = new CampaignEffectRequest[batch.Requests.Length - 1];
            Array.Copy(batch.Requests, 0, requests, 0, requestIndex);
            Array.Copy(batch.Requests, requestIndex + 1, requests, requestIndex,
                requests.Length - requestIndex);
            batches.Add(batch with { Requests = requests });
        }
        next = owner with { CampaignEffectRequests = effects with { PendingBatches = batches.ToArray() } };
        return true;
    }
}
