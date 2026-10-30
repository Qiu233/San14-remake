using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed record PolicyRosterEditorContext(long Sequence);
    private GameplaySession? _forcePresentationSession;
    private GameState? _forcePresentationOwner;
    private MapSceneView? _forcePresentationMap;
    private DeploymentNarrationView? _forcePolicyAnnouncement;
    private DeploymentTargetConfirmation? _forcePolicyDecision;
    private PolicyRosterEditorContext? _policyRosterEditor;
    private Action<GameState>? _forcePresentationAdoptSpeechWorld;

    private void AdoptForceExperienceSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        // The shared caller excludes PendingNormalPeriod candidates. Normal
        // command outboxes retain this exact policy scene and its source page.
        if (!HasForcePresentation || _forcePresentationSession != session || _forcePresentationMap != _residentMap ||
            !ReferenceEquals(_forcePresentationOwner, previous)) return;
        _forcePresentationOwner = replacement;
        _forcePresentationAdoptSpeechWorld?.Invoke(replacement);
    }

    private bool _forceHeaderTransition;
    private bool HasForceExperienceTransition => _forceHeaderTransition;
    private bool HasForcePolicyPresentation => _forcePolicyAnnouncement is not null || _forcePolicyDecision is not null;
    private bool HasForcePresentation => _forceHeaderTransition || HasForcePolicyPresentation || _policyRosterEditor is not null;
    private bool IsForcePresentationCurrent => _session == _forcePresentationSession && _session is { } session &&
        ReferenceEquals(PersonnelPresentationOwner(session), _forcePresentationOwner) && _residentMap == _forcePresentationMap;

    private static (long Sequence, int Index, CampaignEffectRequest Request)? FindForcePresentation(GameState owner, int player)
    {
        foreach (var batch in owner.CampaignEffectRequests?.PendingBatches ?? [])
            for (int i = 0; i < batch.Requests.Length; i++)
            {
                var request = batch.Requests[i];
                if (batch.Source == CampaignEffectSource.ForcePolicyLevelUp && request.ForcePolicyLevelUp?.ForceId == player ||
                    ForceExperienceHeaderChange.IsGainSource(batch.Source) && request.TalentForceExperiencePresentation is
                        { Operation: TalentForceExperiencePresentationOperation.ExperienceGain } gain && gain.ForceId == player)
                    return (batch.Sequence, i, request);
            }
        return null;
    }

    private bool TryOpenForceExperiencePresentation()
    {
        if (HasForcePresentation)
        {
            //A live policy editor may legitimately publish a newer owner.
            if (_policyRosterEditor is not null || IsForcePresentationCurrent) return true;
            CloseForcePresentation();
        }
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || (HasAdministrationPageExceptStory || (HasStoryPresentation && !IsStoryPresentationCurrent)) ||
            HasMilitaryOrderPage || HasEnvironmentSettings || HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace ||
            HasPersonnelConfirmation || _detailsView is not null || _diplomacyNegotiation is not null ||
            _repository is null || _campaignComposition is not { } composition ||
            _session is not { PlayerForceId: int player } session ||
            (session.HasPendingOperation && session.PendingNormalPeriod is null && session.PendingCampaignEventCommand is null) ||
            _residentMap is not { IsPlayableMapReady: true } map || _forceHud is not { } hud) return false;
        var owner = SynchronizePersonnelPresentationOwner(session);
        var reached = FindForcePresentation(owner, player);
        if (reached is not { } entry) return false;
        _forcePresentationSession = session; _forcePresentationOwner = owner; _forcePresentationMap = map;
        _forcePresentationAdoptSpeechWorld = next => owner = next;
        var rules = composition.PrepareArmyQueries(owner);
        if (entry.Request.TalentForceExperiencePresentation is not null)
        {
            hud.Refresh(CurrentForceHudView(owner, player));
            _forceHeaderTransition = true;
            if (hud.BeginExperienceTransition(() => IsForcePresentationCurrent && CanAdvanceNarration && !_advancingPeriod,
                previous => RunUiAction(() => CompleteHeader(previous)), PlayForceExperienceSound))
            {
                RetirePeriodReportView(); CloseActionReport(); map.CloseCityCommandMenu();
                map.SetDetailsOpen(true, keepMapVisible: true); SetMapFocus(true); hud.Show();
                return true;
            }
            _mapHint.Text = _ui.Get("detail_unknown");
            CloseForcePresentation();
            return false;
        }
        RetirePeriodReportView(); CloseActionReport(); map.CloseCityCommandMenu();
        var scene = entry.Request.ForcePolicyLevelUp!;
        if (scene.Stage == ForcePolicyLevelUpStage.PolicyEditor)
        {
            _policyRosterEditor = new(entry.Sequence);
            if (FindPolicyGovernanceNarration(owner, player) is not null)
            { ShowPolicyGovernanceNarrations(_policyRosterEditor); return true; }
            OpenGovernanceRoster(_policyRosterEditor);
            if (!HasGovernanceRosterPage) _policyRosterEditor = null;
            return HasGovernanceRosterPage;
        }
        if (scene.Stage == ForcePolicyLevelUpStage.Announcement)
        {
            var view = new DeploymentNarrationView { KeyboardInput = HandleGameplayNarrationKeyboard }; _forcePolicyAnnouncement = view; AddChild(view);
            try
            {
                view.ConfigureAnnouncement(_repository, _ui, scene.AnnouncementTextAtSource,
                    () => RunUiAction(() => AdvancePolicy(ForcePolicyLevelUpInputKind.AnnouncementCompleted)),
                    () => _preferences.AutomaticMessageAdvance,
                    () => _forcePolicyAnnouncement == view && IsForcePresentationCurrent && CanAdvanceNarration);
                view.Name = "ForcePolicyLevelUpAnnouncement";
                SetMapFocus(true); map.SetDetailsOpen(true, keepMapVisible: true); HideDeploymentHud();
                return true;
            }
            catch { CloseForcePresentation(); throw; }
        }
        var question = new DeploymentTargetConfirmation(); _forcePolicyDecision = question; AddChild(question);
        try
        {
            question.ConfigureWrappedMessage(_repository, _ui, scene.ConfirmationTextAtSource,
                accepted => RunUiAction(() => AdvancePolicy(accepted
                    ? ForcePolicyLevelUpInputKind.OpenPolicy : ForcePolicyLevelUpInputKind.DeclinePolicy)));
            question.Name = "ForcePolicyLevelUpDecision";
            if (!CanOpenPolicyRosterEditor(session, owner, player) && question.FindChild("ConfirmYes", true, false) is BaseButton yes)
            { yes.Disabled = true; yes.Hide(); }
            SetMapFocus(true); map.SetDetailsOpen(true, keepMapVisible: true); HideDeploymentHud();
            return true;
        }
        catch { CloseForcePresentation(); throw; }

        void CompleteHeader(int previous)
        {
            if (!IsForcePresentationCurrent) { CloseForcePresentation(); return; }
            //All gains were accumulated by one header, not ACKed as individual
            //level changes. Re-find after each removal; array indices may shift.
            int display = previous;
            while (true)
            {
                var current = PersonnelPresentationOwner(session);
                var next = (current.CampaignEffectRequests?.PendingBatches ?? [])
                    .Where(batch => ForceExperienceHeaderChange.IsGainSource(batch.Source))
                    .SelectMany(batch => batch.Requests.Select((request, index) => (batch.Sequence, index, request)))
                    .FirstOrDefault(row => row.request.TalentForceExperiencePresentation is
                        { Operation: TalentForceExperiencePresentationOperation.ExperienceGain } gain && gain.ForceId == player);
                if (next.request is null) break;
                if (!session.TryCompleteForceExperiencePresentation(current, next.Sequence, next.index, display, rules)) return;
                display = OwnedForce164Inputs.ReadProgress(PersonnelPresentationOwner(session), player).Level
                    ?? throw new InvalidDataException("The completed header level became unknown.");
            }
            CloseForcePresentation(); SynchronizeForcePresentationOwner();
            ContinueForcePresentations();
        }
        void AdvancePolicy(ForcePolicyLevelUpInputKind kind)
        {
            if (!IsForcePresentationCurrent) { CloseForcePresentation(); return; }
            bool host = CanOpenPolicyRosterEditor(session, owner, player);
            if (!session.TryAdvanceForcePolicyLevelUp(owner, entry.Sequence, entry.Index,
                new(kind, kind == ForcePolicyLevelUpInputKind.AnnouncementCompleted ? host : null), rules)) return;
            CloseForcePresentation(); SynchronizeForcePresentationOwner(); ContinueForcePresentations();
        }
    }

    private bool CanOpenPolicyRosterEditor(GameplaySession session, GameState owner, int forceId) =>
        _repository is not null && _residentMap is { IsPlayableMapReady: true } &&
        ReferenceEquals(_session, session) && ReferenceEquals(PersonnelPresentationOwner(session), owner) &&
        (owner.Phase is TurnPhase.Strategy or TurnPhase.Reporting) && session.Rules.GovernanceRosterRules is not null &&
        owner.Forces.ContainsKey(forceId) && PlayableOutcome.Evaluate(owner, forceId) == PlayableResult.Ongoing;

    private static int? FindPolicyEditorEntry(GameState owner, long sequence, int player)
    {
        var batch = owner.CampaignEffectRequests?.PendingBatches.FirstOrDefault(item => item.Sequence == sequence &&
            item.Source == CampaignEffectSource.ForcePolicyLevelUp);
        if (batch is null) return null;
        for (int i = 0; i < batch.Requests.Length; i++)
            if (batch.Requests[i].ForcePolicyLevelUp is { Stage: ForcePolicyLevelUpStage.PolicyEditor } scene && scene.ForceId == player) return i;
        return null;
    }

    private static CampaignEffectBatch? FindPolicyGovernanceNarration(GameState owner, int player) =>
        owner.CampaignEffectRequests?.PendingBatches.FirstOrDefault(batch => batch.Source == CampaignEffectSource.GovernanceRoster &&
            batch.Requests.Length > 0 && batch.Requests.All(request => request.GovernanceNarration?.ForceId == player));

    private void CompletePolicyRosterEditor(PolicyRosterEditorContext context)
    {
        if (_session is not { PlayerForceId: int player } session || _campaignComposition is not { } composition ||
            _policyRosterEditor != context) return;
        var owner = SynchronizePersonnelPresentationOwner(session);
        if (FindPolicyEditorEntry(owner, context.Sequence, player) is not int index) { CloseForcePresentation(); return; }
        if (!session.TryAdvanceForcePolicyLevelUp(owner, context.Sequence, index,
            new(ForcePolicyLevelUpInputKind.PolicyEditorReturned), composition.PrepareArmyQueries(owner))) return;
        CloseForcePresentation(); SynchronizeForcePresentationOwner(); ContinueForcePresentations();
    }

    private void ShowPolicyGovernanceNarrations(PolicyRosterEditorContext context)
    {
        if (_session is not { PlayerForceId: int player } session || _repository is null ||
            _campaignComposition is not { } composition || _residentMap is not { } map || _policyRosterEditor != context) return;
        var owner = SynchronizePersonnelPresentationOwner(session);
        _forcePresentationOwner = owner;
        _forcePresentationAdoptSpeechWorld = next => owner = next;
        var batch = FindPolicyGovernanceNarration(owner, player);
        if (batch is null) { CompletePolicyRosterEditor(context); return; }
        int page = 0;
        Next();
        void Next()
        {
            if (!IsForcePresentationCurrent || _policyRosterEditor != context) return;
            if (page == batch.Requests.Length)
            {
                if (FindPolicyEditorEntry(owner, context.Sequence, player) is not int policyIndex) return;
                var result = session.IssuePolicyLevelUpOrder(owner, context.Sequence, policyIndex,
                    new CompleteGovernanceNarrationOrder(player, batch.Sequence), new(_presentationRandom.State));
                if (!result.Accepted) { _mapHint.Text = result.Error ?? _ui.Get("detail_unknown"); return; }
                SynchronizeForcePresentationOwner(); ShowPolicyGovernanceNarrations(context); return;
            }
            var response = batch.Requests[page].GovernanceNarration!;
            var query = composition.PrepareArmyQueries(owner).QueryGovernanceNarrationPortrait(owner, response.SpeakerId);
            IReadOnlyList<LoadedSprite> layers = [];
            if (query.Value is { } route)
            {
                var sprites = new LocalizedUiSpriteSource(_repository);
                layers = route.LoadSprites(sprites.LoadTexturePackSprite);
            }
            else GD.PushWarning(query.UnresolvedInput ?? "Policy response portrait is unavailable.");
            var view = new DeploymentNarrationView { KeyboardInput = HandleGameplayNarrationKeyboard }; _forcePolicyAnnouncement = view; AddChild(view);
            try
            {
                view.Configure(_repository, _ui, owner.Officers[response.SpeakerId].Name, response.TextAtSource, layers,
                    () => RunUiAction(() =>
                    {
                        if (_forcePolicyAnnouncement != view || !IsForcePresentationCurrent) return;
                        _forcePolicyAnnouncement = null; view.Abandon(); view.GetParent()?.RemoveChild(view); view.QueueFree();
                        page++; Next();
                    }), () => _preferences.AutomaticMessageAdvance,
                    () => _forcePolicyAnnouncement == view && IsForcePresentationCurrent && CanAdvanceNarration);
                view.Name = "PolicyEditorGovernanceNarration";
                SetMapFocus(true); map.SetDetailsOpen(true, keepMapVisible: true); HideDeploymentHud();
            }
            catch { CloseForcePresentation(); throw; }
        }
    }

    private void SynchronizeForcePresentationOwner()
    {
        if (_session is not { } session) return;
        if (SynchronizePersonnelPresentationOwner(session).CampaignAppRandom32 is { } app) _presentationRandom.InstallCommittedState(app.State);
    }

    private void ContinueForcePresentations()
    {
        if (!TryOpenPersonnelPresentation())
        { RefreshPlayControls(); RefreshDiplomaticNegotiation(); }
    }

    private void PlayForceExperienceSound()
    {
        //The source is SE31, not one of the personnel HAPPY cues. Its actual
        //audio resolver is supplied by the common audio layer.
        _sceneAudioBinding?.PlayForceExperienceGainSound();
    }

    private void CloseForcePresentation()
    {
        var map = _forcePresentationMap;
        _forceHud?.AbandonExperienceTransition(); _forceHeaderTransition = false;
        var announcement = _forcePolicyAnnouncement; _forcePolicyAnnouncement = null;
        if (announcement is not null) { announcement.Abandon(); announcement.GetParent()?.RemoveChild(announcement); announcement.QueueFree(); }
        var decision = _forcePolicyDecision; _forcePolicyDecision = null;
        if (decision is not null) { decision.Abandon(); decision.GetParent()?.RemoveChild(decision); decision.QueueFree(); }
        _policyRosterEditor = null;
        _forcePresentationAdoptSpeechWorld = null;
        _forcePresentationSession = null; _forcePresentationOwner = null; _forcePresentationMap = null;
        if (map is not null && _residentMap == map && GodotObject.IsInstanceValid(map))
        { map.SetDetailsOpen(false); if (!_failed && !_shuttingDown) SetMapFocus(true); }
    }
}
