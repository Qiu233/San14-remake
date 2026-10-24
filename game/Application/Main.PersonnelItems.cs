using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private PersonnelItemPage? _personnelItemPage;
    private GameplaySession? _personnelItemSession;
    private GameState? _personnelItemWorld;
    private MapSceneView? _personnelItemMap;
    private bool HasPersonnelItemPage => _personnelItemPage is not null;

    private bool IsPersonnelItemPageCurrent(PersonnelItemPage page) =>
        !_failed && !_shuttingDown && _personnelItemPage == page &&
        ReferenceEquals(_session, _personnelItemSession) && ReferenceEquals(_world, _personnelItemWorld) &&
        ReferenceEquals(_session?.World, _personnelItemWorld) && _residentMap == _personnelItemMap;

    private void OpenPersonnelItems()
    {
        if (_failed || _shuttingDown || _advancingPeriod || IsReturningToTitle || HasAdministrationPage || HasMilitaryOrderPage ||
            HasCampaignSavePage || HasMapSystemMenu || HasActionReport || HasPersonnelConfirmation || _detailsView is not null ||
            _repository is null || _residentMap is not { IsPlayableMapReady: true } map ||
            _session is not { PlayerForceId: int player } session || session.World is not { Phase: TurnPhase.Strategy } world ||
            !ReferenceEquals(world, _world) || PlayableOutcome.Evaluate(world, player) != PlayableResult.Ongoing) return;
        var model = PersonnelCommandView.Create(world, player);
        CloseCommandWorkspace();
        var page = new PersonnelItemPage(); _personnelItemPage = page;
        _personnelItemSession = session; _personnelItemWorld = world; _personnelItemMap = map;
        GameOrder? order = null;
        AddChild(page);
        try
        {
            page.Configure(_repository, _ui, _playCommandDeck.Theme, model, ClosePersonnelItems,
                () => { if (IsPersonnelItemPageCurrent(page) && order is not null) RequestPersonnelOrder(order, page); }, Evaluate);
            SetMapFocus(true); map.CloseCityCommandMenu(); map.SetDetailsOpen(true, keepMapVisible: true); HideDeploymentHud();
        }
        catch { ClosePersonnelItems(); throw; }

        void Evaluate(PersonnelItemSelection? selected)
        {
            if (!IsPersonnelItemPageCurrent(page)) return;
            order = null;
            PersonnelEvaluation? result = null;
            int? before = null, after = null;
            if (selected is not null && model.FindItem(selected.ItemId) is { HolderOfficerId: int holder } item &&
                model.RulerId is int ruler && selected.RecipientId is int officerId && model.FindOfficer(officerId) is { } officer)
            {
                bool eligible = selected.Revoke ? holder != ruler && officerId == holder : holder == ruler && officerId != ruler;
                if (eligible)
                {
                    var change = new PersonnelChange(selected.Revoke ? PersonnelActionKind.RevokeItem : PersonnelActionKind.GrantItem,
                        officerId, item.Id, holder, selected.Revoke ? ruler : officerId);
                    result = session.EvaluatePersonnelChange(change);
                    before = officer.Loyalty;
                    if (result.Quote is { } quote && before is int loyalty) after = Math.Clamp(checked(loyalty + quote.LoyaltyDelta), 0, 100);
                    if (result.Allowed == true && result.Quote is not null)
                        order = selected.Revoke ? new RevokeItemOrder(player, item.Id, holder) : new GrantItemOrder(player, item.Id, officerId);
                }
            }
            page.ApplyEvaluation(new(order is not null, result?.Quote?.CommandBooks, result?.Quote?.Gold, before, after,
                _ui.Get(order is not null ? "play_personnel_items_ready" :
                    result is { Allowed: null } ? "play_personnel_items_unavailable" : "play_personnel_items_select")));
        }
    }

    private void ClosePersonnelItems()
    {
        var page = _personnelItemPage; var map = _personnelItemMap;
        if (page is not null && _personnelConfirmationItemParent == page) ClosePersonnelConfirmation(false);
        _personnelItemPage = null; _personnelItemSession = null; _personnelItemWorld = null; _personnelItemMap = null;
        if (page is not null) { page.Abandon(); page.QueueFree(); }
        if (map is not null && map == _residentMap && GodotObject.IsInstanceValid(map)) map.SetDetailsOpen(false);
        if (!_failed && !_shuttingDown && map is not null && map == _residentMap) SetMapFocus(true);
    }
}
