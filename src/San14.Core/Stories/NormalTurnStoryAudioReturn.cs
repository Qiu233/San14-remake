using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>The invocation's real requested music and special-mode return
/// values. It contains no native handle or fabricated playback position.</summary>
public sealed record NormalTurnStoryAudioReturn(int? RequestedMusicIndex, int SpecialMode)
{
    [JsonRequired] public CampaignCameraSnapshot? InvocationCamera { get; init; }

    internal void Validate()
    {
        if (RequestedMusicIndex is < 0 or > 116 || InvocationCamera is { } camera &&
            (!float.IsFinite(camera.FocusX) || !float.IsFinite(camera.FocusY) || !float.IsFinite(camera.FocusZ) ||
             !float.IsFinite(camera.FocusW) || !double.IsFinite(camera.Distance) || !double.IsFinite(camera.Yaw) ||
             !double.IsFinite(camera.Pitch)))
            throw new InvalidDataException("The normal Story audio return request is invalid.");
    }
}
