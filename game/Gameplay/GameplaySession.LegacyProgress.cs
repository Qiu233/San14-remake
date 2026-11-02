using San14.Core;

namespace San14.Client.Gameplay;

public sealed partial class GameplaySession
{
    /// <summary>Borrowed presentation owner for the existing saved story transaction.</summary>
    public GameState? PendingProgressCandidate => _simulation.PendingProgressCandidate;
    public CampaignStoryRequest? PendingLegacyStory => _simulation.PendingLegacyStory;
    public bool CanRetryLegacyProgress => _simulation.CanRetryLegacyProgress;
}
