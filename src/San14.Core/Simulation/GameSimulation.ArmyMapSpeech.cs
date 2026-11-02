namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Records acceptance of the prepared spatial voices by the actual
    /// audio owner. It never selects voice data or advances its random stream.</summary>
    public bool TryCompleteArmyMapSpeechVoices(GameState expectedOwner, long sequence)
    {
        var pending = _normalPeriod;
        var owner = SourcePresentationOwner;
        if (!ReferenceEquals(owner, expectedOwner) || owner.ArmyMapSpeech is not { } speech ||
            pending is not null && !NormalPeriodOperation.HasContinuation(pending.Candidate))
            return false;
        int index = -1;
        for (int i = 0; i < speech.Entries.Length; i++)
            if (speech.Entries[i].Sequence == sequence) { index = i; break; }
        if (index < 0 || speech.Entries[index] is not { TextAtSource: not null, SpatialVoicesAccepted: false } entry ||
            entry.SpatialVoices is not { IsEmpty: false }) return false;
        var accepted = owner with
        {
            ArmyMapSpeech = speech with { Entries = speech.Entries.SetItem(index, entry with { SpatialVoicesAccepted = true }) }
        };
        PublishSourcePresentationOwner(accepted, pending);
        return true;
    }

    /// <summary>Starts one real queued speech using its actual formatter and
    /// App stream. Display-frame timers stay in the host's small snapshot.</summary>
    public bool TryStartArmyMapSpeech(GameState expectedOwner, ArmyMapSpeechState snapshot,
        long sequence, bool armyLayoutAvailable, IArmyMapSpeechRules rules,
        out ArmyMapSpeechStartResult? result, CampaignAppRandomState? appRandomSnapshot = null)
    {
        result = null;
        var pending = _normalPeriod;
        var owner = SourcePresentationOwner;
        if (!ReferenceEquals(owner, expectedOwner) || owner.ArmyMapSpeech is null ||
            pending is not null && !NormalPeriodOperation.HasContinuation(pending.Candidate))
            return false;
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(rules);
        var working = owner with { ArmyMapSpeech = snapshot };
        //The Intelligence narration host and map bubbles share the App stream
        //during this reached wait. Other suspended transactions keep their
        //existing candidate-owned stream contract.
        bool sharesPresentationApp = pending is null || pending.PendingIntelligenceNarrationTaskId is not null ||
            pending.RequiresAutomaticIntelligenceContinuation || BaseCapturePersonPrefixProgress.IsPresentationPending(owner) ||
            HiredArmyCaptureProgress.IsPresentationPending(owner);
        if (sharesPresentationApp && appRandomSnapshot is not null)
            working = working with { CampaignAppRandom32 = appRandomSnapshot };
        result = rules.Start(working, sequence, armyLayoutAvailable);
        if (result.UnresolvedOwner is not null) return false;
        // A source cancellation (for example a missing Army layout) is a real
        // queue update even though no bubble was started.
        PublishSourcePresentationOwner(result.State, pending);
        return true;
    }

    /// <summary>Removes only the bubble whose real playback has completed.</summary>
    public bool TryCompleteArmyMapSpeech(GameState expectedOwner, ArmyMapSpeechState snapshot, long sequence)
    {
        var pending = _normalPeriod;
        var owner = SourcePresentationOwner;
        if (!ReferenceEquals(owner, expectedOwner) || owner.ArmyMapSpeech is null ||
            pending is not null && !NormalPeriodOperation.HasContinuation(pending.Candidate))
            return false;
        ArgumentNullException.ThrowIfNull(snapshot);
        int index = -1;
        for (int i = 0; i < snapshot.Entries.Length; i++)
            if (snapshot.Entries[i].Sequence == sequence) { index = i; break; }
        if (index < 0 || snapshot.Entries[index].TextAtSource is null ||
            owner.ArmyMapSpeech.Entries.FirstOrDefault(entry => entry.Sequence == sequence) is not { TextAtSource: not null } original ||
            original.SpatialVoices is not { } voices || !voices.IsEmpty && !original.SpatialVoicesAccepted)
            return false;
        var completed = owner with { ArmyMapSpeech = snapshot with { Entries = snapshot.Entries.RemoveAt(index) } };
        PublishSourcePresentationOwner(completed, pending);
        return true;
    }
}
