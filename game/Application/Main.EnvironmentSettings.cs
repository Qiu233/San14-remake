using Godot;
using San14.Data;

public partial class Main
{
    private ClientPreferences _preferences = null!;
    private EnvironmentSettingsView? _environmentSettings;
    private DeploymentTargetConfirmation? _environmentConfirmation;
    private DisplaySettingsHost? _displaySettingsHost;
    private DisplaySettingsHost? _environmentDisplayHost;
    private DisplayWindowSnapshot? _environmentDisplayEntry;
    private bool _environmentKeepDisplay, _environmentDisplayChanged;
    private MapSceneView? _environmentCgMap;
    private CgQuality? _environmentCgEntry;
    private bool _environmentCgChanged, _environmentKeepCg;
    private SceneTreeTimer? _environmentResolutionTimer;
    private Action? _environmentResolutionTimeout;
    private CancellationTokenSource? _environmentApplyCancellation;
    private bool HasEnvironmentSettings => _environmentSettings is not null;
    private int CurrentListScrollSpeed() => (int)_preferences.ListScroll;

    private void OpenEnvironmentSettings()
    {
        if (_failed || _shuttingDown || HasEnvironmentSettings || _repository is null) return;
        var title=_titleMenu;
        bool fromTitle=title is not null&&_playerForceId is null;
        var parent=_mapSystemMenu;
        var map=_mapSystemMap;
        var session=fromTitle?_session:_mapSystemSession;
        if(!fromTitle&&(parent is null||session is null||!ReferenceEquals(_session,session)||
            !ReferenceEquals(_mapSystemWorld,session.World)||!ReferenceEquals(_world,session.World)||_residentMap!=map))return;
        var world = session?.World;
        var entry = _preferences.Navigation;
        var audioEntry = _preferences.Audio;
        var listEntry = _preferences.ListScroll;
        bool automaticSavingEntry = _preferences.AutomaticSaving;
        var voiceEntry = _preferences.Voice;
        var automaticMessageAdvanceEntry = _preferences.AutomaticMessageAdvance;
        var keyboardEntry = _preferences.KeyboardBindings;
        var presentationEntry = _preferences.Presentation;
        var speakerBinding = _sceneAudioBinding;
        GameAudioSpeakerLayout? speakerEntry = speakerBinding?.CurrentSpeakerLayout;
        var cgQualities = map?.SupportedCgQualities ?? Enum.GetValues<CgQuality>();
        CgQuality? cgEntry = map is null ? _preferences.CgQuality ?? CgQuality.Normal
            : cgQualities.Count == 0 ? null : map.ReadCgQuality();
        _environmentCgMap = map; _environmentCgEntry = cgEntry;
        _environmentCgChanged = false; _environmentKeepCg = false;
        var displayHost = _displaySettingsHost ??= DisplaySettingsHost.Create(GetWindow());
        displayHost?.RefreshPublication();
        var displayEntry = displayHost?.Capture();
        _environmentDisplayHost = displayHost; _environmentDisplayEntry = displayEntry;
        _environmentKeepDisplay = false; _environmentDisplayChanged = false;
        var view = new EnvironmentSettingsView(); _environmentSettings = view;
        parent?.SetBusy(true); parent?.Hide();
        if(fromTitle)title!.Hide();
        AddChild(view);
        try
        {
            view.Configure(_repository, _ui, entry, audioEntry, listEntry, automaticSavingEntry,
                () => RunUiAction(RequestBack), () => RunUiAction(Apply), () => RunUiAction(RequestDefaults),
                audio => { if (Current()) _uiAudioFeedback?.SetVolumes(audio); },
                () => { if (Current()) _uiAudioFeedback?.PlayEffectsPreview(); }, voiceEntry, automaticMessageAdvanceEntry,
                keyboardEntry, presentationEntry, displayEntry?.Preferences, displayHost?.Resolutions,
                displayHost is null ? null : value => RunUiAction(() => PreviewDisplay(value)),
                cgEntry, cgQualities, quality =>
                {
                    if (!Current()) return false;
                    bool accepted = map?.ApplyCgQuality(quality) ?? true;
                    if (accepted) _environmentCgChanged = true;
                    return accepted;
                }, speakerEntry, fromTitle, layout => speakerBinding?.QuerySpeakerLayoutAvailability(layout)
                    ?? new SpeakerLayoutAvailability(false, "environment.speaker_layout.current_audio_owner"),
                mapBackdrop: !fromTitle);
            if (map?.CgQualityChangeUnavailableReason is { } cgReason) view.SetMeta("cg_quality_unavailable", cgReason);
        }
        catch { CloseEnvironmentSettings(); throw; }

        bool Current()
        {
            var expected = fromTitle ? world : _mapSystemWorld;
            return !_failed && !_shuttingDown && _environmentSettings == view &&
                (fromTitle?_titleMenu==title&&_playerForceId is null:_mapSystemMenu==parent&&_residentMap==map) &&
                ReferenceEquals(_session, session) && ReferenceEquals(session?.World, expected) && ReferenceEquals(_world, expected);
        }

        void RequestBack()
        {
            if (!Current() || _environmentConfirmation is not null) return;
            if (view.Draft == entry && view.AudioDraft == audioEntry && view.ListScrollDraft == listEntry &&
                view.AutomaticSavingDraft == automaticSavingEntry && view.VoiceDraft == voiceEntry &&
                view.AutomaticMessageAdvanceDraft == automaticMessageAdvanceEntry &&
                !view.KeyboardDraft.HasChanges(keyboardEntry) && view.PresentationDraft == presentationEntry &&
                view.DisplayDraft == displayEntry?.Preferences && view.CgQualityDraft == cgEntry &&
                view.SpeakerLayoutDraft == speakerEntry &&
                (!view.SpeakerLayoutEdited || _preferences.SpeakerLayout is null || view.SpeakerLayoutDraft == _preferences.SpeakerLayout)) CloseEnvironmentSettings();
            else Ask(0x275C, () => CloseEnvironmentSettings());
        }
        void RequestDefaults()
        {
            if (Current() && _environmentConfirmation is null)
                Ask(0x2760, () => { view.ApplyOptions(new()); view.ApplyAudioOptions(new());
                    view.ApplyPresentationOptions(new()); view.ApplyCgQualityOptions(CgQuality.Normal);
                    view.ApplySpeakerLayoutOptions(fromTitle ? GameAudioSpeakerLayout.Stereo : speakerEntry, fromTitle);
                    view.ApplyListScroll(ListScrollSpeed.Slow); view.ApplyAutomaticSaving(true); view.ApplyVoice(new());
                    view.ApplyAutomaticMessageAdvance(AutomaticMessageAdvanceMode.Manual); view.ResetKeyboardBindings();
                    if (displayHost is not null)
                    {
                        var mode = displayHost.Resolutions.Any(row => row.Windowed) ? GameWindowMode.Windowed : GameWindowMode.Fullscreen;
                        var first = displayHost.Resolutions.FirstOrDefault(row => row.Supports(mode));
                        if (first is not null) view.ApplyDisplayOptions(new(mode, first.Width, first.Height));
                    } });
        }
        async void Apply()
        {
            if (!Current() || _environmentConfirmation is not null || _environmentApplyCancellation is not null) return;
            view.SetBusy(true);
            var cancellation = new CancellationTokenSource(); _environmentApplyCancellation = cancellation;
            bool attemptedSpeakerChange = false;
            try
            {
                if (fromTitle && speakerBinding is not null && view.SpeakerLayoutDraft is { } layout && layout != speakerEntry)
                {
                    attemptedSpeakerChange = true;
                    await speakerBinding.ApplySpeakerLayoutAsync(layout, _ui.SourceLocale,
                        _preferences.MusicSelection, cancellation.Token);
                    if (!Current()) return;
                }
                _preferences.Save(view.Draft, view.AudioDraft, view.ListScrollDraft, view.AutomaticSavingDraft,
                    view.VoiceDraft, view.AutomaticMessageAdvanceDraft, keyboardBindings: view.KeyboardDraft.Export(),
                    presentation: view.PresentationDraft,
                    display: view.DisplayDraft == displayEntry?.Preferences ? _preferences.Display : view.DisplayDraft,
                    cgQuality: view.CgQualityDraft,
                    speakerLayout: view.SpeakerLayoutEdited ? view.SpeakerLayoutDraft : _preferences.SpeakerLayout);
                if (view.SpeakerLayoutEdited && view.SpeakerLayoutDraft is { } committedLayout)
                    _uiAudioFeedback?.SpeakerLayoutPreferenceCommitted(committedLayout);
                _campaignComposition?.ConfigurePersonnelNarrationVoice(_preferences.Voice.Capture());
                _campaignComposition?.ConfigureApplicationPresentationOptions(_preferences.Presentation);
                _sceneAudioBinding?.SetVoiceEnabled(_preferences.Voice.Enabled);
                map?.SetSourceNavigationOptions(_preferences.Navigation);
                map?.SetKeyboardBindings(_preferences.KeyboardBindings);
                _environmentKeepDisplay = true; _environmentKeepCg = true;
                CloseEnvironmentSettings();
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception error)
            {
                //An actual output switch precedes the atomic preference save.
                //A failed commit restores the real entry output and title BGM.
                if (attemptedSpeakerChange && Current() && speakerBinding is not null && speakerEntry is { } oldLayout &&
                    ReferenceEquals(_sceneAudioBinding, speakerBinding) && speakerBinding.CurrentSpeakerLayout != oldLayout)
                {
                    try { await speakerBinding.ApplySpeakerLayoutAsync(oldLayout, _ui.SourceLocale,
                        _preferences.MusicSelection, cancellation.Token); }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                    catch (Exception restoreError) { GD.PushWarning(restoreError.ToString()); }
                }
                if (Current()) { view.SetBusy(false); view.ApplyNotice(_ui.Get("settings_save_failed")); }
                GD.PushWarning(error.ToString());
            }
            finally
            {
                if (ReferenceEquals(_environmentApplyCancellation, cancellation)) _environmentApplyCancellation = null;
                cancellation.Dispose();
            }
        }
        void PreviewDisplay(DisplayPreferences value)
        {
            if (!Current() || _environmentConfirmation is not null || displayHost is null) return;
            var before = displayHost.Capture();
            try { displayHost.Apply(value); _environmentDisplayChanged = true; }
            catch
            {
                displayHost.Restore(before); view.ApplyDisplayOptions(before.Preferences, false); throw;
            }
            if (value.Extent == before.Size) return;
            view.SetBusy(true);
            var question = new DeploymentTargetConfirmation(); _environmentConfirmation = question; AddChild(question);
            try
            {
                var resolutionMessage = new EnvironmentResolutionMessage(_repository!, _ui.SourceLocale);
                question.ConfigureWrappedMessage(_repository!, _ui,
                    resolutionMessage.Format(15), accepted => RunUiAction(() =>
                    {
                        if (!Current() || _environmentConfirmation != question) return;
                        StopEnvironmentResolutionTimeout();
                        _environmentConfirmation = null; question.Abandon(); question.QueueFree();
                        if (!accepted)
                        {
                            displayHost.Restore(before);
                            view.ApplyDisplayOptions(before.Preferences, false);
                        }
                        view.SetBusy(false);
                    }));
                question.Name = "EnvironmentResolutionConfirmation";
                ulong deadline = Time.GetTicksMsec() + 15000;
                int shownSeconds = 15;
                void ScheduleResolutionTick()
                {
                    ulong scheduledAt = Time.GetTicksMsec();
                    ulong remaining = scheduledAt < deadline ? deadline - scheduledAt : 0;
                    _environmentResolutionTimer = GetTree().CreateTimer(Math.Min(1, remaining / 1000.0), true, false, true);
                    _environmentResolutionTimeout = () =>
                    {
                        StopEnvironmentResolutionTimeout();
                        if (!Current() || _environmentConfirmation != question) return;
                        ulong now = Time.GetTicksMsec();
                        int seconds = now < deadline ? (int)((deadline - now + 999) / 1000) : 0;
                        if (seconds == 0) { question.RequestCancel(); return; }
                        if (seconds != shownSeconds)
                        { question.ApplyWrappedMessage(resolutionMessage.Format(seconds)); shownSeconds = seconds; }
                        ScheduleResolutionTick();
                    };
                    _environmentResolutionTimer.Timeout += _environmentResolutionTimeout;
                }
                ScheduleResolutionTick();
            }
            catch
            {
                StopEnvironmentResolutionTimeout();
                _environmentConfirmation = null; question.Abandon(); question.QueueFree();
                displayHost.Restore(before); view.ApplyDisplayOptions(before.Preferences, false);
                view.SetBusy(false); throw;
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

    private void StopEnvironmentResolutionTimeout()
    {
        if (_environmentResolutionTimer is { } timer && _environmentResolutionTimeout is { } timeout)
            timer.Timeout -= timeout;
        _environmentResolutionTimer = null; _environmentResolutionTimeout = null;
    }

    private void ApplyCommittedCgQuality(MapSceneView map)
    {
        if (_preferences.CgQuality is { } quality && !map.ApplyCgQuality(quality))
            GD.PushWarning(map.CgQualityChangeUnavailableReason ?? "environment.CG_quality.initial_owner");
    }

    private void ApplySavedDisplayPreferences()
    {
        var host = _displaySettingsHost ??= DisplaySettingsHost.Create(GetWindow());
        if (_preferences.Display is not { } preferences || host is null) return;
        if (host.CanApply(preferences)) host.Apply(preferences);
        else GD.PushWarning("environment.display.saved_mode_not_available_on_current_monitor");
    }

    private void CloseEnvironmentSettings(bool resumeParent = true)
    {
        _environmentApplyCancellation?.Cancel(); _environmentApplyCancellation = null;
        StopEnvironmentResolutionTimeout();
        var question = _environmentConfirmation; _environmentConfirmation = null;
        if (question is not null) { question.Abandon(); question.QueueFree(); }
        var view = _environmentSettings; _environmentSettings = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        var displayHost = _environmentDisplayHost; var displayEntry = _environmentDisplayEntry;
        _environmentDisplayHost = null; _environmentDisplayEntry = null;
        if (_environmentDisplayChanged && !_environmentKeepDisplay && displayHost is not null && displayEntry is not null)
            displayHost.Restore(displayEntry);
        _environmentKeepDisplay = false; _environmentDisplayChanged = false;
        var cgMap = _environmentCgMap; var cgEntry = _environmentCgEntry;
        _environmentCgMap = null; _environmentCgEntry = null;
        if (_environmentCgChanged && !_environmentKeepCg && cgMap is not null && cgEntry is { } quality &&
            GodotObject.IsInstanceValid(cgMap) && ReferenceEquals(_residentMap, cgMap) && !cgMap.ApplyCgQuality(quality))
            GD.PushWarning(cgMap.CgQualityChangeUnavailableReason ?? "environment.CG_quality.restore_owner");
        _environmentCgChanged = false; _environmentKeepCg = false;
        // Edits preview through the shared mixer. Apply has already replaced
        // the committed preferences; every other close restores their entry mix.
        if (view is not null) _uiAudioFeedback?.SetVolumes(_preferences.Audio);
        if (resumeParent && _mapSystemMenu is { } parent && ResumeMapSystemMenu()) parent.SetBusy(false);
        if(resumeParent)ResumeTitleMenu();
    }
}
