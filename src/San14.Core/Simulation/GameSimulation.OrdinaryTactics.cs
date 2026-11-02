namespace San14.Core;

public sealed partial class GameSimulation
{
    public bool TryBeginOrdinaryTacticPresentation(GameState expectedOwner,
        ManualTacticInterventionState expectedManager, long sequence, GameState? preparedState = null)
    {
        if (!TryReadOrdinaryTacticOwner(expectedOwner, out var owner) ||
            !OrdinaryTacticPlaybackRules.TryBegin(owner, expectedManager, sequence, preparedState, out var next)) return false;
        PublishOrdinaryTacticOwner(next, false);
        return true;
    }

    /// <summary>Records the once-only counter-zero presentation. Never applies
    /// damage, healing, modifiers or other gameplay operations again.</summary>
    public bool TryCompleteOrdinaryTacticResults(GameState expectedOwner,
        ManualTacticInterventionState expectedManager, long sequence)
    {
        if (!TryReadOrdinaryTacticOwner(expectedOwner, out var owner) ||
            !OrdinaryTacticPlaybackRules.TryCompleteResults(owner, expectedManager, sequence, out var next)) return false;
        PublishOrdinaryTacticOwner(next, false);
        return true;
    }

    /// <summary>Retires only a real completed ordinary node. The reached
    /// simulation stage owns clearing EffectBusy after the queue drains.</summary>
    public bool TryCompleteOrdinaryTacticPresentation(GameState expectedOwner,
        ManualTacticInterventionState expectedManager, long sequence)
    {
        if (!TryReadOrdinaryTacticOwner(expectedOwner, out var owner) ||
            !OrdinaryTacticPlaybackRules.TryRetire(owner, expectedManager, sequence, out var next)) return false;
        PublishOrdinaryTacticOwner(next, true);
        return true;
    }

    private bool TryReadOrdinaryTacticOwner(GameState expectedOwner, out GameState owner)
    {
        owner = _progress?.ManualTactic?.Candidate ?? _progress?.ArmyProgress?.Candidate ?? _normalPeriod?.Candidate ?? State;
        if (_progress is { ManualTactic: null, ArmyProgress: null } || !ReferenceEquals(owner, expectedOwner)) return false;
        if (_progress?.ManualTactic is { } manual &&
            (manual.ContinueAfterPresentation || manual.Presentation is not
                { Kind: TacticPresentationKind.OrdinaryEffects })) return false;
        return true;
    }

    private void PublishOrdinaryTacticOwner(GameState next, bool retired)
    {
        var manager = next.ManualTacticIntervention!;
        if (_progress?.ManualTactic is { } manual)
            _progress = new(null, retired
                ? manual with { Candidate = next,
                    Presentation = manager.Effects.Length == 0 ? null :
                        new(manager.Effects[0].Sequence, TacticPresentationKind.OrdinaryEffects),
                    ContinueAfterPresentation = manager.Effects.Length == 0 }
                : manual with { Candidate = next });
        else if (_progress?.ArmyProgress is { } army) _progress = new(null, null, army with { Candidate = next });
        else if (_normalPeriod is { } period) _normalPeriod = period with { Candidate = next };
        else State = next;
    }
}
