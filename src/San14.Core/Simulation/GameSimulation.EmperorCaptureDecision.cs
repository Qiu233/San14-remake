namespace San14.Core;

public sealed partial class GameSimulation
{
    public EmperorCaptureDecisionRequest? PendingEmperorCaptureDecision =>
        AutomaticEmperorCaptureLifecycle.PendingDecision(EmperorCaptureOwner());

    /// <summary>The real Yes/No reply updates only this invocation's decision.
    /// The existing worker resumes the already completed capture prefix.</summary>
    public bool TryCompleteEmperorCaptureDecision(GameState expectedOwner,
        EmperorCaptureDecisionRequest expectedRequest, bool receive)
    {
        var owner = EmperorCaptureOwner();
        if (!ReferenceEquals(owner, expectedOwner) ||
            !ReferenceEquals(AutomaticEmperorCaptureLifecycle.PendingDecision(owner), expectedRequest) ||
            owner.AutomaticEmperorCapture is not
                { Phase: AutomaticEmperorCapturePhase.AwaitingChoice, SelectedMode: null,
                  NeedsPresentationReceipt: false, PendingDecision: { } request } progress ||
            !ReferenceEquals(request, expectedRequest)) return false;
        var next = owner with { AutomaticEmperorCapture = progress with
        {
            SelectedMode = receive ? 1 : 2, Phase = AutomaticEmperorCapturePhase.Opening,
            PendingDecision = null
        } };
        if (_normalPeriod is { } normal) _normalPeriod = normal with { Candidate = next };
        else if (_progress?.ManualTactic is { } manual)
            _progress = new(null, manual with { Candidate = next, ContinueAfterPresentation = true });
        else State = next;
        return true;
    }

    private GameState EmperorCaptureOwner() =>
        _normalPeriod?.Candidate ?? _progress?.ManualTactic?.Candidate ?? State;
}
