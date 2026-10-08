namespace San14.Core;

/// <summary>Completes independent native period owners at one fixed boundary:
/// after waiting updates, before provisional movement and all contacts.</summary>
public interface IPlayableNativePeriodContinuationRules
{
    GameState CompleteWaiting(GameState state);
}
