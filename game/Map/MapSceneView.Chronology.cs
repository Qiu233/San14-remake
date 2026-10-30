using Godot;
using San14.Core;
using San14.Data;
using NVector3=System.Numerics.Vector3;
using NMatrix=System.Numerics.Matrix4x4;

public partial class MapSceneView
{
    private Control? chronologyOwner;
    private GameState? chronologyWorld;
    private MainMapCameraControllerState? chronologyPriorPose;
    private ChronologyMapCameraAdapter? chronologyCamera;
    private bool chronologyPriorDetails,chronologyPriorVisible,chronologyPriorGeographicVisible;
    private ChronologyMapFlareLayer? chronologyFlares;
    private D3D11TerritoryPublication? chronologyTerritory;
    private ChronologyInformationMapSnapshot? chronologySnapshot;
    private readonly List<(NVector3 Position,Vector2 Offset)?> chronologyAnchors=[];
    private float[]? chronologyMapColors;
    private ulong chronologyRevision,chronologyPresentedRevision;
    internal bool IsChronologyMapActive=>chronologyOwner is not null;

    internal bool BeginChronology(Control owner,ChronologyInformationMapSnapshot snapshot,out string? missing)
    {
        missing=null;
        if(chronologyOwner==owner){UpdateChronology(owner,snapshot);return true;}
        if(chronologyOwner is not null||!IsPlayableMapReady||_periodMapActive||_cgQualityRebuilding||
            storyPresentationOwner is not null||uniqueDirectionOwner is not null||
            _sourceMainCamera is not {CurrentRuntime.Transition:null,CurrentRuntime.RequestMode188:0} primary||
            _logicalWorld is not {} world||_d3d11Ownership is null||_d3d11Markers is null||ForcePalette is null)
        {missing="chronology.map.active-owner";return false;}
        try
        {
            chronologyOwner=owner;chronologyWorld=world;
            chronologyPriorPose=primary.CurrentState;
            chronologyPriorDetails=_detailsOpen;chronologyPriorVisible=Visible;
            chronologyPriorGeographicVisible=geographicView?.Visible==true;
            geographicView?.Hide();
            chronologyCamera=ChronologyMapCameraAdapter.FromPrimary(primary);
            chronologyMapColors=new float[60*4];
            for(int color=0;color<60;color++)
            {
                var value=ForcePalette.MapColor(color);int at=color*4;
                chronologyMapColors[at]=value.RedFloat;chronologyMapColors[at+1]=value.GreenFloat;
                chronologyMapColors[at+2]=value.BlueFloat;chronologyMapColors[at+3]=value.AlphaFloat;
            }
            var flares=new ChronologyMapFlareLayer{Name="ChronologyMapFlares",ZIndex=6};
            StrategicMapOverlayParent().AddChild(flares);chronologyFlares=flares;
            flares.Configure(_repository);
            owner.TreeExiting+=ChronologyOwnerExiting;
            // Keep real native presentation ticking, while ordinary command
            // and keyboard map input remain under their existing details gate.
            SetDetailsOpen(true,keepMapVisible:true);
            ResetSourceCameraInput();_sourceMainMode!.SetCurrentOverride28C(4);
            PublishSourceCamera();
            UpdateChronology(owner,snapshot);
            return true;
        }
        catch(Exception error) when(error is InvalidDataException or NotSupportedException)
        {missing=error.Message;RetireChronologyMap(restore:true);return false;}
        catch {RetireChronologyMap(restore:true);throw;}
    }

    internal void UpdateChronology(Control owner,ChronologyInformationMapSnapshot snapshot)
    {
        if(chronologyOwner!=owner||!ReferenceEquals(chronologyWorld,_logicalWorld))
            throw new InvalidOperationException("chronology.map.owner");
        if(ReferenceEquals(chronologySnapshot,snapshot))return;
        if(snapshot.AreaColors.IsDefault||snapshot.AreaColors.Length!=501||snapshot.HexIds.IsDefault||snapshot.HexIds.Length!=4)
            throw new InvalidDataException("chronology.map.saved-fields");
        foreach(byte color in snapshot.AreaColors)
            if(color>60)throw new InvalidDataException("chronology.map.saved-area-color");
        var current=_d3d11CurrentWorldQueries?.Invoke(chronologyWorld!)??
            throw new InvalidDataException("chronology.map.current-object-owner");
        var colors=new byte[D3D11TerritoryPublisher.CellCount];
        var map=_d3d11Ownership!.StaticMap;
        for(int hex=0;hex<colors.Length;hex++)
            colors[hex]=map.TerrainFlags[hex]==0?(byte)60:snapshot.AreaColors[Math.Min(map.AreaSlots[hex],(ushort)500)];
        var anchors=new List<(NVector3 Position,Vector2 Offset)?>(4);
        foreach(ushort hex in snapshot.HexIds)anchors.Add(CaptureChronologyFlare(current,hex));
        // Each selected record is one immutable publication. No world state,
        // live affiliation or caller-owned saved arrays are modified.
        _d3d11Markers!.ApplyChronologyShortNames(snapshot,current);
        chronologyAnchors.Clear();chronologyAnchors.AddRange(anchors);
        chronologyTerritory=new(colors,0xC000000000000000UL|++chronologyRevision,chronologyMapColors!);
        chronologySnapshot=snapshot;chronologyFlares!.HideFlares();
        RemoveMeta("chronology_map_unresolved");
        ScheduleD3D11MapFrame();
    }

    private (NVector3 Position,Vector2 Offset)? CaptureChronologyFlare(SourceWorldContext current,int hex)
    {
        if(hex>=48400)return null;
        var pointer=current.ReadHexTableReference(hex)??throw new InvalidDataException("chronology.flare.hex");
        ushort link=current.ReadHexObjectLink(pointer)??throw new InvalidDataException("chronology.flare.object-link");
        var city=current.ReadCityTableReference(link<52?link:0)??throw new InvalidDataException("chronology.flare.city");
        var gate=current.ReadGateTableReference(link is >=52 and <=61?link-51:0)??throw new InvalidDataException("chronology.flare.gate");
        bool cityValid=current.ReadBaseNativeValidity(city)??throw new InvalidDataException("chronology.flare.city-validity");
        bool gateValid=current.ReadBaseNativeValidity(gate)??throw new InvalidDataException("chronology.flare.gate-validity");
        var imperial=current.ReadCityTableReference(47)??throw new InvalidDataException("chronology.flare.imperial-city");
        if(hex==current.ReadBaseCurrentHexIndex(imperial)){cityValid=true;hex=2357;}
        var point=CeHexWorldCoordinates.Project(hex);float x=(float)point.X,z=(float)point.Z;
        float y=_sourceMainCameraTerrain?.Query(x,z)??throw new InvalidDataException("chronology.flare.landscape");
        return (new(x,Math.Max(445,y),z),cityValid?new(-46,-68):gateValid?new(-48,-68):new(-46,-49));
    }

    private void PresentChronologyFrame(D3D11MapInput input)
    {
        if(chronologyOwner is null||chronologyFlares is null||chronologyPresentedRevision==chronologyRevision||
            !ReferenceEquals(input.WorldState,chronologyWorld)||!ReferenceEquals(input.TerritoryOwnership,chronologyTerritory)||
            input.SourceFrame is not {} frame||chronologyCamera is null||frame.ControllerState!=chronologyCamera.CurrentState)return;
        var viewport=new NMatrix(960,0,0,0,0,-540,0,0,0,0,1,0,960,540,0,1);
        for(int index=0;index<chronologyAnchors.Count;index++)
        {
            Vector2? pixel=null;
            if(chronologyAnchors[index] is {} anchor)
            {
                var projected=ChronologyMapProjection.Project(new(anchor.Position,1),
                    frame.Camera.SourceWorldToProjection,viewport,false,new(1920,1080));
                if(projected is {} p)pixel=new Vector2(p.X,p.Y)+anchor.Offset;
                else SetMeta("chronology_map_unresolved","chronology.flare.clipped-output-not-written-by-source");
            }
            chronologyFlares.Present(index,pixel);
        }
        chronologyPresentedRevision=chronologyRevision;
    }

    internal void EndChronology(Control owner)
    {if(chronologyOwner==owner)RetireChronologyMap(restore:true);}
    private void ChronologyOwnerExiting()=>RetireChronologyMap(restore:true);
    private void RetireChronologyMap(bool restore)
    {
        if(chronologyOwner is not {} owner)return;
        chronologyOwner=null;
        if(GodotObject.IsInstanceValid(owner))owner.TreeExiting-=ChronologyOwnerExiting;
        chronologySnapshot=null;chronologyWorld=null;chronologyTerritory=null;chronologyMapColors=null;
        chronologyAnchors.Clear();chronologyPresentedRevision=0;chronologyCamera=null;
        _d3d11Markers?.ClearChronologyShortNames();
        if(chronologyFlares is {} flares){flares.Hide();flares.QueueFree();chronologyFlares=null;}
        if(restore&&_loaded&&!_nativeMapClosing&&_sourceMainCamera is {} primary&&chronologyPriorPose is {} prior)
        {
            primary.StopTimedMotion();
            primary.ApplyImmediateRequest(ChronologyMapCameraAdapter.CreateRestoreRequest(prior,primary.Parameters),
                _sourceMainCameraInputs!.CurrentMaximumYOverride,SourceCameraTerrainQualifier);
            primary.ApplyTitleExitFar(MainMapCameraEntrySource.Load(_repository,_text.SourceLocale),
                new(MainDataPresent:true,HostGate210Present:_loadedSceneResources is not null,MainCpuPresent:true));
            _sourceMainMode?.SetCurrentOverride28C(-1);
            SetDetailsOpen(chronologyPriorDetails,keepMapVisible:chronologyPriorVisible);Visible=chronologyPriorVisible;
            ResetSourceCameraInput();PublishSourceCamera();ScheduleD3D11MapFrame();
        }
        if(restore&&geographicView is {} geographic&&GodotObject.IsInstanceValid(geographic))
            geographic.Visible=chronologyPriorGeographicVisible;
        chronologyPriorPose=null;
    }
}
