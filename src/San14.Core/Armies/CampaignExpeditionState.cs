namespace San14.Core;

/// <summary>A started expedition to one province. Its sequence number and
/// year/month identify the source campaign, independent of its member armies.</summary>
public sealed record CampaignExpedition(int Id, int ProvinceId, int SequenceNumber,
    int StartYear, int StartMonth, bool IsWithdrawing);

/// <summary>Absent expeditions are free entries in a known controller. Null
/// province sequences retain unknown counters; a missing counter is not zero.</summary>
public sealed record CampaignExpeditionState(Dictionary<int, CampaignExpedition> Expeditions,
    Dictionary<int, int>? ProvinceSequences = null)
{
    public CampaignExpeditionState Copy() => this with
    {
        Expeditions = new(Expeditions),
        ProvinceSequences = ProvinceSequences is null ? null : new(ProvinceSequences)
    };
}

public sealed partial record GameState
{
    /// <summary>Null preserves old saves without the expedition controller.</summary>
    public CampaignExpeditionState? CampaignExpeditions { get; init; }
}
