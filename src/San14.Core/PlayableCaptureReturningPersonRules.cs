namespace San14.Core;

public sealed record PlayableCaptureReturningPersonResolution(GameState State,
    int DestinationLocationId, string RuleId);

/// <summary>A captured Home can change a busy returning Person's first location
/// without destroying their current Return, second location or native status.</summary>
public interface IPlayableCaptureReturningPersonRules
{
    PlayableCaptureReturningPersonResolution? ChangeCapturedHome(GameState state,
        int capturedLocationId, int previousForceId, OfficerState officer);

    /// <summary>The source Home/Primary callbacks precede native City transfer.
    /// Its actual former owner/District must not be guessed from the Person.</summary>
    PlayableCaptureReturningPersonResolution? ChangeCapturedHome(GameState state,
        int capturedLocationId, int previousForceId, OfficerState officer, CityState beforeCaptureCity) =>
        ChangeCapturedHome(state, capturedLocationId, previousForceId, officer);
}
