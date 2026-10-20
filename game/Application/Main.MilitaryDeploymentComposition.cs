using Godot;
using San14.Core;

public partial class Main
{
    private MilitaryDeploymentBatchPanel? _deploymentComposition;

    private void BuildMilitaryDeploymentComposition(VBoxContainer military)
    {
        _deploymentComposition = new MilitaryDeploymentBatchPanel();
        military.AddChild(_deploymentComposition);
        _playOfficer.ItemSelected += _ => RefreshMilitaryDeploymentComposition();
        _playFormation.ItemSelected += _ => RefreshMilitaryDeploymentComposition();
        _playStructureKind.ItemSelected += _ => RefreshMilitaryDeploymentComposition();
        _playTroops.ValueChanged += _ => RefreshMilitaryDeploymentComposition();
        foreach (OptionButton tactic in _playTactics)
            tactic.ItemSelected += _ => RefreshMilitaryDeploymentComposition();
    }

    private void ConfigureMilitaryDeploymentComposition()
    {
        _deploymentComposition?.Configure(_ui.Get, CaptureMilitaryDeploymentUnit,
            () => _playStructureKind.Selected >= 0
                ? PlayableConstructionRules.DescribeSlot(_playStructureKind.GetSelectedId()).KindId : null,
            order =>
            {
                bool accepted = false;
                RunPlayAction(() => { accepted = ApplyPlayableOrder(order); });
                return accepted;
            });
    }

    private DeploymentUnitRequest? CaptureMilitaryDeploymentUnit(DeploymentAction action)
    {
        if (_world is null || _playerForceId is null || _playCity.Selected < 0 ||
            _playOfficer.Selected < 0 || _playFormation.Selected < 0) return null;
        return new(_playOfficer.GetSelectedId(), _playFormation.GetSelectedId(), (int)_playTroops.Value,
            _playTactics.Select(choice => choice.Selected >= 0 && choice.GetSelectedId() != NoTacticId?
                choice.GetSelectedId():0).ToArray(), [action])
        {
            NavalFormationId = ResolveDeploymentNavalFormation(_world, _playOfficer.GetSelectedId()),
            Behavior = _militaryOrders.BehaviorSelection,
            WaypointHexIds = _militaryOrders.WaypointSnapshot()
        };
    }

    private void RefreshMilitaryDeploymentComposition() => _deploymentComposition?.Refresh(_world, _playerForceId,
        _playCity.Selected >= 0 ? _playCity.GetSelectedId() : null, _selectedCell?.Id);

    private void ResetMilitaryDeploymentComposition() => _deploymentComposition?.ResetDraft();

    private int? ResolveDeploymentNavalFormation(GameState world,int officerId)
    {
        var result=_campaignComposition?.PrepareArmyQueries(world).ResolveDefaultNavalFormation(world,officerId);
        return result?.Available==true?result.FormationId:null;
    }
}
