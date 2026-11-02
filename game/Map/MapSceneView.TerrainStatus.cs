using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private Func<GameState,SourceWorldContext,HexReference?,MapSummaryPage>? _queryTerrainStatus;
    private MapSummaryPanel? _terrainStatusPanel;
    private GameState? _terrainStatusWorld;
    private SourceWorldContext? _terrainStatusCurrent;
    private int? _terrainStatusHex;
    private bool _terrainStatusKnown;
    private bool _terrainStatusAvailable;

    internal void SetMapTerrainStatusQuery(Func<GameState,SourceWorldContext,HexReference?,MapSummaryPage> query)
    { _queryTerrainStatus = query; }

    private void TickMapTerrainStatus(GameState world, SourceWorldContext current, int band, bool known)
    {
        if (_queryTerrainStatus is null || _summaryRoot is null || _summaryResources is null) return;
        // A blocked UI pointer is not a new cursor Hex: keep the last real map
        // publication, exactly as CInfoSummaryHex retains its scene cursor owner.
        int? hex = known ? _summaryInputSelection.Hex?.PhysicalSlot : _terrainStatusHex;
        if (!_terrainStatusKnown || !ReferenceEquals(world,_terrainStatusWorld) ||
            !ReferenceEquals(current,_terrainStatusCurrent) || (known && hex != _terrainStatusHex))
        {
            var page = _queryTerrainStatus(world,current,hex is int physical ? new(physical) : null);
            if (page.ReceiverValid is null)
            {
                _terrainStatusAvailable=false;
                SetMeta("source_terrain_status_issue",page.UnresolvedOwner ?? "terrain.status_owner");
            }
            else
            {
                if (_terrainStatusPanel is null)
                {
                    var panel = new MapSummaryPanel { Name="MapTerrainStatus", Visible=false, Position=new(960,0) };
                    _summaryRoot.AddChild(panel); panel.Configure(_summaryResources,page.LayoutName,
                        new Dictionary<int,UiSourceNodeGeometry>{[14]=new(Size:new Vector2(240,24))}); _terrainStatusPanel=panel;
                }
                _terrainStatusPanel.Apply(page);
                _terrainStatusAvailable=page.ReceiverValid==true;
                RemoveMeta("source_terrain_status_issue");
            }
            _terrainStatusWorld=world; _terrainStatusCurrent=current; _terrainStatusHex=hex; _terrainStatusKnown=true;
        }
        if (_terrainStatusPanel is {} view) view.Visible=_terrainStatusAvailable && band<5 && !_detailsOpen;
    }

    private void SuspendMapTerrainStatus() { _terrainStatusPanel?.Hide(); }
    private void ReleaseMapTerrainStatus()
    {
        _terrainStatusPanel=null; _terrainStatusWorld=null; _terrainStatusCurrent=null;
        _terrainStatusHex=null; _terrainStatusKnown=false; _terrainStatusAvailable=false;
    }
}
