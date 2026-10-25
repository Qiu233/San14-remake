namespace San14.Core;

/// <summary>The shared ordinary-node transitions for the session and an
/// exclusively borrowed period candidate. These publish no gameplay effects.</summary>
public static class OrdinaryTacticPlaybackRules
{
    public static bool TryBegin(GameState owner, ManualTacticInterventionState expectedManager,
        long sequence, GameState? preparedState, out GameState next)
    {
        next = owner;
        if (!Matches(owner, expectedManager)) return false;
        int index = expectedManager.OrdinaryPlaybacks.Length;
        if (index >= expectedManager.Effects.Length || expectedManager.Effects[index].Sequence != sequence ||
            expectedManager.Effects[index].Kind != TacticPresentationKind.OrdinaryEffects) return false;
        next = owner with
        {
            CampaignAppRandom32 = preparedState is null ? owner.CampaignAppRandom32 : preparedState.CampaignAppRandom32,
            ManualTacticIntervention = expectedManager with
            { OrdinaryPlaybacks = expectedManager.OrdinaryPlaybacks.Add(new(sequence, false)) }
        };
        return true;
    }

    public static bool TryCompleteResults(GameState owner, ManualTacticInterventionState expectedManager,
        long sequence, out GameState next)
    {
        next = owner;
        if (!Matches(owner, expectedManager)) return false;
        int index = FindPlayback(expectedManager, sequence);
        if (index < 0 || expectedManager.OrdinaryPlaybacks[index].ResultsPublished) return false;
        var playback = expectedManager.OrdinaryPlaybacks[index];
        next = owner with { ManualTacticIntervention = expectedManager with
        { OrdinaryPlaybacks = expectedManager.OrdinaryPlaybacks.SetItem(index, playback with { ResultsPublished = true }) } };
        return true;
    }

    public static bool TryRetire(GameState owner, ManualTacticInterventionState expectedManager,
        long sequence, out GameState next)
    {
        next = owner;
        if (!Matches(owner, expectedManager)) return false;
        int index = FindPlayback(expectedManager, sequence);
        if (index < 0 || !expectedManager.OrdinaryPlaybacks[index].ResultsPublished) return false;
        var effects = new TacticPresentationRequest[expectedManager.Effects.Length - 1];
        Array.Copy(expectedManager.Effects, effects, index);
        Array.Copy(expectedManager.Effects, index + 1, effects, index, effects.Length - index);
        next = owner with { ManualTacticIntervention = expectedManager with
        { Effects = effects, OrdinaryPlaybacks = expectedManager.OrdinaryPlaybacks.RemoveAt(index) } };
        return true;
    }

    private static bool Matches(GameState owner, ManualTacticInterventionState expectedManager) =>
        expectedManager is not null && ReferenceEquals(owner.ManualTacticIntervention, expectedManager) &&
        expectedManager.Effects is { Length: > 0 } && !expectedManager.OrdinaryPlaybacks.IsDefault;

    private static int FindPlayback(ManualTacticInterventionState manager, long sequence)
    {
        for (int index = 0; index < manager.OrdinaryPlaybacks.Length; index++)
            if (manager.OrdinaryPlaybacks[index].Sequence == sequence)
                return index < manager.Effects.Length && manager.Effects[index].Sequence == sequence &&
                    manager.Effects[index].Kind == TacticPresentationKind.OrdinaryEffects ? index : -1;
        return -1;
    }
}
