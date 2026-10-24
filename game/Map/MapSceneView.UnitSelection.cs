using Godot;
using San14.Data;

public partial class MapSceneView
{
    private Func<int, bool, bool>? _unitSelectionClicked;
    private Func<Vector2, int, bool, bool, bool>? _unitSelectionPointer;
    private Action? _unitSelectionReset;
    private bool _unitSelectionChromePress;
    internal bool SourceSelectionShiftHeld => sourceCameraShiftHeld;

    internal void SetUnitSelectionPicker(Func<int, bool, bool>? clicked = null,
        Func<Vector2, int, bool, bool, bool>? pointer = null, Action? reset = null)
    {
        ResetUnitSelectionPointer();
        _unitSelectionClicked = clicked; _unitSelectionPointer = pointer; _unitSelectionReset = reset;
    }
    private bool TrySelectCommandArmy(int id) => _unitSelectionClicked?.Invoke(id, sourceCameraControlHeld) == true;
    private bool SampleUnitSelectionPointer(MainMapCameraCanonicalPoint point, int leftState)
    {
        // WindowInput observes levels even when a chrome control consumes the
        // event. Only a real map-surface press may arm this page's selector.
        if (_unitSelectionPointer is null) return false;
        if (leftState == 3) _unitSelectionChromePress = !_sourceCameraLeftUiPressed;
        if (_unitSelectionChromePress)
        {
            if (leftState is 0 or 1) _unitSelectionChromePress = false;
            return true;
        }
        return _unitSelectionPointer?.Invoke(new(point.X34, point.Y38), leftState,
            sourceCameraShiftHeld, sourceCameraControlHeld) == true;
    }
    private void ResetUnitSelectionPointer()
    { _unitSelectionChromePress = false; _unitSelectionReset?.Invoke(); }
}
