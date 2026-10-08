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
public sealed record OriginalSceneViewSourceCameraInputs(
    NVector3 CurrentScale90, bool ContextOwner3E4Bit29, uint NativeClipProfile,
    OriginalNativeCameraTangentDispatch TangentDispatch, float OwnerFovOverride290,
    bool CurrentMaximumYOverride, ulong SourceImageBase, string Provenance);

public partial class OriginalSceneView
{
    private OriginalMainMapCameraAdapter? _sourceMainCamera;
    private OriginalMainMapCameraFrame? _sourceMainCameraFrame;
    private OriginalMainMapCameraTerrainSource? _sourceMainCameraTerrain;
    private OriginalSceneViewSourceCameraInputs? _sourceMainCameraInputs;
    private LoadedOriginalMainMapCameraNorthCommandSource? _sourceMainCameraNorth;
    private LoadedOriginalMainMapSeasonCameraOpticsSource? _sourceMainCameraOptics;
    private OriginalMapSeasonSelection? _sourceMainCameraOpticsSeason;
    private AspectRatioContainer? _sourceMainCameraAspect;
    private Label? _sourceMainCameraNotice;
    private readonly Dictionary<string,string> _sourceCameraMessages = [];
    private long _sourceMainCameraFrameNumber;
    private bool _sourceCameraPreviousCursorKnown;
    private OriginalMainMapCameraCanonicalPoint _sourceCameraPreviousCursor;
    private OriginalMainMapCameraCanonicalPoint _sourceCameraPressedCursor;
    private int _sourceCameraActiveButton = -1;
    private uint _sourceCameraHeldFrames;
    private OriginalMainMapCameraCanonicalPoint _sourceCameraCurrentCursor;
    private bool _sourceCameraCurrentCursorKnown;
    private int _sourceCameraPendingWheel;
    private readonly bool[] _sourceCameraRawNow70=new bool[3];
    private readonly bool[] _sourceCameraRawPrior58=new bool[3];
    private readonly uint[] _sourceCameraRawCounters=new uint[3];
    private bool _sourceCameraRightHeld,_sourceCameraMiddleHeld,_sourceCameraLeftUiPressed;
    private Func<OriginalMainMapCameraTerrainPitchInput,OriginalMainMapCameraTerrainPitchResult>? SourceCameraTerrainQualifier =>
        _sourceMainCameraTerrain is { } terrain?terrain.QualifyPitch:null;

    public OriginalMainMapCameraFrame? OriginalSourceMainCameraFrame => _sourceMainCameraFrame;
    public OriginalMainMapCameraAdapter? OriginalSourceMainCameraOwner => _sourceMainCamera;
    public OriginalMainMapCameraTerrainSource? OriginalSourceMainCameraTerrain => _sourceMainCameraTerrain;

    /// <summary>For a later owned renderer update. The source camera is
    /// initialized from the current campaign ruler, independent of RGB framing.</summary>
    public void SetOriginalSourceCameraInputs(OriginalSceneViewSourceCameraInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if(string.IsNullOrWhiteSpace(inputs.Provenance))
            throw new InvalidDataException("Current source camera publication requires renderer provenance.");
        _sourceMainCameraInputs=inputs;
        if(_loaded)
        {
            if(_sourceMainCamera is null)InitializeOriginalSourceCamera();
            else PublishOriginalSourceCamera();
        }
    }

    public void SetOriginalSourceCameraTerrain(OriginalMapStaticModelGroundHeightSource field)
    {
        if(_loadedSceneResources is not { } scene || field.Scene.Scene.Source.ResourceUri!=scene.Scene.Source.ResourceUri ||
            field.Scene.Scene.Source.DecodedSha256!=scene.Scene.Source.DecodedSha256)
            throw new InvalidDataException("Current camera Field must belong to the loaded source Scene revision.");
        _sourceMainCameraTerrain=OriginalMainMapCameraTerrainSource.FromCurrentField(field);
    }

    private void AttachOriginalSourceCameraSurface(VBoxContainer column)
    {
        if(!_embedded){column.AddChild(_surface);return;}
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

    private void InitializeOriginalSourceCamera()
    {
        if(!_embedded||_logicalWorld is not GameState world||_playerForceId is not int player||_loadedSceneResources is null)return;
        ReadOriginalSourceCameraMessages();
        try
        {
            _sourceMainCameraInputs??=ReadFreshOriginalSourceCameraInputs();
            if(_sourceMainCameraTerrain is null)
            {
                try {ConstructOriginalSourceCameraTerrain();}
                catch(InvalidDataException error)
                {SetMeta("source_main_camera_terrain_error",error.Message);SetOriginalSourceCameraStatus("pending_terrain");}
            }
            _sourceMainCamera=OriginalMainMapCameraAdapter.FromOwnedCampaign(_repository,_text.SourceLocale,
                world,player,$"remake-owned-world:{world.ScenarioKey}@{world.ContentHashes.GetValueOrDefault(world.ScenarioKey,"")};map={world.MapSha256}",
                _sourceMainCameraInputs.CurrentMaximumYOverride);
            _sourceMainCameraNorth??=OriginalMainMapCameraNorthCommandSource.Load(_repository,_text.SourceLocale);
            // Host210 is the successfully created asset context. This Scene
            // has actually loaded its assets; the owned Main Data/CPU above
            // exist. Fresh context flags or GPU target readiness are unrelated.
            _sourceMainCamera.ApplyTitleExitFar(OriginalMainMapCameraEntrySource.Load(_repository,_text.SourceLocale),
                new(MainDataPresent:true,HostGate210Present:_loadedSceneResources is not null,MainCpuPresent:true));
            _sourceMainCameraOptics=null;_sourceMainCameraOpticsSeason=null;_sourceMainCameraFrame=null;
            _sourceMainCameraFrameNumber=0;
            ResetOriginalSourceCameraInput();
            PublishOriginalSourceCamera();
            SetOriginalSourceCameraStatus(_sourceMainCameraTerrain is null?"pending_terrain":"ready");
        }
        catch(InvalidDataException error)
        {
            _sourceMainCamera=null;_sourceMainCameraFrame=null;
            SetMeta("source_main_camera_error",error.Message);
            SetOriginalSourceCameraStatus("unavailable");
        }
    }

    private void ReleaseOriginalSourceCamera()
    {
        _sourceMainCamera=null;_sourceMainCameraFrame=null;_sourceMainCameraTerrain=null;
        _sourceMainCameraNorth=null;
        _sourceMainCameraOptics=null;_sourceMainCameraOpticsSeason=null;
        _sourceMainCameraFrameNumber=0;
        ResetOriginalSourceCameraInput();
        RemoveMeta("source_main_camera_owner");
        RemoveMeta("source_main_camera_ready");
        if(_sourceMainCameraNotice is not null)_sourceMainCameraNotice.Visible=false;
    }

    private bool ResetOriginalSourceCamera()
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
        InitializeOriginalSourceCamera();
        return _sourceMainCameraFrame is not null;
    }

    private bool PublishOriginalSourceCamera()
    {
        if(!_loaded||_sourceMainCamera is null||_sourceMainCameraInputs is not { } input)return false;
        var frame=_sourceMainCamera.PrepareCurrent(new(input.CurrentScale90,input.ContextOwner3E4Bit29,
            input.NativeClipProfile,input.TangentDispatch,input.OwnerFovOverride290,input.SourceImageBase,
            ++_sourceMainCameraFrameNumber,input.Provenance)
            {SourceSelectedFovV410=PrepareOriginalSourceSeasonCameraFov()});
        _sourceMainCameraFrame=frame;
        var camera=frame.Camera;
        var position=ToGodot(camera.CurrentPose.Position80);
        if(!UseOriginalD3D11Map)
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
        if(!UseOriginalD3D11Map&&_terrainGbiMaterial is not null)
        {
            _terrainGbiMaterial.SetEye(_terrainGbiMaterial.Material,position);
            if(_territoryOverlay is not null)_terrainGbiMaterial.SetEye(_territoryOverlay.Material,position);
        }
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
        ScheduleOriginalNativeMapFrame();
        return true;
    }

    private float? PrepareOriginalSourceSeasonCameraFov()
    {
        if(_logicalWorld?.NativeMapSeason is not { } season||_sourceMainCamera is null)return null;
        var selectedSeason=OriginalMapSeasonMath.SelectMonth(season.ThirdDate.Month);
        if(_sourceMainCameraOptics is null||_sourceMainCameraOpticsSeason!=selectedSeason)
        {
            var resources=OriginalMapSeasonResourceConsumer.Load(_repository,season.ThirdDate.Month);
            var provider=OriginalMapSeasonCameraApplication.ReadProvider(resources.Action);
            var seed=OriginalMapSeasonCameraApplication.ReadSourceSceneClockSeed(provider);
            var selected=OriginalMapSeasonCameraApplication.SelectSourceEmptyAxes(provider,
                seed.Controls.SelectorScalar1A0,seed.Controls.SelectorScalar1A4);
            _sourceMainCameraOptics=LoadedOriginalMainMapSeasonCameraOpticsSource.Load(
                _repository,_text.SourceLocale,selected);
            _sourceMainCameraOpticsSeason=selectedSeason;
        }
        uint extension=BinaryPrimitives.ReadUInt32LittleEndian(
            _sourceMainCamera.Source.Camera.Properties[315].RawValue);
        // The source Main constructor sets1324.bit0. The owned primary Main
        // is Perspective (1488.bit0 clear); clip reversal does not gate V410.
        // This supported fresh Main has no later1078 edit; property315 is0.
        return _sourceMainCameraOptics.ComputeCurrent(
            LoadedOriginalMainMapSeasonCameraOpticsSource.FreshMainSource1324,
            _sourceMainCameraFrame?.Camera.Lens.Flags??0,extension,
            LoadedOriginalMainMapSeasonCameraOpticsSource.OrdinaryMainV468CallerFlags);
    }

    /// <summary>Source row-vector inverse View rows become Godot basis columns.
    /// Source perspective has M34=-1; its backward camera axis is Godot +Z.</summary>
    public static Transform3D CreateSourceCameraGodotTransform(OriginalMapCameraConstructionPacket camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        var world=OriginalNativeMatrixMath.Inverse3F6EB0(camera.SourceWorldToView,out _);
        var basis=new Basis(new(world.M11,world.M12,world.M13),new(world.M21,world.M22,world.M23),new(world.M31,world.M32,world.M33));
        return new(basis,new(camera.CurrentPose.Position80.X,camera.CurrentPose.Position80.Y,camera.CurrentPose.Position80.Z));
    }

    public override void _Process(double delta)
    {
        if(!_loaded||_sourceMainCamera is null||_sourceMainCameraInputs is not { } input)return;
        if(_detailsOpen||!IsVisibleInTree()){ResetOriginalSourceCameraInput();return;}
        if(UseOriginalD3D11Map&&_d3d11Map is {LastError:not null})return;
        Run(()=>
        {
            int clock=unchecked((int)Time.GetTicksMsec());
            ApplyOriginalSourceCameraInputFrame(clock,input);
            _sourceMainCamera.Tick(clock,clock,_sourceMainCamera.Parameters.SourceInertiaTimeoutMilliseconds,
                input.CurrentMaximumYOverride,SourceCameraTerrainQualifier);
            AdvanceOriginalNativeMapPresentationTime(delta);
            if(_d3d11Map is not null)_d3d11Elapsed+=delta;
            // Normal interactive Map scope; original dialog/transition v30
            // mappings are still a separate UI-state task.
            _d3d11AnimationDue=OriginalD3D11MapInteractive&&_d3d11AnimationPacer?.TryAccept()==true;
            PublishOriginalSourceCamera();
        });
    }

    private bool HandleOriginalSourceCameraInput(InputEvent input)
    {
        if(!_embedded||_detailsOpen||_sourceMainCamera is null||_sourceMainCameraFrame is null||_sourceMainCameraInputs is not { } current)return false;
        if(input is InputEventMouseButton button)
        {
            var cursor=CanonicalSourceCursor(button.Position);
            _sourceCameraCurrentCursor=cursor;_sourceCameraCurrentCursorKnown=true;
            if(button.ButtonIndex==MouseButton.Left)
            {
                if(button.Pressed&&button.DoubleClick&&_openCityDetails is not null)
                {
                    ResetOriginalSourceCameraInput();PickTerrain(button.Position);
                    if(_selectedCityId is int cityId)_openCityDetails(cityId);
                    return true;
                }
                _sourceCameraRawNow70[0]=button.Pressed;
                if(button.Pressed){_sourceCameraLeftUiPressed=true;_pressPosition=button.Position;_dragDistance=0;}
                else
                {
                    bool click=_sourceCameraLeftUiPressed&&_dragDistance<=4&&_pressPosition.DistanceTo(button.Position)<=4;
                    _sourceCameraLeftUiPressed=false;
                    if(click)PickTerrain(button.Position);
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

    private void ApplyOriginalSourceCameraInputFrame(int clock,OriginalSceneViewSourceCameraInputs input)
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
        var delta=OriginalMainMapCameraController.CanonicalDelta(cursor,
            _sourceCameraPreviousCursorKnown?_sourceCameraPreviousCursor:cursor,true);
        _sourceCameraPreviousCursor=cursor;_sourceCameraPreviousCursorKnown=true;
        int wheel=_sourceCameraPendingWheel;_sourceCameraPendingWheel=0;
        Array.Copy(_sourceCameraRawNow70,_sourceCameraRawPrior58,3);
        if(delta.X44==0&&delta.Y48==0&&wheel==0)return;
        if(active==1&&OriginalSourceDragThreshold(cursor))
            _sourceMainCamera.ApplyPanDragFromFrame(_sourceMainCameraFrame,delta.X44,delta.Y48,codes[1]==1,
                clock,input.CurrentMaximumYOverride,SourceCameraTerrainQualifier);
        else if(active==0)
            _sourceMainCamera.ApplyOrbitDrag(delta.X44,delta.Y48,input.CurrentMaximumYOverride,SourceCameraTerrainQualifier);
        else if(wheel!=0)
        {
            if(_sourceMainCameraTerrain is null&&OriginalMainMapCameraController.SelectWheelZoomTarget(
                _sourceMainCamera.Parameters,_sourceMainCamera.CurrentZoom,wheel).Band<2)
            {SetOriginalSourceCameraStatus("pending_terrain");return;}
            _sourceMainCamera.QueueWheelZoomFromFrame(_sourceMainCameraFrame,wheel,cursor.X34,cursor.Y38,
                clock,input.CurrentMaximumYOverride,SourceCameraTerrainQualifier);
        }
    }

    private bool OriginalSourceDragThreshold(OriginalMainMapCameraCanonicalPoint current)
    {
        int x=unchecked(current.X34-_sourceCameraPressedCursor.X34),y=unchecked(current.Y38-_sourceCameraPressedCursor.Y38);
        return _sourceCameraHeldFrames>15||unchecked(x*x+y*y)>100;
    }

    private void ResetOriginalSourceCameraInput()
    {
        _sourceCameraActiveButton=-1;_sourceCameraHeldFrames=0;
        _sourceCameraPreviousCursorKnown=false;_sourceCameraCurrentCursorKnown=false;_sourceCameraPendingWheel=0;
        _sourceCameraRightHeld=false;_sourceCameraMiddleHeld=false;_sourceCameraLeftUiPressed=false;
        Array.Clear(_sourceCameraRawNow70);Array.Clear(_sourceCameraRawPrior58);Array.Clear(_sourceCameraRawCounters);
    }

    private OriginalMainMapCameraCanonicalPoint CanonicalSourceCursor(Vector2 surfacePosition)
    {
        var viewport=(SubViewport)_surface.GetChild(0);
        return OriginalMainMapCameraController.CanonicalCursor(new(unchecked((int)surfacePosition.X),unchecked((int)surfacePosition.Y)),
            unchecked((int)_surface.Size.X),unchecked((int)_surface.Size.Y),viewport.Size.X,viewport.Size.Y);
    }

    private bool FocusOriginalSourceCamera(int physicalHex)
    {
        if(_sourceMainCamera is null||_sourceMainCameraInputs is not { } input)return false;
        var focus=OriginalCeHexWorldCoordinates.Project(physicalHex);
        var request=new OriginalMainMapCameraControllerRequest(0,new((float)focus.X,0,(float)focus.Z,1),-1,62.83185308,-1,0);
        _sourceMainCamera.ApplyImmediateRequest(request,input.CurrentMaximumYOverride,SourceCameraTerrainQualifier);
        PublishOriginalSourceCamera();
        return true;
    }

    private bool TryOriginalSourceCameraPick(Vector2 surfacePosition,out TerrainPick hit)
    {
        hit=default;
        if(UseOriginalD3D11Map && !OriginalD3D11MapInteractive)return false;
        var camera=_sourceMainCameraFrame;
        var picker=_terrainPicker;
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

    private void ReadOriginalSourceCameraMessages()
    {
        if(_sourceCameraMessages.Count!=0)return;
        using var file=Godot.FileAccess.Open("res://ui/source-main-map-camera.json",Godot.FileAccess.ModeFlags.Read)
            ??throw new InvalidDataException("Source map camera status resource is missing.");
        using var document=JsonDocument.Parse(file.GetAsText());
        foreach(var message in document.RootElement.GetProperty(_text.SourceLocale).EnumerateObject())
            _sourceCameraMessages.Add(message.Name,message.Value.GetString()??throw new InvalidDataException("Source camera status text is empty."));
    }

    private void SetOriginalSourceCameraStatus(string key)
    {
        ReadOriginalSourceCameraMessages();
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
