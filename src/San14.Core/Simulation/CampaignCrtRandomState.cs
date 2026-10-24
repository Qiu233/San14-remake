namespace San14.Core;

/// <summary>The campaign's independent CRT random stream, including its full
/// unsigned state. A missing snapshot does not imply a default seed.</summary>
public sealed record CampaignCrtRandomState(uint State);

public sealed partial record GameState
{
    public CampaignCrtRandomState? CampaignCrtRandom { get; init; }
}
