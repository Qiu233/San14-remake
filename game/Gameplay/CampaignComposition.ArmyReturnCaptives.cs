using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

public sealed partial class CampaignComposition
{
    public Task<CampaignPeriodAdvanceResult> ConfirmArmyReturnCaptiveBatchAsync(GameplaySession sourceSession,
        ConfirmArmyReturnCaptiveBatchOrder order, CancellationToken cancellationToken = default,
        CampaignAppRandomState? appRandomSnapshot = null, NarrationVoiceSettings? narrationVoiceSettings = null,
        Func<GameState, NativeArmyProgressStageMoment?, GameState>? periodStep = null,
        CampaignStoryBindings? storyBindings = null,
        Func<GameState, NativeArmyProgressStageMoment, GameState>? stageAdmission = null)
        => ReplyArmyReturnCaptiveAsync(sourceSession, order, cancellationToken, appRandomSnapshot,
            narrationVoiceSettings, periodStep, storyBindings, stageAdmission);

    public Task<CampaignPeriodAdvanceResult> ConfirmArmyReturnCaptiveExecutionAsync(GameplaySession sourceSession,
        ConfirmArmyReturnCaptiveExecutionOrder order, CancellationToken cancellationToken = default,
        CampaignAppRandomState? appRandomSnapshot = null, NarrationVoiceSettings? narrationVoiceSettings = null,
        Func<GameState, NativeArmyProgressStageMoment?, GameState>? periodStep = null,
        CampaignStoryBindings? storyBindings = null,
        Func<GameState, NativeArmyProgressStageMoment, GameState>? stageAdmission = null)
        => ReplyArmyReturnCaptiveAsync(sourceSession, order, cancellationToken, appRandomSnapshot,
            narrationVoiceSettings, periodStep, storyBindings, stageAdmission);

    public Task<CampaignPeriodAdvanceResult> CompleteArmyReturnCaptivePresentationAsync(GameplaySession sourceSession,
        CompleteArmyReturnCaptivePresentationOrder order, CancellationToken cancellationToken = default,
        CampaignAppRandomState? appRandomSnapshot = null, NarrationVoiceSettings? narrationVoiceSettings = null,
        Func<GameState, NativeArmyProgressStageMoment?, GameState>? periodStep = null,
        CampaignStoryBindings? storyBindings = null,
        Func<GameState, NativeArmyProgressStageMoment, GameState>? stageAdmission = null)
        => ReplyArmyReturnCaptiveAsync(sourceSession, order, cancellationToken, appRandomSnapshot,
            narrationVoiceSettings, periodStep, storyBindings, stageAdmission);
}
