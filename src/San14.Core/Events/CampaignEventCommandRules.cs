namespace San14.Core;

public sealed record CampaignEventCommandTransition(GameState? State, string? UnresolvedOwner);

/// <summary>Normal resource eligibility and the single invocation execution
/// belong to Data. Both methods receive one private candidate transaction.</summary>
public interface ICampaignEventCommandRules
{
    CampaignEventCommandTransition PrepareCampaignEventCommand(GameState candidate, int eventId, long sequence);
    CampaignEventCommandTransition ContinueCampaignEventCommand(GameState candidate, CampaignEventCommandPlayback playback);
}
