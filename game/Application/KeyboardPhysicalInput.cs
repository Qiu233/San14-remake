using Godot;
using San14.Data;

/// <summary>Godot physical keys to USB keyboard usages. Shared by the editor
/// and map input; character Unicode and Windows virtual-key codes are not used.</summary>
internal static class KeyboardPhysicalInput
{
    internal static bool TryEncode(InputEventKey input, out int code)
    {
        int usage = Hid(input.PhysicalKeycode);
        code = usage;
        if (usage == 0) return false;
        code |= input.ShiftPressed ? KeyboardBindingCatalog.ShiftModifier :
            input.CtrlPressed ? KeyboardBindingCatalog.ControlModifier : 0;
        return true;
    }

    internal static int Hid(Key physicalKey)
    {
        int key = (int)physicalKey;
        if (key >= (int)Key.A && key <= (int)Key.Z) return key - (int)Key.A + 4;
        if (key >= (int)Key.Key1 && key <= (int)Key.Key9) return key - (int)Key.Key1 + 0x1E;
        if (key >= (int)Key.F1 && key <= (int)Key.F12) return key - (int)Key.F1 + 0x3A;
        if (key >= (int)Key.F13 && key <= (int)Key.F24) return key - (int)Key.F13 + 0x68;
        if (key >= (int)Key.Kp1 && key <= (int)Key.Kp9) return key - (int)Key.Kp1 + 0x59;
        return physicalKey switch
        {
            Key.Key0 => 0x27, Key.Enter => 0x28, Key.Escape => 0x29,
            Key.Backspace => 0x2A, Key.Tab or Key.Backtab => 0x2B, Key.Space => 0x2C,
            Key.Minus => 0x2D, Key.Equal => 0x2E, Key.Bracketleft => 0x2F,
            Key.Bracketright => 0x30, Key.Backslash => 0x31, Key.Numbersign => 0x32, Key.Semicolon => 0x33,
            Key.Apostrophe => 0x34, Key.Quoteleft => 0x35, Key.Comma => 0x36,
            Key.Period => 0x37, Key.Slash => 0x38, Key.Capslock => 0x39,
            Key.Print => 0x46, Key.Scrolllock => 0x47, Key.Pause => 0x48,
            Key.Insert => 0x49, Key.Home => 0x4A, Key.Pageup => 0x4B,
            Key.Delete => 0x4C, Key.End => 0x4D, Key.Pagedown => 0x4E,
            Key.Right => 0x4F, Key.Left => 0x50, Key.Down => 0x51, Key.Up => 0x52,
            Key.Numlock => 0x53, Key.KpDivide => 0x54, Key.KpMultiply => 0x55,
            Key.KpSubtract => 0x56, Key.KpAdd => 0x57, Key.KpEnter => 0x58,
            Key.Kp0 => 0x62, Key.KpPeriod => 0x63, Key.Menu => 0x65, Key.Yen => 0x89,
            Key.Ctrl => 0xE0, Key.Shift => 0xE1, Key.Alt => 0xE2, Key.Meta => 0xE3,
            _ => 0
        };
    }
}
