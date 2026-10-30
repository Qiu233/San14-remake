using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private MapCommanderCards? _commanderCards;
    private CommanderHudPublication? _commanderCardSource;
    private GameState? _commanderCardsWorld;
    private GameplaySession? _commanderCardsSession;
    private MapSceneView? _commanderCardsMap;
    private IReadOnlyList<int>? _commanderCardBindings;
    private KeyboardBindingCatalog? _commanderKeyboardCatalog;
    private IReadOnlyDictionary<int,OfficerPortraitRoute>? _commanderCardPortraits;
    private (int Person,ushort? Appearance)[] _commanderPortraitIdentity=[];
    private int _commanderPortraitVersion=-1;
    private string? _commanderCardsLocale;
    private string[] _commanderShortcuts=new string[5];
    private CommanderSelectionPage? _commanderSelectionPage;
    private GameState? _commanderSelectionPageWorld;
    private GameplaySession? _commanderSelectionPageSession;
    private MapSceneView? _commanderSelectionPageMap;
    private bool HasCommanderSelectionPage => _commanderSelectionPage is not null;

    private bool CommanderCardInputAdmitted()=>_commanderCards is not null&&!_failed&&!_shuttingDown&&
        !IsReturningToTitle&&_mapFocused&&_commanderCardsSession==_session&&_commanderCardsMap==_residentMap&&
        GameplayMapKeyboardAdmitted(allowCommanderTarget:true)&&
        (_advancingPeriod?_periodPlayback is not null:ReferenceEquals(_commanderCardsWorld,_world));

    private void RefreshCommanderCards(GameState? borrowed=null)
    {
        if(_repository is null||_campaignComposition is null||_session is not{PlayerForceId:int player} session||
            _residentMap is not{IsPlayableMapReady:true} map)
        {CloseCommanderCards();return;}
        GameState? world=borrowed??(_advancingPeriod?_periodPlayback?.Candidate:_world);
        if(world is null){RefreshCommanderCardSelection();RefreshCommanderCardsVisibility();return;}
        if(_commanderCardsLocale is not null&&_commanderCardsLocale!=_ui.SourceLocale)CloseCommanderCards();
        if(HasCommanderSelectionPage&&(!ReferenceEquals(_commanderSelectionPageWorld,world)||
            _commanderSelectionPageSession!=session||_commanderSelectionPageMap!=map))CloseCommanderSelectionPage();
        bool changed=!ReferenceEquals(_commanderCardsWorld,world)||_commanderCardsSession!=session||_commanderCardsMap!=map;
        if(_commanderCards is null)
        {
            var view=new MapCommanderCards();_commanderCards=view;AddChild(view);
            view.HoveredOfficer=officer=>
            {
                if(_commanderCardsMap is not{} currentMap)return;
                if(officer is null||!_advancingPeriod&&ReferenceEquals(_commanderCards,view)&&
                    _residentMap==currentMap&&ReferenceEquals(_commanderCardsWorld,_world))
                    currentMap.SetMapSummaryHover(view,officer is int person?new(MapSummaryHoverKind.Commander,person):null);
            };
            view.Configure(_repository,_ui,CommanderCardInputAdmitted,
                slot=>RunUiAction(()=>TryFocusCommanderCard(slot), allowDuringPeriod: true),()=>RunUiAction(OpenCommanderSelectionPage),
                (slot,tactic)=>RunUiAction(()=>RequestCommanderTactic(slot,tactic), allowDuringPeriod: true),
                slot=>RunUiAction(()=>RequestCommanderAutomatic(slot), allowDuringPeriod: true),slot=>RunUiAction(()=>RemoveCommanderCard(slot)));
            //The ordinary map action group shares this canvas and follows
            //the resident cards in the GUI tree, before later modal pages.
            map.MountContextActions(this,view,()=>!_advancingPeriod&&!_failed&&!_shuttingDown&&
                !IsReturningToTitle&&_mapFocused&&_residentMap==map&&_world is{Phase:TurnPhase.Strategy}&&
                ReferenceEquals(_world,_session?.World)&&GameplayMapKeyboardAdmitted());
            _commanderKeyboardCatalog=KeyboardBindingCatalog.Load(_repository,_ui.SourceLocale);
            _commanderCardsLocale=_ui.SourceLocale;changed=true;
        }
        var army=_campaignComposition.PrepareArmyQueries(world);
        if(changed||_commanderCardSource is null)
            _commanderCardSource=army.QueryCommanderHud(world,player,_advancingPeriod);
        var source=_commanderCardSource;
        var identity=source.Cards.Where(card=>card.OfficerId is not null)
            .Select(card=>(card.OfficerId!.Value,card.PortraitAppearanceIndex)).ToArray();
        if(_commanderCardPortraits is null||!identity.SequenceEqual(_commanderPortraitIdentity)||
            _commanderPortraitVersion!=_repository.PortraitResourceVersion)
        {
            _commanderCardPortraits=army.QueryOfficerPortraitRoutes(world,identity.Select(row=>row.Item1).ToArray());
            _commanderPortraitIdentity=identity;_commanderPortraitVersion=_repository.PortraitResourceVersion;
        }
        if(!ReferenceEquals(_commanderCardBindings,_preferences.KeyboardBindings))
        {
            _commanderShortcuts=Enumerable.Range(0,5).Select(index=>string.Join(" / ",Enumerable.Range(0,2)
                .Select(slot=>_commanderKeyboardCatalog!.FormatBinding(_preferences.KeyboardBindings[(25+index)*2+slot]))
                .Where(value=>value.Length!=0))).ToArray();
            _commanderCardBindings=_preferences.KeyboardBindings;
        }
        _commanderCardsWorld=world;_commanderCardsSession=session;_commanderCardsMap=map;
        _commanderCards.Apply(source,_commanderCardPortraits,_commanderShortcuts,
            !_advancingPeriod&&world.Phase==TurnPhase.Strategy,
            !_advancingPeriod&&world.Phase==TurnPhase.Strategy&&source.Capacity is >0&&source.UnresolvedInput is null,
            _manualTacticCommand?.Publication.SelectedUnitId,_manualTacticCommand?.TacticSlot);
        RefreshCommanderCardsVisibility();
    }

    private void RefreshCommanderCardSelection()=>_commanderCards?.ApplySelection(
        _manualTacticCommand?.Publication.SelectedUnitId,_manualTacticCommand?.TacticSlot);

    // Map focus/modal changes only hide this small control. No query, world
    // capture or candidate rebuilding belongs to this visibility operation.
    private void RefreshCommanderCardsVisibility()
    {
        if (_commanderCards is not null)
        {
            _commanderCards.Visible = !_failed && !_shuttingDown && !IsReturningToTitle &&
                _mapFocused && GameplayMapKeyboardAdmitted(allowCommanderTarget:true) && !HasCommanderSelectionPage;
            if(!_commanderCards.Visible)_commanderCardsMap?.SetMapSummaryHover(_commanderCards,null);
        }
    }

    private bool TryFocusCommanderCard(int slot)
    {
        if((uint)slot>=5||!CommanderCardInputAdmitted()||_commanderCardSource is not{} source)return false;
        var card=source.Cards.FirstOrDefault(row=>row.Slot==slot);
        if(card?.State==CommanderHudCardState.Empty&&!_advancingPeriod)
        {OpenCommanderSelectionPage();return true;}
        return card?.HexId is int hex&&_residentMap!.FocusCommanderHex(hex);
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
            }), CurrentListScrollSpeed);
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
        var view = _commanderCards; var map=_commanderCardsMap; _commanderCards = null;
        _commanderCardSource = null; _commanderCardsWorld = null; _commanderCardsSession = null;
        _commanderCardsMap = null; _commanderCardBindings = null; _commanderKeyboardCatalog = null;
        _commanderCardPortraits = null;_commanderPortraitIdentity=[];_commanderPortraitVersion=-1;
        _commanderCardsLocale=null;RetireCommanderCardRequests();
        if (view is not null) { if(map is not null&&GodotObject.IsInstanceValid(map))map.SetMapSummaryHover(view,null);view.Hide(); view.GetParent()?.RemoveChild(view); view.QueueFree(); }
    }
}
