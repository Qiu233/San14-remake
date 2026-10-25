namespace San14.Core;

public enum CampaignTurnStopReason { OrdinaryForcesResolved, CalendarLimit, ChallengeService, TutorialForceDefeat }

/// <summary>A reached normal Turn stop. Null ending admission retains unknown
/// application-host state; it is neither false admission nor playback receipt.
/// The independent native terminal result, when present, owns victory/defeat.</summary>
public sealed record CampaignTurnStopRequest(CampaignTurnStopReason Reason,
    int? RemainingOrdinaryForceId, bool? EndingRequested);

public sealed partial record GameState
{
    public CampaignTurnStopRequest? CampaignTurnStop { get; init; }
}

internal static class CampaignTurnStopValidation
{
    internal static void Validate(GameState state)
    {
        if (state.CampaignTurnStop is not { } stop) return;
        if (!Enum.IsDefined(stop.Reason) || stop.RemainingOrdinaryForceId is < 0 or > 51 ||
            stop.Reason is (CampaignTurnStopReason.CalendarLimit or CampaignTurnStopReason.ChallengeService or CampaignTurnStopReason.TutorialForceDefeat) && stop.RemainingOrdinaryForceId is not null)
            throw new InvalidDataException("Saved normal Turn stop request is invalid.");
    }
}
