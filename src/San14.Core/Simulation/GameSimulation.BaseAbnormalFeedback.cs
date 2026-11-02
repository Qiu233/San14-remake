namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Transfers the reached Base notification into the shared speech
    /// manager through its actual admission rules. A known admission refusal is
    /// handled; an unresolved source leaves both owners untouched.</summary>
    public bool TryPrepareBaseAbnormalFeedback(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, IBaseAbnormalFeedbackRules rules,
        out BaseAbnormalFeedbackPreparationResult? result, CampaignAppRandomState? appRandomSnapshot = null)
    {
        result = null;
        if (expectedSource != CampaignEffectSource.NormalCombatApplication ||
            !TryReadSourcePresentationHead(expectedOwner, expectedSource, batchSequence, requestIndex,
                out var owner, out var pending, out var effects, out int batch, out var request) ||
            request.BaseAbnormalFeedback is not { } feedback) return false;
        ArgumentNullException.ThrowIfNull(rules);
        var working = pending is null && appRandomSnapshot is not null
            ? owner with { CampaignAppRandom32 = appRandomSnapshot } : owner;
        result = rules.PrepareBaseAbnormalFeedback(working, feedback);
        if (!result.Handled || result.UnresolvedOwner is not null) return false;
        CompleteSourcePresentationHead(result.State, pending, effects, batch, requestIndex,
            result.State.ActiveMonthlyScene, result.State.CoalitionCameraPresentation);
        result = result with { State = SourcePresentationOwner };
        return true;
    }
}
