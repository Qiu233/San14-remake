namespace San14.Core;

public sealed record CampaignEventOccurrenceDate(ushort Year, byte Month, byte Day);

/// <summary>The campaign's original event history. An absent owner is unknown;
/// an absent date in a known
/// owner is the constructor's unset occurrence date.</summary>
public sealed record CampaignEventHistoryState(bool[] Occurred, bool[] Acknowledged,
    CampaignEventOccurrenceDate?[] OccurrenceDates, bool InitialStrategyProcessed);

/// <summary>Independent current manager decisions, reset by event resource
/// loading. These are not world occurrence flags.</summary>
public sealed record CampaignEventEligibilityState(bool[] Blocked)
{
    // The independent current script-service recording gate. Fresh resource
    // loading sets it false; a restored missing field is not the fresh state.
    public bool? HistoryRecordingSuppressed { get; init; }
    //Current manager result of the actual scan, never a per-frame rescan or legacy false default.
    public bool? HasAvailableResult { get; init; }
}

/// <summary>Current campaign facts used by events. The elapsed-period offset is
/// added to the existing calendar owner's period increments; it is not a second clock.</summary>
public sealed record CampaignEventWorldState(int ScenarioId, int ScenarioMode, int ElapsedPeriodOffset)
{
    // The current archive-owned signed control; old saves retain unknown rather than a guessed setting.
    public int? HistoricalEventControl { get; init; }
    public sbyte? CurrentUserIndex { get; init; }
    // Search encounters await their event scripts; these are not permanent history flags.
    public byte? SpecialPersonEncounterFlags { get; init; }
    // Campaign event accomplishments are independent of occurrence history
    // and of the application's cross-campaign achievement service.
    public uint? EventAchievementFlags { get; init; }
}

public sealed partial record GameState
{
    public CampaignEventHistoryState? CampaignEvents { get; init; }
    public CampaignEventEligibilityState? CampaignEventEligibility { get; init; }
    public CampaignEventWorldState? CampaignEventWorld { get; init; }
}
