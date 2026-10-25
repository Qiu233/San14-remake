namespace San14.Core;

public sealed partial class GameSimulation
{
    public bool TryBeginOrdinaryTacticPresentation(GameState expectedOwner,
        ManualTacticInterventionState expectedManager, long sequence, GameState? preparedState = null)
    {
        if (!TryReadOrdinaryTacticManager(expectedOwner, expectedManager, out var owner)) return false;
        int next = expectedManager.OrdinaryPlaybacks.Length;
        if (next >= expectedManager.Effects.Length || expectedManager.Effects[next].Sequence != sequence ||
            expectedManager.Effects[next].Kind != TacticPresentationKind.OrdinaryEffects) return false;
        //The real audio batch has already accepted the once-selected voices.
        //Only its App stream is adopted; no other prepared world fields are published.
        var started = preparedState is null ? owner : owner with { CampaignAppRandom32 = preparedState.CampaignAppRandom32 };
        PublishOrdinaryTacticManager(started, expectedManager with
        { OrdinaryPlaybacks = expectedManager.OrdinaryPlaybacks.Add(new(sequence, false)) }, false);
        return true;
    }

    /// <summary>Records the once-only counter-zero presentation. Never applies
    /// damage, healing, modifiers or other gameplay operations again.</summary>
    public bool TryCompleteOrdinaryTacticResults(GameState expectedOwner,
        ManualTacticInterventionState expectedManager, long sequence)
    {
        if (!TryReadOrdinaryTacticManager(expectedOwner, expectedManager, out var owner)) return false;
        int index = FindOrdinaryTacticPlayback(expectedManager, sequence);
        if (index < 0 || expectedManager.OrdinaryPlaybacks[index].ResultsPublished) return false;
        var playback = expectedManager.OrdinaryPlaybacks[index];
        PublishOrdinaryTacticManager(owner, expectedManager with
        { OrdinaryPlaybacks = expectedManager.OrdinaryPlaybacks.SetItem(index, playback with { ResultsPublished = true }) }, false);
        return true;
    }

    /// <summary>Retires only a real completed ordinary node. The reached
    /// simulation stage owns clearing EffectBusy after the queue drains.</summary>
    public bool TryCompleteOrdinaryTacticPresentation(GameState expectedOwner,
        ManualTacticInterventionState expectedManager, long sequence)
    {
        if (!TryReadOrdinaryTacticManager(expectedOwner, expectedManager, out var owner)) return false;
        int index = FindOrdinaryTacticPlayback(expectedManager, sequence);
        if (index < 0 || !expectedManager.OrdinaryPlaybacks[index].ResultsPublished) return false;
        var effects = new TacticPresentationRequest[expectedManager.Effects.Length - 1];
        Array.Copy(expectedManager.Effects, effects, index);
        Array.Copy(expectedManager.Effects, index + 1, effects, index, effects.Length - index);
        PublishOrdinaryTacticManager(owner, expectedManager with
        { Effects = effects, OrdinaryPlaybacks = expectedManager.OrdinaryPlaybacks.RemoveAt(index) }, true);
        return true;
    }

    private bool TryReadOrdinaryTacticManager(GameState expectedOwner, ManualTacticInterventionState expectedManager,
        out GameState owner)
    {
        owner = _progress?.ManualTactic?.Candidate ?? _normalPeriod?.Candidate ?? State;
        if (expectedManager is null || _progress is { ManualTactic: null } || !ReferenceEquals(owner, expectedOwner) ||
            !ReferenceEquals(owner.ManualTacticIntervention, expectedManager) ||
            expectedManager.Effects is not { Length: > 0 } || expectedManager.OrdinaryPlaybacks.IsDefault) return false;
        if (_progress?.ManualTactic is { } manual &&
            (manual.ContinueAfterPresentation || manual.Presentation is not
                { Kind: TacticPresentationKind.OrdinaryEffects })) return false;
        return true;
    }

    private static int FindOrdinaryTacticPlayback(ManualTacticInterventionState manager, long sequence)
    {
        for (int index = 0; index < manager.OrdinaryPlaybacks.Length; index++)
            if (manager.OrdinaryPlaybacks[index].Sequence == sequence)
                return index < manager.Effects.Length && manager.Effects[index].Sequence == sequence &&
                    manager.Effects[index].Kind == TacticPresentationKind.OrdinaryEffects ? index : -1;
        return -1;
    }

    private void PublishOrdinaryTacticManager(GameState owner, ManualTacticInterventionState manager, bool retired)
    {
        var next = owner with { ManualTacticIntervention = manager };
        if (_progress?.ManualTactic is { } manual)
            _progress = new(null, retired
                ? manual with { Candidate = next,
                    Presentation = manager.Effects.Length == 0 ? null :
                        new(manager.Effects[0].Sequence, TacticPresentationKind.OrdinaryEffects),
                    ContinueAfterPresentation = manager.Effects.Length == 0 }
                : manual with { Candidate = next });
        else if (_normalPeriod is { } period) _normalPeriod = period with { Candidate = next };
        else State = next;
    }
}
