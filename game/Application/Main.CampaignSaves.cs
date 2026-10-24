using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private CampaignSaveView? _campaignSaveView;
    private DeploymentTargetConfirmation? _campaignSaveConfirmation;
    private GameplaySession? _campaignSaveSession;
    private GameState? _campaignSaveWorld;
    private MapSceneView? _campaignSaveMap;
    private bool HasCampaignSavePage => _campaignSaveView is not null;

    private void OpenCampaignSaves(bool saving)
    {
        if (_failed || _shuttingDown || HasCampaignSavePage || HasAdministrationPage || HasMilitaryOrderPage || HasCommandWorkspace ||
            _detailsView is not null || HasActionReport || HasPersonnelConfirmation || _repository is null ||
            saving && _session?.PlayerForceId is null) return;
        var session=_session;
        if(session is null&&(_titleMenu is null||saving))return;
        var presentation = new CampaignSavePresentation(_repository);
        IReadOnlyList<CampaignSaveSlot> entries = CampaignSaves.ReadSlots(presentation.Capture);
        if (!saving) entries = entries.Concat(CampaignSaves.ReadAutomaticSlots(presentation.Capture)).ToArray();
        foreach (var entry in entries)
            if (entry.Summary?.PresentationIssue is { } issue)
                GD.PushWarning($"Save slot {entry.Number} presentation: {issue}");
        var world = session?.World;
        var map = _residentMap;
        var view = new CampaignSaveView();
        _campaignSaveView = view; _campaignSaveSession = session; _campaignSaveWorld = world; _campaignSaveMap = map;
        AddChild(view);
        try
        {
            map?.CloseCityCommandMenu(); map?.SetDetailsOpen(true, keepMapVisible: true);
            SetMapFocus(_mapFocused);
            view.Configure(_repository, _ui, saving, entries, SelectedPlayableSaveSlot,
                key => ScenarioSaveTitle(key), CloseCampaignSaves,
                slot => RunUiAction(() => RequestCampaignFileAction(slot)));
        }
        catch { CloseCampaignSaves(); throw; }

        bool Current() => !_failed && !_shuttingDown && _campaignSaveView == view &&
            ReferenceEquals(_session, session) && ReferenceEquals(session?.World, world) && ReferenceEquals(_world, world);

        void RequestCampaignFileAction(int slot)
        {
            if (!Current() || _campaignSaveConfirmation is not null) return;
            // Inspect again at the operation boundary. A file may have changed
            // while this list was open; confirmation never relies on old labels.
            CampaignSaveSlot current = CampaignSaves.ReadSlot(slot);
            if (!saving && !current.CanLoad)
            { view.ApplyNotice(_ui.Get("save_slot_unreadable")); return; }
            if (!saving || current.Kind != CampaignSaveSlotKind.Empty)
            {
                view.SetBusy(true);
                var question = new DeploymentTargetConfirmation(); _campaignSaveConfirmation = question; AddChild(question);
                try
                {
                    question.ConfigureGameMessage(_repository!, _ui,
                        saving ? 0x272D : 0x272B, confirmed =>
                        {
                            if (!Current() || _campaignSaveConfirmation != question) return;
                            _campaignSaveConfirmation = null; question.Abandon(); question.QueueFree();
                            view.SetBusy(false);
                            if (confirmed) Execute(slot);
                        });
                    question.Name = "CampaignFileConfirmation";
                }
                catch
                { _campaignSaveConfirmation = null; question.Abandon(); question.QueueFree(); view.SetBusy(false); throw; }
            }
            else Execute(slot);
        }

        void Execute(int slot)
        {
            if (!Current()) return;
            view.SetBusy(true);
            try
            {
                if (saving) SavePlayable(slot); else LoadPlayable(slot);
                int index = _playSaveSlot.GetItemIndex(slot);
                if (index >= 0) _playSaveSlot.Select(index);
                RefreshSaveSlots();
                CloseCampaignSaves();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                if (Current()) { view.SetBusy(false); view.ApplyNotice(_ui.Get("save_file_failed")); }
                GD.PushWarning(error.ToString());
            }
        }
    }

    private string ScenarioSaveTitle(string key)
    {
        return _repository!.ScenarioDocuments.SingleOrDefault(entry=>
            San14.Data.GameResourceId.File(entry.SourcePath).ToString()==key)?.Title??_ui.Get("detail_unknown");
    }

    private void CloseCampaignSaves()
    {
        if (_campaignSaveView is null && _campaignSaveConfirmation is null && _campaignSaveMap is null) return;
        var map = _campaignSaveMap; _campaignSaveMap = null;
        _campaignSaveSession = null; _campaignSaveWorld = null;
        var question = _campaignSaveConfirmation; _campaignSaveConfirmation = null;
        if (question is not null) { question.Abandon(); question.QueueFree(); }
        var view = _campaignSaveView; _campaignSaveView = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        bool parent = ResumeMapSystemMenu();
        if (map is not null && GodotObject.IsInstanceValid(map) && _residentMap == map)
            map.SetDetailsOpen(parent, keepMapVisible: parent);
        if (!_failed && !_shuttingDown) SetMapFocus(_mapFocused);
        ResumeTitleMenu();
    }
}
