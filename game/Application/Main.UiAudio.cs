using Godot;

public partial class Main
{
    private Label _uiAudioStatus = null!;
    private UiAudioFeedback? _uiAudioFeedback;

    private void BeginUiAudio()
    {
        var feedback = new UiAudioFeedback(this, _repository!.SourceDirectory, _overrides.DirectoryPath,
            () => IsInsideTree() && !_failed);
        feedback.Changed += UiAudioFeedbackChanged;
        feedback.ServiceReady += AttachSceneAudioSession;
        _uiAudioFeedback = feedback;
        feedback.SetWindowFocused(_lifetimeWindow?.HasFocus() == true);
        feedback.SetVolumes(_preferences.Audio);
        if (_preferences.SpeakerLayout is { } layout) feedback.SetInitialSpeakerLayout(layout);
        feedback.Start();
    }

    private void UiAudioFeedbackChanged(UiAudioFeedbackStatus status)
    {
        if (status.Availability == UiAudioAvailability.Unavailable)
            SetMeta("ui_audio_unavailable_reason", status.Reason ?? "unknown");
        else
            RemoveMeta("ui_audio_unavailable_reason");
        if (status.Source is { } source)
        {
            SetMeta("ui_audio_source_uri", source.BankUri);
            SetMeta("ui_audio_source_sha256", source.BankHash);
            SetMeta("ui_audio_select_frames", source.SelectFrames);
            SetMeta("ui_audio_cancel_frames", source.CancelFrames);
            SetMeta("ui_audio_tab_frames", source.TabFrames);
        }
        if (status.SelectCount is int selectCount) SetMeta("ui_audio_select_count", selectCount);
        if (status.CancelCount is int cancelCount) SetMeta("ui_audio_cancel_count", cancelCount);
        if (status.TabCount is int tabCount) SetMeta("ui_audio_tab_count", tabCount);
        UpdateUiAudioStatus();
    }

    private void UpdateUiAudioStatus()
    {
        if (_uiAudioStatus is null || _failed || !GodotObject.IsInstanceValid(_uiAudioStatus)) return;
        UiAudioFeedbackStatus status = _uiAudioFeedback?.Status ?? new();
        string availability = status.Availability switch
        {
            UiAudioAvailability.Loading => "loading",
            UiAudioAvailability.Ready => "ready",
            UiAudioAvailability.Unavailable => "unavailable",
            _ => throw new InvalidOperationException("Unknown UI audio availability.")
        };
        _uiAudioStatus.Text = _ui.Get("ui_audio_" + availability);
        _uiAudioStatus.TooltipText = status.Reason ?? "";
        SetMeta("ui_audio_status", availability);
        UpdateSceneAudioStatus();
    }

    private void PlayUiAudio(bool cancel) => _uiAudioFeedback?.Play(cancel);

    public override void _ExitTree()
    {
        _shuttingDown = true;
        CancelNewGamePreparation(restoreParent: false);
        _periodAdvanceCancellation?.Cancel();
        CloseCommandWorkspace();
        CloseEnvironmentSettings(resumeParent:false);
        _displaySettingsHost?.Dispose(); _displaySettingsHost = null;
        CloseTitleMenu();
        CloseNewGameSetup();
        CloseScenarioSelection();
        _campaignApplication?.Detach();
        _campaignApplication = null;
        foreach (var application in _retiredCampaignApplications) application.Detach();
        _retiredCampaignApplications.Clear();
        _mapPresentation?.LeaveResidentMap();
        _type6Host?.Stop(deferRemoval: true);
        _cityDomesticPresenter?.Draft.Dispose();
        DisconnectWindowLifetime();
        _uiAudioFeedback?.CancelPreparation();
        LeaveSceneAudioContext();
        _sceneAudioBinding?.Dispose();
        _uiAudioFeedback?.Dispose();
    }
}
