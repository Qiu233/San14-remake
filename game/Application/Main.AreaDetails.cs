using Godot;
using San14.Client.Gameplay;
using San14.Data;

public partial class Main
{
    private IReadOnlyDictionary<int, DetailForceAppearance?> CurrentAreaForces(int areaId)
    {
        if (_world is null || _campaignComposition is null || !_world.Areas.TryGetValue(areaId, out var area))
            return new Dictionary<int, DetailForceAppearance?>();
        var forces = (area.AdjacentAreaIds ?? []).Take(8).Prepend(areaId)
            .Select(id => _world.Areas.GetValueOrDefault(id)?.HexId)
            .Where(hex => hex.HasValue && _world.Territory.ContainsKey(hex.Value))
            .Select(hex => _world.Territory[hex!.Value].ForceId ?? 0);
        return _campaignComposition.DetailForces(_world, forces);
    }

    private void OpenMapAreaDetails(MapSceneView owner, int areaId)
    {
        if (_failed || HasAdministrationPage || HasMilitaryOrderPage || HasCampaignSavePage || HasMapSystemMenu || HasCommandWorkspace || _detailsView is not null ||
            (_sceneView != owner && _residentMap != owner) || _repository is null ||
            _world is null || _selectedSnapshot is null) return;
        var view = new AreaDetailsView();
        _detailsView = view;
        AddChild(view);
        try
        {
            owner.SetDetailsOpen(true, keepMapVisible: true);
            view.Configure(_repository, AreaDetailView.Create(_world, _selectedSnapshot, areaId, _ui,
                new CeAreaDetailQueries(_repository.SourceDirectory, _ui.SourceLocale),
                _campaignComposition?.PrepareArmyQueries(_world).ReadAreaRepresentativeSurfaceCode(_world, areaId),
                CurrentAreaForces(areaId),
                _campaignComposition?.PrepareArmyQueries(_world).ReadAreaDetailMiniMap(_world, areaId)), _ui, () =>
            {
                if (_detailsView != view) return;
                view.Hide(); view.QueueFree(); _detailsView = null;
                if ((_sceneView == owner || _residentMap == owner) && GodotObject.IsInstanceValid(owner))
                    owner.SetDetailsOpen(false);
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
