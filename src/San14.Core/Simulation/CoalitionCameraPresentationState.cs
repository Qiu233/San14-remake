namespace San14.Core;

/// <summary>The actual camera captured immediately before a coalition focus
/// was accepted. It remains owned until its matching restore is accepted.</summary>
public sealed record CoalitionCameraPresentationState(CampaignCameraSnapshot SavedCamera, int TargetHexId);

public sealed partial record GameState
{
    public CoalitionCameraPresentationState? CoalitionCameraPresentation { get; init; }
}

internal static class CoalitionCameraPresentationValidation
{
    internal static void Validate(GameState state)
    {
        if (state.CoalitionCameraPresentation is { } value &&
            (value.TargetHexId is < 0 or > 48400 || value.SavedCamera is null || !Camera(value.SavedCamera)))
            throw new InvalidDataException("Invalid admitted coalition camera presentation.");
    }

    internal static bool Camera(CampaignCameraSnapshot value) =>
        float.IsFinite(value.FocusX) && float.IsFinite(value.FocusY) && float.IsFinite(value.FocusZ) &&
        float.IsFinite(value.FocusW) && double.IsFinite(value.Distance) && value.Distance >= 0 &&
        double.IsFinite(value.Yaw) && double.IsFinite(value.Pitch);
}
