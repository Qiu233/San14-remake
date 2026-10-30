using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private CityOfficerMovementView? _cityOfficerMovementView;
    private CityOfficerMovementPersonSelectionView? _cityOfficerMovementPersons;
    private CityOfficerMovementCitySelectionView? _cityOfficerMovementCities;
    private CityOfficerMovementDraft? _cityOfficerMovementDraft;
    private GameplaySession? _cityOfficerMovementSession;
    private GameState? _cityOfficerMovementWorld;
    private MapSceneView? _cityOfficerMovementMap;
    private AreaAdministrationOfficerContent? _cityOfficerMovementCardContent;
    private bool HasCityOfficerMovementPage => _cityOfficerMovementView is not null;
    private bool IsCityOfficerMovementCurrent => !_failed && !_shuttingDown && HasCityOfficerMovementPage &&
        _cityOfficerMovementDraft is not null && ReferenceEquals(_session, _cityOfficerMovementSession) &&
        ReferenceEquals(_world, _cityOfficerMovementWorld) && ReferenceEquals(_session?.World, _cityOfficerMovementWorld) &&
        _residentMap == _cityOfficerMovementMap && _cityOfficerMovementMap is not null;
    private bool CanEditCityOfficerMovement => IsCityOfficerMovementCurrent && _cityOfficerMovementPersons is null && _cityOfficerMovementCities is null;
    private bool HasCityOfficerMovementMapSelection => IsCityOfficerMovementCurrent && _cityOfficerMovementCities?.IsMapMode == true;

    private void AdoptCityOfficerMovementSpeechWorld(GameplaySession session, GameState previous, GameState replacement)
    {
        if (_cityOfficerMovementView is null || _cityOfficerMovementDraft is null ||
            _cityOfficerMovementSession != session || _cityOfficerMovementMap is null ||
            _cityOfficerMovementMap != _residentMap || !ReferenceEquals(_cityOfficerMovementWorld, previous)) return;
        // Keep the captured candidates and destination query. The selector's
        // live admission and new map picks follow this presentation owner.
        _cityOfficerMovementWorld = replacement;
    }

    private void OpenCityOfficerMovement(int city, OfficerMoveMode mode)
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasActionReport || HasPersonnelConfirmation || HasAdministrationPage || HasMilitaryOrderPage ||
            HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace || _detailsView is not null ||
            _repository is null || _session is not { PlayerForceId: int force } session || session.HasPendingOperation ||
            _world is not { Phase: TurnPhase.Strategy } world || !ReferenceEquals(session.World, world) ||
            _campaignComposition is not { } composition || _residentMap is not { IsPlayableMapReady: true } map) return;
        var opened = composition.PrepareArmyQueries(world).OpenCityOfficerMovementDraft(world, city, mode);
        if (!opened.Available) { CityOfficerMovementUnavailable(opened.UnresolvedInput!); return; }
        _cityOfficerMovementDraft = opened.Value!; _cityOfficerMovementSession = session;
        _cityOfficerMovementWorld = world; _cityOfficerMovementMap = map;
        _cityOfficerMovementCardContent = new(_repository);
        var view = new CityOfficerMovementView(); _cityOfficerMovementView = view; AddChild(view);
        try
        {
            SetMapFocus(true); map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true); HideDeploymentHud();
            view.Configure(_repository, _ui, mode, () => CanEditCityOfficerMovement,
                () => RunUiAction(() =>
                {
                    if (!CanEditCityOfficerMovement) return;
                    CloseCityOfficerMovement(); ReturnToCityMenu(session, map, city);
                }),
                () => RunPlayAction(() => ConfirmCityOfficerMovement(force)),
                () => RunUiAction(OpenCityOfficerMovementPersons),
                () => RunUiAction(OpenCityOfficerMovementCities));
            RefreshCityOfficerMovement();
        }
        catch { CloseCityOfficerMovement(); throw; }
    }

    private void RefreshCityOfficerMovement()
    {
        if (!IsCityOfficerMovementCurrent) return;
        var page = _cityOfficerMovementDraft!.QueryPage();
        if (!page.Available) { CityOfficerMovementUnavailable(page.UnresolvedInput!); return; }
        _cityOfficerMovementView!.Apply(page.Value!);
        var card = _cityOfficerMovementDraft.QueryOfficerCard();
        _cityOfficerMovementView.ApplyOfficerCard(_cityOfficerMovementCardContent!.Create(card.Available ? card.Value : null));
        _cityOfficerMovementView.SetBusy(!CanEditCityOfficerMovement);
    }

    private void OpenCityOfficerMovementPersons()
    {
        if (!CanEditCityOfficerMovement || _repository is null) return;
        var draft = _cityOfficerMovementDraft!;
        var query = draft.QueryPage();
        if (!query.Available) { CityOfficerMovementUnavailable(query.UnresolvedInput!); return; }
        var page = query.Value!;
        if (_cityOfficerMovementSession?.PlayerForceId is not int player) return;
        var people = draft.QueryPersons(player);
        if (!people.Available || people.Value is null) { CityOfficerMovementUnavailable(people.UnresolvedInput ?? "city-officer-movement.person_columns"); return; }
        var persons = new CityOfficerMovementPersonSelectionView();
        _cityOfficerMovementPersons = persons; AddChild(persons);
        _cityOfficerMovementView!.SetBusy(true); _cityOfficerMovementView.Hide();
        try
        {
            persons.Configure(_repository, _ui, _cityOfficerMovementView.SharedResources, page.Mode, people.Value, page.SelectedOfficerIds,
                () => IsCityOfficerMovementCurrent && _cityOfficerMovementPersons == persons,
                result => RunUiAction(() =>
                {
                    if (!IsCityOfficerMovementCurrent || _cityOfficerMovementPersons != persons || _cityOfficerMovementDraft != draft) return;
                    _cityOfficerMovementPersons = null; persons.Abandon(); persons.QueueFree();
                    _cityOfficerMovementView!.Show();
                    if (result is not null) AcceptCityOfficerMovement(draft.SelectOfficers(result));
                    RefreshCityOfficerMovement();
                }), CurrentListScrollSpeed);
        }
        catch
        {
            _cityOfficerMovementPersons = null; persons.Abandon(); persons.QueueFree();
            _cityOfficerMovementView!.Show(); RefreshCityOfficerMovement(); throw;
        }
    }

    private void OpenCityOfficerMovementCities()
    {
        if (!CanEditCityOfficerMovement || _repository is null) return;
        var draft = _cityOfficerMovementDraft!;
        var query = draft.QueryPage();
        if (!query.Available || query.Value is not { Mode: OfficerMoveMode.Dispatch } page)
        { CityOfficerMovementUnavailable(query.UnresolvedInput ?? "city-officer-movement.map_publication"); return; }
        if(page.Destinations.Count==0)return;
        var destinations=draft.QueryDestinations();
        if(!destinations.Available||destinations.Value is not{MapSelection:{} mapSource} destinationSelection)
        {CityOfficerMovementUnavailable(destinations.UnresolvedInput??"city-officer-movement.destination_selection");return;}
        var map = _cityOfficerMovementMap!;
        var selector = new CityOfficerMovementCitySelectionView();
        _cityOfficerMovementCities = selector; AddChild(selector);
        _cityOfficerMovementView!.SetBusy(true); _cityOfficerMovementView.Hide();
        bool Current() => IsCityOfficerMovementCurrent && _cityOfficerMovementCities == selector && _cityOfficerMovementDraft == draft;
        selector.TreeExiting+=()=>{if(GodotObject.IsInstanceValid(map))map.EndDiplomacyTargetPick(selector);};
        void BindMode(bool list)
        {
            if (!Current()) return;
            map.EndDiplomacyTargetPick(selector);
            map.SetDetailsOpen(list, keepMapVisible: true);
            if (!list) map.BeginDiplomacyTargetPick(selector, _cityOfficerMovementWorld!, mapSource,
                () => Current() && selector.IsMapMode,selector.CanChooseTarget,id => RunUiAction(() =>
                { if (Current()) selector.PickMapDestination(id); }));
            HideDeploymentHud();
        }
        try
        {
            selector.Configure(_repository, _ui, destinationSelection, Current,
                result => RunUiAction(() =>
                {
                    if (!Current()) return;
                    map.EndDiplomacyTargetPick(selector);
                    _cityOfficerMovementCities = null; selector.Abandon(); selector.QueueFree();
                    map.SetDetailsOpen(true, keepMapVisible: true); _cityOfficerMovementView!.Show();
                    if (result is int id) AcceptCityOfficerMovement(draft.SelectDestination(id));
                    RefreshCityOfficerMovement();
                }), list => RunUiAction(() => BindMode(list)),map.SourceRightCancelReleased,CurrentListScrollSpeed);
            BindMode(false);
        }
        catch
        {
            map.EndDiplomacyTargetPick(selector);
            _cityOfficerMovementCities = null; selector.Abandon(); selector.QueueFree();
            map.SetDetailsOpen(true, keepMapVisible: true);
            _cityOfficerMovementView!.Show(); RefreshCityOfficerMovement(); throw;
        }
    }

    private void ConfirmCityOfficerMovement(int force)
    {
        if (!CanEditCityOfficerMovement) return;
        var order = _cityOfficerMovementDraft!.BuildOrder(force);
        if (!order.Available) { CityOfficerMovementUnavailable(order.UnresolvedInput!); return; }
        var view = _cityOfficerMovementView!;
        view.SetBusy(true);
        try
        {
            if (!ApplyPlayableOrder(order.Value!, CloseCityOfficerMovement))
            { RefreshCityOfficerMovement(); view.ApplyNotice(_playMessage.Text); }
        }
        catch { if (IsCityOfficerMovementCurrent) RefreshCityOfficerMovement(); throw; }
    }

    private bool AcceptCityOfficerMovement(AreaAdministrationDraftResult result)
    {
        if (result.Available) return true;
        CityOfficerMovementUnavailable(result.UnresolvedInput ?? "officer-movement.unavailable"); return false;
    }
    private void CityOfficerMovementUnavailable(string reason)
    {
        string message = _ui.Get("detail_unknown"); _playMessage.Text = message;
        _cityOfficerMovementView?.ApplyNotice(message); GD.PushWarning(reason);
    }
    private bool HandleCityOfficerMovementMapBackInput(InputEvent input)
    {
        if (!HasCityOfficerMovementMapSelection || input is not InputEventMouseButton { ButtonIndex: MouseButton.Right } button)
            return false;
        if (_cityOfficerMovementMap!.SourceRightCancelReleased(button))
        { RequestCityOfficerMovementBack(); GetViewport().SetInputAsHandled(); }
        return true;
    }

    private void RequestCityOfficerMovementBack()
    {
        if (!IsCityOfficerMovementCurrent) return;
        if (_cityOfficerMovementCities is { } cities) cities.RequestCancel();
        else if (_cityOfficerMovementPersons is { } persons) persons.RequestCancel();
        else _cityOfficerMovementView!.RequestBack();
    }
    private void CloseCityOfficerMovement()
    {
        if (!HasCityOfficerMovementPage && _cityOfficerMovementDraft is null && _cityOfficerMovementMap is null) return;
        var map = _cityOfficerMovementMap; _cityOfficerMovementMap = null;
        _cityOfficerMovementSession = null; _cityOfficerMovementWorld = null;
        var cities = _cityOfficerMovementCities; _cityOfficerMovementCities = null;
        if (cities is not null && map is not null && GodotObject.IsInstanceValid(map)) map.EndDiplomacyTargetPick(cities);
        var persons = _cityOfficerMovementPersons; _cityOfficerMovementPersons = null;
        var view = _cityOfficerMovementView; _cityOfficerMovementView = null;
        var draft = _cityOfficerMovementDraft; _cityOfficerMovementDraft = null;
        _cityOfficerMovementCardContent = null;
        if (cities is not null) { cities.Abandon(); cities.QueueFree(); }
        if (persons is not null) { persons.Abandon(); persons.QueueFree(); }
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        draft?.Dispose();
        if (map is not null && GodotObject.IsInstanceValid(map) && _residentMap == map) map.SetDetailsOpen(false);
        if (!_failed && !_shuttingDown && _residentMap is not null) SetMapFocus(_mapFocused);
    }
}
