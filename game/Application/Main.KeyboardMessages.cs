using Godot;
using San14.Data;

public partial class Main
{
    /// <summary>EventView input is delivered before ordinary map/modal early
    /// returns. It changes the actual wait, never an effect-outbox receipt.</summary>
    private bool HandleGameplayNarrationKeyboard(InputEvent input)
    {
        var view = new[] { _rulerDecision?.Warning, _storyPresentation?.View.Message, _duelCaptureAnnouncement?.View, _personnelPresentation, _forcePolicyAnnouncement, _governanceNarration,
            _transportNarration, _deploymentNarration, _campaignDeploymentNarration, _generalUnitTarget?.Narration, _administrationNarration, _cityDomesticNarration, _cityMerchantNarration }
            .FirstOrDefault(candidate => candidate is not null && candidate.IsVisibleInTree() && !candidate.IsQueuedForDeletion());
        return _duelCaptureAnnouncement is { } capture && ReferenceEquals(view, capture.View)
            ? HandleDuelCaptureNarrationKeyboard(input) : HandleNarrationKeyboard(input, view);
    }

    private bool HandleNarrationKeyboard(InputEvent input, DeploymentNarrationView? view) =>
        HandleNarrationKeyboard(input, view is null ? (Func<int, bool>?)null : view.RequestKeyboardMessageMode);

    private bool HandleNarrationKeyboard(InputEvent input, Func<int, bool>? requestMode) =>
        HandleNarrationKeyboard(input, requestMode, allowDuringPeriod: false);

    private bool HandleNarrationKeyboard(InputEvent input, Func<int, bool>? requestMode, bool allowDuringPeriod)
    {
        if (input is not InputEventKey key) return false;
        int usage = KeyboardPhysicalInput.Hid(key.PhysicalKeycode);
        if (!key.Pressed && gameplayKeyboardConsumedPresses.Remove(usage)) return true;
        if (!key.Pressed || key.Echo || _preferences is null || _failed || _shuttingDown || (_advancingPeriod && !allowDuringPeriod) ||
            IsReturningToTitle || HasEnvironmentSettings || HasCampaignSavePage || HasMapSystemMenu ||
            _detailsView is not null || GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit) return false;
        if (requestMode is null) return false;
        int encoded = usage | (key.ShiftPressed ? KeyboardBindingCatalog.ShiftModifier : 0) |
            (key.CtrlPressed ? KeyboardBindingCatalog.ControlModifier : 0);
        var bindings = _preferences.KeyboardBindings;
        foreach (int action in new[] { 13, 23, 24 })
            for (int slot = 0; slot < 2; slot++)
            {
                int binding = bindings[action * 2 + slot];
                bool matches = binding == KeyboardBindingCatalog.ReservedBinding
                    ? key.PhysicalKeycode == Key.Ctrl && !key.ShiftPressed
                    : binding != 0 && binding == encoded;
                if (!matches || !requestMode(action)) continue;
                gameplayKeyboardConsumedPresses.Add(usage);
                return true;
            }
        return false;
    }
}
