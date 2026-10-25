namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Starts one real queued speech using its actual formatter and
    /// App stream. Display-frame timers stay in the host's small snapshot.</summary>
    public bool TryStartArmyMapSpeech(GameState expectedOwner, ArmyMapSpeechState snapshot,
        long sequence, bool armyLayoutAvailable, IArmyMapSpeechRules rules,
        out ArmyMapSpeechStartResult? result, CampaignAppRandomState? appRandomSnapshot = null)
    {
        result = null;
        var pending = _normalPeriod;
        var owner = pending?.Candidate ?? State;
        if (_progress is not null || !ReferenceEquals(owner, expectedOwner) || owner.ArmyMapSpeech is null ||
            pending is not null && !NormalPeriodOperation.HasContinuation(pending.Candidate))
            return false;
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(rules);
        var working = owner with { ArmyMapSpeech = snapshot };
        if (pending is null && appRandomSnapshot is not null)
            working = working with { CampaignAppRandom32 = appRandomSnapshot };
        result = rules.Start(working, sequence, armyLayoutAvailable);
        if (result.UnresolvedOwner is not null) return false;
        // A source cancellation (for example a missing Army layout) is a real
        // queue update even though no bubble was started.
        if (pending is not null) _normalPeriod = pending with { Candidate = result.State };
        else State = result.State;
        return true;
    }

    /// <summary>Removes only the bubble whose real playback has completed.</summary>
    public bool TryCompleteArmyMapSpeech(GameState expectedOwner, ArmyMapSpeechState snapshot, long sequence)
    {
        var pending = _normalPeriod;
        var owner = pending?.Candidate ?? State;
        if (_progress is not null || !ReferenceEquals(owner, expectedOwner) || owner.ArmyMapSpeech is null ||
            pending is not null && !NormalPeriodOperation.HasContinuation(pending.Candidate))
            return false;
        ArgumentNullException.ThrowIfNull(snapshot);
        int index = -1;
        for (int i = 0; i < snapshot.Entries.Length; i++)
            if (snapshot.Entries[i].Sequence == sequence) { index = i; break; }
        if (index < 0 || snapshot.Entries[index].TextAtSource is null ||
            !owner.ArmyMapSpeech.Entries.Any(entry => entry.Sequence == sequence && entry.TextAtSource is not null))
            return false;
        var completed = owner with { ArmyMapSpeech = snapshot with { Entries = snapshot.Entries.RemoveAt(index) } };
        if (pending is not null) _normalPeriod = pending with { Candidate = completed };
        else State = completed;
        return true;
    }
}
