using System.Collections.Immutable;

namespace San14.Core;

/// <summary>One source-selected spatial voice at the actual speaker pose.</summary>
public sealed record ArmyMapSpeechSpatialVoiceRequest(TalentNarrationVoiceRequest Voice,
    float SourceX, float SourceY, float SourceZ);

/// <summary>One actual queued Army speech. A non-null source text records
/// that the entry has started; elapsed time belongs to the presentation clock.</summary>
public sealed record ArmyMapSpeechEntry(long Sequence, int ArmyId, int OfficerId,
    int ActionCode, int HexId, int? SceneryId, byte DelayFrames,
    double ElapsedSeconds, string? TextAtSource)
{
    /// <summary>The source-time Base identity used by the speech line.
    /// Zero retains the source's valid null receiver.</summary>
    public int? ContextBaseLocationId { get; init; }
    public int? ContextTraitId { get; init; }
    public int? ContextAreaId { get; init; }
    public ImmutableArray<ArmyMapSpeechSpatialVoiceRequest>? SpatialVoices { get; init; }
    public bool SpatialVoicesAccepted { get; init; }
}

/// <summary>A save checkpoint for the map speech manager. The host advances
/// its small working copy without replacing the world on every display frame.</summary>
public sealed record ArmyMapSpeechState(long NextSequence, ImmutableArray<ArmyMapSpeechEntry> Entries);

public sealed record ArmyMapSpeechStartResult(GameState State, bool Started, string? UnresolvedOwner = null);

public interface IArmyMapSpeechRules
{
    ArmyMapSpeechStartResult Start(GameState state, long sequence, bool armyLayoutAvailable);
}

public sealed partial record GameState
{
    public ArmyMapSpeechState? ArmyMapSpeech { get; init; }
}
