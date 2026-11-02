using Godot;
using San14.Data;

public partial class Main
{
    private TitleEditMenuView? _titleEditMenu;
    private MusicEditorView? _musicEditor;
    private MusicSelectionView? _musicSelection;
    private DeploymentTargetConfirmation? _musicQuestion;
    private MusicEditCatalog? _musicCatalog;
    private MusicEditDraft? _musicDraft;
    private bool HasTitleEditMenu => _titleEditMenu is not null;
    private bool IsMusicEditorCurrent => !_failed && !_shuttingDown && _titleMenu is not null &&
        _titleEditMenu is not null && _playerForceId is null && _musicEditor is not null && _musicDraft is not null;

    private void OpenTitleEditMenu()
    {
        if (_failed || _shuttingDown || _titleMenu is not { } title || HasTitleEditMenu || HasCollectionMenu ||
            HasEnvironmentSettings || HasCampaignSavePage || _titleConfirmation is not null || _repository is null) return;
        var view = new TitleEditMenuView(); _titleEditMenu = view; AddChild(view); title.Hide();
        try
        {
            view.Configure(_repository, _ui,
                () => !_failed && !_shuttingDown && _titleEditMenu == view && _musicEditor is null &&
                    !HasRegisteredOfficers && !HasHistoricalOfficers && !HasItemEditors && !HasPersonalityEditors && !HasEventEditors,
                () => RunUiAction(() => CloseTitleEditMenu()), () => RunUiAction(OpenMusicEditor),
                () => RunUiAction(OpenRegisteredOfficers), () => RunUiAction(OpenHistoricalOfficers),
                () => RunUiAction(OpenHistoricalItemsEditor), () => RunUiAction(OpenNewItemsEditor),
                () => RunUiAction(OpenPersonalityEditors), () => RunUiAction(OpenEventEditors),
                resources: TryBorrowApplicationUiResources(view));
        }
        catch { CloseTitleEditMenu(); throw; }
    }

    private void OpenMusicEditor()
    {
        if (_repository is null || _musicEditor is not null || _titleEditMenu is not { } parent || _titleMenu is null) return;
        var catalog = MusicEditCatalog.Load(_repository, _ui.SourceLocale);
        var draft = _preferences.MusicSelection.BeginEdit(catalog);
        _musicCatalog = catalog; _musicDraft = draft;
        var view = new MusicEditorView(); _musicEditor = view; AddChild(view); parent.SetBusy(true); parent.Hide();
        try
        {
            _sceneAudioBinding?.BeginMusicEdit();
            var messages = new AdministrationMessages(_repository, _ui.SourceLocale);
            view.Configure(_repository, _ui, catalog.Groups.Select(group => group.Name).ToArray(),
                group => catalog.Rows.Where(row => row.Group == group).OrderBy(row => row.Row)
                    .Select(row => new MusicEditorRow(row.SourceSlot, row.ContextName,
                        row.SourceSlot == 28 ? "" : catalog.Name(draft.Selection(row.SourceSlot)),
                        row.SourceSlot == 28 ? "" : messages.Format(row.HelpMessageId), row.Row)).ToArray(),
                () => IsMusicEditorCurrent && _musicEditor == view && _musicSelection is null && _musicQuestion is null,
                () => RunUiAction(RequestMusicEditorReturn), () => RunUiAction(ApplyMusicEditor),
                () => RunUiAction(() => AskMusicQuestion(0x2867, () => { draft.InitializeDefaults(); view.Refresh(); })),
                slot => RunUiAction(() => OpenMusicSelection(slot)), CurrentListScrollSpeed, () => draft.CanInitializeDefaults, resources: TryBorrowApplicationUiResources(view));
        }
        catch { CloseMusicEditor(); throw; }
    }

    private void OpenMusicSelection(int slot)
    {
        if (!IsMusicEditorCurrent || _musicSelection is not null || _musicQuestion is not null || _repository is null) return;
        var draft = _musicDraft!; var catalog = _musicCatalog!; var parent = _musicEditor!;
        var view = new MusicSelectionView(); _musicSelection = view; AddChild(view); parent.SetBusy(true); parent.Hide();
        try
        {
            view.Configure(_repository, _ui, catalog.Candidates.Select(row => new MusicSelectionRow(row.Index, row.Name, row.Number)).ToArray(),
                draft.Selection(slot), () => IsMusicEditorCurrent && _musicSelection == view && _musicQuestion is null,
                chosen => RunUiAction(() =>
                {
                    if (!IsMusicEditorCurrent || _musicSelection != view || _musicDraft != draft) return;
                    _sceneAudioBinding?.StopMusicPreview();
                    _musicSelection = null; view.Abandon(); view.QueueFree();
                    if (chosen is int index) draft.Select(slot, index);
                    parent.Show(); parent.SetBusy(false); parent.Refresh();
                }), index => RunUiAction(() => PreviewMusic(slot, index)), CurrentListScrollSpeed,
                resources: TryBorrowApplicationUiResources(view));
        }
        catch
        {
            _musicSelection = null; view.Abandon(); view.QueueFree(); parent.Show(); parent.SetBusy(false); throw;
        }
    }

    private void PreviewMusic(int slot, int? index)
    {
        if (!IsMusicEditorCurrent || _musicSelection is null) return;
        if (index is int music) _sceneAudioBinding?.PreviewMusic(_preferences.MusicSelection.Preview(_ui.SourceLocale, slot, music));
        else _sceneAudioBinding?.StopMusicPreview();
    }

    private void ApplyMusicEditor()
    {
        if (!IsMusicEditorCurrent || _musicQuestion is not null || _musicSelection is not null) return;
        var view = _musicEditor!; view.SetBusy(true);
        try { _preferences.CommitMusic(_musicDraft!.Commit()); CloseMusicEditor(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            view.SetBusy(false); view.ApplyNotice(_ui.Get("settings_save_failed")); GD.PushWarning(error.ToString());
        }
    }

    private void RequestMusicEditorReturn()
    {
        if (!IsMusicEditorCurrent || _musicQuestion is not null || _musicSelection is not null) return;
        if (_musicDraft!.HasChanges) AskMusicQuestion(0x2866, CloseMusicEditor); else CloseMusicEditor();
    }

    private void AskMusicQuestion(int message, Action yes)
    {
        if (!IsMusicEditorCurrent || _musicQuestion is not null || _musicSelection is not null || _repository is null) return;
        var view = _musicEditor!; view.SetBusy(true);
        var question = new DeploymentTargetConfirmation(); _musicQuestion = question; AddChild(question);
        try
        {
            question.ConfigureGameMessage(_repository, _ui, message, confirmed => RunUiAction(() =>
            {
                if (!IsMusicEditorCurrent || _musicQuestion != question || _musicEditor != view) return;
                _musicQuestion = null; question.Abandon(); question.QueueFree(); view.SetBusy(false);
                if (confirmed) yes();
            }));
            question.Name = "MusicEditorConfirmation";
        }
        catch { _musicQuestion = null; question.Abandon(); question.QueueFree(); view.SetBusy(false); throw; }
    }

    private void RequestTitleEditBack()
    {
        if (HasRegisteredOfficers) RequestRegisteredOfficersBack();
        else if (HasHistoricalOfficers) RequestHistoricalOfficersBack();
        else if (HasItemEditors) RequestItemEditorsBack();
        else if (HasPersonalityEditors) RequestPersonalityEditorsBack();
        else if (HasEventEditors) RequestEventEditorsBack();
        else if (_musicQuestion is { } question) question.RequestCancel();
        else if (_musicSelection is { } selection) selection.RequestBack();
        else if (_musicEditor is not null) RequestMusicEditorReturn();
        else _titleEditMenu?.RequestBack();
    }

    private void CloseMusicEditor()
    {
        var question = _musicQuestion; _musicQuestion = null;
        var selection = _musicSelection; _musicSelection = null;
        var editor = _musicEditor; _musicEditor = null;
        _musicCatalog = null; _musicDraft = null;
        if (question is not null) { question.Abandon(); question.QueueFree(); }
        if (selection is not null) { selection.Abandon(); selection.QueueFree(); }
        if (editor is not null) { editor.Abandon(); editor.QueueFree(); _sceneAudioBinding?.EndMusicEdit(!_failed && !_shuttingDown); }
        if (!_failed && !_shuttingDown && _titleEditMenu is { } parent) { parent.SetBusy(false); parent.Show(); }
    }

    private void CloseTitleEditMenu(bool resumeTitle = true)
    {
        CloseRegisteredOfficers(resumeParent: false);
        CloseHistoricalOfficers(resumeParent: false);
        CloseItemEditors(resumeParent: false);
        ClosePersonalityEditors(resumeParent: false);
        CloseEventEditors(resumeParent: false);
        CloseMusicEditor();
        var view = _titleEditMenu; _titleEditMenu = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (resumeTitle) ResumeTitleMenu();
    }
}
