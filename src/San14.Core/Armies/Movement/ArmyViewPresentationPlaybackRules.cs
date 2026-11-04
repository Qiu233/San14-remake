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
        bool seenArmyViewCommand = false;
        bool seenRetirement = false;
        var sources = new HashSet<CampaignEffectSource>();
        foreach (var batch in effects.PendingBatches)
        {
            for (int index = 0; index < batch.Requests.Length; index++)
            {
                var request = batch.Requests[index];
                if (request.HistoryNotification is not null) continue;
                bool sourceHead = sources.Add(batch.Source);
                bool armyViewHead = !seenArmyViewCommand;
                if (IsArmyViewCommand(batch.Source, request)) seenArmyViewCommand = true;
                if (!seenRetirement && batch.Source == CampaignEffectSource.ArmyMovement && sourceHead && request.ArmyDeparture is not null)
                    return new(batch.Source, batch.Sequence, index, request);
                if (armyViewHead && batch.Source == CampaignEffectSource.NormalCombatApplication &&
                    request.NormalCombatTargetReaction is not null)
                    return new(batch.Source, batch.Sequence, index, request);
                if (batch.Source == CampaignEffectSource.ArmyViewRetirement && request.ArmyViewRetirement is not null)
                {
                    if (armyViewHead) return new(batch.Source, batch.Sequence, index, request);
                    seenRetirement = true;
                }
                if (!seenRetirement && request.ArmyTraitViewRefresh is not null && firstTrait is { } trait &&
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
            SourceHead(effects, CampaignEffectSource.ArmyMovement, stopBeforeRetirement: true) is not { } head ||
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
            FirstTrait(effects, stopBeforeRetirement: true) is not { } head || head.Source != expectedSource ||
            head.BatchSequence != batchSequence || head.RequestIndex != requestIndex) return false;
        if (expectedSource is CampaignEffectSource.FireDamage or CampaignEffectSource.TerrainDamage)
        {
            if (SourceHead(effects, expectedSource) is not { } first ||
                first.BatchSequence != batchSequence || first.RequestIndex != requestIndex) return false;
        }
        next = Remove(owner, effects, head);
        return true;
    }

    /// <summary>Consecutive reached reaction commands in the real Army-view
    /// channel. Non-view narration/audio/history never delays a model command;
    /// an earlier departure or trait refresh still owns that channel first.</summary>
    public static IReadOnlyList<ArmyViewPresentationHead> TargetReactionPrefix(GameState owner)
    {
        var prefix = new List<ArmyViewPresentationHead>();
        if (owner.CampaignEffectRequests is not { } effects) return prefix;
        foreach (var batch in effects.PendingBatches)
            for (int index = 0; index < batch.Requests.Length; index++)
            {
                var request = batch.Requests[index];
                if (request.HistoryNotification is not null) continue;
                if (batch.Source == CampaignEffectSource.NormalCombatApplication && request.NormalCombatTargetReaction is not null)
                    prefix.Add(new(batch.Source, batch.Sequence, index, request));
                else if (batch.Source == CampaignEffectSource.ArmyMovement && request.ArmyDeparture is not null ||
                    TraitSource(batch.Source) && request.ArmyTraitViewRefresh is not null ||
                    batch.Source == CampaignEffectSource.ArmyViewRetirement && request.ArmyViewRetirement is not null) return prefix;
            }
        return prefix;
    }

    /// <summary>Receipts actual pending-variant admission/coalescing or the
    /// original known no-view branch; this is not animation completion.</summary>
    public static bool TryCompleteTargetReaction(GameState owner, long batchSequence, int requestIndex,
        NormalCombatTargetReactionRequest expectedRequest, out GameState next)
    {
        next = owner;
        var prefix = TargetReactionPrefix(owner);
        if (prefix.Count == 0 || expectedRequest is null || expectedRequest.ArmyId <= 0 ||
            expectedRequest.Variant is not (1 or 2)) return false;
        var head = prefix[0];
        if (head.BatchSequence != batchSequence || head.RequestIndex != requestIndex ||
            head.Request.NormalCombatTargetReaction != expectedRequest || owner.CampaignEffectRequests is not { } effects) return false;
        next = Remove(owner, effects, head);
        return true;
    }

    /// <summary>Receipts an actual accepted prefix in one immutable replacement.
    /// All identities use the same pre-receipt owner, so same-batch indices never
    /// shift between requests and no extra render frame is needed per Army.</summary>
    public static bool TryCompleteTargetReactions(GameState owner,
        IReadOnlyList<ArmyViewPresentationHead> expectedPrefix, out GameState next)
    {
        next = owner;
        if (expectedPrefix is null || expectedPrefix.Count == 0 || owner.CampaignEffectRequests is not { } effects) return false;
        var actual = TargetReactionPrefix(owner);
        if (expectedPrefix.Count > actual.Count) return false;
        var indices = new Dictionary<long, HashSet<int>>();
        for (int index = 0; index < expectedPrefix.Count; index++)
        {
            var expected = expectedPrefix[index]; var head = actual[index];
            if (expected is null || expected.Source != head.Source || expected.BatchSequence != head.BatchSequence ||
                expected.RequestIndex != head.RequestIndex || expected.Request.NormalCombatTargetReaction is not { } reaction ||
                reaction.ArmyId <= 0 || reaction.Variant is not (1 or 2) ||
                head.Request.NormalCombatTargetReaction != reaction) return false;
            if (!indices.TryGetValue(head.BatchSequence, out var removed)) indices.Add(head.BatchSequence, removed = []);
            removed.Add(head.RequestIndex);
        }
        var batches = new List<CampaignEffectBatch>(effects.PendingBatches.Length);
        foreach (var batch in effects.PendingBatches)
        {
            if (!indices.TryGetValue(batch.Sequence, out var removed)) { batches.Add(batch); continue; }
            var requests = batch.Requests.Where((_, index) => !removed.Contains(index)).ToArray();
            if (requests.Length != 0) batches.Add(batch with { Requests = requests });
        }
        next = owner with { CampaignEffectRequests = effects with { PendingBatches = batches.ToArray() } };
        return true;
    }

    /// <summary>Consumes only the reached source head after actual model
    /// retirement admission. The request's explicit animation flag is part of
    /// the expected identity; no Unit absence or exclusion can complete it.</summary>
    public static bool TryCompleteRetirement(GameState owner, long batchSequence, int requestIndex,
        ArmyViewRetirementRequest expectedRequest, out GameState next)
    {
        next = owner;
        if (expectedRequest is null || expectedRequest.ArmyId <= 0 ||
            owner.CampaignEffectRequests is not { } effects || Next(owner) is not { } head ||
            head.Source != CampaignEffectSource.ArmyViewRetirement || head.BatchSequence != batchSequence ||
            head.RequestIndex != requestIndex || head.Request.ArmyViewRetirement != expectedRequest)
            return false;
        next = Remove(owner, effects, head);
        return true;
    }

    private static bool IsArmyViewCommand(CampaignEffectSource source, CampaignEffectRequest request) =>
        source == CampaignEffectSource.ArmyMovement && request.ArmyDeparture is not null ||
        TraitSource(source) && request.ArmyTraitViewRefresh is not null ||
        source == CampaignEffectSource.NormalCombatApplication && request.NormalCombatTargetReaction is not null ||
        source == CampaignEffectSource.ArmyViewRetirement && request.ArmyViewRetirement is not null;

    private static bool TraitSource(CampaignEffectSource source) => source is
        CampaignEffectSource.ArmyFalseReport or CampaignEffectSource.ArmyTraitMaintenance or
        CampaignEffectSource.FacilityTraps or CampaignEffectSource.FireDamage or CampaignEffectSource.TerrainDamage or
        CampaignEffectSource.NormalCombatApplication or CampaignEffectSource.FacilityEffects or
        CampaignEffectSource.ArmyBattleDefeat or CampaignEffectSource.ArmyMovement;

    private static ArmyViewPresentationHead? SourceHead(CampaignEffectRequests effects, CampaignEffectSource source,
        bool stopBeforeRetirement = false)
    {
        foreach (var batch in effects.PendingBatches)
        {
            if (stopBeforeRetirement && batch.Source == CampaignEffectSource.ArmyViewRetirement &&
                batch.Requests.Any(request => request.ArmyViewRetirement is not null)) return null;
            if (batch.Source == source)
                for (int index = 0; index < batch.Requests.Length; index++)
                    if (batch.Requests[index].HistoryNotification is null)
                        return new(source, batch.Sequence, index, batch.Requests[index]);
        }
        return null;
    }
    private static ArmyViewPresentationHead? FirstTrait(CampaignEffectRequests effects, bool stopBeforeRetirement = false)
    {
        foreach (var batch in effects.PendingBatches)
            for (int index = 0; index < batch.Requests.Length; index++)
            {
                var request = batch.Requests[index];
                if (stopBeforeRetirement && batch.Source == CampaignEffectSource.ArmyViewRetirement &&
                    request.ArmyViewRetirement is not null) return null;
                if (request.ArmyTraitViewRefresh is not null)
                    return new(batch.Source, batch.Sequence, index, request);
            }
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
