using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private MapSeasonScenarioPreviewFrame? _scenarioPreviewCalendar;
    private CityGateMapMarkerSources? _scenarioPreviewMarkerSources;
    private StartupMapCameraAdapter? _sourceStartupCamera;
    private bool _adoptingStartedCampaign;
    private bool _startedCampaignAccepted=true;
    private bool _retainedScenarioNativeOwner;
    internal bool HasRetainedScenarioNativeOwner=>_retainedScenarioNativeOwner;
    private NativeMapSeasonState? _pendingStartedSeason;
    private D3D11CurrentSeasonFrame? _pendingAcceptedPreviewSeason;
    private TaskCompletionSource<bool>? _startedMapFrame;
    private GameState? _startedMapWorld;
    internal CityGateMapMarkerSources CityGateMarkerSources=>_d3d11Markers?.MarkerSources??
        _scenarioPreviewMarkerSources??throw new InvalidOperationException("The actual source marker resources are absent.");
    internal bool IsNewGameScenarioPreview=>_scenarioPreviewCalendar is not null;
    private MainMapCameraControllerParameters ActiveSourceCameraParameters=>
        chronologyCamera?.Parameters??_sourceStartupCamera?.Parameters??_sourceMainCamera?.Parameters??
        throw new InvalidOperationException("An actual active camera owner is required.");
    private bool ActiveSourceCameraStable=>chronologyCamera is not null || (_sourceStartupCamera is {} startup
        ? startup.CurrentRuntime.Transition is null : _sourceMainCamera?.CurrentRuntime.Transition is null);
    internal event Action? ScenarioPreviewPresented;
    internal bool IsScenarioPreviewReady=>IsNewGameScenarioPreview&&InitialSceneLoad.IsCompletedSuccessfully&&
        !_cgQualityRebuilding&&_loaded&&_projection is not null&&D3D11MapInteractive&&
        _d3d11Map?.LastAcceptedInput is {CurrentSeason.Calendar:MapSeasonScenarioPreviewFrame} accepted&&
        ReferenceEquals(accepted.WorldState,_logicalWorld);
    internal (Texture2D Texture,D3D11MapInput Input,D3D11MapReceipt Receipt)? ScenarioPreviewPresentation=>
        IsNewGameScenarioPreview?_d3d11Map?.AcceptedPresentation:null;

    internal void EnterScenarioForceSelection()
    {
        if(!IsScenarioPreviewReady||_sourceStartupCamera is not {} startup)
            throw new InvalidOperationException("The source title map must actually open before force entry.");
        startup.EnterForceSelection(unchecked((int)Time.GetTicksMsec()));
        PublishSourceCamera();
    }

    private void SetScenarioPreviewCalendar(MapSeasonScenarioPreviewFrame? calendar)
    {
        if(calendar is null)
        {
            if(_cameraEntry==MapCameraEntry.NewGameForceSelection)
                throw new InvalidDataException("A force map requires the selected scenario's real calendar source.");
            return;
        }
        if(_cameraEntry!=MapCameraEntry.NewGameForceSelection||!_embedded||
            _logicalWorld is not {} world||(world.Phase!=TurnPhase.Initializing||world.NativeClock is not null||world.NativeArmySimulation is not null)||_playerForceId is not null)
            throw new InvalidDataException("Scenario rendering requires the actual unstarted campaign and no final player.");
        if(!UseD3D11Map)throw new NotSupportedException("The source force map requires the existing D3D12/native map output.");
        _scenarioPreviewCalendar=calendar;_retainedScenarioNativeOwner=true;_startedCampaignAccepted=false;
    }

    private SourceWorldContext? CaptureCurrentVisualWorld(GameState world)=>
        _d3d11CurrentWorldQueries?.Invoke(world)??
        throw new InvalidDataException("The actual current map visual query owner is unavailable.");
    private ArmyGraphicsCurrentInputs? CaptureCurrentArmyMotion(GameState world,UnitState unit)=>
        _d3d11ArmyMotionQueries?.Invoke(world,unit);
    private FacilitySceneModelFrame CaptureCurrentFacilityModels(GameState world,MapStaticModelAssetCatalog assets,
        UnitGeometry geometry,MapStaticModelGroundHeightSource ground,IReadOnlyList<FacilitySceneConnection> connections)=>
        _facilityModelQuery?.Invoke(world,assets,geometry,ground,connections)??
        throw new InvalidDataException("facility.presentation.model_query");
    private D3D11CityGatePublication? CaptureCurrentCityGate()=>IsNewGameScenarioPreview
        ? _d3d11CityGate?.CaptureScenarioPreview(_logicalWorld!,_d3d11CommonFrustumMode)
        : _d3d11CityGate?.Capture(_logicalWorld!,_d3d11CommonFrustumMode);

    private void AcceptScenarioMapRoleFrame(D3D11MapSurface surface,D3D11MapInput input,D3D11MapReceipt receipt)
    {
        if(IsNewGameScenarioPreview)
        {
            // The source overlay replaces ordinary map labels. Geometry and
            // typed City/Gate/Area/facility publications still run normally.
            if(_d3d11Markers is not null)_d3d11Markers.Visible=false;
            ScenarioPreviewPresented?.Invoke();
        }
        if(_startedMapWorld is {} world&&ReferenceEquals(input.WorldState,world)&&
            input.CurrentSeason?.Calendar is MapSeasonRuntimeFrame)
        {
            _startedCampaignAccepted=true;_startedMapWorld=null;
            _startedMapFrame?.TrySetResult(true);
        }
    }

    internal void AdoptStartedCampaign(GameState world,int player,ScenarioCityPreview snapshot,
        Action<int> openCityDetails,Action<int> acceptHex,Action<int> selectCommandCity,
        Action<int> selectCommandUnit,Action<int> selectCommandArea)
    {
        if(!IsScenarioPreviewReady||_adoptingStartedCampaign||_d3d11Map is not {} surface||
            world.Phase!=TurnPhase.Strategy||world.NativeMapSeason is null||world.NativeArmySimulation is null||
            !world.Forces.ContainsKey(player)||snapshot.Locale!=_text.SourceLocale||
            world.ScenarioKey!=_logicalWorld!.ScenarioKey||world.MapSha256!=_logicalWorld.MapSha256)
            throw new InvalidDataException("Started campaign cannot adopt this prepared source map owner.");
        _adoptingStartedCampaign=true;_startedCampaignAccepted=false;
        // Stop input and new camera publications while the admitted frame
        // still reaches its real Canvas Draw/Post boundary before adoption.
        SetDetailsOpen(true,keepMapVisible:true);SetProcess(false);
        InitialSceneLoad=Adopt();
        async Task Adopt()
        {
            try
            {
                // No source process tick or caller can submit while this flag
                // is set. Await the real pump; PostDraw must remain unblocked.
                await surface.DrainAsync();
                if(!IsInsideTree()||_nativeMapClosing||_d3d11Map!=surface||surface.IsClosed)
                    throw new OperationCanceledException("The prepared source map retired during campaign adoption.");
                var last=surface.LastAcceptedInput?.CurrentSeason??
                    throw new InvalidOperationException("The final actual accepted preview season is absent.");
                _pendingAcceptedPreviewSeason=last;_pendingStartedSeason=world.NativeMapSeason;
                _logicalWorld=world;_playerForceId=player;_sourceSnapshot=snapshot;
                _openCityDetails=openCityDetails;_acceptHex=acceptHex;
                _selectCommandCity=selectCommandCity;_selectCommandUnit=selectCommandUnit;_selectCommandArea=selectCommandArea;
                _closeScene=()=>{};_cameraEntry=MapCameraEntry.CurrentRuler;
                _scenarioPreviewCalendar=null;_sourceStartupCamera=null;_sourceMainCamera=null;
                InstallCampaignArmyPublisher();
                _sourceMainMode!.SetCurrentOverride28C(-1);
                if(_d3d11Markers is null)InitializeD3D11CampaignMapLocations();
                _d3d11Markers!.AdoptCampaignPlayer(world,player);
                _d3d11Markers.ConfigureArmyLabels(_repository,snapshot,CaptureCurrentVisualWorld,
                    MainMapCameraControllerParameters.LoadSourceDefaults(_repository,_text.SourceLocale),CaptureCurrentArmyPresence);
                _d3d11Markers.Visible=true;
                _facilityConnections=CaptureFacilitySceneConnections(world);
                _startedMapWorld=world;_startedMapFrame=new(TaskCreationOptions.RunContinuationsAsynchronously);
                InitializeSourceCamera();
                // The next actual render attempt adopts the live calendar in
                // the retained preparation-number sequence. It must then pass
                // native and Canvas acceptance before this task completes.
                _adoptingStartedCampaign=false;SetProcess(true);
                ScheduleD3D11MapFrame();
                await _startedMapFrame.Task;
                SetDetailsOpen(false);
            }
            finally {_adoptingStartedCampaign=false;}
        }
    }

    internal System.Numerics.Vector3 ResolveScenarioPreviewHexAnchor(int hex)
    {
        var terrain = _sceneTerrain?.WorldGroundHeight ??
            throw new InvalidOperationException("The force map has no loaded Scene Landscape world Field.");
        if (!ReferenceEquals(_deploymentTargetAnchors?.Terrain, terrain)) _deploymentTargetAnchors = new(terrain);
        var anchor = _deploymentTargetAnchors.Query(hex);
        return anchor.GroundWorld ?? throw new InvalidDataException(anchor.UnresolvedInput ??
            "The force map Hex has no resolved world anchor.");
    }

    internal bool TryPickScenarioPreviewHex(MainMapCameraCanonicalPoint cursor,out int hex)
    {
        hex=-1;
        if(!IsScenarioPreviewReady)return false;
        var status=ReadSourceCameraRay(cursor,out var camera,out var picker,out var ray);
        if(status!=MapHoverRayStatus.Ready)return false;
        var o=ray.Origin;var d=ray.Direction;
        if(!picker!.TryPick(new(o.X,o.Y,o.Z),new(d.X,d.Y,d.Z),camera!.Camera.Lens.Far,out var hit))return false;
        return CeHexWorldCoordinates.TryPickTerrainHit(new(o.X,o.Y,o.Z),new(d.X,d.Y,d.Z),
            new(hit.Position.X,hit.Position.Y,hit.Position.Z),out hex);
    }
}
