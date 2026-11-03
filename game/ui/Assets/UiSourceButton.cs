using Godot;

/// <summary>BaseButton input with the original five artwork states. Caption
/// styling is supplied by the source control, since text and sprite tables differ.</summary>
internal partial class UiSourceButton : UiSourceHitButton
{
    private Control[] _states = [];
    private Action<int>? _caption;
    private bool _hasVisuals;
    private int _lastState = -1;
    private bool _selected;

    internal virtual UiAudioCue PressedCue => UiAudioCue.ButtonSelect;
    internal event Action<UiAudioCue>? FeedbackRequested;
    internal virtual bool SupportsDisabledFeedback => false;
    internal bool DisabledFeedbackAdmitted => SupportsDisabledFeedback && Disabled &&
        IsInsideTree() && IsVisibleInTree() && !IsQueuedForDeletion() && SourceInputAdmitted;
    internal Func<bool>? InputAdmission { get; set; }
    internal bool SourceInputAdmitted => CanProcess() && (InputAdmission?.Invoke() ?? true);

    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && base._HasPoint(point);

    // The source requests feedback before publishing the release to its caller.
    // Keep this publisher ahead of callbacks installed by Configure, including
    // when the audio observer detaches and later reattaches this control.
    public UiSourceButton() => Pressed += () => RequestFeedback();

    protected bool RequestFeedback()
    {
        if (!SourceInputAdmitted) return false;
        FeedbackRequested?.Invoke(PressedCue);
        return true;
    }

    // Only source-qualified common controls publish this pointer attempt.
    // It carries no Pressed event or business activation.
    protected void RequestDisabledFeedback()
    {
        if (DisabledFeedbackAdmitted) FeedbackRequested?.Invoke(UiAudioCue.Invalid);
    }

    internal virtual bool Selected
    {
        get => _selected;
        set { if (_selected == value) return; _selected = value; QueueRedraw(); }
    }

    // A check frame is independent of its button's pointer/disabled pose.
    // Selected ordinary buttons retain their existing pressed-pose behavior.
    internal virtual int VisualState => !Disabled && Selected ? 2 : InputVisualState;
    protected int InputVisualState => Disabled ? (IsHovered() ? 4 : 3) : GetDrawMode() switch
    {
        DrawMode.Pressed or DrawMode.HoverPressed => 2,
        DrawMode.Hover => 1,
        _ => 0
    };

    internal void ConfigureStates(Control[] states, Action<int>? caption = null)
    {
        if (states.Length != 5 || states.Any(layer => layer.GetParent() != this))
            throw new ArgumentException("Source button requires five owned artwork states.", nameof(states));
        _states = states;
        ConfigureVisuals(caption);
    }

    // Source artwork can update a retained part tree instead of owning five
    // parallel Control trees. Input, state calculation and caption timing stay
    // in this same button implementation.
    internal void ConfigureVisualState(Action<int> visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        _states = [];
        ConfigureVisuals(visual);
    }

    private void ConfigureVisuals(Action<int>? caption)
    {
        _caption = caption;
        _hasVisuals = true;
        IgnoreTextureSize = true;
        FocusMode = FocusModeEnum.None;
        _lastState = -1;
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
        ButtonDown += QueueRedraw;
        ButtonUp += QueueRedraw;
        ApplyState();
    }

    public override void _Draw() => ApplyState();

    private void ApplyState()
    {
        if (!_hasVisuals || _lastState == VisualState) return;
        int state = VisualState;
        for (int i = 0; i < _states.Length; i++) _states[i].Visible = i == state;
        _caption?.Invoke(state);
        _lastState = state;
    }
}
