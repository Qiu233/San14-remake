using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private MapCommanderCards? _commanderCards;
    private CommanderSelectionView? _commanderCardSource;
    private GameState? _commanderCardsWorld;
    private GameplaySession? _commanderCardsSession;
    private MapSceneView? _commanderCardsMap;
    private IReadOnlyList<int>? _commanderCardBindings;
    private KeyboardBindingCatalog? _commanderKeyboardCatalog;
    private IReadOnlyDictionary<int,OfficerPortraitRoute>? _commanderCardPortraits;
    private CommanderSelectionPage? _commanderSelectionPage;
    private GameState? _commanderSelectionPageWorld;
    private GameplaySession? _commanderSelectionPageSession;
    private MapSceneView? _commanderSelectionPageMap;
    private bool HasCommanderSelectionPage => _commanderSelectionPage is not null;

    private void RefreshCommanderCards()
    {
        if (_repository is null || _campaignComposition is null || _world is not { } world ||
            _session is not { PlayerForceId: int player } session ||
            _residentMap is not { IsPlayableMapReady: true } map)
        { CloseCommanderCards(); return; }
        if (HasCommanderSelectionPage && (!ReferenceEquals(_commanderSelectionPageWorld,world) ||
            _commanderSelectionPageSession != session || _commanderSelectionPageMap != map)) CloseCommanderSelectionPage();
        bool changed = !ReferenceEquals(_commanderCardsWorld,world) || _commanderCardsSession != session ||
            _commanderCardsMap != map;
        if (_commanderCards is null)
        {
            var view = new MapCommanderCards(); _commanderCards = view; AddChild(view);
            view.Configure(_repository,_ui,index => RunUiAction(() => TryFocusCommanderCard(index)),
                () => RunUiAction(OpenCommanderSelectionPage));
            _commanderKeyboardCatalog = KeyboardBindingCatalog.Load(_repository,_ui.SourceLocale);
            changed = true;
        }
        if (changed || !ReferenceEquals(_commanderCardBindings,_preferences.KeyboardBindings))
        {
            var army = _campaignComposition.PrepareArmyQueries(world);
            var source = changed || _commanderCardSource is null ? army.QueryCommanderCards(world,player) : _commanderCardSource;
            if (changed || _commanderCardPortraits is null)
                _commanderCardPortraits = army.QueryOfficerPortraitRoutes(world,source.Cards.Select(card => card.OfficerId).ToArray());
            var portraits = _commanderCardPortraits;
            var cards = Enumerable.Range(0,5).Select(index =>
            {
                var card = index < source.Cards.Count ? source.Cards[index] : null;
                string shortcut = string.Join(" / ",Enumerable.Range(0,2).Select(slot =>
                    _commanderKeyboardCatalog!.FormatBinding(_preferences.KeyboardBindings[(25+index)*2+slot]))
                    .Where(value => value.Length != 0));
                return new MapCommanderCard(index,card?.OfficerId,card?.Name ?? "",shortcut,
                    source.Capacity is int capacity ? index < capacity : null,
                    card is null ? null : portraits.GetValueOrDefault(card.OfficerId));
            }).ToArray();
            _commanderCards.Apply(cards,source.Available && source.Capacity is > 0 && world.Phase == TurnPhase.Strategy);
            _commanderCardSource = source; _commanderCardsWorld = world;
            _commanderCardsSession = session; _commanderCardsMap = map;
            _commanderCardBindings = _preferences.KeyboardBindings;
        }
        RefreshCommanderCardsVisibility();
    }

    // Map focus/modal changes only hide this small control. No query, world
    // capture or candidate rebuilding belongs to this visibility operation.
    private void RefreshCommanderCardsVisibility()
    {
        if (_commanderCards is not null)
            _commanderCards.Visible = !_failed && !_shuttingDown && !_advancingPeriod && !IsReturningToTitle &&
                _mapFocused && GameplayMapKeyboardAdmitted() && !HasCommanderSelectionPage;
    }

    private bool TryFocusCommanderCard(int slot)
    {
        if ((uint)slot >= 5 || !GameplayMapKeyboardAdmitted() || _advancingPeriod ||
            _commanderCardsSession != _session || _commanderCardsMap != _residentMap ||
            !ReferenceEquals(_commanderCardsWorld,_world) || _commanderCardSource is not { } source ||
            slot >= source.Cards.Count) return false;
        var card = source.Cards[slot];
        if (!card.LocationKnown || card.HexId is not int hex) return false;
        return _residentMap!.FocusCommanderHex(hex);
    }

    private void OpenCommanderSelectionPage()
    {
        if (!GameplayMapKeyboardAdmitted() || _advancingPeriod || _repository is null ||
            _session is not { PlayerForceId: int player } session || _world is not { Phase: TurnPhase.Strategy } world ||
            _residentMap is not { } map || !ReferenceEquals(world,session.World) || _campaignComposition is null) return;
        var model = _campaignComposition.PrepareArmyQueries(world).QueryCommanderSelection(world,player);
        var page = new CommanderSelectionPage(); _commanderSelectionPage = page;
        _commanderSelectionPageWorld = world; _commanderSelectionPageSession = session; _commanderSelectionPageMap = map;
        AddChild(page);
        try
        {
            page.Configure(_repository,_ui,_playCommandDeck.Theme,model,CloseCommanderSelectionPage,ids => RunPlayAction(() =>
            {
                if (_commanderSelectionPage != page || !ReferenceEquals(world,_world) || _session != session || _residentMap != map) return;
                if (!ApplyPlayableOrder(new SetCommanderSelectionOrder(player,ids),CloseCommanderSelectionPage))
                    page.ApplyNotice(_playMessage.Text);
            }));
            map.CloseCityCommandMenu(); map.SetDetailsOpen(true,keepMapVisible:true); SetMapFocus(true); HideDeploymentHud();
            RefreshCommanderCardsVisibility();
        }
        catch { CloseCommanderSelectionPage(); throw; }
    }

    private void CloseCommanderSelectionPage()
    {
        var page = _commanderSelectionPage; var map = _commanderSelectionPageMap;
        _commanderSelectionPage = null; _commanderSelectionPageWorld = null;
        _commanderSelectionPageSession = null; _commanderSelectionPageMap = null;
        if (page is not null) { page.Abandon(); page.GetParent()?.RemoveChild(page); page.QueueFree(); }
        if (map is not null && map == _residentMap && !_failed && !_shuttingDown)
        { map.SetDetailsOpen(false); SetMapFocus(true); }
        RefreshCommanderCardsVisibility();
    }

    private void CloseCommanderCards()
    {
        CloseCommanderSelectionPage();
        var view = _commanderCards; _commanderCards = null;
        _commanderCardSource = null; _commanderCardsWorld = null; _commanderCardsSession = null;
        _commanderCardsMap = null; _commanderCardBindings = null; _commanderKeyboardCatalog = null;
        _commanderCardPortraits = null;
        if (view is not null) { view.Hide(); view.GetParent()?.RemoveChild(view); view.QueueFree(); }
    }
}
