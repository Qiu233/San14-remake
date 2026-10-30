using San14.Core;

namespace San14.Client.Gameplay;

public sealed partial class GameplaySession
{
    public EmperorCaptureDecisionRequest? PendingEmperorCaptureDecision =>
        _simulation.PendingEmperorCaptureDecision;

    public bool TryCompleteEmperorCaptureDecision(GameState expectedOwner,
        EmperorCaptureDecisionRequest expectedRequest, bool receive) =>
        PlayerForceId == expectedRequest.CapturingForceId &&
        _simulation.TryCompleteEmperorCaptureDecision(expectedOwner, expectedRequest, receive);
}
