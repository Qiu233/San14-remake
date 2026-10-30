using San14.Core;

namespace San14.Client.Gameplay;

public sealed partial class GameplaySession
{
    public bool TryPrepareEndingStep(GameState expectedOwner, ICampaignEndingRules rules,
        out CampaignEndingProgress? prepared, bool fromEvent900 = false) =>
        _simulation.TryPrepareEndingStep(expectedOwner, rules, out prepared, fromEvent900);
    public bool TryPrepareEndingStep(GameState expectedOwner, ICampaignEndingRules rules,
        out CampaignEndingProgress? prepared, out string? unresolvedOwner, bool fromEvent900 = false) =>
        _simulation.TryPrepareEndingStep(expectedOwner, rules, out prepared, out unresolvedOwner, fromEvent900);
    public bool TryCompleteEndingPresentation(GameState owner, CampaignEndingProgress progress, long sequence,
        ICampaignEndingRules rules, out string? unresolvedOwner) =>
        _simulation.TryCompleteEndingPresentation(owner, progress, sequence, rules, out unresolvedOwner);
    public bool TryConfirmEndingAbdication(GameState owner, CampaignEndingProgress progress, long sequence,
        bool accepted, ICampaignEndingRules rules, out string? unresolvedOwner) =>
        _simulation.TryConfirmEndingAbdication(owner, progress, sequence, accepted, rules, out unresolvedOwner);
    public bool TryConfirmEndingKingdom(GameState owner, CampaignEndingProgress progress, long sequence,
        int kingdomId, ICampaignEndingRules rules, out string? unresolvedOwner) =>
        _simulation.TryConfirmEndingKingdom(owner, progress, sequence, kingdomId, rules, out unresolvedOwner);
    public bool TryCancelEndingKingdom(GameState owner, CampaignEndingProgress progress, long sequence,
        ICampaignEndingRules rules, out string? unresolvedOwner) =>
        _simulation.TryCancelEndingKingdom(owner, progress, sequence, rules, out unresolvedOwner);
    public bool TryPrepareEndingStoryStep(GameState owner, ICampaignStoryPreparationRules rules,
        out CampaignStoryPreparationResult result) => _simulation.TryPrepareEndingStoryStep(owner, rules, out result);
    public bool TryCompleteEndingStoryStep(GameState owner, CampaignEndingProgress progress, CampaignStoryStepReceipt receipt) =>
        _simulation.TryCompleteEndingStoryStep(owner, progress, receipt);
}
