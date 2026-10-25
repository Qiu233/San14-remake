namespace San14.Core;

/// <summary>Unconsumed Base15/16 notification operands from1B4BD0.
/// The map speech owner must perform its real admission and playback.</summary>
public sealed record BaseAbnormalFeedbackRequest(int BaseLocationId, int OfficerId, int HexId, int Action);

public sealed record BaseAbnormalFeedbackPreparationResult(GameState State, bool Handled,
    string? UnresolvedOwner = null);

public interface IBaseAbnormalFeedbackRules
{
    BaseAbnormalFeedbackPreparationResult PrepareBaseAbnormalFeedback(GameState state,
        BaseAbnormalFeedbackRequest request);
}
