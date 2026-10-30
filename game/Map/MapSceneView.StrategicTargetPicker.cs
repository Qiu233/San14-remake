using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private Control? strategicTargetMapOwner;
    private StrategicTargetMapQuery? strategicTargetMapQuery;
    private D3D11CommandMapData? strategicTargetTerritory;
    private Func<bool>? strategicTargetAdmitted;
    private SceneryMapPolicy? strategicTargetPreviousPresentation;
    private MapSummarySelectionMode strategicTargetPreviousSummary;
    private bool strategicTargetPreviousGeography;

    internal void BeginStrategicTargetPick(Control owner, GameState expectedWorld, StrategicTargetMapQuery query,
        Func<bool> admitted, Func<int,bool> allowed, Action<int> picked)
    {
        if (!ReferenceEquals(_logicalWorld,expectedWorld) || !IsPlayableMapReady)
            throw new InvalidOperationException("Strategic target selection requires its live map owner.");
        if (strategicTargetMapOwner is { } previous) EndStrategicTargetPick(previous);
        if (_commandTargetPicked is not null || _commandMapDisplay is {Present:true})
            throw new InvalidOperationException("Another command owns the map target or territory provider.");
        var data = D3D11CommandMapData.CaptureStrategicFocus(query);
        ulong revision = checked(++_commandMapRevision);
        _commandMapDisplay = D3D11CommandMapPublication.ForCampaignFocus(data,revision,revision,
            checked(++_commandMapProviderGeneration));
        strategicTargetTerritory = data;
        strategicTargetMapOwner = owner; strategicTargetMapQuery = query; strategicTargetAdmitted = admitted;
        strategicTargetPreviousSummary = _summaryMode;
        strategicTargetPreviousGeography = geographicView?.Visible == true; geographicView?.Hide();
        strategicTargetPreviousPresentation = _mapPresentation?.BeginStrategicTargetMode(query);
        _d3d11Markers?.SetStrategicTargetSelection(query,allowed);
        bool force = query.Kind == StrategicCommandTargetKind.Force;
        informationCanvas?.SetDiplomacyTargetSelection(force ? id=>query.IsEnabled(id)&&allowed(id) : null,
            force ? picked : null);
        ApplyKeyboardMapInformation();
        bool Current() => ReferenceEquals(strategicTargetMapOwner,owner) && admitted();
        SetCommandTargetPicker(hex=>
        {
            if (Current() && query.QueryHex(hex) is {Allowed:true,Id:int id} && allowed(id)) picked(id);
        },hex=>Current() && query.QueryHex(hex) is {Allowed:true,Id:int id} && allowed(id),hex=>
        {
            if (!Current() || force) return;
            MapSummaryHoverTarget? target = null;
            if (hex is int point && query.QueryHex(point) is {Allowed:true,Id:int id} && allowed(id))
                target = new(query.Kind == StrategicCommandTargetKind.Area ? MapSummaryHoverKind.Area : MapSummaryHoverKind.Foothold,id);
            SetMapSummaryHover(owner,target);
        });
        SetMapSummaryMode(force ? MapSummarySelectionMode.Normal : query.Kind == StrategicCommandTargetKind.Area
            ? MapSummarySelectionMode.Domestic : MapSummarySelectionMode.Foothold);
        ScheduleD3D11MapFrame();
    }

    // Source scene admission updates the provider before the independent
    // candidate gate. Rejected targets do not mask or recolour territory.
    private void HoverStrategicTargetTerritory(int? point)
    {
        if (strategicTargetMapOwner is null || strategicTargetAdmitted?.Invoke() != true || point is not int hex ||
            strategicTargetTerritory is not { } data || _commandMapDisplay is not {Data:{ } visible} display ||
            !ReferenceEquals(data,visible)) return;
        ushort old = display.HoverHex < D3D11CommandMapData.CellCount ? data.StrategicFocusAt((int)display.HoverHex) : (ushort)0;
        if (data.StrategicFocusAt(hex) == old) return;
        _commandMapDisplay = D3D11CommandMapPublication.ForCampaignFocus(data,checked(++_commandMapRevision),
            display.DataRevision,display.ProviderGeneration,(uint)hex);
        ScheduleD3D11MapFrame();
    }

    internal void EndStrategicTargetPick(Control owner)
    {
        if (!ReferenceEquals(strategicTargetMapOwner,owner)) return;
        if (strategicTargetTerritory is { } data && ReferenceEquals(_commandMapDisplay?.Data,data))
            _commandMapDisplay = D3D11CommandMapPublication.ForCampaignFocus(null,checked(++_commandMapRevision),0,
                checked(++_commandMapProviderGeneration));
        strategicTargetTerritory = null; strategicTargetAdmitted = null;
        SetMapSummaryHover(owner,null); strategicTargetMapOwner = null; strategicTargetMapQuery = null; SetCommandTargetPicker(null);
        _d3d11Markers?.SetStrategicTargetSelection(null,null);
        informationCanvas?.SetDiplomacyTargetSelection(null,null);
        if (strategicTargetPreviousPresentation is { } previous) _mapPresentation?.RestoreStrategicTargetMode(previous);
        strategicTargetPreviousPresentation = null;
        if (geographicView is not null) geographicView.Visible = strategicTargetPreviousGeography;
        SetMapSummaryMode(strategicTargetPreviousSummary); ApplyKeyboardMapInformation(); ScheduleD3D11MapFrame();
    }
}
