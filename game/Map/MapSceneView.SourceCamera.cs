using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using Godot;
using San14.Core;
using San14.Data;
using NMatrix = System.Numerics.Matrix4x4;
using NVector3 = System.Numerics.Vector3;

/// <summary>Explicit current publication inputs; changing these is an owned
/// renderer/Field update, never an inferred original process value.</summary>
public sealed record SceneViewSourceCameraInputs(
    NVector3 CurrentScale90, bool ContextOwner3E4Bit29, uint NativeClipProfile,
    NativeCameraTangentDispatch TangentDispatch, float OwnerFovOverride290,
    bool CurrentMaximumYOverride, string Provenance);

public partial class MapSceneView
{
    private MainMapCameraAdapter? _sourceMainCamera;
    private MainMapCameraFrame? _sourceMainCameraFrame;
    private MainMapCameraTerrainSource? _sourceMainCameraTerrain;
    private SceneViewSourceCameraInputs? _sourceMainCameraInputs;
    private LoadedMainMapCameraNorthCommandSource? _sourceMainCameraNorth;
    private LoadedMainMapSeasonCameraOpticsSource? _sourceMainCameraOptics;
    private MapSeasonSelection? _sourceMainCameraOpticsSeason;
    private AspectRatioContainer? _sourceMainCameraAspect;
    private Label? _sourceMainCameraNotice;
    private readonly Dictionary<string,string> _sourceCameraMessages = [];
    private long _sourceMainCameraFrameNumber;
    private bool _sourceCameraPreviousCursorKnown;
    private MainMapCameraCanonicalPoint _sourceCameraPreviousCursor;
    private MainMapCameraCanonicalPoint _sourceCameraPressedCursor;
    private int _sourceCameraActiveButton = -1;
    private uint _sourceCameraHeldFrames;
    private MainMapCameraCanonicalPoint _sourceCameraCurrentCursor;
    private bool _sourceCameraCurrentCursorKnown;
    private int _sourceCameraPendingWheel;
    private readonly bool[] _sourceCameraRawNow70=new bool[3];
    private readonly bool[] _sourceCameraRawPrior58=new bool[3];
    private readonly uint[] _sourceCameraRawCounters=new uint[3];
    private bool _sourceCameraLeftUiPressed;
    private MainMapCameraCanonicalPoint? _sourceRightCancelPress;
    private double _sourceActiveMilliseconds;
    internal uint SourceActiveMilliseconds => unchecked((uint)(ulong)_sourceActiveMilliseconds);
    internal long SourceAnimationUpdateSequence { get; private set; }

    internal bool TryReadSourceFocusHex(out int hex)
    {
        hex = -1;
        return _sourceMainCameraFrame is { ControllerState: var state } &&
            CeHexWorldCoordinates.TryPick((float)state.FocusX, (float)state.FocusZ, out hex);
    }
    private Func<MainMapCameraTerrainPitchInput,MainMapCameraTerrainPitchResult>? SourceCameraTerrainQualifier =>
        _sourceMainCameraTerrain is { } terrain?terrain.QualifyPitch:null;

    internal float? MainCommandZoomLevel { get; private set; }
    internal event Action<float?>? MainCommandZoomChanged;
    private void PublishMainCommandZoom(float? zoom)
    {
        //Only the strict threshold matters to this presentation consumer.
        //Do not query or capture world/header state on camera frames.
        bool? previous = MainCommandZoomLevel is float old ? old > 4 : null;
        bool? current = zoom is float value ? value > 4 : null;
        MainCommandZoomLevel = zoom;
        if (previous != current) MainCommandZoomChanged?.Invoke(zoom);
    }

    public MainMapCameraFrame? SourceMainCameraFrame => _sourceMainCameraFrame;
    internal bool SourcePointerInputActive => _embedded&&!_detailsOpen&&!sourceCameraInputSuspended&&IsVisibleInTree()&&_sourceMainCameraFrame is not null;
    public MainMapCameraAdapter? SourceMainCameraOwner => _sourceMainCamera;
    public MainMapCameraTerrainSource? SourceMainCameraTerrain => _sourceMainCameraTerrain;

    /// <summary>For a later owned renderer update. The source camera is
    /// initialized from the current campaign ruler, independent of RGB framing.</summary>
    public void SetSourceCameraInputs(SceneViewSourceCameraInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if(string.IsNullOrWhiteSpace(inputs.Provenance))
            throw new InvalidDataException("Current source camera publication requires renderer provenance.");
        _sourceMainCameraInputs=inputs;
        if(_loaded)
        {
            if(_sourceMainCamera is null&&_sourceStartupCamera is null)InitializeSourceCamera();
            else PublishSourceCamera();
        }
    }

    public void SetSourceCameraTerrain(MapStaticModelGroundHeightSource field)
    {
        if(_loadedSceneResources is not { } scene || field.Scene.Scene.Source.ResourceUri!=scene.Scene.Source.ResourceUri ||
            field.Scene.Scene.Source.DecodedSha256!=scene.Scene.Source.DecodedSha256)
            throw new InvalidDataException("Current camera Field must belong to the loaded source Scene revision.");
        _sourceMainCameraTerrain=MainMapCameraTerrainSource.FromCurrentField(field);
        _deploymentTargetAnchors=null;
    }

    private void AttachSourceCameraSurface(VBoxContainer column)
    {
        if(!_embedded){column.AddChild(_surface);return;}
        AttachSourceNavigationInput();
        _sourceMainCameraAspect=new AspectRatioContainer {
            Name="SourceMainCameraAspect",SizeFlagsHorizontal=SizeFlags.ExpandFill,
            SizeFlagsVertical=SizeFlags.ExpandFill,StretchMode=AspectRatioContainer.StretchModeEnum.Fit,
            MouseFilter=MouseFilterEnum.Ignore
        };
        column.AddChild(_sourceMainCameraAspect);
        _sourceMainCameraAspect.AddChild(_surface);
        _sourceMainCameraNotice=new Label {
            Name="SourceMainCameraStatus",Visible=false,AnchorBottom=1,AnchorTop=1,AnchorRight=1,
            OffsetLeft=12,OffsetRight=-12,OffsetTop=-64,OffsetBottom=-12,
            AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=MouseFilterEnum.Ignore
        };
        AddChild(_sourceMainCameraNotice);
    }

    private void InitializeSourceCamera()
    {
        if(!_embedded||_logicalWorld is not GameState world||_loadedSceneResources is null)return;
        if(_cameraEntry == MapCameraEntry.CurrentRuler && _playerForceId is null)return;
        ReadSourceCameraMessages();
        try
        {
            _sourceMainCameraInputs??=ReadFreshSourceCameraInputs();
            if(_sourceMainCameraTerrain is null)
            {
                try {ConstructSourceCameraTerrain();}
                catch(InvalidDataException error)
                {SetMeta("source_main_camera_terrain_error",error.Message);SetSourceCameraStatus("pending_terrain");}
            }
            string provenance=$"remake-owned-world:{world.ScenarioKey}@{world.ContentHashes.GetValueOrDefault(world.ScenarioKey,"")};map={world.MapSha256}";
            if(_cameraEntry==MapCameraEntry.NewGameForceSelection)
            {
                _sourceStartupCamera=StartupMapCameraAdapter.FromTitleMap(_repository,_text.SourceLocale,provenance,
                    new(MainDataPresent:true,HostGate210Present:_loadedSceneResources is not null,MainCpuPresent:true));
            }
            else _sourceMainCamera=_cameraEntry == MapCameraEntry.FreshController
                ? MainMapCameraAdapter.FromFreshController(_repository,_text.SourceLocale,provenance,
                    _sourceMainCameraInputs.CurrentMaximumYOverride)
                : MainMapCameraAdapter.FromOwnedCampaign(_repository,_text.SourceLocale,world,_playerForceId!.Value,provenance,
                    _sourceMainCameraInputs.CurrentMaximumYOverride);
            _sourceMainCameraNorth??=MainMapCameraNorthCommandSource.Load(_repository,_text.SourceLocale);
            // Host210 is the successfully created asset context. This Scene
            // has actually loaded its assets; the owned Main Data/CPU above
            // exist. Fresh context flags or GPU target readiness are unrelated.
            if(_cameraEntry == MapCameraEntry.CurrentRuler)
                _sourceMainCamera!.ApplyTitleExitFar(MainMapCameraEntrySource.Load(_repository,_text.SourceLocale),
                    new(MainDataPresent:true,HostGate210Present:_loadedSceneResources is not null,MainCpuPresent:true));
            _sourceMainCameraOptics=null;_sourceMainCameraOpticsSeason=null;_sourceMainCameraFrame=null;
            _sourceMainCameraFrameNumber=0;
            _sourceMainMode??=MainMapCameraModeOwner.CreateFresh(_text.SourceLocale,"resident-map/primary-Main");
            _sourceMainMode.SetCurrentOverride28C(_sourceStartupCamera?.AppModeOverride28C??-1);
            InitializeSceneryMap();
            ResetSourceCameraInput();
            PublishSourceCamera();
            SetSourceCameraStatus(_sourceMainCameraTerrain is null?"pending_terrain":"ready");
        }
        catch(InvalidDataException error)
        {
            _sourceMainCamera=null;_sourceStartupCamera=null;_sourceMainCameraFrame=null;
            SetMeta("source_main_camera_error",error.Message);
            SetSourceCameraStatus("unavailable");
        }
    }

    private void ReleaseSourceCamera()
    {
        TacticPresentationViews.Clear();
        RetireStoryEffectCommonCamera();
        RetireChronologyMap(restore:false);
        _sourceMainCamera=null;_sourceStartupCamera=null;_sourceMainCameraFrame=null;_sourceMainCameraTerrain=null;
        PublishMainCommandZoom(null);
        _deploymentTargetAnchors=null;_commandTargetPicked=null;_commandTargetPointerAllowed=null;
        _commandTargetShiftWaypoint=false;
        _sourceMainCameraNorth=null;
        _sourceMainCameraOptics=null;_sourceMainCameraOpticsSeason=null;
        _sourceMainCameraFrameNumber=0;
        _sourceMainMode=null;
        ResetSourceCameraInput();
        RemoveMeta("source_main_camera_owner");
        RemoveMeta("source_main_camera_ready");
        if(_sourceMainCameraNotice is not null)_sourceMainCameraNotice.Visible=false;
    }

    internal bool RequestNorthHeading()
    {
        if (!D3D11MapInteractive || _detailsOpen ||
            _sourceMainCamera is not { } camera || _sourceMainCameraNorth is not { } north) return false;
        // The toolbar owns only the source yaw request. The live controller
        // retains focus, distance and pitch, including its normal busy rule.
        camera.QueueTimedRequest(north.CreateNorthRequest(), unchecked((int)Time.GetTicksMsec()));
        return true;
    }

    private bool ResetSourceCamera()
    {
        if(!_embedded)return false;
        if(_sourceMainCamera is not null&&_sourceMainCameraNorth is { } north)
        {
            // Existing north/reset UI hook emits the original command3.
            // Current focus/distance/pitch remain owned by this controller;
            // normal _Process Tick and publication advance its500ms transition.
            // A request ignored by native188 is still a handled UI action.
            _sourceMainCamera.QueueTimedRequest(north.CreateNorthRequest(),unchecked((int)Time.GetTicksMsec()));
            return true;
        }
        InitializeSourceCamera();
        return _sourceMainCameraFrame is not null;
    }

    private bool PublishSourceCamera()
    {
        if(!_loaded||_sourceMainCamera is null&&_sourceStartupCamera is null||_sourceMainCameraInputs is not { } input)return false;
        var frameInputs=new MainMapCameraFrameInputs(input.CurrentScale90,input.ContextOwner3E4Bit29,
            input.NativeClipProfile,input.TangentDispatch,input.OwnerFovOverride290,
            ++_sourceMainCameraFrameNumber,input.Provenance)
            {SourceSelectedFovV410=PrepareSourceSeasonCameraFov()};
        var frame=chronologyCamera is {} chronology?chronology.PrepareCurrent(frameInputs):
            _sourceStartupCamera is {} startup?startup.PrepareCurrent(frameInputs):_sourceMainCamera!.PrepareCurrent(frameInputs);
        _sourceMainCameraFrame=frame;
        PublishMainCommandZoom(MapSeasonMainCameraPhase.FromOwnerZoomFields(
            frame.ZoomSelection.Band1E0, frame.ZoomSelection.Fraction1E4));
        if(_sourceMainCamera is {} primary)PublishStoryEffectCommonCamera(primary.CurrentState, input);
        PublishTacticPresentationViews();
        var camera=frame.Camera;
        var position=ToGodot(camera.CurrentPose.Position80);
        if(!UseD3D11Map)
        {
            _camera.Transform=CreateSourceCameraGodotTransform(camera);
            _camera.Projection=Camera3D.ProjectionType.Perspective;
            _camera.KeepAspect=Camera3D.KeepAspectEnum.Height;
            _camera.Fov=Mathf.RadToDeg(camera.Lens.VerticalFovRadians);
            _camera.Near=camera.Lens.Near;_camera.Far=camera.Lens.Far;
        }
        if(_sourceMainCameraAspect is not null)_sourceMainCameraAspect.Ratio=camera.Lens.Aspect;
        var state=frame.ControllerState;
        _center=new((float)state.FocusX,(float)state.FocusY,(float)state.FocusZ);
        UpdateTerrainLod(position,camera.Lens.Aspect,camera.Matrices.WorldToProjection100);
        if (!UseD3D11Map) _sceneTerrain?.UpdateEye(position, _territoryOverlay?.Material);
        _locations?.SetViewportHeight(_surface.Size.Y);
        _unitLayer?.SetViewportHeight(_surface.Size.Y);
        _structureLayer?.SetViewportHeight(_surface.Size.Y);
        SetMeta("source_main_camera_owner",camera.FrameProvenance);
        SetMeta("source_main_camera_ready",true);
        SetMeta("source_main_camera_frame",camera.FrameNumber);
        SetMeta("source_main_camera_distance",state.Distance90);
        SetMeta("source_main_camera_zoom_band",frame.ZoomSelection.Band1E0);
        SetMeta("source_main_camera_fraction_10000",frame.ZoomSelection.Fraction1E4);
        SetMeta("playable_camera_calibration",_sourceStartupCamera is not null?"original-source-startup-CB60-controller":"original-source-main38D-controller");
        SetMeta("playable_zoom_level",frame.ZoomSelection.Band1E0);
        RemoveMeta("playable_zoom_factor");
        ScheduleNativeMapFrame();
        PresentGodotScenery(frame);
        return true;
    }

    private float? PrepareSourceSeasonCameraFov()
    {
        var source=_sourceStartupCamera?.Source??_sourceMainCamera?.Source;
        int? month=_scenarioPreviewCalendar?.ThirdDate.Month??_logicalWorld?.NativeMapSeason?.ThirdDate.Month;
        if(month is null||source is null)return null;
        var selectedSeason=MapSeasonMath.SelectMonth(month.Value);
        if(_sourceMainCameraOptics is null||_sourceMainCameraOpticsSeason!=selectedSeason)
        {
            var resources=MapSeasonResourceConsumer.Load(_repository,month.Value);
            var provider=MapSeasonCameraApplication.ReadProvider(resources.Action);
            var seed=MapSeasonCameraApplication.ReadSourceSceneClockSeed(provider);
            var selected=MapSeasonCameraApplication.SelectSourceEmptyAxes(provider,
                seed.Controls.SelectorScalar1A0,seed.Controls.SelectorScalar1A4);
            _sourceMainCameraOptics=LoadedMainMapSeasonCameraOpticsSource.Load(
                _repository,_text.SourceLocale,selected);
            _sourceMainCameraOpticsSeason=selectedSeason;
        }
        uint extension=BinaryPrimitives.ReadUInt32LittleEndian(
            source.Camera.Properties[315].RawValue);
        // The source Main constructor sets1324.bit0. The owned primary Main
        // is Perspective (1488.bit0 clear); clip reversal does not gate V410.
        // This supported fresh Main has no later1078 edit; property315 is0.
        return _sourceMainCameraOptics.ComputeCurrent(
            LoadedMainMapSeasonCameraOpticsSource.FreshMainSource1324,
            _sourceMainCameraFrame?.Camera.Lens.Flags??0,extension,
            LoadedMainMapSeasonCameraOpticsSource.OrdinaryMainV468CallerFlags);
    }

    /// <summary>Source row-vector inverse View rows become Godot basis columns.
    /// Source perspective has M34=-1; its backward camera axis is Godot +Z.</summary>
    public static Transform3D CreateSourceCameraGodotTransform(MapCameraConstructionPacket camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        var world=NativeMatrixMath.Invert(camera.SourceWorldToView,out _);
        var basis=new Basis(new(world.M11,world.M12,world.M13),new(world.M21,world.M22,world.M23),new(world.M31,world.M32,world.M33));
        return new(basis,new(camera.CurrentPose.Position80.X,camera.CurrentPose.Position80.Y,camera.CurrentPose.Position80.Z));
    }

    public override void _Process(double delta)
    {
        if(!_loaded||_adoptingStartedCampaign||_sourceMainCamera is null&&_sourceStartupCamera is null||_sourceMainCameraInputs is not { } input)return;
        if((_detailsOpen&&storyPresentationOwner is null&&chronologyOwner is null)||!IsVisibleInTree())
        {
            ResetSourceCameraInput();
            // A settings preview may rebuild the physical Cloud graph. Give
            // its bootstrap real process elapsed time while the dialog owns
            // input; do not tick the camera or advance campaign simulation.
            if (_cgQualityRebuilding && IsVisibleInTree()) AdvanceNativeMapPresentationTime(delta);
            return;
        }
        if(UseD3D11Map&&_d3d11Map is {LastError:not null})return;
        // The explicit owner gate above admits Story presentation. UI Run
        // intentionally rejects all details input and cannot drive this clock.
        try
        {
            long sourceFrameStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!_periodMapActive || !_periodMapPaused) _sourceActiveMilliseconds += delta * 1000;
            int clock=unchecked((int)Time.GetTicksMsec());
            if(chronologyCamera is not null) { } // Dedicated source controller has no input override.
            else if(_sourceStartupCamera is {} startup)startup.Advance(clock);
            else
            {
                if(storyPresentationOwner is null && uniqueDirectionOwner is null && !sourceCameraInputSuspended)ApplySourceCameraInputFrame(clock,input);
                _sourceMainCamera!.Tick(clock,clock,_sourceMainCamera.Parameters.SourceInertiaTimeoutMilliseconds,
                    input.CurrentMaximumYOverride,SourceCameraTerrainQualifier);
            }
            AdvanceNativeMapPresentationTime(delta);
            if(_d3d11Map is not null)_d3d11Elapsed+=delta;
            // Normal interactive Map scope; original dialog/transition v30
            // mappings are still a separate UI-state task.
            // Godot's application limiter admits this actual process
            // frame. Polling a second wall-clock limiter here would discard
            // real callbacks instead of waiting for the original App frame.
            _d3d11AnimationDue=D3D11MapInteractive&&_d3d11AnimationPacer?.TryAcceptProcessFrame(Engine.GetProcessFrames())==true;
            if (_d3d11AnimationDue) SourceAnimationUpdateSequence++;
            bool tacticUpdateDue = _d3d11AnimationDue;
            if (_d3d11AnimationDue && _d3d11AnimationPacer is { } pacer)
            {
                AdvanceMapSummaryIndicators(pacer.FramesPerSecond(pacer.FpsMode));
                _d3d11Markers?.AdvanceCityAttention(pacer.FramesPerSecond(pacer.FpsMode));
                _d3d11Markers?.AdvanceDamageFeedback(pacer.FpsMode);
            }
            PublishSourceCamera();
            AdvanceUniqueDirectionClock(delta, tacticUpdateDue);
            AdvanceStoryEffectClock(delta, tacticUpdateDue);
            if (tacticUpdateDue) ordinaryTacticUpdate?.Invoke();
            if (tacticUpdateDue && _d3d11AnimationPacer is { } progressPacer)
                _periodProgressFrameDriver?.OnSourceFrame(progressPacer.FramesPerSecond(progressPacer.FpsMode), sourceFrameStartedAt);
        }
        catch (Exception error) { HandleSceneError(error); }
    }

    private bool HandleSourceCameraInput(InputEvent input)
    {
        if(!_embedded||_detailsOpen||_sourceMainCamera is null||_sourceMainCameraFrame is null||_sourceMainCameraInputs is not { } current)return false;
        if(input is InputEventMouseButton button)
        {
            var cursor=CanonicalSourceCursor(button.Position);
            _sourceCameraCurrentCursor=cursor;_sourceCameraCurrentCursorKnown=true;
            SetSourceCameraLogicalButton(button.ButtonIndex,button.Pressed);
            sourceCameraShiftHeld=button.ShiftPressed;sourceCameraControlHeld=button.CtrlPressed;
            if(button.ButtonIndex==MouseButton.Left)
            {
                if(!_periodMapActive&&button.Pressed&&button.DoubleClick&&_commandTargetPicked is null&&_openCityDetails is not null)
                {
                    ResetSourceCameraInput();PickTerrain(button.Position,released:false);
                    if(_mapSelection.Current?.CityDetails?.Id is int cityId)_openCityDetails(cityId);
                    return true;
                }
                if(button.Pressed)
                {
                    _sourceCameraLeftUiPressed=true;_pressPosition=button.Position;_dragDistance=0;
                    if(!_periodMapActive || _periodMapOwner?.ManualCommandSelectionOpen == true && _commandTargetPicked is not null)
                        _commandTargetPressAllowed=CommandTargetPointerDown(button.Position);
                    if(!_periodMapActive) SampleUnitSelectionPointer(cursor,3);
                }
                else
                {
                    bool selectionConsumed=!_periodMapActive&&SampleUnitSelectionPointer(cursor,1);
                    bool selectionShift=(_unitSelectionPointer is not null||_commandTargetShiftWaypoint)&&sourceCameraShiftHeld;
                    var pressed=CanonicalSourceCursor(_pressPosition);
                    int dx=unchecked(cursor.X34-pressed.X34),dy=unchecked(cursor.Y38-pressed.Y38);
                    bool click=!selectionConsumed&&_sourceCameraLeftUiPressed&&
                        (selectionShift?unchecked(dx*dx+dy*dy)<=100:
                            _dragDistance<=4&&_pressPosition.DistanceTo(button.Position)<=4);
                    _sourceCameraLeftUiPressed=false;
                    bool manualTarget = _periodMapOwner?.ManualCommandSelectionOpen == true && _commandTargetPicked is not null;
                    if((!_periodMapActive||manualTarget)&&click&&(_commandTargetPicked is null||_commandTargetPressAllowed))PickTerrain(button.Position,released:true);
                    _commandTargetPressAllowed=false;
                }
            }
            else if(button.Pressed&&button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                _sourceCameraPendingWheel=unchecked(_sourceCameraPendingWheel+(button.ButtonIndex==MouseButton.WheelUp?120:-120));
            }
            return true;
        }
        if(input is InputEventMouseMotion motion)
        {
            if(_sourceCameraLeftUiPressed)_dragDistance+=motion.Relative.Length();
            _sourceCameraCurrentCursor=CanonicalSourceCursor(motion.Position);_sourceCameraCurrentCursorKnown=true;
            return true;
        }
        return false;
    }

    private void ApplySourceCameraInputFrame(int clock,SceneViewSourceCameraInputs input)
    {
        //152B0 submits keyboard, mouse and edge requests in that order. Each
        // consumer reads the latest controller; the normal tick publishes later.
        ApplySourceNavigationKeyboard(input);
        ApplySourceCameraPointerFrame(clock,input);
        ApplySourceNavigationEdge(input);
    }

    private void ApplySourceCameraPointerFrame(int clock,SceneViewSourceCameraInputs input)
    {
        if(_sourceMainCamera is null||_sourceMainCameraFrame is null||!_sourceCameraCurrentCursorKnown)return;
        // The remake owns these raw buffers: its prior sampled levels are +58,
        // current sampled levels are +70. Source logical right/left/middle
        // use slots0/1/2; window input and GUI input share this mapping.
        int[] codes=new int[3];int active=-1;
        for(int i=0;i<3;i++)
        {
            bool a=_sourceCameraRawPrior58[i],b=_sourceCameraRawNow70[i];
            int code=a?(b?2:1):(b?3:0);codes[i]=code;
            _sourceCameraRawCounters[i]=code==2?unchecked(_sourceCameraRawCounters[i]+1):0;
            if(code==3)active=i;
            else if(active==-1&&_sourceCameraActiveButton==i&&code is 1 or 2)active=i;
        }
        var cursor=_sourceCameraCurrentCursor;
        if(active>=0)
        {
            if(active!=_sourceCameraActiveButton)_sourceCameraPressedCursor=cursor;
            _sourceCameraHeldFrames=_sourceCameraRawCounters[active];
        }
        else {_sourceCameraPressedCursor=default;_sourceCameraHeldFrames=0;}
        _sourceCameraActiveButton=active;
        var delta=MapCameraController.CanonicalDelta(cursor,
            _sourceCameraPreviousCursorKnown?_sourceCameraPreviousCursor:cursor,true);
        _sourceCameraPreviousCursor=cursor;_sourceCameraPreviousCursorKnown=true;
        int wheel=_sourceCameraPendingWheel;_sourceCameraPendingWheel=0;
        Array.Copy(_sourceCameraRawNow70,_sourceCameraRawPrior58,3);
        bool selectionConsumed=!_periodMapActive&&SampleUnitSelectionPointer(cursor,codes[1]);
        // A release over chrome may be consumed before Surface.GuiInput.
        // Retire its surface press even when only WindowInput observed it.
        if(codes[1]==1)_sourceCameraLeftUiPressed=false;
        if(selectionConsumed)return;
        if(delta.X44==0&&delta.Y48==0&&wheel==0)return;
        if(active==1&&SourceDragThreshold(cursor))
            _sourceMainCamera.ApplyPanDragFromFrame(_sourceMainCameraFrame,delta.X44,delta.Y48,codes[1]==1,
                clock,input.CurrentMaximumYOverride,SourceCameraTerrainQualifier);
        else if(active==0)
            _sourceMainCamera.ApplyOrbitDrag(delta.X44,delta.Y48,input.CurrentMaximumYOverride,SourceCameraTerrainQualifier);
        else if(wheel!=0)
        {
            if(_sourceMainCameraTerrain is null&&MapCameraController.SelectWheelZoomTarget(
                _sourceMainCamera.Parameters,_sourceMainCamera.CurrentZoom,wheel).Band<2)
            {SetSourceCameraStatus("pending_terrain");return;}
            _sourceMainCamera.QueueWheelZoomFromFrame(_sourceMainCameraFrame,wheel,cursor.X34,cursor.Y38,
                clock,input.CurrentMaximumYOverride,SourceCameraTerrainQualifier);
        }
    }

    private bool SourceDragThreshold(MainMapCameraCanonicalPoint current)
    {
        return MapHoverSelection.IsFrozen(_sourceCameraActiveButton,_sourceCameraHeldFrames,
            _sourceCameraPressedCursor.X34,_sourceCameraPressedCursor.Y38,current.X34,current.Y38);
    }

    private void ResetSourceCameraInput()
    {
        ResetUnitSelectionPointer();
        ResetSourceNavigationKeyboard();
        _sourceCameraActiveButton=-1;_sourceCameraHeldFrames=0;
        _sourceCameraPreviousCursorKnown=false;_sourceCameraCurrentCursorKnown=false;_sourceCameraPendingWheel=0;
        _sourceCameraLeftUiPressed=false;_sourceRightCancelPress=null;
        _commandTargetPressAllowed=false;
        Array.Clear(_sourceCameraRawNow70);Array.Clear(_sourceCameraRawPrior58);Array.Clear(_sourceCameraRawCounters);
    }

    private MainMapCameraCanonicalPoint CanonicalSourceCursor(Vector2 surfacePosition)
    {
        var viewport=(SubViewport)_surface.GetChild(0);
        return MapCameraController.CanonicalCursor(new(unchecked((int)surfacePosition.X),unchecked((int)surfacePosition.Y)),
            unchecked((int)_surface.Size.X),unchecked((int)_surface.Size.Y),viewport.Size.X,viewport.Size.Y);
    }

    private void SetSourceCameraLogicalButton(MouseButton button,bool pressed)
    {
        int slot=button switch{MouseButton.Right=>0,MouseButton.Left=>1,MouseButton.Middle=>2,_=>-1};
        if(slot>=0)_sourceCameraRawNow70[slot]=pressed;
    }

    // Main owns mode3's cancel action; this map owns its canonical pointer
    // geometry and clears an armed click with the rest of its input lifecycle.
    internal bool SourceRightCancelReleased(InputEventMouseButton button)
    {
        Vector2 local=_surface.GetGlobalTransformWithCanvas().AffineInverse()*button.Position;
        var cursor=CanonicalSourceCursor(local);
        if(button.Pressed){_sourceRightCancelPress=cursor;return false;}
        var start=_sourceRightCancelPress;_sourceRightCancelPress=null;
        if(start is not { } pressed)return false;
        int x=unchecked(cursor.X34-pressed.X34),y=unchecked(cursor.Y38-pressed.Y38);
        // Source button-up clears the held-frame counter before the cancel
        // predicate. Only its canonical endpoint distance remains here.
        return unchecked(x*x+y*y)<=100;
    }

    internal bool FocusReportHex(int physicalHex) =>
        (uint)physicalHex < 48400u && FocusSourceCamera(physicalHex);

    private bool FocusSourceCamera(int physicalHex)
    {
        if(_sourceMainCamera is null||_sourceMainCameraInputs is not { } input)return false;
        var focus=CeHexWorldCoordinates.Project(physicalHex);
        var request=new MainMapCameraControllerRequest(0,new((float)focus.X,0,(float)focus.Z,1),-1,62.83185308,-1,0);
        _sourceMainCamera.ApplyImmediateRequest(request,input.CurrentMaximumYOverride,SourceCameraTerrainQualifier);
        PublishSourceCamera();
        return true;
    }

    private bool TrySourceCameraHexPick(Vector2 surfacePosition,out TerrainPick hit,out int hex)
    {
        hit=default;hex=-1;
        var cursor=CanonicalSourceCursor(surfacePosition);
        if(ReadSourceCameraRay(cursor,out var camera,out var picker,out var ray)!=MapHoverRayStatus.Ready)return false;
        var o=ray.Origin;var d=ray.Direction;
        if(!MapCameraPickSegment.TryCreate(camera!.Camera.Geometry,ray,out var segment)||
            !picker!.TryPick(new(segment.Origin.X,segment.Origin.Y,segment.Origin.Z),
                new(segment.Direction.X,segment.Direction.Y,segment.Direction.Z),segment.MaximumDistance,out hit))return false;
        hit=hit with {Distance=hit.Distance+segment.StartDistance};
        if(_projection?.Policy.SourceFormulaId==CeHexWorldCoordinates.FormulaId)
            return CeHexWorldCoordinates.TryPickTerrainHit(new(o.X,o.Y,o.Z),new(d.X,d.Y,d.Z),
                new(hit.Position.X,hit.Position.Y,hit.Position.Z),out hex);
        return _projection?.TryPick(hit.Position.X,hit.Position.Z,out hex)==true;
    }

    private MapHoverRayStatus ReadSourceCameraRay(MainMapCameraCanonicalPoint cursor,
        out MainMapCameraFrame? camera,out TerrainPicker? picker,out MainMapCameraCursorRay ray)
    {
        camera=null;picker=null;ray=default;
        if(UseD3D11Map&&!D3D11MapInteractive)return MapHoverRayStatus.Unavailable;
        camera=_sourceMainCameraFrame;picker=CurrentTerrainPicker;
        if(_d3d11PresentedInput is {SourceFrame:{ } nativeCamera,TerrainPicker:{ } nativePicker})
        {camera=nativeCamera;picker=nativePicker;}
        if(_nativeMapOwner is { } native && native.Package.HasAcceptedOutput && native.Package.Surface.Visible)
        {
            // The camera controller and LOD can advance while GPU work is
            // queued. Pick the image that is actually on screen, including
            // after a failed update that retained its prior live output.
            if(_nativeMapPresentedCapture is not { TerrainPicker: { } displayedPicker } displayed)return MapHoverRayStatus.Unavailable;
            camera=displayed.MainCamera;picker=displayedPicker;
        }
        if(camera is null)return MapHoverRayStatus.NoWrite;
        if(picker is null)return MapHoverRayStatus.Unavailable;
        try {ray=camera.CursorRay(cursor.X34,cursor.Y38);}
        catch(InvalidDataException){return MapHoverRayStatus.Unavailable;}
        return ray.SourceReady?MapHoverRayStatus.Ready:MapHoverRayStatus.NoWrite;
    }

    private void ReadSourceCameraMessages()
    {
        if(_sourceCameraMessages.Count!=0)return;
        using var file=Godot.FileAccess.Open("res://ui/source-main-map-camera.json",Godot.FileAccess.ModeFlags.Read)
            ??throw new InvalidDataException("Source map camera status resource is missing.");
        using var document=JsonDocument.Parse(file.GetAsText());
        foreach(var message in document.RootElement.GetProperty(_text.SourceLocale).EnumerateObject())
            _sourceCameraMessages.Add(message.Name,message.Value.GetString()??throw new InvalidDataException("Source camera status text is empty."));
    }

    private void SetSourceCameraStatus(string key)
    {
        ReadSourceCameraMessages();
        SetMeta("source_main_camera_status",key);
        SetMeta("source_main_camera_status_text",_sourceCameraMessages[key]);
        if(_sourceMainCameraNotice is not null)
        {
            _sourceMainCameraNotice.Text=_sourceCameraMessages[key];
            _sourceMainCameraNotice.Visible=key!="ready";
        }
        if(key!="ready")_locationNotice.Text=_sourceCameraMessages[key];
    }
}
