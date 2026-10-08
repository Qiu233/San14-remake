namespace San14.Core;

/// <summary>Changes a live source Army commander's Home when its City falls.
/// The resulting Unit.SourceLocationId is a current affiliation projection,
/// not a new native field or a change to construction provenance.</summary>
public interface IPlayableCaptureArmyHomeRules
{
    /// <summary>The captured Base's current Person registry order, projected
    /// to active Army IDs. Null means that the reached order is unknown.</summary>
    int[]? ReadCapturedArmyHomeOrder(GameState state, int capturedLocationId,
        int previousForceId) => null;

    PlayableCaptureReturningPersonResolution? ChangeCapturedArmyHome(GameState state,
        int capturedLocationId, int previousForceId, UnitState army, CityState beforeCaptureCity);
}
