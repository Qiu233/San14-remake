using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class CreatedEventWorkspace(GameplaySession session, CampaignComposition composition,
        MapSceneView map, ArmyCampaignRuleAdapter? rules, EditedEventPlaybackRules? execution, bool mapFocused)
    {
        internal readonly GameplaySession Session = session;
        internal readonly CampaignComposition Composition = composition;
        internal readonly MapSceneView Map = map;
        internal readonly ArmyCampaignRuleAdapter? Rules = rules;
        internal readonly EditedEventPlaybackRules? Execution = execution;
        internal readonly bool MapFocusedAtEntry = mapFocused;
        internal GameState? SelectionWorld;
        internal GameState? ResultAttemptWorld;
        internal Task? MediaResume;
        internal CreatedEventSelectionView? Selection;
        internal StoryPlaybackHost? Host;
        internal EditedEventResultSummaryView? Summary;
        internal DeploymentTargetConfirmation? Question;
        internal EventSourceReference? Source;
        internal bool Retired;
    }

    private CreatedEventWorkspace? _createdEventWorkspace;
    private EditedEventPlayback? _createdEventFailedProgress;
    private bool HasCreatedEventPage => _createdEventWorkspace is not null;
    private bool HasCreatedEventPlayback => _createdEventWorkspace is { Host: not null } or { Summary: not null } || HasCreatedEventResults;

    private bool IsCreatedEventWorkspaceCurrent(CreatedEventWorkspace owner) =>
        !_failed && !_shuttingDown && !IsReturningToTitle && !owner.Retired &&
        ReferenceEquals(_createdEventWorkspace, owner) && ReferenceEquals(_session, owner.Session) &&
        ReferenceEquals(_residentMap, owner.Map) && ReferenceEquals(_campaignComposition, owner.Composition);

    private bool IsCreatedEventPlaybackCurrent(CreatedEventWorkspace owner) =>
        IsCreatedEventWorkspaceCurrent(owner) && owner.Source is { } source &&
        owner.Session.PendingEditedEventPlayback?.Source == source;

    private CreatedEventWorkspace CreateCreatedEventWorkspace(GameplaySession session,
        CampaignComposition composition, MapSceneView map)
    {
        // A restored summary is complete saved presentation data. It must
        // not open the current title archive or rebuild its business rules.
        var rules = composition.PrepareArmyQueries(session.World);
        rules.ConfigurePersonnelNarrationVoice(_preferences.Voice.Capture());
        if (map.SourceMainCameraTerrain is { } terrain) rules.ConfigureStoryCameraTerrain(terrain);
        if (session.PendingEditedEventPlayback?.Phase is EditedEventPlaybackPhase.Summary or EditedEventPlaybackPhase.PostResult)
            return new(session, composition, map, rules, null, _mapFocused);
        // This actual application enters a fresh manual playback manager with
        // its nested-execution suppression clear. Audio is staged, not played
        // while the candidate list or event transaction is being prepared.
        var audio = new CampaignNormalEventAudioInputs(_ui.SourceLocale,
            readApplicationField1AC: () => 0, readApplicationPointerPresent: () => _sceneAudioBinding is not null);
        return new(session, composition, map, rules, new(rules, GetEditedEventStore(), audio), _mapFocused);
    }

    private void OpenCreatedEvents()
    {
        if (_failed || _shuttingDown || IsReturningToTitle || _repository is null || HasCreatedEventPage ||
            _session is not { PlayerForceId: not null } session ||
            _campaignComposition is not { } composition || _residentMap is not { IsPlayableMapReady: true } map) return;
        if (session.PendingEditedEventPlayback is not null)
        {
            _createdEventFailedProgress = null;
            TryResumeCreatedEventPlayback(); return;
        }
        if (session.HasPendingOperation || session.World.Phase != TurnPhase.Strategy ||
            HasAdministrationPage || HasMilitaryOrderPage || HasCampaignSavePage || HasEnvironmentSettings ||
            HasMapSystemMenu || HasCommandWorkspace || _detailsView is not null || HasActionReport) return;
        var owner = CreateCreatedEventWorkspace(session, composition, map);
        _createdEventWorkspace = owner; owner.SelectionWorld = session.World;
        var view = new CreatedEventSelectionView(); owner.Selection = view; AddChild(view);
        try
        {
            var library = GetEditedEventStore().Current;
            var rows = owner.Execution!.ReadManualCandidates(session.World).Select(row =>
                new CreatedEventSelectionRow(row.Slot, row.Name, library.FindEvent(row.Slot)!.Description,
                    row.CanExecute, row.UnresolvedOwner is null ? "" : _ui.Get("detail_unknown"))).ToArray();
            view.Configure(_repository, _ui, rows,
                () => IsCreatedEventWorkspaceCurrent(owner) && owner.Question is null && ReferenceEquals(owner.SelectionWorld, session.World),
                selected => RunUiAction(() =>
                {
                    if (!IsCreatedEventWorkspaceCurrent(owner) || !ReferenceEquals(owner.SelectionWorld, session.World)) return;
                    if (selected is not int slot) { CloseCreatedEvents(); return; }
                    RequestBeginCreatedEvent(owner, slot);
                }), CurrentListScrollSpeed);
            view.RegisterHeader(_sourceHeaders);
            map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true);
            SetMapFocus(false); HideDeploymentHud();
        }
        catch { CloseCreatedEvents(); throw; }
    }

    private void RequestBeginCreatedEvent(CreatedEventWorkspace owner, int slot)
    {
        if (!IsCreatedEventWorkspaceCurrent(owner) || owner.Selection is not { } view || owner.Question is not null ||
            !ReferenceEquals(owner.SelectionWorld, owner.Session.World)) return;
        var warning = owner.Execution!.ReadManualConfirmationRequired(owner.Session.World);
        if (!warning.Available)
        { view.ApplyNotice(_ui.Format("play_event_rule_unavailable", warning.UnresolvedInput ?? "edited_event.manual_warning")); return; }
        if (warning.Value != true) { Begin(); return; }
        var question = new DeploymentTargetConfirmation(); owner.Question = question; AddChild(question); view.SetBusy(true);
        try
        {
            question.ConfigureGameMessage(_repository!, _ui, 0x28D6, confirmed => RunUiAction(() =>
            {
                if (!IsCreatedEventWorkspaceCurrent(owner) || owner.Question != question) return;
                owner.Question = null; question.Abandon(); question.QueueFree(); view.SetBusy(false);
                if (confirmed) Begin();
            }));
            question.Name = "CreatedEventAchievementWarning";
        }
        catch { owner.Question = null; question.Abandon(); question.QueueFree(); view.SetBusy(false); throw; }

        void Begin()
        {
            if (!IsCreatedEventWorkspaceCurrent(owner) || !ReferenceEquals(owner.SelectionWorld, owner.Session.World)) return;
            if (!owner.Session.TryBeginEditedEventPlayback(owner.Session.World, slot, owner.Execution!, out string? missing,
                new(_presentationRandom.State)))
            { view.ApplyNotice(_ui.Format("play_event_rule_unavailable", missing ?? "edited_event.manual_begin")); return; }
            owner.Selection = null; owner.SelectionWorld = null;
            view.Abandon(); view.QueueFree(); RefreshPlayControls();
            try { OpenCreatedEventPlayback(owner); }
            catch (Exception error) { FailCreatedEventPlayback(owner, error); }
        }
    }

    private bool TryResumeCreatedEventPlayback()
    {
        if (HasCreatedEventPage)
        {
            if (HasCreatedEventResults) return TryResumeCreatedEventResults();
            return true;
        }
        if (_failed || _shuttingDown || IsReturningToTitle || _advancingPeriod || _repository is null ||
            HasAdministrationPage || HasMilitaryOrderPage || HasCampaignSavePage || HasEnvironmentSettings ||
            HasMapSystemMenu || HasCommandWorkspace || _detailsView is not null || HasActionReport ||
            _session is not { PlayerForceId: not null, PendingEditedEventPlayback: { } pending } session ||
            _campaignComposition is not { } composition || _residentMap is not { IsPlayableMapReady: true } map ||
            ReferenceEquals(pending, _createdEventFailedProgress)) return false;
        var owner = CreateCreatedEventWorkspace(session, composition, map);
        _createdEventWorkspace = owner;
        try { OpenCreatedEventPlayback(owner); }
        catch (Exception error) { FailCreatedEventPlayback(owner, error); }
        return true;
    }

    private void OpenCreatedEventPlayback(CreatedEventWorkspace owner)
    {
        if (!IsCreatedEventWorkspaceCurrent(owner) || owner.Session.PendingEditedEventPlayback is not { } pending) return;
        owner.Source = pending.Source;
        if (pending.Phase == EditedEventPlaybackPhase.Summary)
        { TryResumeCreatedEventResults(); return; }
        if (pending.Phase == EditedEventPlaybackPhase.PostResult)
        { TryResumeCreatedEventResults(); return; }
        if (pending.Phase == EditedEventPlaybackPhase.Results)
        { TryResumeCreatedEventResults(); return; }
        OpenCreatedEventHost(owner);
    }

    private void OpenCreatedEventHost(CreatedEventWorkspace owner)
    {
        if (owner.Host is not null) return;
        var audio = _sceneAudioBinding ?? throw new InvalidDataException("edited_event.audio.scene_owner");
        var host = new StoryPlaybackHost(); owner.Host = host; AddChild(host);
        owner.Map.CloseCityCommandMenu(); owner.Map.SetDetailsOpen(true, keepMapVisible: true);
        owner.Map.RefreshPlayableWorld(owner.Session.World); SetMapFocus(false); HideDeploymentHud();
        host.Configure(_repository!, _ui, new(owner.Rules!, () => owner.Session.World,
            () => owner.Session.PendingEditedEventPlayback?.Story,
            () =>
            {
                if (!IsCreatedEventPlaybackCurrent(owner) ||
                    !owner.Session.TryPrepareEditedEventPlaybackStep(owner.Session.World, owner.Execution!, out var prepared) ||
                    prepared.UnresolvedOwner is not null)
                    throw new InvalidDataException("edited_event.text.prepare");
            }, receipt =>
            {
                if (!IsCreatedEventPlaybackCurrent(owner) || owner.Session.PendingEditedEventPlayback is not { } progress) return false;
                return owner.Session.TryCompleteEditedEventPlaybackStep(owner.Session.World, progress, receipt);
            }), owner.Map, audio, _preferences.MusicSelection,
            () => IsCreatedEventPlaybackCurrent(owner) &&
                owner.Session.PendingEditedEventPlayback?.Phase == EditedEventPlaybackPhase.Presentation &&
                CanAdvanceNarration && !_advancingPeriod &&
                !HasPersonnelPresentation && !HasRulerDecision,
            () => _preferences.AutomaticMessageAdvance, CurrentListScrollSpeed, HandleNarrationKeyboard,
            AdoptStoryRandom, () => RunUiAction(() => TryResumeCreatedEventResults()),
            error => RunUiAction(() => FailCreatedEventPlayback(owner, error)), inputWaitKeyboard: HandleNarrationKeyboard);
        host.Name = "CreatedEventStoryPlayback";
    }

    private void FinishCreatedEventPlayback(CreatedEventWorkspace owner)
    {
        if (!IsCreatedEventPlaybackCurrent(owner) || owner.Session.PendingEditedEventPlayback is not { } pending) return;
        owner.ResultAttemptWorld = owner.Session.World;
        if (!owner.Session.TryFinishEditedEventPlayback(owner.Session.World, pending, owner.Execution!, out string? missing))
        { FailCreatedEventPlayback(owner, new InvalidDataException(missing ?? "edited_event.results")); return; }
        if (owner.Session.PendingEditedEventPlayback is { Phase: EditedEventPlaybackPhase.Results })
        {
            owner.Composition.PublishApplicationServices(owner.Session.World);
            owner.Map.RefreshPlayableWorld(owner.Session.World);
            RefreshPlayControls();
            TryResumeCreatedEventResults();
            return;
        }
        if (owner.Session.PendingEditedEventPlayback is { Phase: EditedEventPlaybackPhase.Summary } summary)
        {
            owner.Composition.PublishApplicationServices(owner.Session.World);
            owner.Map.RefreshPlayableWorld(owner.Session.World);
            RefreshPlayControls(); TryResumeCreatedEventResults();
            return;
        }
        CompleteCreatedEventWorkspace(owner);
    }

    private void OpenCreatedEventSummary(CreatedEventWorkspace owner, EditedEventPlayback progress)
    {
        if (!IsCreatedEventPlaybackCurrent(owner) || owner.Summary is not null || progress.Summary is not { } content) return;
        var view = new EditedEventResultSummaryView(); owner.Summary = view; AddChild(view);
        owner.Map.CloseCityCommandMenu(); owner.Map.SetDetailsOpen(true, keepMapVisible: true);
        SetMapFocus(false); HideDeploymentHud();
        try
        {
            view.Configure(_repository!, content,
                () => IsCreatedEventPlaybackCurrent(owner) && ReferenceEquals(owner.Session.PendingEditedEventPlayback, progress),
                () => RunUiAction(() =>
                {
                    if (!IsCreatedEventPlaybackCurrent(owner) || owner.Summary != view ||
                        !ReferenceEquals(owner.Session.PendingEditedEventPlayback, progress)) return;
                    view.SetBusy(true);
                    if (!owner.Session.TryCompleteEditedEventSummary(owner.Session.World, progress, out string? missing))
                    { view.SetBusy(false); ReportPlayFailure(new InvalidDataException(missing ?? "edited_event.summary.confirm")); return; }
                    owner.Summary = null; view.Abandon(); view.QueueFree();
                    RefreshPlayControls(); TryResumeCreatedEventResults();
                }));
            view.ZIndex = 97;
        }
        catch { owner.Summary = null; view.Abandon(); view.QueueFree(); throw; }
    }

    private void CompleteCreatedEventWorkspace(CreatedEventWorkspace owner)
    {
        _createdEventFailedProgress = null;
        CloseCreatedEvents();
        AdoptStoryRandom(owner.Session.World);
        owner.Composition.PublishApplicationServices(owner.Session.World);
        RefreshCurrentPlayWorld();
    }

    private void FailCreatedEventPlayback(CreatedEventWorkspace owner, Exception error)
    {
        if (!IsCreatedEventWorkspaceCurrent(owner)) return;
        // An unresolved tail keeps the exact Core cursor and can be explicitly
        // retried from Created Events. Refresh must not replay it every frame.
        _createdEventFailedProgress = owner.Session.PendingEditedEventPlayback;
        CloseCreatedEvents(); RefreshPlayControls(); ReportPlayFailure(error);
    }

    private void RequestCreatedEventsBack()
    {
        if (_createdEventWorkspace?.Question is { } question) question.RequestCancel();
        else if (_createdEventWorkspace?.Summary is { } summary) summary.RequestConfirmation();
        else if (_createdEventWorkspace?.Host is { } host) host.RequestSkip();
        else _createdEventWorkspace?.Selection?.RequestBack();
    }

    private void CloseCreatedEvents()
    {
        var owner = _createdEventWorkspace; _createdEventWorkspace = null;
        if (owner is null || owner.Retired) return;
        owner.Retired = true;
        if (owner.Question is { } question) { question.Abandon(); question.QueueFree(); }
        if (owner.Selection is { } selection) { selection.Abandon(); selection.QueueFree(); }
        if (owner.Host is { } host) { host.Abandon(); host.QueueFree(); }
        if (owner.Summary is { } summary) { summary.Abandon(); summary.QueueFree(); }
        if (GodotObject.IsInstanceValid(owner.Map) && ReferenceEquals(_residentMap, owner.Map))
        {
            owner.Map.SetDetailsOpen(false);
            if (!_failed && !_shuttingDown) SetMapFocus(owner.MapFocusedAtEntry);
        }
    }
}
