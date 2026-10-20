using Godot;

public partial class Main
{
    private ClientPreferences _preferences = null!;
    private EnvironmentSettingsView? _environmentSettings;
    private DeploymentTargetConfirmation? _environmentConfirmation;
    private bool HasEnvironmentSettings => _environmentSettings is not null;
    private int CurrentListScrollSpeed() => (int)_preferences.ListScroll;

    private void OpenEnvironmentSettings()
    {
        if (_failed || _shuttingDown || HasEnvironmentSettings || _repository is null ||
            _mapSystemMenu is not { } parent || _mapSystemMap is not { } map ||
            _mapSystemSession is not { } session || !ReferenceEquals(_session, session) ||
            !ReferenceEquals(_mapSystemWorld, session.World) || !ReferenceEquals(_world, session.World) || _residentMap != map) return;
        var world = session.World;
        var entry = _preferences.Navigation;
        var audioEntry = _preferences.Audio;
        var listEntry = _preferences.ListScroll;
        var view = new EnvironmentSettingsView(); _environmentSettings = view;
        parent.SetBusy(true); parent.Hide(); AddChild(view);
        try
        {
            view.Configure(_repository, _ui, entry, audioEntry, listEntry,
                () => RunUiAction(RequestBack), () => RunUiAction(Apply), () => RunUiAction(RequestDefaults),
                audio => { if (Current()) _uiAudioFeedback?.SetVolumes(audio); });
        }
        catch { CloseEnvironmentSettings(); throw; }

        bool Current() => !_failed && !_shuttingDown && _environmentSettings == view && _mapSystemMenu == parent &&
            _residentMap == map && ReferenceEquals(_session, session) && ReferenceEquals(session.World, world) &&
            ReferenceEquals(_world, world);

        void RequestBack()
        {
            if (!Current() || _environmentConfirmation is not null) return;
            if (view.Draft == entry && view.AudioDraft == audioEntry && view.ListScrollDraft == listEntry) CloseEnvironmentSettings();
            else Ask(0x275C, () => CloseEnvironmentSettings());
        }
        void RequestDefaults()
        {
            if (Current() && _environmentConfirmation is null)
                Ask(0x2760, () => { view.ApplyOptions(new()); view.ApplyAudioOptions(new()); view.ApplyListScroll(ListScrollSpeed.Slow); });
        }
        void Apply()
        {
            if (!Current() || _environmentConfirmation is not null) return;
            view.SetBusy(true);
            try
            {
                _preferences.Save(view.Draft, view.AudioDraft, view.ListScrollDraft);
                map.SetSourceNavigationOptions(_preferences.Navigation);
                CloseEnvironmentSettings();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                if (Current()) { view.SetBusy(false); view.ApplyNotice(_ui.Get("settings_save_failed")); }
                GD.PushWarning(error.ToString());
            }
        }
        void Ask(int message, Action yes)
        {
            view.SetBusy(true);
            var question = new DeploymentTargetConfirmation(); _environmentConfirmation = question; AddChild(question);
            try
            {
                question.ConfigureGameMessage(_repository!, _ui, message, confirmed => RunUiAction(() =>
                {
                    if (!Current() || _environmentConfirmation != question) return;
                    _environmentConfirmation = null; question.Abandon(); question.QueueFree();
                    view.SetBusy(false);
                    if (confirmed) yes();
                }));
                question.Name = "EnvironmentConfirmation";
            }
            catch
            { _environmentConfirmation = null; question.Abandon(); question.QueueFree(); view.SetBusy(false); throw; }
        }
    }

    private void CloseEnvironmentSettings(bool resumeParent = true)
    {
        var question = _environmentConfirmation; _environmentConfirmation = null;
        if (question is not null) { question.Abandon(); question.QueueFree(); }
        var view = _environmentSettings; _environmentSettings = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        // Edits preview through the shared mixer. Apply has already replaced
        // the committed preferences; every other close restores their entry mix.
        if (view is not null) _uiAudioFeedback?.SetVolumes(_preferences.Audio);
        if (resumeParent && _mapSystemMenu is { } parent && ResumeMapSystemMenu()) parent.SetBusy(false);
    }
}
