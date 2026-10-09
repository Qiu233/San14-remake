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
        _playContinue.Disabled = !CampaignSaves.HasCampaign(SelectedPlayableSaveSlot);
    }

    private void SavePlayable()
    {
        if (_session?.PlayerForceId is null) return;
        CampaignSaves.SaveCampaign(_session!, SelectedPlayableSaveSlot);
        RefreshSaveSlots();
        _playMessage.Text = _ui.Get("play_saved");
    }

    private void LoadPlayable()
    {
        GameSessionSave restored = CampaignSaves.LoadCampaign(SelectedPlayableSaveSlot);
        int index = ScenarioBrowserViews.FindScenario(_shownScenarios, restored.State.ScenarioKey);
        if (index < 0) throw new InvalidDataException("The saved scenario is unavailable in the selected language.");
        ShowScenario(index, restored.State, restored.PlayerForceId);
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
