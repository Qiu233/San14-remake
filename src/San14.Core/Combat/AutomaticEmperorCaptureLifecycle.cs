namespace San14.Core;

public static class AutomaticEmperorCaptureLifecycle
{
    public static bool HasCaptureTail(GameState state) =>
        state.CityCaptureEmperorTail is not null || state.BaseWinnerEmperorTail is not null;

    public static EmperorCaptureDecisionRequest? PendingDecision(GameState state) =>
        state.CampaignEffectRequests is { PendingBatches.Length: 0 }
            ? state.AutomaticEmperorCapture?.PendingDecision : null;

    public static bool IsPresentationPending(GameState state) =>
        state.AutomaticEmperorCapture is { NeedsPresentationReceipt: true } progress &&
        (progress.AwaitingEffectBatchSequence is not long sequence ||
         state.CampaignEffectRequests?.PendingBatches.Any(batch => batch.Sequence == sequence) != false);

    public static bool CanContinue(GameState state) =>
        state.AutomaticEmperorCapture is { PendingDecision: null } && !IsPresentationPending(state);

    /// <summary>Only removal of the actual queued batch by its mature receipt
    /// consumers admits the next business segment.</summary>
    public static GameState ConsumeCompletedPresentation(GameState state)
    {
        if (state.AutomaticEmperorCapture is not { NeedsPresentationReceipt: true } progress) return state;
        if (IsPresentationPending(state))
            throw new InvalidOperationException("The emperor presentation still requires its actual receipts.");
        return state with { AutomaticEmperorCapture = progress with
        { NeedsPresentationReceipt = false, AwaitingEffectBatchSequence = null } };
    }
}
