using San14.Core;

namespace San14.Client.Gameplay;

public sealed partial class GameplaySession
{
    public bool TryCompleteIntelligenceNarration(GameState expectedOwner, long batchSequence, int requestIndex,
        CampaignAppRandomState appRandomSnapshot) =>
        _simulation.TryCompleteIntelligenceNarration(expectedOwner, batchSequence, requestIndex, appRandomSnapshot);
}
