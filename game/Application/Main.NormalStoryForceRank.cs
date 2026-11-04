using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class NormalStoryForceRankWorkspace(GameplaySession session, CampaignComposition composition,
        MapSceneView map, GameState world, ArmyCampaignRuleAdapter rules, CampaignStoryForceRankContinuation request)
    {
        internal readonly GameplaySession Session = session;
        internal readonly CampaignComposition Composition = composition;
        internal readonly MapSceneView Map = map;
        internal readonly GameState World = world;
        internal readonly ArmyCampaignRuleAdapter Rules = rules;
        internal readonly CampaignStoryForceRankContinuation Request = request;
        internal Control? View;
    }
    private NormalStoryForceRankWorkspace? _normalStoryForceRank;
    private bool HasNormalStoryForceRank => _normalStoryForceRank is not null;
    private bool IsNormalStoryForceRankCurrent => _normalStoryForceRank is { } owner &&
        ReferenceEquals(_session, owner.Session) && ReferenceEquals(_campaignComposition, owner.Composition) &&
        ReferenceEquals(_residentMap, owner.Map) && ReferenceEquals(StoryOwner(owner.Session), owner.World) &&
        ReferenceEquals(PendingCampaignStoryForceRank(owner.Session), owner.Request);
    private bool NormalStoryForceRankInputAdmitted(NormalStoryForceRankWorkspace owner) =>
        ReferenceEquals(_normalStoryForceRank, owner) && IsNormalStoryForceRankCurrent && CanAdvanceNarration &&
        !_advancingPeriod && (_storyPresentation?.MediaRestoration.IsCompletedSuccessfully ?? true);

    private bool TryOpenNormalStoryForceRank(GameplaySession session, NormalPeriodOperation? pending,
        CampaignComposition composition, MapSceneView map)
    {
        if (_normalStoryForceRank is not null || PendingCampaignStoryForceRank(session) is not { } request ||
            StoryPlayback(session) is not { } playback) return false;
        //Restoring retained media must not pump the cursor before this input owner exists.
        bool restoreRetainedMedia = _storyPresentation is null &&
            TryOpenStoryPresentation(session, pending, composition, map, playback.Story, restoreMedia: false);
        var rules = composition.PrepareArmyQueries(StoryOwner(session));
        rules.ConfigurePersonnelNarrationVoice(_preferences.Voice.Capture());
        var owner = new NormalStoryForceRankWorkspace(session, composition, map, StoryOwner(session), rules, request);
        _normalStoryForceRank = owner;
        try
        {
            RetirePeriodReportView(); CloseActionReport(); map.CloseCityCommandMenu();
            map.RefreshPlayableWorld(owner.World); map.SetDetailsOpen(true, keepMapVisible: true);
            SetMapFocus(false); HideDeploymentHud();
            switch (request.Stage)
            {
                case CampaignStoryForceRankStage.KingdomName:
                {
                    var view = new KingdomSelectionView(); owner.View = view; AddChild(view);
                    view.Configure(_repository!, _ui,
                        rules.PrepareKingdomNameSelection(request.KingdomChoices),
                        () => NormalStoryForceRankInputAdmitted(owner),
                        selected => RunUiAction(() => SelectNormalStoryKingdomName(owner, selected)), CurrentListScrollSpeed);
                    view.Name = "NormalStoryKingdomSelection"; view.ZIndex = 100;
                    break;
                }
                case CampaignStoryForceRankStage.Messages:
                {
                    var message = request.Messages[request.NextMessage];
                    var view = new DeploymentNarrationView { Name = "NormalStoryRankMessage", ZIndex = 100 };
                    view.KeyboardInput = input => HandleNarrationKeyboard(input, view);
                    owner.View = view; AddChild(view);
                    var resources = TryBorrowApplicationUiResources(view);
                    void Complete() => RunUiAction(() => CompleteNormalStoryRankMessage(owner));
                    if (message.Speaker is null)
                        view.ConfigureAnnouncement(_repository!, _ui, message.Text, Complete,
                            () => _preferences.AutomaticMessageAdvance, () => NormalStoryForceRankInputAdmitted(owner),
                            resources: resources);
                    else
                    {
                        var query = rules.QueryPersonnelPresentationSpeaker(owner.World, message.Speaker);
                        var publication = query.Value ?? throw new InvalidDataException(query.UnresolvedInput);
                        var route = publication.StandingPortrait ?? throw new InvalidDataException(
                            publication.PortraitUnresolvedInput ?? "story.rank.speaker_portrait");
                        var sprites = new LocalizedUiSpriteSource(_repository!);
                        view.Configure(_repository!, _ui, publication.Name, message.Text,
                            route.LoadSprites(sprites.LoadTexturePackSprite), Complete,
                            () => _preferences.AutomaticMessageAdvance, () => NormalStoryForceRankInputAdmitted(owner),
                            resources: resources);
                    }
                    break;
                }
                default: throw new InvalidDataException("story.rank.input_stage");
            }
            if (restoreRetainedMedia && _storyPresentation is { } scene)
                StartStoryMediaRestoration(scene, playback.Story);
            return true;
        }
        catch { CloseNormalStoryForceRank(); throw; }
    }

    private void SelectNormalStoryKingdomName(NormalStoryForceRankWorkspace owner, int? selectedId)
    {
        if (!NormalStoryForceRankInputAdmitted(owner)) return;
        if (!owner.Session.TrySelectCampaignStoryKingdomName(owner.World, owner.Request.StorySequence, owner.Request, selectedId))
            throw new InvalidDataException("story.rank.kingdom_receipt_owner");
        ContinueNormalStoryForceRank(owner);
    }

    private void CompleteNormalStoryRankMessage(NormalStoryForceRankWorkspace owner)
    {
        if (!NormalStoryForceRankInputAdmitted(owner)) return;
        if (!owner.Session.TryCompleteCampaignStoryForceRankMessage(owner.World, owner.Request.StorySequence, owner.Request))
            throw new InvalidDataException("story.rank.message_receipt_owner");
        ContinueNormalStoryForceRank(owner);
    }

    private void ContinueNormalStoryForceRank(NormalStoryForceRankWorkspace owner)
    {
        if (_storyPresentation is { } scene && ReferenceEquals(scene.Session, owner.Session))
            scene.World = StoryOwner(owner.Session);
        CloseNormalStoryForceRank();
        if (PendingCampaignStoryForceRank(owner.Session) is not null)
        {
            //Do not let the input consumed by one page confirm its successor.
            Callable.From(() => RunUiAction(() => RefreshNormalTurnContinuation())).CallDeferred();
        }
        else ContinueNormalStory(owner.Session, owner.Composition);
    }

    private bool HandleNormalStoryForceRankInput(InputEvent input)
    {
        if (_normalStoryForceRank is not { View: KingdomSelectionView view } owner ||
            !NormalStoryForceRankInputAdmitted(owner) || input is not
                (InputEventKey { Keycode: Key.Escape } or InputEventMouseButton { ButtonIndex: MouseButton.Right })) return false;
        if (input is InputEventKey { Pressed: true, Echo: false } or InputEventMouseButton { Pressed: false }) view.RequestBack();
        return true;
    }

    private void CloseNormalStoryForceRank()
    {
        var owner = _normalStoryForceRank; _normalStoryForceRank = null;
        if (owner is null) return;
        if (owner.View is KingdomSelectionView kingdom) kingdom.Abandon();
        if (owner.View is DeploymentNarrationView message) message.Abandon();
        if (owner.View is { } view) { view.GetParent()?.RemoveChild(view); view.QueueFree(); }
        if (GodotObject.IsInstanceValid(owner.Map) && ReferenceEquals(_residentMap, owner.Map))
            owner.Map.SetDetailsOpen(IsStoryPresentationCurrent, keepMapVisible: true);
    }
}
