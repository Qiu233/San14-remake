using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private CityOfficerMovementView? _cityOfficerMovementView;
    private CityOfficerMovementPersonSelectionView? _cityOfficerMovementPersons;
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
    private bool CanEditCityOfficerMovement => IsCityOfficerMovementCurrent && _cityOfficerMovementPersons is null;

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
                destination => RunUiAction(() =>
                {
                    if (CanEditCityOfficerMovement && AcceptCityOfficerMovement(_cityOfficerMovementDraft!.SelectDestination(destination)))
                        RefreshCityOfficerMovement();
                }));
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
        var persons = new CityOfficerMovementPersonSelectionView();
        _cityOfficerMovementPersons = persons; AddChild(persons);
        _cityOfficerMovementView!.SetBusy(true); _cityOfficerMovementView.Hide();
        try
        {
            persons.Configure(_repository, _ui, page.Candidates, page.SelectedOfficerIds, page.MaximumOfficers,
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
    private void RequestCityOfficerMovementBack()
    {
        if (!IsCityOfficerMovementCurrent) return;
        if (_cityOfficerMovementPersons is { } persons) persons.RequestCancel();
        else _cityOfficerMovementView!.RequestBack();
    }
    private void CloseCityOfficerMovement()
    {
        if (!HasCityOfficerMovementPage && _cityOfficerMovementDraft is null && _cityOfficerMovementMap is null) return;
        var map = _cityOfficerMovementMap; _cityOfficerMovementMap = null;
        _cityOfficerMovementSession = null; _cityOfficerMovementWorld = null;
        var persons = _cityOfficerMovementPersons; _cityOfficerMovementPersons = null;
        var view = _cityOfficerMovementView; _cityOfficerMovementView = null;
        var draft = _cityOfficerMovementDraft; _cityOfficerMovementDraft = null;
        _cityOfficerMovementCardContent = null;
        if (persons is not null) { persons.Abandon(); persons.QueueFree(); }
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        draft?.Dispose();
        if (map is not null && GodotObject.IsInstanceValid(map) && _residentMap == map) map.SetDetailsOpen(false);
        if (!_failed && !_shuttingDown && _residentMap is not null) SetMapFocus(_mapFocused);
    }
}
