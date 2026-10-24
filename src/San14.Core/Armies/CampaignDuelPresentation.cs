namespace San14.Core;

/// <summary>Source-time27D900 presentation operands. A nullable Army pair
/// means no Army notification was requested. These are not playback receipts.</summary>
public sealed record CampaignDuelInjuryNotice(int OfficerId, byte BeforeHealthStateId,
    byte AfterHealthStateId, bool PlayerDistrictNotice, int? ArmyId, int? ArmyHexId);

/// <summary>37A710's unadmitted unit notification. App owns probability,
/// queue replacement, lifetime and playback, independently of the model RNG.</summary>
public sealed record CampaignDuelArmyNotice(int ArmyId, int CommanderId, int HexId,
    int OpponentId, CampaignDuelOutcome Outcome, CampaignDuelAftermath Aftermath);

public sealed record CampaignDuelAftermathPresentation(CampaignDuelInjuryNotice[] Injuries,
    CampaignDuelArmyNotice[] ArmyNotices)
{
    public CampaignDuelAftermathPresentation Copy() => this with
    { Injuries = Injuries.ToArray(), ArmyNotices = ArmyNotices.ToArray() };

    //Save/input validation only. Ordinary campaign advancement does not scan
    //the pending presentation queue to prove its own previous writes.
    internal void Validate(GameState state)
    {
        if (Injuries is null || ArmyNotices is null)
            throw new InvalidDataException("Duel aftermath presentation fields are missing.");
        foreach (var notice in Injuries)
            if (notice is null || !state.Officers.ContainsKey(notice.OfficerId) ||
                notice.BeforeHealthStateId > 3 || notice.AfterHealthStateId > 3 ||
                notice.ArmyId.HasValue != notice.ArmyHexId.HasValue ||
                notice.ArmyId is <= 0 || notice.ArmyHexId is < 0 or > 48400)
                throw new InvalidDataException("Duel injury presentation is invalid.");
        foreach (var notice in ArmyNotices)
            if (notice is null || notice.ArmyId <= 0 || !state.Officers.ContainsKey(notice.CommanderId) ||
                !state.Officers.ContainsKey(notice.OpponentId) || notice.HexId is < 0 or > 48400 ||
                notice.Outcome is not (CampaignDuelOutcome.Won or CampaignDuelOutcome.Draw) ||
                !Enum.IsDefined(notice.Aftermath))
                throw new InvalidDataException("Duel Army presentation is invalid.");
    }
}
