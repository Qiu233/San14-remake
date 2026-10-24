using Godot;
using San14.Client.Gameplay;

public partial class Main
{
    private void RefreshPlayControls()
    {
        CampaignCommandView view = CampaignCommandView.Capture(_world, _playerForceId, _selectedForceId, _ui);
        bool editableOptions = view.CanEditNewGameOptions;
        _chooseScenario.Disabled = !editableOptions;
        for (int index = 0; index < _scenarioList.ItemCount; index++)
            _scenarioList.SetItemDisabled(index, !editableOptions);
        _difficulty.Disabled = !editableOptions;
        _initialCredit.Disabled = !editableOptions;
        _capturedOfficers.Disabled = !editableOptions;
        bool strategy = view.CanIssueOrders;
        bool reporting = view.IsReporting;
        _saveInitial.Disabled = !editableOptions;
        _playStart.Disabled = !view.CanStart;
        _playSystemMenu.Disabled = _playerForceId is null||_advancingPeriod||IsReturningToTitle;
        _playCommands.Disabled = !view.CanIssueOrders || _advancingPeriod || IsReturningToTitle;
        _playSave.Disabled = view.Roster is null;
        RefreshSaveSlots();
        RefreshPlayStructureKinds();
        _playStructureKind.Disabled = !strategy;
        RefreshPlayGovernanceChoices();
        _playAdmin.Disabled = !strategy;
        RefreshPlayDeploymentAvailability();
        _playRecruiter.Disabled = !strategy || _playCity.ItemCount == 0;
        _playTrainer.Disabled = !strategy || _playCity.ItemCount == 0;
        RefreshPlayRetargetAvailability();
        RefreshPlayBuildAvailability();
        RefreshPlayOccupationAvailability();
        RefreshPlayWithdrawalAvailability();
        _playDisband.Disabled = !strategy;
        _playAdvance.Disabled = !strategy;
        _playFinishReport.Disabled = !reporting;
        _playCity.Disabled = !strategy;
        _playArea.Disabled = !strategy;
        _playAdminOfficer.Disabled = !strategy;
        _playOfficer.Disabled = !strategy;
        _playDutyOfficer.Disabled = !strategy;
        _playUnit.Disabled = !strategy;
        _playPrepared.Disabled = !strategy;
        _playCancelDeployment.Disabled = !strategy || _playPrepared.ItemCount == 0;
        _playFocus.Disabled = !strategy;
        _playTroops.Editable = strategy;
        _playTransportCity.Disabled = !strategy;
        _playTransportSupplies.Editable = strategy;
        _playTransportGold.Editable = strategy;
        _playTransportArrival.Disabled = !strategy;
        _playTransport.Disabled = !strategy || _playTransportCity.ItemCount == 0;
        _playMoveOfficer.Disabled = !strategy;
        _playMoveCity.Disabled = !strategy;
        _playSummonOfficer.Disabled = !strategy;
        _playMove.Disabled = !strategy || _playMoveOfficer.ItemCount == 0 ||
            _playMoveCity.ItemCount == 0;
        _playSummon.Disabled = !strategy || _playSummonOfficer.ItemCount == 0;
        _playTalentSearch.Disabled = !strategy || _playTalentExecutor.ItemCount == 0;
        _playTalentRecruit.Disabled = !strategy || _playTalentExecutor.ItemCount == 0 ||
            _playTalentTarget.ItemCount == 0;
        if (view.Roster is not { } roster)
        {
            _playCity.Clear();
            _playArea.Clear();
            _playAdminOfficer.Clear();
            _playOfficer.Clear();
            _playFormation.Clear();
            foreach (OptionButton tactic in _playTactics) tactic.Clear();
            _playDutyOfficer.Clear();
            _playDutyStatus.Text = "";
            _playUnit.Clear();
            _playPrepared.Clear();
            _playCancelDeployment.Disabled = true;
            _playTransportCity.Clear();
            _playTransport.Disabled = true;
            _playMoveOfficer.Clear();
            _playMoveCity.Clear();
            _playSummonOfficer.Clear();
            _playMoveStatus.Text = "";
            _playTalentExecutor.Clear();
            _playTalentTarget.Clear();
            _playTalentStatus.Text = "";
            _playTalentSearch.Disabled = true;
            _playTalentRecruit.Disabled = true;
            _playMove.Disabled = true;
            _playSummon.Disabled = true;
            _playStatus.Text = _ui.Get("play_not_started");
            _playReport.Text = "";
            ClosePlayablePeriodReport();
            RefreshExtendedCommands();
            return;
        }
        int oldCity = _playCity.Selected >= 0 ? _playCity.GetSelectedId() : -1;
        _playCity.Clear();
        foreach (NamedCommandChoice location in roster.Bases) _playCity.AddItem(location.Name, location.Id);
        int cityIndex = _playCity.GetItemIndex(oldCity);
        if (cityIndex < 0) cityIndex = _playCity.GetItemIndex(roster.DefaultBaseId);
        if (_playCity.ItemCount > 0) _playCity.Select(cityIndex < 0 ? 0 : cityIndex);
        RefreshPlayCityChoices();
        _playCancelDeployment.Disabled = !strategy || _playPrepared.ItemCount == 0;
        bool selectedCity = _playCity.Selected >= 0 && roster.CityIds.Contains(_playCity.GetSelectedId());
        _playAdmin.Disabled = !strategy || !selectedCity;
        _playTransport.Disabled = !strategy || !selectedCity || _playTransportCity.ItemCount == 0;
        _playMove.Disabled = !strategy || _playMoveOfficer.ItemCount == 0 ||
            _playMoveCity.ItemCount == 0;
        _playSummon.Disabled = !strategy || _playSummonOfficer.ItemCount == 0;
        _playMoveStatus.Text = roster.PendingMoves;
        _playTalentStatus.Text = roster.PendingTalent;
        _playTalentSearch.Disabled = !strategy || _playTalentExecutor.ItemCount == 0;
        _playTalentRecruit.Disabled = !strategy || _playTalentExecutor.ItemCount == 0 ||
            _playTalentTarget.ItemCount == 0;
        _playRecruiter.Disabled = !strategy || !selectedCity;
        _playTrainer.Disabled = !strategy || !selectedCity;
        _playStatus.Text = roster.Status;
        _playReport.Text = reporting ? RenderPlayReport(_world!, _playerForceId!.Value) : "";
        int oldUnit = _playUnit.Selected >= 0 ? _playUnit.GetSelectedId() : -1;
        _playUnit.Clear();
        foreach (NamedCommandChoice unit in roster.Units) _playUnit.AddItem(unit.Name, unit.Id);
        if (_playUnit.ItemCount > 0)
        {
            int unitIndex = _playUnit.GetItemIndex(oldUnit);
            _playUnit.Select(unitIndex < 0 ? 0 : unitIndex);
        }
        RefreshPlayRetargetAvailability();
        RefreshPlayBuildAvailability();
        RefreshPlayOccupationAvailability();
        RefreshPlayWithdrawalAvailability();
        RefreshPlayDisbandAvailability();
        RefreshExtendedCommands();
        RefreshPlayablePeriodReport();
        RefreshNativeTalentControls();
    }

}
