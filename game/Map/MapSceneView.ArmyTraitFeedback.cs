using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private GameState? _traitRefreshWorld;
    private D3D11ArmyPublication? _traitRefreshArmy;
    private ArmyMapPersonalitiesPublication? _traitPersonalities;
    private readonly HashSet<int> _traitPersonalityRefreshed=[];

    internal bool TryRefreshArmyTraitView(GameState world, int armyId, out string? missing)
    {
        missing = null;
        if (!IsPlayableMapReady || !UseD3D11Map || _d3d11Army is null || _d3d11Markers is null ||
            _sourceMainCameraFrame is null || _periodMapActive || _d3d11Map is not { IsClosed: false, LastError: null })
        { missing = "army-trait-view.map_owner"; return false; }
        //Refresh the real Army model publication and Army label fields. The
        //City/Gate/Area marker refresh cannot consume this source command.
        // Departure reconstruction and accepted map publications can replace
        // the label owner after this world's last trait refresh. Reuse the
        // batch only while the actual label publication still belongs to it.
        if (!ReferenceEquals(_traitRefreshWorld, world) || !_d3d11Markers.OwnsArmyTraitPublication(world))
        {
            _traitRefreshArmy = _d3d11Army.Capture(world);
            if (!_d3d11Markers.RefreshArmyTraitViews(world))
            { missing = "army-trait-view.label_owner"; return false; }
            _logicalWorld = world;
            ScheduleD3D11MapFrame();
            _traitRefreshWorld = world;
            _traitPersonalityRefreshed.Clear();
        }
        var publication = _traitRefreshArmy!;
        if (publication.Unavailable.FirstOrDefault(row => row.UnitId == armyId) is { } unavailable)
        { missing = unavailable.Reason; return false; }
        if (!_d3d11Markers.HasRefreshedArmyTraitView(world, armyId, out missing)) return false;
        if(world.Units.ContainsKey(armyId)&&!_traitPersonalityRefreshed.Contains(armyId))
        {
            if(_armyPersonalityQuery is null){missing="army-personalities.source_query";return false;}
            if (!ReferenceEquals(_traitPersonalities?.World, world))
                _traitPersonalities = _armyPersonalityQuery(world, world.Units.Keys);
            var personalities = _traitPersonalities;
            if (!ReferenceEquals(personalities.World, world))
            { missing = "army-personalities.publication_owner"; return false; }
            if (personalities.UnresolvedInput is { } ownerMissing)
            { missing = ownerMissing; return false; }
            if (!personalities.Rows.TryGetValue(armyId, out var row))
            { missing = "army-personalities.publication_row"; return false; }
            if (row.UnresolvedInput is { } rowMissing)
            { missing = rowMissing; return false; }
            _d3d11Markers.RefreshArmyPersonalities(armyId,row.Personalities);
            _traitPersonalityRefreshed.Add(armyId);
        }
        if (world.Units.ContainsKey(armyId) && !publication.UnitCenters.ContainsKey(armyId))
        { missing = "army-trait-view.Army_model"; return false; }
        //A retired Army is reconciled out of both real owners above. Its
        //retirement is not postponed just to keep a speech layout alive.
        return true;
    }
}
