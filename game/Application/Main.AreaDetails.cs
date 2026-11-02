using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private void ConfigureCurrentAreaDetail(AreaDetailsView view, GameState world, int areaId,
        Action close, ArmyCampaignRuleAdapter? preparedRules = null, IReadOnlyList<int>? selectionIds = null,
        Action<InformationDetailSelectionReturn>? returnSelection = null, bool? initialNameSortAscending = null)
    {
        var rules = preparedRules ?? _campaignComposition?.PrepareArmyQueries(world)
            ?? throw new InvalidDataException("area-information.current_campaign_rules");
        RetainCommandDetailSpeechWorld(view, next =>
        {
            world = next;
            rules = _campaignComposition!.PrepareArmyQueries(next);
        });
        view.Configure(_repository ?? throw new InvalidDataException("area-information.resource_owner"),
            rules.QueryAreaInformation(world, areaId), _ui, Return,
            PrepareDetailSelection(rules, world, InformationListKind.Areas, areaId, selectionIds)
                with { InitialNameSortAscending = initialNameSortAscending },
            id => RunInspectionUiAction(world, () => view.Apply(rules.QueryAreaInformation(world, id))));
        void Return()
        {
            returnSelection?.Invoke(view.ReadSelectionReturn());
            close();
        }
    }

    private void OpenMapAreaDetails(MapSceneView owner, int areaId, Action? returned = null)
    {
        if (_failed || HasAdministrationPage || HasMilitaryOrderPage || HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace || _detailsView is not null ||
            (_sceneView != owner && _residentMap != owner) || _repository is null ||
            _world is null || _selectedSnapshot is null) return;
        var view = new AreaDetailsView();
        if (_residentMap == owner && _session is { PlayerForceId: not null })
        {
            OpenCommandDetails(view, closeDetails => ConfigureCurrentAreaDetail(view, _world, areaId, () =>
            {
                if (_commandDetailsView != view) return;
                closeDetails();
                returned?.Invoke();
            }));
            return;
        }
        _detailsView = view;
        AddChild(view);
        try
        {
            owner.SetDetailsOpen(true, keepMapVisible: true);
            ConfigureCurrentAreaDetail(view, _world, areaId, () =>
            {
                if (_detailsView != view) return;
                view.Hide(); view.QueueFree(); _detailsView = null;
                if ((_sceneView == owner || _residentMap == owner) && GodotObject.IsInstanceValid(owner))
                    owner.SetDetailsOpen(false);
                returned?.Invoke();
            });
            view.RegisterHeader(_sourceHeaders);
        }
        catch (Exception error)
        {
            _detailsView = null;
            RemoveChild(view); view.Free();
            owner.SetDetailsOpen(false);
            owner.ReportCityDetailsError(error);
        }
    }
}
