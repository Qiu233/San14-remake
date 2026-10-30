using Godot;

public partial class Main
{
    private void OpenEmperorCaptureDecision(RulerDecisionWorkspace owner,
        string questionText, Action<bool> submit)
    {
        // The ordinary capture uses the same CCmnYesNoState as 216200.
        // Its operands format the question; they do not add a speaker portrait.
        var question = new DeploymentTargetConfirmation();
        owner.View = question;
        owner.Abandon = question.Abandon;
        AddChild(question);
        question.ConfigureWrappedMessage(_repository!, _ui, questionText, receive => RunUiAction(() =>
        {
            if (!IsRulerDecisionCurrent || !ReferenceEquals(owner.View, question) ||
                _advancingPeriod || _failed || _shuttingDown) return;
            submit(receive);
        }));
        question.Name = "EmperorCaptureDecision";
        question.ZIndex = 96;
    }

    private bool HandleEmperorCaptureDecisionInput(InputEvent input)
    {
        if (!IsRulerDecisionCurrent || _rulerDecision?.View is not DeploymentTargetConfirmation question ||
            question.Name != "EmperorCaptureDecision" || _rulerDecision.Warning is not null) return false;
        // Skip/fast-forward and automatic narration never supply this answer.
        // A real Cancel binding or right-button release is an explicit No.
        if (HandleNarrationKeyboard(input, action =>
            {
                if (action != 13) return false;
                question.RequestCancel();
                return true;
            })) return true;
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false })
            return false;
        RunUiAction(question.RequestCancel);
        return true;
    }
}
