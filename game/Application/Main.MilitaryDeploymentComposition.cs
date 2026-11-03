using Godot;
using San14.Core;

public partial class Main
{
    private MilitaryDeploymentBatchPanel? _deploymentComposition;
    private Action? _releaseDeploymentCompositionBindings;

    private void AdoptMilitaryPagesSpeechWorld(San14.Client.Gameplay.GameplaySession session,
        GameState previous,GameState replacement)
    {
        // The speech host has admitted this exact presentation transaction.
        // Every retained page still requires its original session, map and predecessor.
        if(_deploymentDraftSession==session&&_deploymentDraftMap==_residentMap&&
            _deploymentDraftCityId is int city&&_deploymentCityDraft?.IsFor(previous,city)==true)
        {
            _deploymentCityDraft.AcceptPreferenceWorld(previous,replacement);
            _deploymentAdoptSpeechWorld?.Invoke(previous,replacement);
        }
        if(_campaignDeploymentSession==session&&_campaignDeploymentMap==_residentMap&&
            HasCampaignDeploymentPage&&ReferenceEquals(_campaignDeploymentWorld,previous))
            _campaignDeploymentWorld=replacement;
        if(_campaignRetreatSession==session&&_campaignRetreatMap==_residentMap&&
            HasCampaignRetreatPage&&ReferenceEquals(_campaignRetreatWorld,previous))
            _campaignRetreatWorld=replacement;
        if(_cityDeploymentCancellation is { } cancellation&&cancellation.Session==session&&
            cancellation.Map==_residentMap&&ReferenceEquals(cancellation.World,previous))
            cancellation.World=replacement;
        if(_generalUnitTarget is { } target&&target.Session==session&&target.Map==_residentMap&&
            ReferenceEquals(target.World,previous))
        {
            target.Presenter?.AdoptSpeechWorld(previous,replacement);target.World=replacement;
        }
    }

    private void BuildMilitaryDeploymentComposition(VBoxContainer military)
    {
        RetireMilitaryDeploymentComposition();
        var panel = new MilitaryDeploymentBatchPanel();
        _deploymentComposition = panel;
        military.AddChild(panel);
        // The browser owns this binding across ordinary deck reparenting.
        // Capture the actual signal sources so retirement cannot detach a
        // replacement interface's controls through Main's newer fields.
        var officer = _playOfficer;
        var formation = _playFormation;
        var structure = _playStructureKind;
        var troops = _playTroops;
        var tactics = _playTactics.ToArray();
        void RefreshBoundPanel()
        {
            if (ReferenceEquals(_deploymentComposition, panel)) RefreshMilitaryDeploymentComposition();
        }
        void SelectionChanged(long _) => RefreshBoundPanel();
        void TroopsChanged(double _) => RefreshBoundPanel();
        officer.ItemSelected += SelectionChanged;
        formation.ItemSelected += SelectionChanged;
        structure.ItemSelected += SelectionChanged;
        troops.ValueChanged += TroopsChanged;
        foreach (var tactic in tactics) tactic.ItemSelected += SelectionChanged;
        _releaseDeploymentCompositionBindings = () =>
        {
            if (GodotObject.IsInstanceValid(officer)) officer.ItemSelected -= SelectionChanged;
            if (GodotObject.IsInstanceValid(formation)) formation.ItemSelected -= SelectionChanged;
            if (GodotObject.IsInstanceValid(structure)) structure.ItemSelected -= SelectionChanged;
            if (GodotObject.IsInstanceValid(troops)) troops.ValueChanged -= TroopsChanged;
            foreach (var tactic in tactics)
                if (GodotObject.IsInstanceValid(tactic)) tactic.ItemSelected -= SelectionChanged;
        };
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

    private void RetireMilitaryDeploymentComposition()
    {
        var panel = _deploymentComposition;
        _deploymentComposition = null;
        var release = _releaseDeploymentCompositionBindings;
        _releaseDeploymentCompositionBindings = null;
        try { release?.Invoke(); }
        finally { panel?.Retire(); }
    }

    private int? ResolveDeploymentNavalFormation(GameState world,int officerId)
    {
        var result=_campaignComposition?.PrepareArmyQueries(world).ResolveDefaultNavalFormation(world,officerId);
        return result?.Available==true?result.FormationId:null;
    }
}
