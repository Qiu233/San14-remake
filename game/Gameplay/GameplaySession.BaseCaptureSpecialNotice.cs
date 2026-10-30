using San14.Core;

namespace San14.Client.Gameplay;

public sealed partial class GameplaySession
{
    public bool TryCompleteBaseCaptureSpecialNotice(GameState expectedOwner, long batchSequence, int requestIndex,
        CampaignAppRandomState appRandomSnapshot) =>
        _simulation.TryCompleteBaseCaptureSpecialNotice(expectedOwner, batchSequence, requestIndex, appRandomSnapshot);
}
