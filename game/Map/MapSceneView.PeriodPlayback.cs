using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class MapSceneView
{
    // Only completed render data escapes the borrowed simulation checkpoint.
    // In particular, the asynchronous native pump never receives its GameState.
    private sealed record PeriodMapFrame(NativeMapSeasonState Season,
        D3D11TerritoryPublication? Territory, D3D11ArmyPublication? Army,
        ArmyMapGroupingFrame? Groups, D3D11CityGatePublication? Cities,
        D3D11AreaCapitalPublication? Areas, D3D11ArmyLabelPublication? Labels,
        FacilityMapInfoPublication? FacilityInfo, HexFirePresentationPublication? HexFire);
    private PeriodMapFrame? _periodMapFrame;
    private bool _periodMapActive;
    private bool _periodMapPaused;
    private GameState? _periodBorrowedCandidate;
    private D3D11ArmyPublisher? _periodOriginalArmy;
    private CampaignPeriodPlayback? _periodMapOwner;

    internal void BeginPeriodPlayback(CampaignPeriodPlayback owner)
    {
        if (_periodMapOwner is { } previous) EndPeriodPlayback(previous, adoptPresentation: false);
        var nextArmy = _d3d11Army?.ForkPresentation();
        BeginPeriodFacilityConnections();
        _periodOriginalArmy = _d3d11Army;
        _d3d11Army = nextArmy;
        _periodMapOwner = owner;
        _periodMapActive = true; _periodMapFrame = null; _periodMapPaused = false;
        SuspendMapSummaries();
        CloseCityCommandMenu();
        SetDetailsOpen(false);
        foreach (var button in new[] { _mapCityInformation, _mapCityOrders, _mapAreaOrders, _mapUnitOrders, _mapNextUnit })
            if (button is not null) button.Visible = false;
        _d3d11Markers?.ClearPointer();
        _sceneryMap?.Suspend();
    }

    internal void PublishPeriodPlayback(CampaignPeriodPlayback owner, GameState candidate)
    {
        if (!_periodMapActive || !ReferenceEquals(_periodMapOwner, owner)) return;
        if (!UseD3D11Map) return;
        var season = candidate.NativeMapSeason ?? throw new InvalidDataException("Period display has no actual native date.");
        _d3d11Army?.AdvanceMotion(candidate, _d3d11AnimationPacer?.FpsMode ?? 0);
        var cityGateWorld=CaptureCityGateFacilityWorld(candidate,_d3d11CityGate?.Capture(candidate,_d3d11CommonFrustumMode));
        var frame = new PeriodMapFrame(season, _d3d11Ownership?.Capture(candidate),
            _d3d11Army?.Capture(candidate), _d3d11ArmyGroupingQueries?.Invoke(candidate),
            cityGateWorld,
            _d3d11AreaCapital?.Capture(candidate, _d3d11CommonFrustumMode),
            _d3d11Markers?.CaptureArmyLabels(candidate),
            _d3d11Markers?.CaptureFacilityInfo(candidate,cityGateWorld?.Facilities),
            CaptureHexFirePresentation(candidate));
        _d3d11Markers?.RefreshWorld(candidate, ForceMarkerColors);
        _periodMapFrame = frame;
        _periodBorrowedCandidate = candidate;
        ScheduleD3D11MapFrame();
    }

    internal void SetPeriodPlaybackPaused(CampaignPeriodPlayback owner, bool paused)
    {
        if (!ReferenceEquals(_periodMapOwner, owner)) return;
        _periodMapPaused = paused; ScheduleD3D11MapFrame();
    }

    internal void ReleasePeriodPlaybackCandidate(CampaignPeriodPlayback owner)
    { if (ReferenceEquals(_periodMapOwner, owner)) _periodBorrowedCandidate = null; }

    internal void EndPeriodPlayback(CampaignPeriodPlayback owner, bool adoptPresentation,
        GameState? displayedWorld = null)
    {
        if (!ReferenceEquals(_periodMapOwner, owner)) return;
        _periodMapOwner = null;
        _periodMapActive = false; _periodMapFrame = null; _periodBorrowedCandidate = null;
        if (adoptPresentation) _periodOriginalArmy?.Dispose();
        else if (_periodOriginalArmy is not null)
        { _d3d11Army?.Dispose(); _d3d11Army = _periodOriginalArmy; }
        _periodOriginalArmy = null;
        // ClosePeriodAdvanceProgress can publish a camera immediately. Install
        // the authoritative displayed world before that first ordinary frame.
        if (displayedWorld is not null) _logicalWorld = displayedWorld;
        EndPeriodFacilityConnections(adoptPresentation, _logicalWorld);
        if (_logicalWorld is { } world) _d3d11Markers?.RefreshWorld(world, ForceMarkerColors);
        ResetSourceCameraInput();
    }
}
