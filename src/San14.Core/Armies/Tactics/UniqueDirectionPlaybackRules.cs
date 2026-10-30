namespace San14.Core;

/// <summary>Only the actual exclusive Direction host may finish its manager
/// head. These transitions do not apply tactics or alter ordinary-node busy.</summary>
public static class UniqueDirectionPlaybackRules
{
    public static bool TryComplete(GameState owner, ManualTacticInterventionState expectedManager,
        long sequence, out GameState next)
    {
        next = owner;
        if (!MatchesHead(owner, expectedManager, sequence)) return false;
        next = owner with { ManualTacticIntervention = expectedManager.Acknowledge(
            new(sequence, TacticPresentationKind.UniqueDirection)) };
        return true;
    }

    public static bool TryAdoptPreparedAudio(GameState owner, ManualTacticInterventionState expectedManager,
        long sequence, GameState preparedState, out GameState next)
    {
        next = owner;
        if (!MatchesHead(owner, expectedManager, sequence) || preparedState is null ||
            !ReferenceEquals(preparedState.ManualTacticIntervention, expectedManager)) return false;
        next = preparedState.CampaignAppRandom32 is null ? owner :
            owner with { CampaignAppRandom32 = preparedState.CampaignAppRandom32 };
        return true;
    }

    private static bool MatchesHead(GameState owner, ManualTacticInterventionState expectedManager, long sequence) =>
        expectedManager is not null && ReferenceEquals(owner.ManualTacticIntervention, expectedManager) &&
        expectedManager.Directions is { Length: > 0 } &&
        expectedManager.Directions[0] is { Kind: TacticPresentationKind.UniqueDirection } head && head.Sequence == sequence;
}
