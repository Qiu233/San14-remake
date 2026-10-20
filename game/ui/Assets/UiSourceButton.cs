using Godot;

/// <summary>BaseButton input with the original five artwork states. Caption
/// styling is supplied by the source control, since text and sprite tables differ.</summary>
internal partial class UiSourceButton : UiSourceHitButton
{
    private Control[] _states = [];
    private Action<int>? _caption;
    private int _lastState = -1;
    private bool _selected;

    internal virtual bool Selected
    {
        get => _selected;
        set { if (_selected == value) return; _selected = value; QueueRedraw(); }
    }

    internal virtual int VisualState => Disabled ? (IsHovered() ? 4 : 3) : Selected ? 2 : GetDrawMode() switch
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
        _caption = caption;
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
        if (_states.Length == 0 || _lastState == VisualState) return;
        int state = VisualState;
        for (int i = 0; i < _states.Length; i++) _states[i].Visible = i == state;
        _caption?.Invoke(state);
        _lastState = state;
    }
}
