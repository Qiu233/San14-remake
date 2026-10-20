namespace San14.Core;

public sealed record CampaignEventOccurrenceDate(ushort Year, byte Month, byte Day);

/// <summary>The campaign's original event history. An absent owner is unknown;
/// an absent date in a known
/// owner is the constructor's unset occurrence date.</summary>
public sealed record CampaignEventHistoryState(bool[] Occurred, bool[] Acknowledged,
    CampaignEventOccurrenceDate?[] OccurrenceDates, bool InitialStrategyProcessed);

/// <summary>Independent current manager decisions, reset by event resource
/// loading. These are not world occurrence flags.</summary>
public sealed record CampaignEventEligibilityState(bool[] Blocked);

/// <summary>Current campaign facts used by events. The elapsed-period offset is
/// added to the existing calendar owner's period increments; it is not a second clock.</summary>
public sealed record CampaignEventWorldState(int ScenarioId, int ScenarioMode, int ElapsedPeriodOffset)
{
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
