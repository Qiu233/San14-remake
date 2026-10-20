namespace San14.Core;

/// <summary>A selected music theme waiting for presentation. It is not a cue
/// playback receipt. Application settings, cue mapping and the mixer remain
/// presentation-owned. A later selection supersedes an unconsumed selection.</summary>
public sealed record MapMusicIntent(long CalendarCycleOrdinal, int SourceSlot,
    bool UseStoredPlaybackPosition, int FadeInMilliseconds, int FadeOutMilliseconds);

public sealed partial record GameState
{
    public MapMusicIntent? PendingMapMusicIntent { get; init; }
}
