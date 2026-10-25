namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Consumes the first queued trait-view refresh after the actual
    /// render owner has refreshed or retired that Army's view.</summary>
    public bool TryCompleteArmyTraitViewRefresh(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex)
    {
        if (_progress is not null) return false;
        var pending = _normalPeriod;
        var owner = pending?.Candidate ?? State;
        if (!ReferenceEquals(owner, expectedOwner) || owner.CampaignEffectRequests is not { } effects)
            return false;
        for (int batchIndex = 0; batchIndex < effects.PendingBatches.Length; batchIndex++)
        {
            var batch = effects.PendingBatches[batchIndex];
            for (int index = 0; index < batch.Requests.Length; index++)
            {
                if (batch.Requests[index].ArmyTraitViewRefresh is null) continue;
                if (batch.Source != expectedSource || batch.Sequence != batchSequence || index != requestIndex ||
                    expectedSource is not (CampaignEffectSource.ArmyFalseReport or CampaignEffectSource.ArmyTraitMaintenance or
                        CampaignEffectSource.FacilityTraps or CampaignEffectSource.FireDamage or CampaignEffectSource.TerrainDamage or
                        CampaignEffectSource.NormalCombatApplication or CampaignEffectSource.FacilityEffects or CampaignEffectSource.ArmyBattleDefeat))
                    return false;
                CampaignEffectBatch[] batches;
                if (batch.Requests.Length == 1)
                {
                    batches = new CampaignEffectBatch[effects.PendingBatches.Length - 1];
                    Array.Copy(effects.PendingBatches, 0, batches, 0, batchIndex);
                    Array.Copy(effects.PendingBatches, batchIndex + 1, batches, batchIndex, batches.Length - batchIndex);
                }
                else
                {
                    var requests = new CampaignEffectRequest[batch.Requests.Length - 1];
                    Array.Copy(batch.Requests, 0, requests, 0, index);
                    Array.Copy(batch.Requests, index + 1, requests, index, requests.Length - index);
                    batches = (CampaignEffectBatch[])effects.PendingBatches.Clone();
                    batches[batchIndex] = batch with { Requests = requests };
                }
                var completed = owner with { CampaignEffectRequests = effects with { PendingBatches = batches } };
                if (pending is not null) _normalPeriod = pending with { Candidate = completed };
                else State = completed;
                return true;
            }
        }
        return false;
    }
}
