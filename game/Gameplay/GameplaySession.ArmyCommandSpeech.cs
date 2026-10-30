using San14.Core;

namespace San14.Client.Gameplay;

public sealed partial class GameplaySession
{
    public bool TryPrepareArmyCommandSpeech(GameState expectedOwner, CampaignEffectSource source,
        long batchSequence, int requestIndex, IArmyCommandSpeechRules rules,
        out ArmyCommandSpeechPreparationResult? result, CampaignAppRandomState? appRandomSnapshot = null) =>
        _simulation.TryPrepareArmyCommandSpeech(expectedOwner, source, batchSequence, requestIndex,
            rules, out result, appRandomSnapshot);
}
