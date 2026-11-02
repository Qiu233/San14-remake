using Godot;
using San14.Data;

public partial class Main
{
    // This runs before Main's normal modal/busy routing. Only the actual
    // narration wait and the active period worker accept these early keys.
    private bool HandleGameplayEarlyKeyboard(InputEvent input)
    {
        if (HasCampaignHelp) return false;
        if (HandleGameplayNarrationKeyboard(input)) return true;
        if (!_advancingPeriod || input is not InputEventKey key || !key.Pressed || key.Echo ||
            _preferences is null || _failed || _shuttingDown || IsReturningToTitle ||
            HasEnvironmentSettings || HasCampaignSavePage || HasMapSystemMenu ||
            GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit) return false;
        int usage = KeyboardPhysicalInput.Hid(key.PhysicalKeycode);
        if (usage == 0) return false;
        EnsureGameplayKeyboardBindings();
        int encoded = usage | (key.ShiftPressed ? KeyboardBindingCatalog.ShiftModifier : 0) |
            (key.CtrlPressed ? KeyboardBindingCatalog.ControlModifier : 0);
        if (!gameplayKeyboardActions.TryGetValue(encoded, out int action)) return false;
        bool consumed = action == 12 ? TryTogglePeriodPlayback() :
            action is >= 14 and <= 16 && TrySetPeriodPlaybackSpeed(action - 14);
        if (consumed) gameplayKeyboardConsumedPresses.Add(usage);
        return consumed;
    }
}
