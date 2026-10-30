using Godot;

internal partial class StoryPresentationView
{
    private bool inputWait, inputWaitMouseArmed;
    private Vector2 inputWaitPointerDown;
    private int? inputWaitThreshold;
    private int inputWaitUpdates;
    internal Func<InputEvent, bool>? InputWaitKeyboard { get; set; }

    internal void BeginInputWait(Func<AutomaticMessageAdvanceMode> automaticAdvance, Action completed)
    {
        ResetInputWait(); inputWait = true; waitCompleted = completed;
        waitReady = null; sourceWaitUntil = null; waitRequiresDraw = false;
        inputWaitThreshold = AutomaticMessageAdvanceSource.ReadThreshold(text.SourceLocale, automaticAdvance());
    }

    private void ResetInputWait()
    {
        inputWait = false; inputWaitMouseArmed = false; inputWaitPointerDown = default;
        inputWaitThreshold = null; inputWaitUpdates = 0;
    }

    private void CompleteInputWait()
    {
        var complete = waitCompleted; waitCompleted = null; waitReady = null;
        ResetInputWait(); complete?.Invoke();
    }

    // 3F5030 includes the current configurable Cancel action13. It completes
    // only this wait; the existing speed/skip actions retain their own owner.
    internal bool RequestInputWaitAction(int action)
    {
        if (action != 13) return MessageModeRequested?.Invoke(action) ?? false;
        if (!inputWait || !InputAdmitted()) return false;
        CompleteInputWait(); return true;
    }

    private bool ProcessInputWait()
    {
        if (!inputWait) return false;
        if (!InputAdmitted() || waitCompleted is null) return true;
        if (ReadAccelerated?.Invoke() == true) { CompleteInputWait(); return true; }
        if (inputWaitThreshold is not int threshold || threshold == int.MaxValue) return true;
        inputWaitUpdates = unchecked(inputWaitUpdates + 1);
        if (StoryInputWaitPhysicalInput.AutomaticAdvanceHeld()) return true;
        if (inputWaitUpdates >= threshold) CompleteInputWait();
        return true;
    }

    private bool HandleInputWait(InputEvent input)
    {
        if (!inputWait || !InputAdmitted()) return false;
        if (InputWaitKeyboard?.Invoke(input) == true)
        { GetViewport().SetInputAsHandled(); return true; }
        if (StoryInputWaitPhysicalInput.IsDirectConfirm(input))
        {
            GetViewport().SetInputAsHandled(); CompleteInputWait(); return true;
        }
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
        {
            if (PlaybackControlOwnsPointer(mouse.Position)) { inputWaitMouseArmed = false; return false; }
            GetViewport().SetInputAsHandled();
            Vector2 point = canvas.GetGlobalTransformWithCanvas().AffineInverse() * mouse.Position;
            if (mouse.Pressed)
            {
                inputWaitMouseArmed = !GetViewport().GuiIsDragging(); inputWaitPointerDown = point;
            }
            else
            {
                bool confirm = inputWaitMouseArmed && !GetViewport().GuiIsDragging() &&
                    StoryInputWaitPhysicalInput.IsClick(inputWaitPointerDown, point);
                inputWaitMouseArmed = false;
                if (confirm) CompleteInputWait();
            }
            return true;
        }
        return false;
    }
}
