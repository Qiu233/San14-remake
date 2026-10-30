using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class CampaignEventCommandWorkspace(GameplaySession session,
        CampaignComposition composition, MapSceneView map, ArmyCampaignRuleAdapter rules,
        CampaignEventCommandView view, bool mapFocused)
    {
        internal readonly GameplaySession Session = session;
        internal readonly CampaignComposition Composition = composition;
        internal readonly MapSceneView Map = map;
        internal readonly ArmyCampaignRuleAdapter Rules = rules;
        internal readonly GameState World = session.World;
        internal readonly CampaignEventCommandView View = view;
        internal readonly bool MapFocused = mapFocused;
        internal DeploymentTargetConfirmation? Question;
    }
    private CampaignEventCommandWorkspace? _campaignEventCommandWorkspace;
    private bool _campaignEventCommandReturnToList;
    private bool _campaignEventCommandMapFocusedAtEntry = true;
    private CampaignEventCommandViewState? _campaignEventCommandRetainedState;
    private bool HasCampaignEventCommandPage => _campaignEventCommandWorkspace is not null;

    private bool IsCampaignEventCommandCurrent(CampaignEventCommandWorkspace owner) =>
        ReferenceEquals(_campaignEventCommandWorkspace, owner) && ReferenceEquals(_session, owner.Session) &&
        ReferenceEquals(owner.Session.World, owner.World) && ReferenceEquals(_residentMap, owner.Map) &&
        ReferenceEquals(_campaignComposition, owner.Composition) && !_failed && !_shuttingDown && !IsReturningToTitle;

    private void OpenCampaignEventCommand() => OpenCampaignEventCommand(null);

    private void OpenCampaignEventCommand(bool? mapFocusOnClose,CampaignEventCommandViewState? retained=null)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || _repository is null ||
            HasCampaignEventCommandPage || HasAdministrationPage || HasMilitaryOrderPage || HasCampaignSavePage ||
            HasEnvironmentSettings || HasMapSystemMenu || HasCommandWorkspace || HasPersonnelConfirmation ||
            _detailsView is not null || HasActionReport ||
            _session is not { PlayerForceId: int player } session ||
            _campaignComposition is not { } composition || _residentMap is not { IsPlayableMapReady: true } map) return;
        if (session.PendingCampaignEventCommand is not null)
        {
            _automaticCampaignEventCommandAttempt = null;
            RefreshNormalTurnContinuation(); return;
        }
        if (session.HasPendingOperation || session.World.Phase != TurnPhase.Strategy) return;
        var rules = composition.PrepareArmyQueries(session.World);
        var catalog = rules.ReadCampaignEventCommandCatalog(session.World);
        var view = new CampaignEventCommandView();
        var owner = new CampaignEventCommandWorkspace(session, composition, map, rules, view, mapFocusOnClose ?? _mapFocused);
        _campaignEventCommandWorkspace = owner; AddChild(view);
        try
        {
            var sprites = new LocalizedUiSpriteSource(_repository);
            var people = new Dictionary<int, CampaignEventCommandPerson?>();
            CampaignEventCommandPerson? Person(int id)
            {
                if (people.TryGetValue(id, out var cached)) return cached;
                var query = rules.QueryOfficerDetail(owner.World, player, id);
                CampaignEventCommandPerson? result = null;
                if (query.Available && query.Value is { } person)
                {
                    var portrait = person.Portrait;
                    var face = portrait.ResourceName is { } resource && portrait.SpriteName is { } sprite
                        ? sprites.LoadTexturePackSprite(resource, sprite) : null;
                    result = new(id, person.Name, face);
                }
                people.Add(id, result); return result;
            }
            view.Configure(_repository, _ui, catalog,
                () => IsCampaignEventCommandCurrent(owner) && owner.Question is null && !_advancingPeriod,
                id => RunUiAction(() => RequestCampaignEventCommand(owner, id)),
                () => RunUiAction(CloseCampaignEventCommand), CurrentListScrollSpeed, RunUiAction, Person);
            view.RegisterHeader(_sourceHeaders);
            view.CategorySelected+=category=>
            {
                if(!IsCampaignEventCommandCurrent(owner)||_advancingPeriod||owner.Question is not null)return;
                if(rules.MarkMainCommandEventCategorySeen(owner.World,category))RefreshMainCommandPresentation();
            };
            if(retained is not null)view.RestoreState(retained);
            map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true);
            SetMapFocus(false); HideDeploymentHud();
        }
        catch { CloseCampaignEventCommand(); throw; }
    }

    private void RequestCampaignEventCommand(CampaignEventCommandWorkspace owner, int eventId)
    {
        if (!IsCampaignEventCommandCurrent(owner) || owner.Question is not null || _advancingPeriod) return;
        if (eventId != 900) { Begin(); return; }
        var question = new DeploymentTargetConfirmation(); owner.Question = question; AddChild(question);
        owner.View.SetBusy(true);
        try
        {
            question.ConfigureGameMessage(_repository!, _ui, 0x87F2, confirmed => RunUiAction(() =>
            {
                if (!IsCampaignEventCommandCurrent(owner) || owner.Question != question) return;
                owner.Question = null; question.Abandon(); question.QueueFree(); owner.View.SetBusy(false);
                if (confirmed) Begin();
            }));
            question.Name = "CampaignEventCommandConfirmation";
        }
        catch
        { owner.Question = null; question.Abandon(); question.QueueFree(); owner.View.SetBusy(false); throw; }

        void Begin()
        {
            if (!IsCampaignEventCommandCurrent(owner) || _advancingPeriod) return;
            // The worker re-evaluates actual eligibility against this exact
            // world. The displayed catalog never acts as execution authority.
            _campaignEventCommandRetainedState=owner.View.CaptureState();
            CloseCampaignEventCommand();
            _campaignEventCommandMapFocusedAtEntry = owner.MapFocused;
            _campaignEventCommandReturnToList = true;
            _advancingPeriod = true;
            _periodAdvanceTask = RunCampaignEventCommandAsync(owner.Session, owner.Composition, eventId);
        }
    }

    private async Task RunCampaignEventCommandAsync(GameplaySession source, CampaignComposition composition,
        int? eventId = null, ConfirmRulerSuccessionOrder? confirmation = null)
    {
        var cancellation = new CancellationTokenSource();
        _periodAdvanceCancellation = cancellation;
        var map = _residentMap;
        GameplaySession? adopted = null;
        Exception? applicationFailure = null;
        try
        {
            var bindings = CaptureNormalTurnStoryBindings(source, composition, map, cancellation.Token);
            if (map?.SourceMainCameraTerrain is { } terrain) composition.ConfigureBaseCapturePresentationTerrain(terrain);
            var voice = _preferences.Voice.Capture();
            var result = eventId is int selected
                ? await composition.BeginCampaignEventCommandAsync(source, selected, cancellation.Token,
                    new(_presentationRandom.State), voice, bindings)
                : await composition.ContinueCampaignEventCommandAsync(source, cancellation.Token,
                    new(_presentationRandom.State), voice, bindings, confirmation);
            if (_failed || _shuttingDown || !GodotObject.IsInstanceValid(this) || !IsInsideTree() ||
                !result.IsCurrent(_session) || !ReferenceEquals(_residentMap, map)) return;
            _session = result.Session;
            AdoptStoryPresentationSession(source, result.Session, result.Composition, map);
            adopted = result.Session;
            _sceneAudioBinding?.AdoptPlayableSession(source, result.Session);
            _campaignComposition = result.Composition;
            SynchronizeApplicationCrtRandom(StoryOwner(result.Session));
            if (StoryOwner(result.Session).CampaignAppRandom32 is { } random)
                _presentationRandom.InstallCommittedState(random.State);
            try { result.Composition.PublishApplicationServices(StoryOwner(result.Session)); }
            catch (Exception error) { applicationFailure = error; }
            _loadedMap = result.Composition.Map; _map = _loadedMap.Map; _mapOverview = result.Composition.Overview;
            PublishCampaignPresentation(result.Presentation); _playMessage.Text = "";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (eventId is not null && adopted is null) _campaignEventCommandReturnToList = false;
            if (!_shuttingDown && GodotObject.IsInstanceValid(this) && IsInsideTree() && ReferenceEquals(_session, source))
                ReportPlayFailure(error);
        }
        finally
        {
            bool owns = ReferenceEquals(_periodAdvanceCancellation, cancellation);
            if (owns) { _advancingPeriod = false; _periodAdvanceCancellation = null; }
            cancellation.Dispose();
            if (owns && ReferenceEquals(_session, adopted ?? source) && GodotObject.IsInstanceValid(this) &&
                IsInsideTree() && !_shuttingDown && !_failed)
                RunUiAction(() =>
                {
                    RefreshCurrentPlayWorld();
                    if (applicationFailure is not null) ReportPlayFailure(applicationFailure);
                });
        }
    }

    private bool TryReturnToCampaignEventCommandList(GameplaySession session)
    {
        if (!_campaignEventCommandReturnToList || session.HasPendingOperation ||
            session.World.EndingProgress is not null || session.World.NativePlayableTerminal is not null ||
            session.World.CampaignTurnStop is not null || IsReturningToTitle) return false;
        OpenCampaignEventCommand(_campaignEventCommandMapFocusedAtEntry,_campaignEventCommandRetainedState);
        if (!HasCampaignEventCommandPage) return false;
        _campaignEventCommandReturnToList = false; _campaignEventCommandRetainedState=null;
        return true;
    }

    private void RequestCampaignEventCommandBack()
    {
        if (_campaignEventCommandWorkspace is not { } owner || !IsCampaignEventCommandCurrent(owner)) return;
        if (owner.Question is { } question) question.RequestCancel();
        else owner.View.RequestBack();
    }

    private void CloseCampaignEventCommand()
    {
        var owner = _campaignEventCommandWorkspace; _campaignEventCommandWorkspace = null;
        if (owner is null) return;
        if (owner.Question is { } question) { question.Abandon(); question.GetParent()?.RemoveChild(question); question.QueueFree(); }
        owner.View.Abandon(); owner.View.GetParent()?.RemoveChild(owner.View); owner.View.QueueFree();
        if (GodotObject.IsInstanceValid(owner.Map) && ReferenceEquals(_residentMap, owner.Map))
        {
            owner.Map.SetDetailsOpen(false);
            if (!_failed && !_shuttingDown) SetMapFocus(owner.MapFocused);
        }
    }
}
