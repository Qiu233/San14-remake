using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private MilitaryOrdersPanel _militaryOrders = null!;
    private readonly MilitaryCommandViews _militaryViews = new();
    private TacticOrdersPanel _tacticOrders = null!;
    private DiplomacyOrdersPanel _diplomacyOrders = null!;
    private PersonnelOrdersPanel _personnelOrders = null!;
    private void BuildExtendedCommandPanels()
    {
        _militaryOrders = new MilitaryOrdersPanel();
        _playCommandDeck.Content(PlayableCommandDeck.Page.Military).AddChild(_militaryOrders);
        _tacticOrders = new TacticOrdersPanel();
        _playCommandDeck.Content(PlayableCommandDeck.Page.Military).AddChild(_tacticOrders);
        _nativeManualTacticEntry=new Button{Name="NativeManualTacticCommands"};
        _nativeManualTacticEntry.Pressed+=()=>RunUiAction(()=>OpenManualTacticCommands(SelectionId(_playUnit)));
        _playCommandDeck.Content(PlayableCommandDeck.Page.Military).AddChild(_nativeManualTacticEntry);
        _diplomacyOrders = new DiplomacyOrdersPanel();
        _playCommandDeck.Content(PlayableCommandDeck.Page.Diplomacy).AddChild(_diplomacyOrders);
        _personnelOrders = new PersonnelOrdersPanel();
        _playCommandDeck.Content(PlayableCommandDeck.Page.Personnel).AddChild(_personnelOrders);
    }

    private void ConfigureExtendedCommandLocale()
    {
        _militaryOrders.Configure(_ui.Get, command => RunPlayAction(() => RequestMilitaryPanelOrder(command)));
        _tacticOrders.Configure(_ui.Get, _ui.TacticReason, command => RunPlayAction(() => ApplyPlayableOrder(command)),
            (force, unit, tactic, target) => TacticCommandQuery.ForTarget(_world, force, unit, tactic, target,
                _playRules?.TacticRules ?? UnresolvedTacticRules.Instance, _playRules?.Routes));
        _nativeManualTacticEntry.Text=_ui.Get("play_tactic_active_title");
        _diplomacyOrders.Configure(_ui.Get, command => RunPlayAction(() => ApplyPlayableOrder(command)),
            officer => RunUiAction(() => OpenCommandOfficerDetails(officer)),
            force => RunUiAction(() => OpenCommandForceDetails(force)),
            city => RunUiAction(() => OpenCommandCityDetails(city)),
            (kind, target, months, targetCity, gold, supplies) =>
                _world is { } world && _playerForceId is int force && SelectionId(_playCity) is int city &&
                    _campaignComposition is { } composition && _session?.HasPendingOperation != true
                    ? composition.PrepareArmyQueries(world).QueryDiplomacyCommand(world, force, city, kind,
                        target, months, targetCity, gold, supplies)
                    : new([], [], false, null, null));
        _personnelOrders.Configure(_ui.Get, command => RunPlayAction(() => RequestPersonnelOrder(command)),
            inspectOfficer: officer => RunUiAction(() => OpenCommandOfficerDetails(officer)),
            openItems: () => RunUiAction(OpenPersonnelItems));
        UpdateUiAudioStatus();
        UpdateSceneAudioStatus();
    }

    private void RefreshExtendedCommands()
    {
        // These panels belong to the optional development command browser.
        // The normal map's separate command pages query their own current
        // drafts. Keep its real manual-tactic presentation binding alive.
        if (!DeveloperWorkspaceEnabled)
        {
            RefreshManualTacticCommands(_advancingPeriod ? _periodPlayback?.Candidate : _world);
            return;
        }
        int? unit = _playUnit.Selected >= 0 ? _playUnit.GetSelectedId() : null;
        int? city = _playCity.Selected >= 0 ? _playCity.GetSelectedId() : null;
        _militaryOrders.Refresh(_militaryViews.Create(_world, unit, _playerForceId), _selectedCell?.Id);
        _tacticOrders.Visible=_world?.NativeArmySimulation is null;
        if(_tacticOrders.Visible)_tacticOrders.Refresh(_world, unit, _playerForceId, _playRules?.TacticRules ?? UnresolvedTacticRules.Instance,
            _playRules?.Routes, _selectedCell?.Id);
        RefreshManualTacticCommands(_advancingPeriod?_periodPlayback?.Candidate:_world);
        _diplomacyOrders.Refresh(_world, _playerForceId, city);
        _personnelOrders.Refresh(_world, _playerForceId);
        RefreshMilitaryDeploymentComposition();
        RefreshTransportOrders();
    }

}
