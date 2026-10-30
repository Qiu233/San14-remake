using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class NormalStoryResultWorkspace(GameplaySession session, CampaignComposition composition,
        MapSceneView map, GameState world, long sequence, NormalTurnStoryResultSummary summary,
        EventResultSummaryView view)
    {
        internal readonly GameplaySession Session = session;
        internal readonly CampaignComposition Composition = composition;
        internal readonly MapSceneView Map = map;
        internal readonly GameState World = world;
        internal readonly long Sequence = sequence;
        internal readonly NormalTurnStoryResultSummary Summary = summary;
        internal readonly EventResultSummaryView View = view;
    }
    private NormalStoryResultWorkspace? _normalStoryResult;
    private bool HasNormalStoryResult => _normalStoryResult is not null;
    private bool IsNormalStoryResultCurrent => _normalStoryResult is { } owner &&
        ReferenceEquals(_session, owner.Session) && ReferenceEquals(_campaignComposition, owner.Composition) &&
        ReferenceEquals(_residentMap, owner.Map) &&
        ReferenceEquals(StoryOwner(owner.Session), owner.World) &&
        ReferenceEquals(PendingCampaignStoryResult(owner.Session), owner.Summary);

    private bool TryOpenNormalStoryResult(GameplaySession session, NormalPeriodOperation? pending,
        CampaignComposition composition, MapSceneView map)
    {
        if (_normalStoryResult is not null || PendingCampaignStoryResult(session) is not { } summary ||
            StoryPlayback(session) is not { } playback) return false;
        //The same scene remains alive behind the result page until real exit.
        bool restoreRetainedMedia = _storyPresentation is null && playback.Story.Steps.Length != 0 &&
            TryOpenStoryPresentation(session, pending, composition, map, playback.Story, restoreMedia: false);
        var view = new EventResultSummaryView();
        var owner = new NormalStoryResultWorkspace(session, composition, map, StoryOwner(session),
            playback.Story.Sequence, summary, view);
        _normalStoryResult = owner; AddChild(view);
        try
        {
            RetirePeriodReportView(); CloseActionReport(); map.CloseCityCommandMenu();
            map.RefreshPlayableWorld(owner.World);
            map.SetDetailsOpen(true, keepMapVisible: true); SetMapFocus(false); HideDeploymentHud();
            view.Configure(_repository!, summary,
                () => IsNormalStoryResultCurrent && (_storyPresentation?.MediaRestoration.IsCompletedSuccessfully ?? true) &&
                    !_failed && !_shuttingDown && !_advancingPeriod &&
                    !IsReturningToTitle && !HasEnvironmentSettings && !HasCampaignSavePage && !HasMapSystemMenu,
                () => RunUiAction(() => ConfirmNormalStoryResult(owner)));
            //Register the result owner before any synchronous restored-media Pump.
            if (restoreRetainedMedia && _storyPresentation is { } scene)
                StartStoryMediaRestoration(scene, playback.Story);
            return true;
        }
        catch { CloseNormalStoryResult(); throw; }
    }

    private void ConfirmNormalStoryResult(NormalStoryResultWorkspace owner)
    {
        if (!ReferenceEquals(_normalStoryResult, owner) || !IsNormalStoryResultCurrent || _advancingPeriod ||
            _failed || _shuttingDown || IsReturningToTitle ||
            _storyPresentation?.MediaRestoration.IsCompletedSuccessfully == false) return;
        owner.View.SetBusy(true);
        if (!owner.Session.TryCompleteCampaignStoryResult(owner.World, owner.Sequence, owner.Summary))
        { owner.View.SetBusy(false); return; }
        if (_storyPresentation is { } binding && ReferenceEquals(binding.Session, owner.Session))
            binding.World = StoryOwner(owner.Session);
        CloseNormalStoryResult();
        _advancingPeriod = true;
        _periodAdvanceTask = ContinueCampaignStoryAsync(owner.Session, owner.Composition);
    }

    private void CloseNormalStoryResult()
    {
        var owner = _normalStoryResult; _normalStoryResult = null;
        if (owner is null) return;
        owner.View.Abandon(); owner.View.GetParent()?.RemoveChild(owner.View); owner.View.QueueFree();
        if (GodotObject.IsInstanceValid(owner.Map) && ReferenceEquals(_residentMap, owner.Map))
            owner.Map.SetDetailsOpen(false);
    }
}
