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
        _diplomacyOrders = new DiplomacyOrdersPanel();
        _playCommandDeck.Content(PlayableCommandDeck.Page.Diplomacy).AddChild(_diplomacyOrders);
        _personnelOrders = new PersonnelOrdersPanel();
        _playCommandDeck.Content(PlayableCommandDeck.Page.Personnel).AddChild(_personnelOrders);
    }

    private void ConfigureExtendedCommandLocale()
    {
        _militaryOrders.Configure(_ui.Get, command => RunPlayAction(() => ApplyPlayableOrder(command)));
        _tacticOrders.Configure(_ui.Get, _ui.TacticReason, command => RunPlayAction(() => ApplyPlayableOrder(command)),
            (force, unit, tactic, target) => TacticCommandQuery.ForTarget(_world, force, unit, tactic, target,
                _playRules?.TacticRules ?? UnresolvedTacticRules.Instance, _playRules?.Routes));
        _diplomacyOrders.Configure(_ui.Get, command => RunPlayAction(() => ApplyPlayableOrder(command)));
        _personnelOrders.Configure(_ui.Get, command => RunPlayAction(() => ApplyPlayableOrder(command)));
        UpdateUiAudioStatus();
        UpdateSceneAudioStatus();
    }

    private void RefreshExtendedCommands()
    {
        int? unit = _playUnit.Selected >= 0 ? _playUnit.GetSelectedId() : null;
        int? city = _playCity.Selected >= 0 ? _playCity.GetSelectedId() : null;
        _militaryOrders.Refresh(_militaryViews.Create(_world, unit, _playerForceId), _selectedCell?.Id);
        _tacticOrders.Refresh(_world, unit, _playerForceId, _playRules?.TacticRules ?? UnresolvedTacticRules.Instance,
            _playRules?.Routes, _selectedCell?.Id);
        _diplomacyOrders.Refresh(_world, _playerForceId, city);
        _personnelOrders.Refresh(_world, _playerForceId);
        RefreshMilitaryDeploymentComposition();
        RefreshTransportOrders();
    }

}
