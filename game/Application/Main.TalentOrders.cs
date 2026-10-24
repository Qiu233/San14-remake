using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private TalentOrdersView? _talentOrdersView;
    private TalentPersonSelectionView? _talentPersons;
    private TalentDestinationSelectionView? _talentDestinations;
    private DeploymentTargetConfirmation? _talentQuestion;
    private TalentUiDraft? _talentUiDraft;
    private GameplaySession? _talentUiSession;
    private GameState? _talentUiWorld;
    private MapSceneView? _talentUiMap;
    private TalentOrderKind _talentUiKind;
    private AreaAdministrationOfficerContent? _talentCardContent;
    private bool HasTalentOrdersPage => _talentOrdersView is not null;
    private bool IsTalentOrdersCurrent => !_failed && !_shuttingDown && _talentOrdersView is not null &&
        _talentUiDraft is not null && ReferenceEquals(_session, _talentUiSession) && ReferenceEquals(_world, _talentUiWorld) &&
        _residentMap == _talentUiMap && _talentUiMap is not null;
    private bool CanEditTalentOrders => IsTalentOrdersCurrent && _talentPersons is null && _talentDestinations is null && _talentQuestion is null;

    private void OpenTalentOrders(TalentOrderKind kind)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasAdministrationPage || HasMilitaryOrderPage ||
            HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace || HasActionReport || HasPersonnelConfirmation ||
            _detailsView is not null || _repository is null || _campaignComposition is not { } composition ||
            _session is not { PlayerForceId: int player } session || session.HasPendingOperation ||
            session.World is not { Phase: TurnPhase.Strategy, NativeArmySimulation: not null } world ||
            _residentMap is not { IsPlayableMapReady: true } map) return;
        var district = world.Districts.Values.SingleOrDefault(value => value.ForceId == player && value.Number == 1);
        if (district is null) { _playMessage.Text = _ui.Get("detail_unknown"); return; }
        TalentUiDraft draft;
        try
        {
            draft = composition.PrepareArmyQueries(world).OpenTalentUiDraft(world, player, district.Id,
                kind == TalentOrderKind.Recruit, new(_presentationRandom.State));
        }
        catch (InvalidDataException error)
        { _playMessage.Text = _ui.Get("talent_rule_unavailable"); GD.PushWarning(error.ToString()); return; }
        _talentUiDraft = draft; _talentUiSession = session; _talentUiWorld = world; _talentUiMap = map; _talentUiKind = kind;
        _talentCardContent = new(_repository);
        var view = new TalentOrdersView(); _talentOrdersView = view; AddChild(view);
        try
        {
            SetMapFocus(true); map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true); HideDeploymentHud();
            view.Configure(_repository, _ui, kind, () => CanEditTalentOrders,
                () => RunUiAction(RequestTalentOrdersBack), () => RunPlayAction(()=>ConfirmTalentOrders()),
                () => RunPlayAction(()=>ConfirmTalentOrders(keepOpen:true)),
                () => RunUiAction(OpenTalentTargetSelection), () => RunUiAction(OpenTalentExecutorSelection),
                mode=>RunUiAction(()=>{if(CanEditTalentOrders)ChangeTalentDraft(()=>_talentUiDraft!.SetRecruitMode(mode));}),
                ()=>RunUiAction(()=>{if(CanEditTalentOrders&&_talentUiKind==TalentOrderKind.Search){CloseTalentOrders();OpenTalentAutoSearch();}}));
            RefreshTalentOrders();
        }
        catch { CloseTalentOrders(); throw; }
    }

    private void RefreshTalentOrders()
    {
        if (!IsTalentOrdersCurrent) return;
        var draft = _talentUiDraft!;
        int? actor = draft.SelectedExecutorOfficerIds.Count > 0 ? draft.SelectedExecutorOfficerIds[0] : null;
        var executor = TalentOfficerCard(actor, _ui.Get("talent_choose_executors"));
        bool recruitment = _talentUiKind == TalentOrderKind.Recruit;
        var target = recruitment ? TalentOfficerCard(draft.TargetOfficerId, _ui.Get("talent_choose_target")) : null;
        string destination = recruitment
            ? draft.TargetOfficerId is int person && _talentUiWorld!.Officers.GetValueOrDefault(person)?.LocationId is int location
                ? _talentUiWorld.Cities.GetValueOrDefault(location)?.Name ?? _talentUiWorld.Gates.GetValueOrDefault(location)?.Name ?? _ui.Get("detail_unknown")
                : _ui.Get("detail_unknown")
            : draft.Destinations.FirstOrDefault(value => value.CityId == draft.CurrentDestinationCityId)?.Name ?? _ui.Get("talent_choose_destination");
        bool ready = draft.CanSubmit;
        _talentOrdersView!.Apply(new(destination, target, executor, draft.SelectedExecutorOfficerIds.Count, draft.AvailableBooks,
            draft.RequiredBooks, draft.TravelDays, ready, draft.ExecutorOfficerIds.Count > 0 && draft.MaximumExecutors > 0,
            draft.RecruitMode, draft.CanChooseRecruitMode,
            draft.UnresolvedInput is not null ? _ui.Get("talent_rule_unavailable") : draft.AdviceText,
            draft.AdvisorOfficerId is int advisor ? TalentOfficerCard(advisor,"").Portrait : null));
        _talentOrdersView.SetBusy(!CanEditTalentOrders);
        if (draft.UnresolvedInput is { } missing) GD.PushWarning(missing);
    }

    private AreaAdministrationOfficerValues TalentOfficerCard(int? officer, string emptyName)
    {
        if (officer is not int id) return new(emptyName, null, null, null, null);
        var card = _talentUiDraft!.QueryOfficerCard(id);
        if (!card.Available) GD.PushWarning(card.UnresolvedInput);
        return card.Available ? _talentCardContent!.Create(card.Value!)
            : new(_talentUiWorld!.Officers[id].Name, null, null, null, null);
    }

    private void OpenTalentTargetSelection()
    {
        if (!CanEditTalentOrders || _repository is null) return;
        var draft = _talentUiDraft!;
        if (_talentUiKind == TalentOrderKind.Recruit)
        {
            OpenTalentPersonSelection(draft.TargetOfficerIds, draft.TargetOfficerId is int target ? [target] : [],
                1, true, chosen => { if (chosen.Count == 1) draft.SelectRecruitTarget(chosen[0]); });
            return;
        }
        var selector = new TalentDestinationSelectionView(); _talentDestinations = selector; AddChild(selector);
        _talentOrdersView!.SetBusy(true); _talentOrdersView.Hide();
        try
        {
            selector.Configure(_repository, _ui, draft.Destinations, draft.CurrentDestinationCityId,
                () => IsTalentOrdersCurrent && _talentDestinations == selector, chosen => RunUiAction(() =>
                {
                    if (!IsTalentOrdersCurrent || _talentDestinations != selector || _talentUiDraft != draft) return;
                    _talentDestinations = null; selector.Abandon(); selector.QueueFree(); _talentOrdersView!.Show();
                    if (chosen is int city) ChangeTalentDraft(() => draft.SelectDestination(city));
                    else RefreshTalentOrders();
                }), CurrentListScrollSpeed);
        }
        catch
        { _talentDestinations = null; selector.Abandon(); selector.QueueFree(); _talentOrdersView!.Show(); RefreshTalentOrders(); throw; }
    }

    private void OpenTalentExecutorSelection()
    {
        if (!CanEditTalentOrders) return;
        var draft = _talentUiDraft!;
        OpenTalentPersonSelection(draft.ExecutorOfficerIds, draft.SelectedExecutorOfficerIds,
            draft.MaximumExecutors, false, chosen => draft.SelectExecutors(chosen));
    }

    private void OpenTalentPersonSelection(IReadOnlyList<int> ids, IReadOnlyList<int> initial, int maximum, bool single,
        Action<IReadOnlyList<int>> selected)
    {
        if (!CanEditTalentOrders || _repository is null) return;
        var world = _talentUiWorld!;
        var choices = ids.Select(id =>
        {
            var person = world.Officers[id];
            var card = _talentUiDraft!.QueryOfficerCard(id);
            int? Ability(int dimension) => card.Available
                ? card.Value!.Abilities.FirstOrDefault(value => value.Dimension == dimension)?.Effective.Value : null;
            string location = person.LocationId is int city
                ? world.Cities.GetValueOrDefault(city)?.Name ?? world.Gates.GetValueOrDefault(city)?.Name ?? _ui.Get("detail_unknown")
                : _ui.Get("detail_unknown");
            return new TalentPersonChoice(id, person.Name, location,
                person.ForceId is int force && world.Forces.TryGetValue(force, out var faction) ? faction.Name : _ui.Get("detail_unknown"),
                Ability(2), Ability(3), Ability(4));
        }).ToArray();
        var selector = new TalentPersonSelectionView(); _talentPersons = selector; AddChild(selector);
        _talentOrdersView!.SetBusy(true); _talentOrdersView.Hide();
        try
        {
            selector.Configure(_repository, _ui, choices, initial, maximum, single,
                () => IsTalentOrdersCurrent && _talentPersons == selector, chosen => RunUiAction(() =>
                {
                    if (!IsTalentOrdersCurrent || _talentPersons != selector) return;
                    _talentPersons = null; selector.Abandon(); selector.QueueFree(); _talentOrdersView!.Show();
                    if (chosen is not null) ChangeTalentDraft(() => selected(chosen)); else RefreshTalentOrders();
                }), CurrentListScrollSpeed);
        }
        catch
        { _talentPersons = null; selector.Abandon(); selector.QueueFree(); _talentOrdersView!.Show(); RefreshTalentOrders(); throw; }
    }

    private void ChangeTalentDraft(Action action)
    {
        try
        {
            action();
            if (_talentUiDraft?.AppRandomState is { } random) _presentationRandom.InstallCommittedState(random.State);
        }
        catch (InvalidDataException error) { GD.PushWarning(error.ToString()); }
        RefreshTalentOrders();
    }

    private void ConfirmTalentOrders(bool keepOpen=false)
    {
        if (!CanEditTalentOrders || _talentUiSession?.PlayerForceId is not int player) return;
        var draft = _talentUiDraft!; var view = _talentOrdersView!;
        var session=_talentUiSession;var kind=_talentUiKind;
        view.SetBusy(true);
        var order = new PrepareTalentBatchOrder(player, _talentUiKind, draft.Assignments.ToArray());
        try
        {
            if (!ApplyPlayableOrder(order, CloseTalentOrders))
            { RefreshTalentOrders(); view.ApplyNotice(_playMessage.Text); }
            else if(keepOpen&&ReferenceEquals(_session,session)) OpenTalentOrders(kind);
        }
        catch { if (IsTalentOrdersCurrent) RefreshTalentOrders(); throw; }
    }

    private void RequestTalentOrdersBack()
    {
        if (!IsTalentOrdersCurrent) return;
        if (_talentQuestion is { } question) { question.RequestCancel(); return; }
        if (_talentPersons is { } persons) { persons.RequestCancel(); return; }
        if (_talentDestinations is { } destinations) { destinations.RequestCancel(); return; }
        if (_talentUiDraft!.SelectedExecutorOfficerIds.Count == 0) { CloseTalentOrders(); return; }
        var view = _talentOrdersView!;
        var prompt = new DeploymentTargetConfirmation(); _talentQuestion = prompt; AddChild(prompt); view.SetBusy(true);
        try
        {
            prompt.ConfigureWrappedMessage(_repository!, _ui, _ui.Get("talent_discard_confirm"), accepted => RunUiAction(() =>
            {
                if (!IsTalentOrdersCurrent || _talentQuestion != prompt) return;
                _talentQuestion = null; prompt.Abandon(); prompt.QueueFree();
                if (accepted) CloseTalentOrders(); else RefreshTalentOrders();
            }));
        }
        catch { _talentQuestion = null; prompt.Abandon(); prompt.QueueFree(); RefreshTalentOrders(); throw; }
    }

    private void CloseTalentOrders()
    {
        if (_talentOrdersView is null && _talentUiDraft is null && _talentUiMap is null) return;
        var map = _talentUiMap; _talentUiMap = null; _talentUiWorld = null; _talentUiSession = null;
        _talentUiDraft = null; _talentCardContent = null;
        var persons = _talentPersons; _talentPersons = null;
        var destinations = _talentDestinations; _talentDestinations = null;
        var question = _talentQuestion; _talentQuestion = null;
        var view = _talentOrdersView; _talentOrdersView = null;
        persons?.Abandon(); persons?.QueueFree(); destinations?.Abandon(); destinations?.QueueFree();
        question?.Abandon(); question?.QueueFree(); view?.Abandon(); view?.QueueFree();
        if (map is not null && GodotObject.IsInstanceValid(map) && _residentMap == map) map.SetDetailsOpen(false);
        if (!_failed && !_shuttingDown && _residentMap is not null) SetMapFocus(_mapFocused);
    }
}
