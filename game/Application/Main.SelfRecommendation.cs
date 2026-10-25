using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private SelfRecommendationView? _selfRecommendation;
    private GameplaySession? _selfRecommendationSession;
    private NormalPeriodOperation? _selfRecommendationPending;
    private MapSceneView? _selfRecommendationMap;
    private NormalPeriodOperation? _automaticNormalTurnAttempt;
    private bool HasSelfRecommendation => _selfRecommendation is not null;
    private bool IsSelfRecommendationCurrent => _selfRecommendation is not null &&
        ReferenceEquals(_session, _selfRecommendationSession) &&
        ReferenceEquals(_session?.PendingNormalPeriod, _selfRecommendationPending) &&
        ReferenceEquals(_residentMap, _selfRecommendationMap);

    // Called only after the head of the source-time presentation queue has
    // been consumed. A decision and its remaining officer list belong to the
    // suspended candidate, while the visible committed world stays unchanged.
    private bool RefreshNormalTurnContinuation()
    {
        if (HasRulerDecision)
        {
            if (IsRulerDecisionCurrent) return true;
            CloseRulerDecision();
        }
        if (HasStoryPresentation)
        {
            if (IsStoryPresentationCurrent) return true;
            CloseStoryPresentation();
        }
        if (HasCoalitionInvitation)
        {
            if (IsCoalitionInvitationCurrent) return true;
            CloseCoalitionInvitation();
        }
        if (HasSelfRecommendation)
        {
            if (IsSelfRecommendationCurrent) return true;
            CloseSelfRecommendation();
        }
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle ||
            HasAdministrationPage || HasMilitaryOrderPage || HasEnvironmentSettings || HasCampaignSavePage ||
            HasMapSystemMenu || HasCommandWorkspace || HasPersonnelConfirmation || HasPersonnelPresentation ||
            _detailsView is not null || _diplomacyNegotiation is not null || _repository is null ||
            _selectedSnapshot is null || _campaignComposition is not { } composition ||
            _residentMap is not { IsPlayableMapReady: true } map ||
            _session is not { PlayerForceId: int player, PendingNormalPeriod: { } pending } session)
            return false;
        if (TryOpenRulerDecision(session, pending, composition, map, player)) return true;
        if (pending.PendingSelfRecommendation is { } request)
        {
            if (request.ForceId != player)
                throw new InvalidDataException("The self-recommendation does not belong to the local player.");
            var view = new SelfRecommendationView();
            _selfRecommendation = view; _selfRecommendationSession = session;
            _selfRecommendationPending = pending; _selfRecommendationMap = map;
            AddChild(view);
            try
            {
                RetirePeriodReportView(); CloseActionReport();
                view.Configure(_repository, _ui,
                    ReadCurrentOfficerDetail(pending.Candidate, request.OfficerId),
                    request.TextAtSource, () => IsSelfRecommendationCurrent && !_advancingPeriod,
                    accepted => RunUiAction(() =>
                    {
                        if (!IsSelfRecommendationCurrent || _advancingPeriod) return;
                        var choice = new ConfirmSelfRecommendationOrder(player, request.OfficerId, accepted);
                        CloseSelfRecommendation();
                        _advancingPeriod = true;
                        _periodAdvanceTask = AdvancePlayableAsync(session, composition, campaignChoice: choice);
                    }));
                map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true);
                SetMapFocus(false); HideDeploymentHud();
                return true;
            }
            catch { CloseSelfRecommendation(); throw; }
        }
        if (pending.PendingCoalitionInvitation is { } invitation)
        {
            OpenCoalitionInvitation(session, pending, composition, map, player, invitation);
            return true;
        }
        if (pending.PendingStory is not null)
            return TryOpenStoryPresentation(session, pending, composition, map);
        if (!pending.RequiresAutomaticNormalTurnContinuation && !pending.RequiresAutomaticArmyDefeatContinuation) return false;
        // A failed worker must not immediately retry the same immutable
        // operation through the refresh path. The normal Advance remains
        // available for an explicit retry after its cause has been resolved.
        if (ReferenceEquals(_automaticNormalTurnAttempt, pending)) return true;
        _automaticNormalTurnAttempt = pending;
        _advancingPeriod = true;
        _periodAdvanceTask = AdvancePlayableAsync(session, composition);
        return true;
    }

    private void CloseSelfRecommendation()
    {
        var view = _selfRecommendation; var map = _selfRecommendationMap;
        _selfRecommendation = null; _selfRecommendationSession = null;
        _selfRecommendationPending = null; _selfRecommendationMap = null;
        if (view is not null)
        {
            view.Abandon(); view.GetParent()?.RemoveChild(view); view.QueueFree();
        }
        if (map is not null && GodotObject.IsInstanceValid(map) && ReferenceEquals(_residentMap, map))
        {
            map.SetDetailsOpen(false);
            if (!_failed && !_shuttingDown) SetMapFocus(_mapFocused);
        }
    }
}
