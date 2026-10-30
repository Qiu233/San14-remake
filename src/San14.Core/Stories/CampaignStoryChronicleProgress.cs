namespace San14.Core;

/// <summary>A chronology table slot retained by the real story invocation,
/// rather than a reconstructed snapshot of an earlier business world.</summary>
public sealed record CampaignChronicleEntryReference(bool Primary, int Slot)
{
    internal void Validate()
    {
        if (Slot < 0 || Slot >= (Primary ? CampaignChronicleState.PrimaryCapacity : CampaignChronicleState.ImportantCapacity))
            throw new InvalidDataException("The story chronology table slot is invalid.");
    }
}

/// <summary>Source-time chronology work owned by one normal event playback.
/// A null owner in an older active playback remains unknown, never backfilled.</summary>
public sealed record CampaignStoryChronicleProgress(bool Deferred, bool Completed,
    CampaignChronicleEntryReference? WrittenEntry, bool SourceTimeUnavailable = false)
{
    internal void Validate()
    {
        WrittenEntry?.Validate();
        if (Deferred && !Completed && WrittenEntry is not null ||
            SourceTimeUnavailable && (Deferred || WrittenEntry is not null))
            throw new InvalidDataException("A deferred story chronology write has not executed yet.");
    }
}
