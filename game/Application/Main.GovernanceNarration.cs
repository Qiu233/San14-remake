using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private DeploymentNarrationView? _governanceNarration;
    private GameplaySession? _governanceNarrationSession;
    private GameState? _governanceNarrationWorld;
    private MapSceneView? _governanceNarrationMap;

    private Action<GameState>? _governanceNarrationAdoptSpeechWorld;

    private void AdoptGovernanceNarrationSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        if (_governanceNarration is null || _governanceNarrationSession != session ||
            _governanceNarrationMap != _residentMap || !ReferenceEquals(_governanceNarrationWorld, previous)) return;
        _governanceNarrationWorld = replacement;
        _governanceNarrationAdoptSpeechWorld?.Invoke(replacement);
    }

    private void TryOpenGovernanceNarration()
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasAdministrationPage ||
            HasMilitaryOrderPage || HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace || HasActionReport ||
            HasPersonnelConfirmation || _detailsView is not null || _repository is null || _campaignComposition is not { } composition ||
            _residentMap is not { } map || !map.InitialSceneLoad.IsCompletedSuccessfully ||
            !map.GetMeta("scene_loaded", false).AsBool() ||
            _session is not { PlayerForceId: int player } session || session.World is not { Phase: TurnPhase.Strategy } world ||
            !ReferenceEquals(world, _world)) return;
        var batch = world.CampaignEffectRequests?.PendingBatches.FirstOrDefault(row =>
            row.Source == CampaignEffectSource.GovernanceRoster && row.Requests.Length > 0 &&
            row.Requests.All(request => request.GovernanceNarration?.ForceId == player));
        if (batch is null) return;
        var rules = composition.PrepareArmyQueries(world);
        _governanceNarrationAdoptSpeechWorld = next => world = next;
        int index = 0;
        ShowNext();

        bool Current() => !_failed && !_shuttingDown && !IsReturningToTitle &&
            ReferenceEquals(_session, session) && ReferenceEquals(_world, world) &&
            ReferenceEquals(session.World, world) && _residentMap == map;

        void ShowNext()
        {
            if (!Current()) return;
            if (index == batch.Requests.Length)
            {
                if (ApplyPlayableOrder(new CompleteGovernanceNarrationOrder(player, batch.Sequence), CloseGovernanceNarration))
                    TryOpenGovernanceNarration();
                else CloseGovernanceNarration();
                return;
            }
            var response = batch.Requests[index].GovernanceNarration!;
            var portrait = rules.QueryGovernanceNarrationPortrait(world, response.SpeakerId);
            if (!portrait.Available || portrait.Value is not { } route)
            {
                CloseGovernanceNarration();
                _mapHint.Text = portrait.UnresolvedInput ?? _ui.Get("detail_unknown");
                return;
            }
            var view = new DeploymentNarrationView { KeyboardInput = HandleGameplayNarrationKeyboard };
            _governanceNarration = view; _governanceNarrationSession = session;
            _governanceNarrationWorld = world; _governanceNarrationMap = map;
            AddChild(view);
            try
            {
                var resources=TryBorrowApplicationUiResources(view)??throw new InvalidOperationException("Narration requires its prepared application resources.");
                var layers=route.LoadSources(resources.Assets.Portraits.RequireSource);
                view.Configure(_repository!, _ui, world.Officers[response.SpeakerId].Name, response.TextAtSource,
                    layers,resources, () => RunPlayAction(() =>
                    {
                        if (_governanceNarration != view) return;
                        if (!Current()) { CloseGovernanceNarration(); return; }
                        // The view consumes a drawn page through its manual or automatic wait.
                        _governanceNarration = null;
                        view.Abandon(); view.GetParent()?.RemoveChild(view); view.QueueFree();
                        index++; ShowNext();
                    }), () => _preferences.AutomaticMessageAdvance,
                () => CanAdvanceNarration&&_governanceNarration==view&&Current());
                view.Name = "GovernanceNarration";
                SetMapFocus(true); map.SetDetailsOpen(true, keepMapVisible: true); HideDeploymentHud();
            }
            catch { CloseGovernanceNarration(); throw; }
        }
    }

    private void CloseGovernanceNarration()
    {
        var view = _governanceNarration; var map = _governanceNarrationMap;
        _governanceNarrationAdoptSpeechWorld = null;
        _governanceNarration = null; _governanceNarrationSession = null;
        _governanceNarrationWorld = null; _governanceNarrationMap = null;
        if (view is not null)
        { view.Abandon(); view.GetParent()?.RemoveChild(view); view.QueueFree(); }
        if (map is not null && map == _residentMap && GodotObject.IsInstanceValid(map)) map.SetDetailsOpen(false);
        if (!_failed && !_shuttingDown && map is not null && map == _residentMap) SetMapFocus(true);
    }
}
