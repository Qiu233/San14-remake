using Godot;
using San14.Data;

public partial class Main
{
    /// <summary>EventView input is delivered before ordinary map/modal early
    /// returns. It changes the actual wait, never an effect-outbox receipt.</summary>
    private bool HandleGameplayNarrationKeyboard(InputEvent input)
    {
        if (input is not InputEventKey key) return false;
        int usage = KeyboardPhysicalInput.Hid(key.PhysicalKeycode);
        if (!key.Pressed && gameplayKeyboardConsumedPresses.Remove(usage)) return true;
        if (!key.Pressed || key.Echo || _preferences is null || _failed || _shuttingDown || _advancingPeriod ||
            IsReturningToTitle || HasEnvironmentSettings || HasCampaignSavePage || HasMapSystemMenu ||
            _detailsView is not null || GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit) return false;
        var view = new[] { _personnelPresentation, _forcePolicyAnnouncement, _governanceNarration,
            _transportNarration, _deploymentNarration, _campaignDeploymentNarration, _administrationNarration, _cityDomesticNarration, _cityMerchantNarration }
            .FirstOrDefault(candidate => candidate is not null && candidate.IsVisibleInTree() && !candidate.IsQueuedForDeletion());
        if (view is null) return false;
        int encoded = usage | (key.ShiftPressed ? KeyboardBindingCatalog.ShiftModifier : 0) |
            (key.CtrlPressed ? KeyboardBindingCatalog.ControlModifier : 0);
        var bindings = _preferences.KeyboardBindings;
        for (int action = 23; action <= 24; action++)
            for (int slot = 0; slot < 2; slot++)
            {
                int binding = bindings[action * 2 + slot];
                bool matches = binding == KeyboardBindingCatalog.ReservedBinding
                    ? key.PhysicalKeycode == Key.Ctrl && !key.ShiftPressed
                    : binding != 0 && binding == encoded;
                if (!matches || !view.RequestKeyboardMessageMode(action)) continue;
                gameplayKeyboardConsumedPresses.Add(usage);
                return true;
            }
        return false;
    }
}
