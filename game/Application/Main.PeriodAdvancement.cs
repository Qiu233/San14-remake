using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private bool _advancingPeriod;
    private Task? _periodAdvanceTask;
    private CancellationTokenSource? _periodAdvanceCancellation;
    private Control? _periodAdvanceOverlay;
    private CampaignPeriodPlayback? _periodPlayback;
    private MapSceneView? _periodPlaybackMap;
    private int _periodPlaybackSpeed = 1;
    private Label? _periodPlaybackCaption;
    private ProgressBar? _periodPlaybackProgress;
    private Button? _periodPlaybackPause;
    private readonly List<Button> _periodPlaybackSpeeds = [];

    private bool TryTogglePeriodPlayback()
    {
        if (!_advancingPeriod || _periodPlayback is not {} playback || _shuttingDown) return false;
        playback.TogglePause();
        if (GodotObject.IsInstanceValid(_periodPlaybackMap))
            _periodPlaybackMap!.SetPeriodPlaybackPaused(playback, playback.IsPaused);
        RefreshPeriodPlaybackControls(); return true;
    }

    private bool TrySetPeriodPlaybackSpeed(int speedIndex)
    {
        if (!_advancingPeriod || _periodPlayback is not {} playback || _shuttingDown || (uint)speedIndex > 2) return false;
        _periodPlaybackSpeed = speedIndex;
        playback.SetSpeed(speedIndex); RefreshPeriodPlaybackControls(); return true;
    }

    private void RefreshPeriodPlaybackControls()
    {
        if (_periodPlaybackPause is {} pause && _periodPlayback is {} playback)
        {
            pause.Text = playback.IsPaused ? "\u25B6" : "\u2161";
            pause.TooltipText = _ui.Get(playback.IsPaused ? "play_period_resume" : "play_period_pause");
            for (int i = 0; i < _periodPlaybackSpeeds.Count; i++)
                _periodPlaybackSpeeds[i].ButtonPressed = i == playback.Speed;
        }
    }

    private void PublishPeriodPlayback(CampaignPeriodPlayback owner, MapSceneView? map, GameState candidate)
    {
        if (!ReferenceEquals(_periodPlayback, owner) || _shuttingDown || _failed || !IsInsideTree()) return;
        if (GodotObject.IsInstanceValid(map)) map!.PublishPeriodPlayback(owner, candidate);
        if (_periodPlaybackProgress is {} progress && candidate.NativeClock is {} clock)
            progress.Value = clock.UpdatesIntoPeriod;
        if (_periodPlaybackCaption is {} caption && candidate.NativeMapSeason is {} season)
        {
            var date = season.ThirdDate;
            caption.Text = _ui.Get("play_advancing") + $"  {date.Year}/{date.Month:D2}/{date.Day:D2} {date.Hour:D2}:00";
        }
    }

    private async Task AdvancePlayableAsync(GameplaySession source, CampaignComposition composition,
        string? automaticSaveNotice = null, ConfirmDiplomaticNegotiationOrder? diplomaticChoice = null)
    {
        var cancellation = new CancellationTokenSource();
        _periodAdvanceCancellation = cancellation;
        MapSceneView? playbackMap = _residentMap;
        CampaignPeriodPlayback? playback = null;
        Control? overlay = null;
        GameplaySession? adopted = null;
        try
        {
            playback = new(SynchronizationContext.Current ?? throw new InvalidOperationException(
                "Period playback requires the host UI synchronization context."),
                candidate => PublishPeriodPlayback(playback!, playbackMap, candidate),
                () =>
                {
                    if (GodotObject.IsInstanceValid(playbackMap))
                        playbackMap!.ReleasePeriodPlaybackCandidate(playback!);
                }, cancellation.Token);
            _periodPlayback = playback; _periodPlaybackMap = playbackMap;
            playback.SetSpeed(_periodPlaybackSpeed);
            overlay = ShowPeriodAdvanceProgress(playbackMap, playback);
            var voice = _preferences.Voice.Capture();
            var result = diplomaticChoice is null
                ? await composition.AdvancePeriodAsync(source, cancellation.Token, new(_presentationRandom.State), voice, playback.Checkpoint)
                : await composition.ConfirmDiplomaticNegotiationAsync(source, diplomaticChoice, cancellation.Token,
                    new(_presentationRandom.State), voice, playback.Checkpoint);
            if (_failed || _shuttingDown || !GodotObject.IsInstanceValid(this) || !IsInsideTree() ||
                !result.IsCurrent(_session)) return;

            _session = result.Session;
            SynchronizeApplicationCrtRandom(result.Session.PendingNormalPeriod?.Candidate ?? result.Session.World);
            if ((result.Session.PendingNormalPeriod?.Candidate??result.Session.World).CampaignAppRandom32 is { } appRandom)
                _presentationRandom.InstallCommittedState(appRandom.State);
            adopted = result.Session;
            _sceneAudioBinding?.AdoptPlayableSession(source, result.Session);
            _campaignComposition = result.Composition;
            _loadedMap = result.Composition.Map;
            _map = _loadedMap.Map;
            _mapOverview = result.Composition.Overview;
            PublishCampaignPresentation(result.Presentation);
            _playMessage.Text = "";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!_shuttingDown && GodotObject.IsInstanceValid(this) && IsInsideTree() && ReferenceEquals(_session, source))
            {
                if (diplomaticChoice is not null) _diplomacyNegotiationNotice = _ui.Get("play_diplomacy_offer_failed");
                ReportPlayFailure(error);
            }
        }
        finally
        {
            bool ownsAdvance = ReferenceEquals(_periodAdvanceCancellation, cancellation);
            if (ownsAdvance)
            {
                _advancingPeriod = false;
                _periodAdvanceCancellation = null;
                _periodPlayback = null;
                _periodPlaybackMap = null;
            }
            bool resume = ownsAdvance && ReferenceEquals(_session, adopted ?? source);
            // This is the map that borrowed this operation, including during
            // shutdown or campaign replacement. A newer map owns itself.
            if (playback is not null && GodotObject.IsInstanceValid(playbackMap))
                playbackMap!.EndPeriodPlayback(playback, resume && adopted is not null);
            cancellation.Dispose();
            if (GodotObject.IsInstanceValid(this) && IsInsideTree() && !_shuttingDown && !_failed)
            {
                if (resume) RunUiAction(() =>
                {
                    ClosePeriodAdvanceProgress(overlay, playbackMap, resumeMap: true);
                    RefreshCurrentPlayWorld();
                    if (adopted is not null && automaticSaveNotice is not null)
                        _playMessage.Text = automaticSaveNotice;
                    if (_world?.Phase == TurnPhase.Reporting && !HasForcePresentation) SetMapFocus(false);
                });
                else
                {
                    // A replaced campaign owns its own modal state. Retire
                    // only this calculation's overlay after a late result.
                    try { ClosePeriodAdvanceProgress(overlay, playbackMap, resumeMap: false); }
                    catch (Exception error) { GD.PushWarning(error.ToString()); }
                }
            }
        }
    }

    private Control ShowPeriodAdvanceProgress(MapSceneView? map, CampaignPeriodPlayback owner)
    {
        var overlay = new Control { Name = "AdvancePeriodProgress", ZIndex = 100, MouseFilter = MouseFilterEnum.Ignore };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _periodAdvanceOverlay = overlay;
        AddChild(overlay);
        var panel = new PanelContainer { AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1, AnchorBottom = 1,
            OffsetLeft = -330, OffsetRight = 330, OffsetTop = -108, OffsetBottom = -16,
            MouseFilter = MouseFilterEnum.Stop };
        overlay.AddChild(panel);
        var margin = new MarginContainer();
        foreach (string side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 12);
        panel.AddChild(margin);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 6); margin.AddChild(column);
        _periodPlaybackCaption = new Label { Text = _ui.Get("play_advancing"), HorizontalAlignment = HorizontalAlignment.Center };
        column.AddChild(_periodPlaybackCaption);
        _periodPlaybackProgress = new ProgressBar { MaxValue = NativeSubdayStateValidation.UpdatesPerPeriod,
            ShowPercentage = true, CustomMinimumSize = new(300, 12) };
        column.AddChild(_periodPlaybackProgress);
        var controls = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddChild(controls);
        _periodPlaybackPause = new Button { Text = "\u2161", CustomMinimumSize = new(64, 28) };
        _periodPlaybackPause.Pressed += () =>
        { if (ReferenceEquals(_periodPlayback, owner)) TryTogglePeriodPlayback(); };
        controls.AddChild(_periodPlaybackPause);
        _periodPlaybackSpeeds.Clear();
        foreach (var label in new[] { "0.5\u00D7", "1\u00D7", "2\u00D7" })
        {
            int index = _periodPlaybackSpeeds.Count;
            var button = new Button { Text = label, ToggleMode = true, CustomMinimumSize = new(64, 28) };
            button.Pressed += () =>
            { if (ReferenceEquals(_periodPlayback, owner)) TrySetPeriodPlaybackSpeed(index); };
            _periodPlaybackSpeeds.Add(button); controls.AddChild(button);
        }
        RefreshPeriodPlaybackControls();
        if (GodotObject.IsInstanceValid(map)) map!.BeginPeriodPlayback(owner);
        SetMapFocus(true);
        return overlay;
    }

    private void ClosePeriodAdvanceProgress(Control? overlay, MapSceneView? map, bool resumeMap)
    {
        bool ownsOverlay = ReferenceEquals(_periodAdvanceOverlay, overlay);
        if (ownsOverlay)
        {
            _periodAdvanceOverlay = null;
            _periodPlaybackCaption = null; _periodPlaybackProgress = null; _periodPlaybackPause = null;
            _periodPlaybackSpeeds.Clear();
        }
        if (overlay is not null && GodotObject.IsInstanceValid(overlay))
        {
            overlay.Hide();
            if (overlay.GetParent() is Node parent) parent.RemoveChild(overlay);
            overlay.QueueFree();
        }
        if (resumeMap && ownsOverlay && ReferenceEquals(_residentMap, map))
        {
            if (GodotObject.IsInstanceValid(map)) map!.SetDetailsOpen(false);
            SetMapFocus(_mapFocused);
        }
    }
}
