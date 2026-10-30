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
    /// <summary>The original action6 packet's fire-command cause. Null keeps
    /// unknown older knowledge; personality action28 uses ContextTraitId.</summary>
    public bool? FireCommandOrigin { get; init; }
    public int? ContextAreaId { get; init; }
    /// <summary>The duel packet's actual other participant and aftermath,
    /// independent of the speaker or a later unit/commander lookup.</summary>
    public int? ContextDuelOpponentOfficerId { get; init; }
    public CampaignDuelAftermath? ContextDuelAftermath { get; init; }
    /// <summary>The reached facility speech packet's stable Object receiver
    /// and original Army physical operand, independent of a later Unit lookup.</summary>
    public MapObjectReceiverId? ContextFacilityReceiverId { get; init; }
    public int? ContextFacilityArmySlot { get; init; }
    /// <summary>The source packet's definition operand for counter and
    /// facility-attack speech. Null preserves unknown older packets.</summary>
    public int? ContextFacilityDefinitionId { get; init; }
    /// <summary>The actual Base speaker, independent of an Army receiver and
    /// of the Base0 operand retained in the same source packet.</summary>
    public int? BaseSpeakerLocationId { get; init; }
    public int? BaseSpeakerObjectId { get; init; }
    /// <summary>The owned layout's source portrait and color. Both null retains
    /// its known absence for an invalid speaker. A higher-priority packet may
    /// inherit an existing layout, including when it changes the speaker kind.</summary>
    public ushort? BaseSpeakerAppearanceIndex { get; init; }
    public byte? BaseSpeakerForceColor { get; init; }
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
