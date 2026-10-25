using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private LocalCampaignSaves? _campaignSaves;
    private LocalCampaignSaves CampaignSaves => _campaignSaves ??= new(
        ProjectSettings.GlobalizePath("user://saves"), new ResourceSaveCodec(_repository!));

    private int SelectedPlayableSaveSlot => _playSaveSlot.GetSelectedId();

    private void RefreshSaveSlots()
    {
        for (int index = 0; index < _playSaveSlot.ItemCount; index++)
        {
            int slot = _playSaveSlot.GetItemId(index);
            string label = _ui.Format("play_save_slot_item", slot);
            if (CampaignSaves.HasCampaign(slot))
                label += " " + _ui.Get("play_save_slot_used");
            _playSaveSlot.SetItemText(index, label);
        }
        _playContinue.Disabled = !CampaignSaves.HasAnyCampaign;
    }

    private string? SaveAutomaticallyBeforeAdvance(GameplaySession source)
    {
        if (!_preferences.AutomaticSaving) return null;
        try
        {
            // Ordinary single-player requests enter here once, after the
            // existing Advance guards. Source16A8.bit8 skips this save.
            uint flags = OwnedArmyReturnServiceInputs.ReadCurrent(source.World)
                ?? throw new InvalidDataException("Automatic saving requires the current campaign mode flags.");
            if ((flags & 0x100) != 0) return null;
            var speech = CaptureArmySpeechCheckpoint(source);
            CampaignSaves.SaveAutomatic(source, new(_presentationRandom.State), speech.Snapshot, speech.Owner);
            _playContinue.Disabled = false;
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // The source reports a failed file operation, then continues the
            // same progress request. Keep the original atomic file intact.
            GD.PushWarning(error.ToString());
            return _ui.Get("save_file_failed");
        }
    }

    private void SavePlayable()
        => SavePlayable(SelectedPlayableSaveSlot);

    private void SavePlayable(int slot)
    {
        if (_session?.PlayerForceId is null) return;
        var speech = CaptureArmySpeechCheckpoint(_session);
        CampaignSaves.SaveCampaign(_session, slot, new(_presentationRandom.State), speech.Snapshot, speech.Owner);
        RefreshSaveSlots();
        _playMessage.Text = _ui.Get("play_saved");
    }

    private void LoadPlayable()
        => LoadPlayable(SelectedPlayableSaveSlot);

    private void LoadPlayable(int slot)
    {
        bool fromTitle=_titleMenu is not null;
        int entryLocale=_locale.Selected;
        try
        {
        _campaignApplication?.BeginLoad();
        GameSessionSave restored = CampaignSaves.LoadCampaign(slot);
        int index = ScenarioBrowserViews.FindScenario(_shownScenarios, restored.State.ScenarioKey);
        if(index<0&&fromTitle)
        {
            var selection=_repository!.EffectiveScenarios.SingleOrDefault(candidate=>
                GameResourceId.File(candidate.SourcePath).ToString()==restored.State.ScenarioKey);
            if(selection is not null)
            {
                _locale.Select(selection.Locale=="TC"?1:0);UpdateLocale();RefreshScenarios(loadPreview:false);
                index=ScenarioBrowserViews.FindScenario(_shownScenarios,restored.State.ScenarioKey);
            }
        }
        if (index < 0) throw new InvalidDataException("The saved scenario is unavailable in the selected language.");
        ShowScenario(index, restored.State, restored.PlayerForceId,
            restored.PendingProgress, restored.LastStorySequence, restored.PendingManualTactic, restored.PendingNormalPeriod);
        _scenarioList.Select(index);
        RefreshSceneAudioContext();
        int forceIndex = ScenarioBrowserViews.FindForce(_shownForces, restored.PlayerForceId);
        if (forceIndex >= 0)
        {
            _force.Select(forceIndex);
            ShowForce(forceIndex);
        }
        _playMessage.Text = _ui.Get("play_loaded");
        RefreshCurrentPlayWorld();
        CloseTitleMenu();
        if(fromTitle)
        { _browserContent.ProcessMode=ProcessModeEnum.Inherit;_browserContent.Show(); }
        }
        catch
        {
            if(fromTitle&&_session is null&&_locale.Selected!=entryLocale)
            { _locale.Select(entryLocale);UpdateLocale();RefreshScenarios(loadPreview:false); }
            throw;
        }
    }

    private void SaveInitialState()
    {
        if (_world is null) return;
        try
        {
            CampaignSaves.SavePreview(_session!);
            _worldStatus.Text = _ui.InitialStateSaved;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _worldStatus.Text = _safeUi?.StateFileError(error.Message) ?? error.Message;
            GD.PushWarning(error.ToString());
        }
    }

    private void LoadInitialState()
    {
        GameState restored;
        int index;
        try
        {
            restored = CampaignSaves.LoadPreview();
            index = ScenarioBrowserViews.FindScenario(_shownScenarios, restored.ScenarioKey);
            if (index < 0) throw new InvalidDataException("The saved scenario is unavailable in the selected language.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            InvalidDataException or System.Text.Json.JsonException)
        {
            _worldStatus.Text = _safeUi?.StateFileError(error.Message) ?? error.Message;
            GD.PushWarning(error.ToString());
            return;
        }
        ShowScenario(index, restored);
        _scenarioList.Select(index);
        _worldStatus.Text = _ui.InitialStateLoaded;
    }

}
