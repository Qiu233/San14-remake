using Godot;

/// <summary>Physical source inputs for EventView's blank input wait. The
/// logical bits8/9 come from Pad buttons2/3, mapped by the original XInput
/// table to A/B; they are independent of keyboard USB usage40 (Enter).</summary>
internal static class StoryInputWaitPhysicalInput
{
    internal static bool AutomaticAdvanceHeld() =>
        Input.IsMouseButtonPressed(MouseButton.Left) || Input.IsMouseButtonPressed(MouseButton.Right) ||
        (FirstMappedPad() is int pad &&
            (Input.IsJoyButtonPressed(pad, JoyButton.A) || Input.IsJoyButtonPressed(pad, JoyButton.B)));

    internal static bool IsDirectConfirm(InputEvent input) => input switch
    {
        // 3F4F90 reads the selected keyboard usage, on its new down edge.
        InputEventKey key => key.Pressed && !key.Echo && key.PhysicalKeycode == Key.Enter,
        InputEventMouseButton mouse => mouse.ButtonIndex == MouseButton.Right && !mouse.Pressed,
        InputEventJoypadButton button => !button.Pressed && button.Device == FirstMappedPad() &&
            button.ButtonIndex is JoyButton.A or JoyButton.B,
        _ => false
    };

    // 3F5450 uses canonical integer endpoints and accepts the boundary100.
    internal static bool IsClick(Vector2 down, Vector2 up)
    {
        int x = unchecked((int)up.X - (int)down.X), y = unchecked((int)up.Y - (int)down.Y);
        return unchecked(x * x + y * y) <= 100;
    }

    private static int? FirstMappedPad()
    {
        var pads = Input.GetConnectedJoypads();
        // The original consumes Pad0. Do not substitute a later device when
        // the first connected controller has no standardized button mapping.
        return pads.Count > 0 && Input.IsJoyKnown(pads[0]) ? pads[0] : null;
    }
}
