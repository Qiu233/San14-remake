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
    private bool _sourceCameraRightHeld,_sourceCameraMiddleHeld,_sourceCameraLeftUiPressed;
    private Func<MainMapCameraTerrainPitchInput,MainMapCameraTerrainPitchResult>? SourceCameraTerrainQualifier =>
        _sourceMainCameraTerrain is { } terrain?terrain.QualifyPitch:null;

    public MainMapCameraFrame? SourceMainCameraFrame => _sourceMainCameraFrame;
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
            if(_sourceMainCamera is null)InitializeSourceCamera();
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
        if(!_embedded||_logicalWorld is not GameState world||_playerForceId is not int player||_loadedSceneResources is null)return;
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
            _sourceMainCamera=MainMapCameraAdapter.FromOwnedCampaign(_repository,_text.SourceLocale,
                world,player,$"remake-owned-world:{world.ScenarioKey}@{world.ContentHashes.GetValueOrDefault(world.ScenarioKey,"")};map={world.MapSha256}",
                _sourceMainCameraInputs.CurrentMaximumYOverride);
            _sourceMainCameraNorth??=MainMapCameraNorthCommandSource.Load(_repository,_text.SourceLocale);
            // Host210 is the successfully created asset context. This Scene
            // has actually loaded its assets; the owned Main Data/CPU above
            // exist. Fresh context flags or GPU target readiness are unrelated.
            _sourceMainCamera.ApplyTitleExitFar(MainMapCameraEntrySource.Load(_repository,_text.SourceLocale),
                new(MainDataPresent:true,HostGate210Present:_loadedSceneResources is not null,MainCpuPresent:true));
            _sourceMainCameraOptics=null;_sourceMainCameraOpticsSeason=null;_sourceMainCameraFrame=null;
            _sourceMainCameraFrameNumber=0;
            ResetSourceCameraInput();
            PublishSourceCamera();
            SetSourceCameraStatus(_sourceMainCameraTerrain is null?"pending_terrain":"ready");
        }
        catch(InvalidDataException error)
        {
            _sourceMainCamera=null;_sourceMainCameraFrame=null;
            SetMeta("source_main_camera_error",error.Message);
            SetSourceCameraStatus("unavailable");
        }
    }

    private void ReleaseSourceCamera()
    {
        _sourceMainCamera=null;_sourceMainCameraFrame=null;_sourceMainCameraTerrain=null;
        _deploymentTargetAnchors=null;_commandTargetPicked=null;_commandTargetPointerAllowed=null;
        _sourceMainCameraNorth=null;
        _sourceMainCameraOptics=null;_sourceMainCameraOpticsSeason=null;
        _sourceMainCameraFrameNumber=0;
        ResetSourceCameraInput();
        RemoveMeta("source_main_camera_owner");
        RemoveMeta("source_main_camera_ready");
        if(_sourceMainCameraNotice is not null)_sourceMainCameraNotice.Visible=false;
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
        if(!_loaded||_sourceMainCamera is null||_sourceMainCameraInputs is not { } input)return false;
        var frame=_sourceMainCamera.PrepareCurrent(new(input.CurrentScale90,input.ContextOwner3E4Bit29,
            input.NativeClipProfile,input.TangentDispatch,input.OwnerFovOverride290,
            ++_sourceMainCameraFrameNumber,input.Provenance)
            {SourceSelectedFovV410=PrepareSourceSeasonCameraFov()});
        _sourceMainCameraFrame=frame;
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
        SetMeta("playable_camera_calibration","original-source-main38D-controller");
        SetMeta("playable_zoom_level",frame.ZoomSelection.Band1E0);
        RemoveMeta("playable_zoom_factor");
        ScheduleNativeMapFrame();
        return true;
    }

    private float? PrepareSourceSeasonCameraFov()
    {
        if(_logicalWorld?.NativeMapSeason is not { } season||_sourceMainCamera is null)return null;
        var selectedSeason=MapSeasonMath.SelectMonth(season.ThirdDate.Month);
        if(_sourceMainCameraOptics is null||_sourceMainCameraOpticsSeason!=selectedSeason)
        {
            var resources=MapSeasonResourceConsumer.Load(_repository,season.ThirdDate.Month);
            var provider=MapSeasonCameraApplication.ReadProvider(resources.Action);
            var seed=MapSeasonCameraApplication.ReadSourceSceneClockSeed(provider);
            var selected=MapSeasonCameraApplication.SelectSourceEmptyAxes(provider,
                seed.Controls.SelectorScalar1A0,seed.Controls.SelectorScalar1A4);
            _sourceMainCameraOptics=LoadedMainMapSeasonCameraOpticsSource.Load(
                _repository,_text.SourceLocale,selected);
            _sourceMainCameraOpticsSeason=selectedSeason;
        }
        uint extension=BinaryPrimitives.ReadUInt32LittleEndian(
            _sourceMainCamera.Source.Camera.Properties[315].RawValue);
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
        if(!_loaded||_sourceMainCamera is null||_sourceMainCameraInputs is not { } input)return;
        if(_detailsOpen||!IsVisibleInTree()){ResetSourceCameraInput();return;}
        if(UseD3D11Map&&_d3d11Map is {LastError:not null})return;
        Run(()=>
        {
            int clock=unchecked((int)Time.GetTicksMsec());
            ApplySourceCameraInputFrame(clock,input);
            _sourceMainCamera.Tick(clock,clock,_sourceMainCamera.Parameters.SourceInertiaTimeoutMilliseconds,
                input.CurrentMaximumYOverride,SourceCameraTerrainQualifier);
            AdvanceNativeMapPresentationTime(delta);
            if(_d3d11Map is not null)_d3d11Elapsed+=delta;
            // Normal interactive Map scope; original dialog/transition v30
            // mappings are still a separate UI-state task.
            _d3d11AnimationDue=D3D11MapInteractive&&_d3d11AnimationPacer?.TryAccept()==true;
            PublishSourceCamera();
        });
    }

    private bool HandleSourceCameraInput(InputEvent input)
    {
        if(!_embedded||_detailsOpen||_sourceMainCamera is null||_sourceMainCameraFrame is null||_sourceMainCameraInputs is not { } current)return false;
        if(input is InputEventMouseButton button)
        {
            var cursor=CanonicalSourceCursor(button.Position);
            _sourceCameraCurrentCursor=cursor;_sourceCameraCurrentCursorKnown=true;
            if(button.ButtonIndex==MouseButton.Left)
            {
                if(button.Pressed&&button.DoubleClick&&_commandTargetPicked is null&&_openCityDetails is not null)
                {
                    ResetSourceCameraInput();PickTerrain(button.Position);
                    if(_mapSelection.Current?.CityDetails?.Id is int cityId)_openCityDetails(cityId);
                    return true;
                }
                _sourceCameraRawNow70[0]=button.Pressed;
                if(button.Pressed)
                {
                    _sourceCameraLeftUiPressed=true;_pressPosition=button.Position;_dragDistance=0;
                    _commandTargetPressAllowed=CommandTargetPointerDown(button.Position);
                }
                else
                {
                    bool click=_sourceCameraLeftUiPressed&&_dragDistance<=4&&_pressPosition.DistanceTo(button.Position)<=4;
                    _sourceCameraLeftUiPressed=false;
                    if(click&&(_commandTargetPicked is null||_commandTargetPressAllowed))PickTerrain(button.Position);
                    _commandTargetPressAllowed=false;
                }
            }
            else if(button.ButtonIndex is MouseButton.Right or MouseButton.Middle)
            {
                if(button.ButtonIndex==MouseButton.Right)_sourceCameraRightHeld=button.Pressed;
                else _sourceCameraMiddleHeld=button.Pressed;
                _sourceCameraRawNow70[1]=_sourceCameraRightHeld||_sourceCameraMiddleHeld;
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
        // current sampled levels are +70. Physical bindings are explicit remake
        // choices (left -> source slot0, right/middle -> slot1), not native labels.
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
        int x=unchecked(current.X34-_sourceCameraPressedCursor.X34),y=unchecked(current.Y38-_sourceCameraPressedCursor.Y38);
        return _sourceCameraHeldFrames>15||unchecked(x*x+y*y)>100;
    }

    private void ResetSourceCameraInput()
    {
        sourceCameraHeldCommands=0;sourceCameraControlHeld=false;sourceCameraShiftHeld=false;
        _sourceCameraActiveButton=-1;_sourceCameraHeldFrames=0;
        _sourceCameraPreviousCursorKnown=false;_sourceCameraCurrentCursorKnown=false;_sourceCameraPendingWheel=0;
        _sourceCameraRightHeld=false;_sourceCameraMiddleHeld=false;_sourceCameraLeftUiPressed=false;
        _commandTargetPressAllowed=false;
        Array.Clear(_sourceCameraRawNow70);Array.Clear(_sourceCameraRawPrior58);Array.Clear(_sourceCameraRawCounters);
    }

    private MainMapCameraCanonicalPoint CanonicalSourceCursor(Vector2 surfacePosition)
    {
        var viewport=(SubViewport)_surface.GetChild(0);
        return MapCameraController.CanonicalCursor(new(unchecked((int)surfacePosition.X),unchecked((int)surfacePosition.Y)),
            unchecked((int)_surface.Size.X),unchecked((int)_surface.Size.Y),viewport.Size.X,viewport.Size.Y);
    }

    private bool FocusSourceCamera(int physicalHex)
    {
        if(_sourceMainCamera is null||_sourceMainCameraInputs is not { } input)return false;
        var focus=CeHexWorldCoordinates.Project(physicalHex);
        var request=new MainMapCameraControllerRequest(0,new((float)focus.X,0,(float)focus.Z,1),-1,62.83185308,-1,0);
        _sourceMainCamera.ApplyImmediateRequest(request,input.CurrentMaximumYOverride,SourceCameraTerrainQualifier);
        PublishSourceCamera();
        return true;
    }

    private bool TrySourceCameraPick(Vector2 surfacePosition,out TerrainPick hit)
    {
        hit=default;
        if(UseD3D11Map && !D3D11MapInteractive)return false;
        var camera=_sourceMainCameraFrame;
        var picker=CurrentTerrainPicker;
        if(_d3d11PresentedInput is {SourceFrame:{ } nativeCamera,TerrainPicker:{ } nativePicker})
        {camera=nativeCamera;picker=nativePicker;}
        if(_nativeMapOwner is { } native && native.Package.HasAcceptedOutput && native.Package.Surface.Visible)
        {
            // The camera controller and LOD can advance while GPU work is
            // queued. Pick the image that is actually on screen, including
            // after a failed update that retained its prior live output.
            if(_nativeMapPresentedCapture is not { TerrainPicker: { } displayedPicker } displayed)return false;
            camera=displayed.MainCamera;picker=displayedPicker;
        }
        if(camera is null||picker is null)return false;
        var cursor=CanonicalSourceCursor(surfacePosition);var ray=camera.CursorRay(cursor.X34,cursor.Y38);
        if(!ray.SourceReady)return false;
        var o=ray.Origin;var d=ray.Direction;
        return picker.TryPick(new(o.X,o.Y,o.Z),new(d.X,d.Y,d.Z),camera.Camera.Lens.Far,out hit);
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
