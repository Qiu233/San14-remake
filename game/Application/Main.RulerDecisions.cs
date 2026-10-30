using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class RulerDecisionWorkspace(GameplaySession session, GameState world, CampaignComposition composition, MapSceneView map)
    {
        internal readonly GameplaySession Session = session;
        internal readonly GameState World = world;
        internal readonly CampaignComposition Composition = composition;
        internal readonly MapSceneView Map = map;
        internal Control? View;
        internal Action? Abandon;
        internal DeploymentNarrationView? Warning;
    }
    private RulerDecisionWorkspace? _rulerDecision;
    private bool HasRulerDecision => _rulerDecision is not null;
    private bool IsRulerDecisionCurrent => _rulerDecision is { } owner &&
        ReferenceEquals(_session, owner.Session) && ReferenceEquals(_campaignComposition, owner.Composition) &&
        ReferenceEquals(StoryOwner(owner.Session), owner.World) &&
        ReferenceEquals(_residentMap, owner.Map);

    private bool TryOpenRulerDecision(GameplaySession session, NormalPeriodOperation pending,
        CampaignComposition composition, MapSceneView map, int player)
        => TryOpenRulerDecision(session, pending.Candidate, composition, map, player);

    private bool TryOpenRulerDecision(GameplaySession session, GameState world,
        CampaignComposition composition, MapSceneView map, int player)
    {
        if (!ReferenceEquals(StoryOwner(session), world)) return false;
        var successor = world.PersonDeathContinuation?.PendingSuccession;
        var prisoner = world.ArmyDefeatContinuation?.PendingCapturedRulerTreatment ?? world.BaseCaptureContinuation?.PendingCapturedRulerTreatment;
        var emperor = session.PendingEmperorCaptureDecision;
        if (successor is null && prisoner is null && emperor is null) return false;
        var owner = new RulerDecisionWorkspace(session, world, composition, map); _rulerDecision = owner;
        try
        {
            RetirePeriodReportView(); CloseActionReport(); map.CloseCityCommandMenu();
            map.SetDetailsOpen(true, keepMapVisible: true); SetMapFocus(false); HideDeploymentHud();
            //An execution may suspend inside death processing for a successor.
            //That nested question precedes the remaining defeat settlement.
            if (successor is not null)
            {
                if (successor.ForceId != player) throw new InvalidDataException("Ruler succession belongs to another force.");
                OpenRulerSuccessorSelector(owner, successor, Submit);
            }
            else if (prisoner is not null)
            {
                if (prisoner.CapturingForceId != player) throw new InvalidDataException("Prisoner treatment belongs to another force.");
                var view = new CapturedRulerTreatmentView(); owner.View = view; owner.Abandon = view.Abandon;
                AddChild(view);
                view.Configure(_repository!, _ui, ReadCurrentOfficerDetail(world, prisoner.OfficerId),
                    prisoner, () => IsRulerDecisionCurrent && !_advancingPeriod,
                    choice => RunUiAction(() => Submit(new ConfirmCapturedRulerTreatmentOrder(player, prisoner.OfficerId, choice))));
            }
            else if (emperor is not null)
            {
                if (emperor.CapturingForceId != player ||
                    !ReferenceEquals(session.PendingEmperorCaptureDecision, emperor))
                    throw new InvalidDataException("Emperor capture belongs to another decision owner.");
                OpenEmperorCaptureDecision(owner, emperor.TextAtSource, receive =>
                {
                    if (!session.TryCompleteEmperorCaptureDecision(world, emperor, receive)) return;
                    CloseRulerDecision();
                    if (session.PendingEditedEventPlayback is { Phase: EditedEventPlaybackPhase.Results })
                        TryResumeCreatedEventResults();
                    else ContinueCampaignStory(session, composition);
                });
            }
            return true;
        }
        catch { CloseRulerDecision(); throw; }

        void Submit(GameOrder choice)
        {
            if (!IsRulerDecisionCurrent || _advancingPeriod || _failed || _shuttingDown) return;
            CloseRulerDecision();
            ContinueCampaignStory(session, composition, choice);
        }
    }

    private void OpenRulerSuccessorSelector(RulerDecisionWorkspace owner, RulerSuccessionRequest request,
        Action<GameOrder> submit)
    {
        Announce();

        void Announce()
        {
            if(!IsRulerDecisionCurrent||_advancingPeriod)return;
            //28C5D0 announces before the first modal, and repeats only when
            //the source state returns an empty output. No successor is guessed.
            var warning=new DeploymentNarrationView {KeyboardInput=HandleGameplayNarrationKeyboard};
            owner.Warning=warning;AddChild(warning);
            warning.ConfigureAnnouncement(_repository!,_ui,request.EmptyChoiceWarningAtSource,()=>RunUiAction(()=>
            {
                if(!IsRulerDecisionCurrent||owner.Warning!=warning)return;
                owner.Warning=null;warning.Hide();RemoveChild(warning);warning.QueueFree();OpenModal();
            }),()=>_preferences.AutomaticMessageAdvance,
                ()=>CanAdvanceNarration&&IsRulerDecisionCurrent&&owner.Warning==warning);
            warning.Name="RulerSuccessionAnnouncement";warning.ZIndex=97;
        }

        void OpenModal()
        {
            if(!IsRulerDecisionCurrent||_advancingPeriod||owner.Warning is not null)return;
            var query=owner.Composition.PrepareArmyQueries(owner.World)
                .QueryRulerSuccessionSelection(owner.World,request.ForceId);
            var publication=query.Value??throw new InvalidDataException(query.UnresolvedInput);
            var selector=new IntelligenceSelectionView();owner.View=selector;owner.Abandon=selector.Abandon;
            AddChild(selector);
            selector.Configure(_repository!,_ui,RulerSuccessorSelectionContent.Create(publication,_ui.Get("detail_unknown")),
                ()=>IsRulerDecisionCurrent&&!_advancingPeriod&&owner.View==selector&&owner.Warning is null,
                selected=>RunUiAction(()=>
                {
                    if(!IsRulerDecisionCurrent||owner.View!=selector||_advancingPeriod)return;
                    owner.View=null;owner.Abandon=null;selector.Abandon();RemoveChild(selector);selector.QueueFree();
                    if(selected is int id)submit(new ConfirmRulerSuccessionOrder(request.ForceId,id));
                    else Announce();
                }),CurrentListScrollSpeed);
            // The original model88 is zero: no Return button and no common
            //user cancel admission. An actual empty state result still retries.
            selector.ApplySelectionAdmission(null,canReturn:false);
            selector.Name="RulerSuccession";selector.ZIndex=96;
        }
    }

    private void CloseRulerDecision()
    {
        var owner = _rulerDecision; _rulerDecision = null;
        if (owner is null) return;
        owner.Abandon?.Invoke();
        foreach (var view in new Control?[] { owner.Warning, owner.View })
            if (view is not null) { view.Hide(); view.GetParent()?.RemoveChild(view); view.QueueFree(); }
        if (GodotObject.IsInstanceValid(owner.Map) && ReferenceEquals(_residentMap, owner.Map)) owner.Map.SetDetailsOpen(IsStoryPresentationCurrent || HasCreatedEventPlayback, keepMapVisible: true);
    }
}
