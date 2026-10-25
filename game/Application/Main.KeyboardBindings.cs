using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private IReadOnlyList<int>? gameplayKeyboardOwner;
    private readonly Dictionary<int, int> gameplayKeyboardActions = [];
    private readonly HashSet<int> gameplayKeyboardConsumedPresses = [];

    /// <summary>The restored gameplay shortcuts. UI text/capture owners retain
    /// their own events; command admission remains in the existing callbacks.</summary>
    private bool HandleGameplayKeyboard(InputEvent input)
    {
        if (input is not InputEventKey key) return false;
        int usage = KeyboardPhysicalInput.Hid(key.PhysicalKeycode);
        if (!key.Pressed && gameplayKeyboardConsumedPresses.Remove(usage)) return true;
        if (!key.Pressed || key.Echo || usage == 0 || _preferences is null || _failed || _shuttingDown ||
            _advancingPeriod || IsReturningToTitle || HasSelfRecommendation || HasCoalitionInvitation || _diplomacyNegotiation is not null ||
            _session is not { PlayerForceId: not null } || _world is null ||
            HasEnvironmentSettings || GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit) return false;

        EnsureGameplayKeyboardBindings();
        // Capture intentionally prefers Shift. Runtime matching must retain
        // both flags so an extra modifier cannot activate an unmodified key.
        int encoded = usage | (key.ShiftPressed ? KeyboardBindingCatalog.ShiftModifier : 0) |
            (key.CtrlPressed ? KeyboardBindingCatalog.ControlModifier : 0);
        if (!gameplayKeyboardActions.TryGetValue(encoded, out int command)) return false;
        bool consumed;
        if (command == 13) consumed = RequestGameplayKeyboardCancel();
        else if (command == 11 && HasMapSystemMenu && !HasCampaignSavePage && !HasScenarioSettings &&
            _mapSystemConfirmation is null)
        { RunUiAction(CloseMapSystemMenu); consumed = true; }
        else if (!GameplayMapKeyboardAdmitted()) consumed = false;
        else if (command == 11) { RunUiAction(OpenMapSystemMenu); consumed = true; }
        else if (command == 12 && _world.Phase == TurnPhase.Strategy)
        { RunPlayAction(AdvancePlayable); consumed = true; }
        else if (command == 30) { RunUiAction(FocusNextGameplayCity); consumed = true; }
        else if (command is >= 17 and <= 22) consumed = _residentMap?.ToggleInformationLayer(command) == true;
        else if (command is >= 25 and <= 29) consumed = TryFocusCommanderCard(command - 25);
        else consumed = false;
        if (consumed) gameplayKeyboardConsumedPresses.Add(usage);
        return consumed;
    }

    private void EnsureGameplayKeyboardBindings()
    {
        var bindings = _preferences!.KeyboardBindings;
        if (!ReferenceEquals(gameplayKeyboardOwner, bindings))
        {
            gameplayKeyboardActions.Clear();
            // Rebuild this small input lookup only when the immutable app
            // preference owner is replaced, never on map frames/publications.
            foreach (int bindingCommand in Enumerable.Range(11, 20))
                for (int slot = 0; slot < 2; slot++)
                    if (bindings[bindingCommand * 2 + slot] is > 0 and not KeyboardBindingCatalog.ReservedBinding)
                        gameplayKeyboardActions.TryAdd(bindings[bindingCommand * 2 + slot], bindingCommand);
            gameplayKeyboardOwner = bindings;
        }
    }

    private bool GameplayMapKeyboardAdmitted() =>
        _residentMap is { IsPlayableMapReady: true } map && map.IsVisibleInTree() &&
        !HasMilitaryOrderPage && !HasAdministrationPage && !HasCampaignSavePage && !HasMapSystemMenu &&
        !HasScenarioSettings && !HasCommandWorkspace && !HasActionReport && !HasPersonnelConfirmation &&
        _detailsView is null && _commandDetailsView is null && _reportMapReturn is null && _periodReportView is null;

    private bool RequestGameplayKeyboardCancel()
    {
        // Back never silently dismisses a spoken page or accepts a question.
        foreach (var speech in new[] { _transportNarration, _deploymentNarration, _administrationNarration,
            _cityDomesticNarration, _cityMerchantNarration, _governanceNarration })
            if (speech is not null && speech.IsVisibleInTree() && !speech.IsQueuedForDeletion()) return true;
        Action? back = null;
        if (_personnelConfirmation is { } personnel) back = personnel.RequestCancel;
        else if (_campaignSaveConfirmation is { } save) back = save.RequestCancel;
        else if (_mapSystemConfirmation is { } system) back = system.RequestCancel;
        else if (_unitOrderConfirmation is { } unitQuestion) back = unitQuestion.RequestCancel;
        else if (_governanceRosterConfirmation is { } governanceQuestion) back = governanceQuestion.RequestCancel;
        else if (HasCampaignSavePage) back = CloseCampaignSaves;
        else if (HasScenarioSettings) back = _scenarioSettingsView!.RequestBack;
        else if (HasMapSystemMenu) back = CloseMapSystemMenu;
        else if (_commandDetailsView is { } detail) back = detail.RequestClose;
        else if (_reportMapReturn is { } reportMap) back = reportMap.RequestBack;
        else if (_falseReportTarget is not null) back = RequestFalseReportBack;
        else if (_unitOrderTarget is not null) back = CancelUnitTarget;
        else if (HasGovernanceRosterPage) back = CloseGovernanceRoster;
        else if (HasPersonnelAdministrationPage) back = RequestPersonnelAdministrationBack;
        else if (HasOfficerOfficePage) back = RequestOfficerOfficesBack;
        else if (HasCampaignCommandPage) back = RequestCampaignCommandBack;
        else if (HasCommanderSelectionPage) back = CloseCommanderSelectionPage;
        else if (HasPersonnelItemPage) back = ClosePersonnelItems;
        else if (HasTalentOrdersPage) back = RequestTalentOrdersBack;
        else if (HasTalentAutoSearchPage) back = RequestTalentAutoSearchBack;
        else if (HasDiplomacyOrdersPage) back = RequestDiplomacyOrdersBack;
        else if (HasCityOfficerMovementPage) back = RequestCityOfficerMovementBack;
        else if (HasCityMerchantPage) back = RequestCityMerchantBack;
        else if (HasCommandWorkspace) back = CloseCommandWorkspace;
        else if (_cityDomesticPresenter is { } domestic)
            back = () => { if (_cityDomesticSelectors?.TryCancel() != true) domestic.RequestBack(); };
        else if (_administrationPresenter is { } administration) back = administration.RequestBack;
        else if (_transportTarget is { Visible: true }) back = CancelTransportTarget;
        else if (_transportSetup is { Visible: true } transport)
            back = () => { if (_transportSetupPresenter?.TryCancelModal() != true) transport.RequestBack(); };
        else if (_deploymentSetup is { } deployment && deployment.IsVisibleInTree() && !deployment.IsQueuedForDeletion())
            back = () => { if (_deploymentSetupPresenter?.TryCancelModal() != true) deployment.RequestClose(); };
        else if (_deploymentTarget is { } target && target.IsVisibleInTree() && !target.IsQueuedForDeletion())
            back = () => { if (_deploymentTargetPresenter?.TryCancelOverlay() != true) target.RequestBack(); };
        else if (_detailsView is { } details && details.IsVisibleInTree() && !details.IsQueuedForDeletion())
            back = details.RequestClose;
        else if (_actionReportView is { } actionReport && actionReport.IsVisibleInTree() && !actionReport.IsQueuedForDeletion())
            back = actionReport.RequestAcknowledge;
        else if (_periodReportView is { } report && report.IsVisibleInTree() && !report.IsQueuedForDeletion())
            back = report.RequestAcknowledge;
        else if (_residentMap is { IsCityCommandMenuOpen: true } map)
            back = () => { ClearDeploymentCityDraft(); map.CloseCityCommandMenu(); };
        else if (_sceneView is null && _residentMap is not null && _world is { Phase: TurnPhase.Reporting })
            back = () => RunPlayAction(FinishPlayableReport);
        else if (_sceneView is null && _residentMap is not null && !_mapFocused)
            back = () => SetMapFocus(true);
        if (back is null) return false;
        PlayUiAudio(cancel: true); RunUiAction(back); return true;
    }

    private void FocusNextGameplayCity()
    {
        if (_world is not { } world || _playerForceId is not int player || _residentMap is not { } map ||
            _playCity.ItemCount == 0) return;
        int first = _playCity.Selected;
        // Follow the same current owned-city list used by the real city picker.
        // A key press does not enumerate/capture a second world or create a city.
        for (int step = 1; step <= _playCity.ItemCount; step++)
        {
            int index = (first + step) % _playCity.ItemCount;
            int id = _playCity.GetItemId(index);
            if (!world.Cities.TryGetValue(id, out var city) || city.ForceId != player || city.HexId is not int hex) continue;
            if (!map.FocusReportHex(hex)) return;
            _playCity.Select(index); RefreshPlayControls();
            SelectMapTarget(hex); SetMapFocus(true); return;
        }
    }
}
