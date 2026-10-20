namespace San14.Core;

public enum CampaignDuelHistoryKnowledge { Unknown, KnownEmpty }

/// <summary>One campaign's current duel-service knowledge. A missing owner or
/// gate value is unknown. This finite owner is not an active-duel continuation.</summary>
public sealed record CampaignDuelServiceState(bool? Active, bool? Busy,
    CampaignDuelHistoryKnowledge History)
{
    public static void Validate(GameState state)
    {
        if (state.CampaignDuelService is { } service && !Enum.IsDefined(service.History))
            throw new InvalidDataException("Campaign duel history knowledge is invalid.");
    }
}

public sealed partial record GameState
{
    public CampaignDuelServiceState? CampaignDuelService { get; init; }
    public byte? DuelOpportunityCount { get; init; }
}
