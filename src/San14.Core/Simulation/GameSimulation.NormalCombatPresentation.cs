namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Records real source-time reaction admission by the retained Army
    /// model owner. Pending/coalesced variants continue on that owner's source
    /// animation clock after this request is consumed.</summary>
    public bool TryCompleteNormalCombatTargetReaction(GameState expectedOwner,
        long batchSequence, int requestIndex, NormalCombatTargetReactionRequest expectedRequest)
    {
        var owner = SourcePresentationOwner;
        if (!ReferenceEquals(owner, expectedOwner) ||
            !ArmyViewPresentationPlaybackRules.TryCompleteTargetReaction(owner, batchSequence,
                requestIndex, expectedRequest, out var completed)) return false;
        PublishSourcePresentationOwner(completed, _normalPeriod);
        return true;
    }
    public bool TryCompleteNormalCombatTargetReactions(GameState expectedOwner,
        IReadOnlyList<ArmyViewPresentationHead> expectedPrefix)
    {
        var owner = SourcePresentationOwner;
        if (!ReferenceEquals(owner, expectedOwner) ||
            !ArmyViewPresentationPlaybackRules.TryCompleteTargetReactions(owner, expectedPrefix, out var completed)) return false;
        PublishSourcePresentationOwner(completed, _normalPeriod);
        return true;
    }

    /// <summary>Records the actual model owner's retirement admission for
    /// this exact stopped source World and unconsumed retirement head.</summary>
    public bool TryCompleteArmyViewRetirement(GameState expectedOwner,
        long batchSequence, int requestIndex, ArmyViewRetirementRequest expectedRequest)
    {
        var owner = SourcePresentationOwner;
        if (!ReferenceEquals(owner, expectedOwner) ||
            !ArmyViewPresentationPlaybackRules.TryCompleteRetirement(owner, batchSequence,
                requestIndex, expectedRequest, out var completed)) return false;
        PublishSourcePresentationOwner(completed, _normalPeriod);
        return true;
    }

    /// <summary>Receipts the real controller movie/camera lifetime, or its
    /// actual disabled branch, without repeating the committed damage-tier write.</summary>
    public bool TryCompleteBaseDamageTierCutscene(GameState expectedOwner,
        long batchSequence, int requestIndex, BaseDamageTierCutsceneRequest expectedRequest)
    {
        var owner = SourcePresentationOwner;
        if (!ReferenceEquals(owner, expectedOwner) ||
            !BaseDamageTierCutscenePlaybackRules.TryComplete(owner, batchSequence, requestIndex,
                expectedRequest, out var completed)) return false;
        PublishSourcePresentationOwner(completed, _normalPeriod);
        return true;
    }

}
