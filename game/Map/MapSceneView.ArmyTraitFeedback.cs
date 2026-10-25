using San14.Core;

public partial class MapSceneView
{
    private GameState? _traitRefreshWorld;
    private D3D11ArmyPublication? _traitRefreshArmy;

    internal bool TryRefreshArmyTraitView(GameState world, int armyId, out string? missing)
    {
        missing = null;
        if (!IsPlayableMapReady || !UseD3D11Map || _d3d11Army is null || _d3d11Markers is null ||
            _sourceMainCameraFrame is null || _periodMapActive || _d3d11Map is not { IsClosed: false, LastError: null })
        { missing = "army-trait-view.map_owner"; return false; }
        //Refresh the real Army model publication and Army label fields. The
        //City/Gate/Area marker refresh cannot consume this source command.
        if (!ReferenceEquals(_traitRefreshWorld, world))
        {
            _traitRefreshArmy = _d3d11Army.Capture(world);
            if (!_d3d11Markers.RefreshArmyTraitViews(world))
            { missing = "army-trait-view.label_owner"; return false; }
            _logicalWorld = world;
            ScheduleD3D11MapFrame();
            _traitRefreshWorld = world;
        }
        var publication = _traitRefreshArmy!;
        if (publication.Unavailable.FirstOrDefault(row => row.UnitId == armyId) is { } unavailable)
        { missing = unavailable.Reason; return false; }
        if (!_d3d11Markers.HasRefreshedArmyTraitView(world, armyId, out missing)) return false;
        if (world.Units.ContainsKey(armyId) && !publication.UnitCenters.ContainsKey(armyId))
        { missing = "army-trait-view.Army_model"; return false; }
        //A retired Army is reconciled out of both real owners above. Its
        //retirement is not postponed just to keep a speech layout alive.
        return true;
    }
}
