using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private TalentOrdersView? _talentOrdersView;
    private Control? _talentPersons;
    private TalentDestinationSelectionView? _talentDestinations;
    private DeploymentTargetConfirmation? _talentQuestion;
    private TalentUiDraft? _talentUiDraft;
    private GameplaySession? _talentUiSession;
    private GameState? _talentUiWorld;
    private MapSceneView? _talentUiMap;
    private TalentOrderKind _talentUiKind;
    private AreaAdministrationOfficerContent? _talentCardContent;
    private int? TalentSearchCurrentPage => !IsTalentOrdersCurrent || _talentUiKind != TalentOrderKind.Search ? null
        : _talentDestinations?.IsVisibleInTree() == true ? 51
        : _talentPersons?.IsVisibleInTree() == true ? 52
        : _talentOrdersView?.IsVisibleInTree() == true ? 50 : null;
    private int? TalentSearchSelectedCityId => TalentSearchCurrentPage == 51
        ? _talentDestinations!.SelectedCityId : _talentUiDraft?.CurrentDestinationCityId;
    private IReadOnlyList<int> TalentSearchSelectedPersonIds => TalentSearchCurrentPage == 52
        ? _talentPersons switch
        {
            TalentSearchPersonSelectionView search => search.SelectedOfficerIds,
            TalentRecruitExecutorSelectionView recruit => recruit.SelectedOfficerIds,
            _ => []
        } : _talentUiDraft?.SelectedExecutorOfficerIds ?? [];
    private Func<int, bool>? TalentSearchCityAdmission { get; set; }
    private Func<int, bool>? TalentSearchPersonAdmission { get; set; }
    private bool TalentSearchCityReturnAllowed { get; set; } = true;
    private bool TalentSearchPersonReturnAllowed { get; set; } = true;
    private long TalentSearchCityAcceptedSequence { get; set; }
    private long TalentSearchPersonAcceptedSequence { get; set; }
    private long TalentSearchAcceptedSequence { get; set; }
    private int? TalentSearchLastAcceptedCityId { get; set; }
    private IReadOnlyList<int> TalentSearchLastAcceptedPersonIds { get; set; } = [];
    private void BindTalentSearchSelectionAdmission()
    {
        if (_talentUiKind != TalentOrderKind.Search) return;
        _talentDestinations?.ApplySelectionAdmission(TalentSearchCityAdmission, TalentSearchCityReturnAllowed);
        if (_talentPersons is TalentSearchPersonSelectionView search)
            search.ApplySelectionAdmission(TalentSearchPersonAdmission, TalentSearchPersonReturnAllowed);
    }
    private bool HasTalentOrdersPage => _talentOrdersView is not null;
    private bool IsTalentOrdersCurrent => !_failed && !_shuttingDown && _talentOrdersView is not null &&
        _talentUiDraft is not null && ReferenceEquals(_session, _talentUiSession) && ReferenceEquals(_world, _talentUiWorld) &&
        _residentMap == _talentUiMap && _talentUiMap is not null;
    private void AdoptTalentOrdersSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        // The common speech entry admits the committed successor. Keep the
        // captured draft, current selectors and pending confirmation intact.
        if (_talentOrdersView is null || _talentUiDraft is null || _talentUiSession != session ||
            _talentUiMap is null || _talentUiMap != _residentMap || !ReferenceEquals(_talentUiWorld, previous) ||
            !_talentUiDraft.TryAdoptPresentationWorld(previous, replacement, new(_presentationRandom.State))) return;
        _talentUiWorld = replacement;
    }

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
            if (kind == TalentOrderKind.Search) ObserveTutorialInformation(19);
            BindCurrentTutorialTalentSearch();
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
            draft.RequiredBooks, draft.TravelDays, ready, recruitment ? draft.CanPrepareRecruitExecutorSelection : draft.CanPrepareExecutorSelection,
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
            OpenTalentRecruitTargetSelection();
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
                    if (chosen is int city)
                    {
                        if (ChangeTalentDraft(() => draft.SelectDestination(city)))
                        {
                            TalentSearchLastAcceptedCityId = city;
                            TalentSearchCityAcceptedSequence++;
                        }
                    }
                    else RefreshTalentOrders();
                }), CurrentListScrollSpeed);
            BindCurrentTutorialTalentSearch();
            BindTalentSearchSelectionAdmission();
        }
        catch
        { _talentDestinations = null; selector.Abandon(); selector.QueueFree(); _talentOrdersView!.Show(); RefreshTalentOrders(); throw; }
    }

    private void OpenTalentExecutorSelection()
    {
        if (!CanEditTalentOrders) return;
        var draft = _talentUiDraft!;
        if (_talentUiKind == TalentOrderKind.Search) { OpenTalentSearchPersonSelection(); return; }
        OpenTalentRecruitExecutorSelection();
    }

    private void OpenTalentRecruitTargetSelection()
    {
        if (!CanEditTalentOrders || _repository is null || _talentUiKind != TalentOrderKind.Recruit) return;
        var draft = _talentUiDraft!;
        TalentRecruitPersonSelection model;
        try { model = _campaignComposition!.PrepareArmyQueries(_talentUiWorld!).PrepareTalentRecruitTargetSelection(draft); }
        catch (InvalidDataException error)
        { GD.PushWarning(error.ToString()); _talentOrdersView!.ApplyNotice(_ui.Get("talent_rule_unavailable")); return; }
        var selector = new TalentRecruitTargetSelectionView(); _talentPersons = selector; AddChild(selector);
        _talentOrdersView!.SetBusy(true); _talentOrdersView.Hide();
        try
        {
            selector.Configure(_repository, _ui, model,
                () => IsTalentOrdersCurrent && _talentPersons == selector, chosen => RunUiAction(() =>
                {
                    if (!IsTalentOrdersCurrent || _talentPersons != selector || _talentUiDraft != draft) return;
                    _talentPersons = null; selector.Abandon(); selector.QueueFree(); _talentOrdersView!.Show();
                    if (chosen is int target) ChangeTalentDraft(() => draft.SelectRecruitTarget(target));
                    else RefreshTalentOrders();
                }), CurrentListScrollSpeed);
        }
        catch
        { _talentPersons = null; selector.Abandon(); selector.QueueFree(); _talentOrdersView!.Show(); RefreshTalentOrders(); throw; }
    }

    private void OpenTalentRecruitExecutorSelection()
    {
        if (!CanEditTalentOrders || _repository is null || _talentUiKind != TalentOrderKind.Recruit || _talentUiDraft?.CanPrepareRecruitExecutorSelection != true) return;
        var draft = _talentUiDraft!;
        try { if (!draft.PrepareRecruitExecutorSelection()) return; }
        catch (InvalidDataException error)
        { GD.PushWarning(error.ToString()); RefreshTalentOrders(); _talentOrdersView!.ApplyNotice(_ui.Get("talent_rule_unavailable")); return; }
        finally
        { if (draft.AppRandomState is { } random) _presentationRandom.InstallCommittedState(random.State); }
        TalentRecruitPersonSelection model;
        try { model = _campaignComposition!.PrepareArmyQueries(_talentUiWorld!).PrepareTalentRecruitExecutorSelection(draft); }
        catch (InvalidDataException error)
        { GD.PushWarning(error.ToString()); _talentOrdersView!.ApplyNotice(_ui.Get("talent_rule_unavailable")); return; }
        var selector = new TalentRecruitExecutorSelectionView(); _talentPersons = selector; AddChild(selector);
        _talentOrdersView!.SetBusy(true); _talentOrdersView.Hide();
        try
        {
            selector.Configure(_repository, _ui, model,
                () => IsTalentOrdersCurrent && _talentPersons == selector, chosen => RunUiAction(() =>
                {
                    if (!IsTalentOrdersCurrent || _talentPersons != selector || _talentUiDraft != draft) return;
                    _talentPersons = null; selector.Abandon(); selector.QueueFree(); _talentOrdersView!.Show();
                    if (chosen is not null) ChangeTalentDraft(() => draft.ApplyRecruitExecutorSelection(chosen));
                    else RefreshTalentOrders();
                }), CurrentListScrollSpeed);
        }
        catch
        { _talentPersons = null; selector.Abandon(); selector.QueueFree(); _talentOrdersView!.Show(); RefreshTalentOrders(); throw; }
    }

    private void OpenTalentSearchPersonSelection()
    {
        if (!CanEditTalentOrders || _repository is null || _talentUiKind != TalentOrderKind.Search || _talentUiDraft?.CanPrepareExecutorSelection != true) return;
        var draft = _talentUiDraft!;
        try { if (!draft.PrepareExecutorSelection()) return; }
        catch (InvalidDataException error)
        { GD.PushWarning(error.ToString()); _talentOrdersView!.ApplyNotice(_ui.Get("talent_rule_unavailable")); return; }
        finally
        { if (draft.AppRandomState is { } random) _presentationRandom.InstallCommittedState(random.State); }
        TalentSearchPersonSelection model;
        try { model = _campaignComposition!.PrepareArmyQueries(_talentUiWorld!).PrepareTalentSearchPersonSelection(draft); }
        catch (InvalidDataException error)
        { GD.PushWarning(error.ToString()); _talentOrdersView!.ApplyNotice(_ui.Get("talent_rule_unavailable")); return; }
        var selector = new TalentSearchPersonSelectionView(); _talentPersons = selector; AddChild(selector);
        _talentOrdersView!.SetBusy(true); _talentOrdersView.Hide();
        try
        {
            selector.Configure(_repository, _ui, model,
                () => IsTalentOrdersCurrent && _talentPersons == selector, chosen => RunUiAction(() =>
                {
                    if (!IsTalentOrdersCurrent || _talentPersons != selector || _talentUiDraft != draft) return;
                    _talentPersons = null; selector.Abandon(); selector.QueueFree(); _talentOrdersView!.Show();
                    if (chosen is not null)
                    {
                        if (ChangeTalentDraft(() => draft.SelectExecutors(chosen)))
                        { TalentSearchLastAcceptedPersonIds = chosen.ToArray(); TalentSearchPersonAcceptedSequence++; }
                    }
                    else RefreshTalentOrders();
                }), CurrentListScrollSpeed);
            BindCurrentTutorialTalentSearch(); BindTalentSearchSelectionAdmission();
        }
        catch
        { _talentPersons = null; selector.Abandon(); selector.QueueFree(); _talentOrdersView!.Show(); RefreshTalentOrders(); throw; }
    }

    private bool ChangeTalentDraft(Action action)
    {
        bool applied = false;
        try
        {
            action();
            applied = true;
            if (_talentUiDraft?.AppRandomState is { } random) _presentationRandom.InstallCommittedState(random.State);
        }
        catch (InvalidDataException error) { GD.PushWarning(error.ToString()); }
        RefreshTalentOrders();
        return applied;
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
            else
            {
                if (kind == TalentOrderKind.Search) TalentSearchAcceptedSequence++;
                if (keepOpen && ReferenceEquals(_session, session)) OpenTalentOrders(kind);
            }
        }
        catch { if (IsTalentOrdersCurrent) RefreshTalentOrders(); throw; }
    }

    private void RequestTalentOrdersBack()
    {
        if (!IsTalentOrdersCurrent) return;
        if (_talentQuestion is { } question) { question.RequestCancel(); return; }
        if (_talentPersons is TalentSearchPersonSelectionView search) { search.RequestCancel(); return; }
        if (_talentPersons is TalentRecruitTargetSelectionView target) { target.RequestCancel(); return; }
        if (_talentPersons is TalentRecruitExecutorSelectionView persons) { persons.RequestCancel(); return; }
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
        if (persons is TalentSearchPersonSelectionView search) search.Abandon();
        else if (persons is TalentRecruitTargetSelectionView target) target.Abandon();
        else if (persons is TalentRecruitExecutorSelectionView recruit) recruit.Abandon();
        persons?.QueueFree(); destinations?.Abandon(); destinations?.QueueFree();
        question?.Abandon(); question?.QueueFree(); view?.Abandon(); view?.QueueFree();
        if (map is not null && GodotObject.IsInstanceValid(map) && _residentMap == map) map.SetDetailsOpen(false);
        if (!_failed && !_shuttingDown && _residentMap is not null) SetMapFocus(_mapFocused);
    }
}
