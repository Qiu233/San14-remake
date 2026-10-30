using San14.Core;

namespace San14.Client.Gameplay;

public sealed partial class GameplaySession
{
    public EditedEventPlayback? PendingEditedEventPlayback => _simulation.PendingEditedEventPlayback;
    public bool TryBeginEditedEventPlayback(GameState expectedOwner, int slot,
        IEditedEventPlaybackRules rules, out string? unresolvedOwner,
        CampaignAppRandomState? appRandomSnapshot = null) =>
        _simulation.TryBeginEditedEventPlayback(expectedOwner, slot, rules, out unresolvedOwner, appRandomSnapshot);
    public bool TryPrepareEditedEventPlaybackStep(GameState expectedOwner,
        IEditedEventPlaybackRules rules, out CampaignStoryPreparationResult result) =>
        _simulation.TryPrepareEditedEventPlaybackStep(expectedOwner, rules, out result);
    public bool TryCompleteEditedEventPlaybackStep(GameState expectedOwner,
        EditedEventPlayback expectedProgress, CampaignStoryStepReceipt receipt) =>
        _simulation.TryCompleteEditedEventPlaybackStep(expectedOwner, expectedProgress, receipt);
    public bool TryFinishEditedEventPlayback(GameState expectedOwner,
        EditedEventPlayback expectedProgress, IEditedEventPlaybackRules rules, out string? unresolvedOwner) =>
        _simulation.TryFinishEditedEventPlayback(expectedOwner, expectedProgress, rules, out unresolvedOwner);
    public bool TryCompleteEditedEventSummary(GameState expectedOwner,
        EditedEventPlayback expectedProgress, out string? unresolvedOwner) =>
        _simulation.TryCompleteEditedEventSummary(expectedOwner, expectedProgress, out unresolvedOwner);

    public bool TryAdvanceEditedEventPostResult(GameState expectedOwner, EditedEventPlayback expectedProgress,
        IEditedEventPostResultRules rules, out string? unresolvedOwner) =>
        _simulation.TryAdvanceEditedEventPostResult(expectedOwner, expectedProgress, rules, out unresolvedOwner);

}
