namespace San14.Core;

/// <summary>A selected music theme waiting for presentation. It is not a cue
/// playback receipt. Application settings, cue mapping and the mixer remain
/// presentation-owned. A later selection supersedes an unconsumed selection.</summary>
public sealed record MapMusicIntent(long CalendarCycleOrdinal, int SourceSlot,
    bool UseStoredPlaybackPosition, int FadeInMilliseconds, int FadeOutMilliseconds);

/// <summary>Completion of the same selected calendar intent on the same
/// published or borrowed owner, after actual acceptance by the audio host.</summary>
public static class MapMusicIntentCompletion
{
    public static bool TryComplete(GameState owner, GameState expectedOwner,
        MapMusicIntent expectedIntent, out GameState completed)
    {
        completed = owner;
        if (!ReferenceEquals(owner, expectedOwner) || expectedIntent is null ||
            !ReferenceEquals(owner.PendingMapMusicIntent, expectedIntent)) return false;
        completed = owner with { PendingMapMusicIntent = null };
        return true;
    }
}

public sealed partial record GameState
{
    public MapMusicIntent? PendingMapMusicIntent { get; init; }
}
