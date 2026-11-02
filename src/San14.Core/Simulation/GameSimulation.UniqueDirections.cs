namespace San14.Core;

public sealed partial class GameSimulation
{
    public bool TryCompleteUniqueDirectionPresentation(GameState expectedOwner,
        ManualTacticInterventionState expectedManager, long sequence)
    {
        //The legacy manual operation continues through ResumeManualTactic,
        //which consumes this same head and then runs its real remaining stage.
        if (_progress?.ManualTactic is not null) return false;
        var owner = UniqueDirectionOwner;
        if (!ReferenceEquals(owner, expectedOwner) ||
            !UniqueDirectionPlaybackRules.TryComplete(owner, expectedManager, sequence, out var next)) return false;
        PublishUniqueDirectionOwner(next);
        return true;
    }

    public bool TryAdoptUniqueDirectionPreparedAudio(GameState expectedOwner,
        ManualTacticInterventionState expectedManager, long sequence, GameState preparedState)
    {
        var owner = UniqueDirectionOwner;
        if (!ReferenceEquals(owner, expectedOwner) ||
            !UniqueDirectionPlaybackRules.TryAdoptPreparedAudio(owner, expectedManager, sequence, preparedState, out var next)) return false;
        PublishUniqueDirectionOwner(next);
        return true;
    }

    private GameState UniqueDirectionOwner =>
        _progress?.ManualTactic?.Candidate ?? _progress?.ArmyProgress?.Candidate ?? _progress?.Story?.Candidate ?? _normalPeriod?.Candidate ?? State;

    private void PublishUniqueDirectionOwner(GameState next)
    {
        if (_progress?.ManualTactic is { } manual) _progress = new(null, manual with { Candidate = next });
        else if (_progress?.ArmyProgress is { } army) _progress = new(null, null, army with { Candidate = next });
        else if (_progress?.Story is { } story) _progress = new(story with { Candidate = next }, null);
        else if (_normalPeriod is { } period) _normalPeriod = period with { Candidate = next };
        else State = next;
    }
}
