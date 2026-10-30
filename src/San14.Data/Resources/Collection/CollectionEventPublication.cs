using San14.Data.Formats;

namespace San14.Data;

public sealed record CollectionEventMarker(int NodeIndex, int SpriteId)
{
    public CollectionImageRoute? Image { get; init; }
    public string? UnresolvedOwner { get; init; }
}

public sealed record CollectionEventTab(int Id, string Caption);

public sealed record CollectionEventRow(int RowId, int EventId, int VariantIndex,
    int? DisplayNumber, string Name, bool? Unlocked)
{
    public int Kind { get; init; }
    public int TriggerKind { get; init; }
    public int RepeatMode { get; init; }
    public IReadOnlyList<CollectionEventMarker> Markers { get; init; } = [];
    public CollectionImageRoute? Thumbnail { get; init; }
    public string? UnresolvedOwner { get; init; }
    public bool CanSelect => true;
    public bool CanPlay => Unlocked == true;
}

/// <summary>One original Data-table part. The Collection content consumer
/// appends its stored text without printf substitution or message evaluation.</summary>
public sealed record CollectionEventText(int DataIndex, string Template, string? Text,
    string? UnresolvedOwner = null)
{
    public int DataSection { get; init; } = 3;
}

public sealed record CollectionEventDetail(int RowId, int EventId, string Name,
    CollectionEventText Summary, IReadOnlyList<CollectionEventText> ContentParts,
    IReadOnlyList<string> ContentLines, S14EventConditionProgram? ConditionProgram)
{
    public IReadOnlyList<string> UnresolvedOwners { get; init; } = [];
    public string? ConditionsUnresolvedOwner { get; init; }
    public IReadOnlyList<string> ConditionLines { get; init; } = [];
}

public abstract record CollectionEventWorldChange;
public sealed record CollectionEventTransferCity(int CityId, int ForceId) : CollectionEventWorldChange;
public sealed record CollectionEventReplaceRuler(int OfficerId, int ForceId) : CollectionEventWorldChange;
public sealed record CollectionEventSetKingdomName(int KingdomDefinitionId, int ForceId) : CollectionEventWorldChange;
public sealed record CollectionEventAttachOfficer(int OfficerId, int PatronOfficerId) : CollectionEventWorldChange;
public sealed record CollectionEventDate(int Year, int Month, int Day);

/// <summary>Original preparation data, not an initialized world or a playback receipt.
/// The host must load this actual scenario and apply the listed changes before executing Scripts.</summary>
public sealed record CollectionEventPlaybackPlan(int RowId, int EventId, int VariantFlag,
    int ScenarioId, string? ScenarioPath, int PlayerRulerId, CollectionEventDate? Date,
    IReadOnlyList<CollectionEventWorldChange> WorldChanges, IReadOnlyList<int> ScriptEventIds)
{
    public int ExecutionMode => 3;
    public int RestoreScenarioId => 8;
    public IReadOnlyList<string> UnresolvedOwners { get; init; } = [];
    public bool RequiresScenarioWorld => true;
}
