using Godot;
using San14.Core;
using San14.Data;
using NMatrix=System.Numerics.Matrix4x4;

internal sealed record SceneryMapCapture(GameState World,SourceWorldContext Current,
    MainMapCameraFrame Camera,D3D11SceneryFrame Units,SceneryMapPresentation Presentation,
    NMatrix Viewport,MapControlSurfaceExtent Surface,bool CameraStable);

/// <summary>One real map's labels, model positions and UI resources, shared
/// by the native and ordinary Godot presentation paths.</summary>
internal partial class SceneryMapLayer : Control
{
    internal D3D11SceneryMapLabels Controller {get;}=new(){Name="SceneryLabels"};
    internal D3D11ScenerySource Source {get;private set;}=null!;
    internal SceneryMapCapture? Prepared {get;private set;}
    internal SceneryMapCapture? Accepted {get;private set;}
    private bool ownsResources;
    private MapFont font=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private MapPresentationState presentation=null!;
    private Func<GameState,SourceWorldContext?> capture=null!;
    private GameState? currentWorld;
    private SourceWorldContext? current;
    private bool retired,acceptAfterDraw,acceptFrameSubscribed;
    private bool? capturedClipBypass;
    private Func<Vector2I>? renderExtent;
    private Func<GameState,SourceWorldContext,SceneryMapHexSelection>? readPrimary;
    private Func<bool>? inputAllowed;

    internal SceneryMapLayer()
    {
        MouseFilter=MouseFilterEnum.Ignore;
        AddChild(Controller);Controller.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }
    internal void Configure(GameResourceRepository repository,LoadedSceneResources scene,
        string fixedDataPath,string locale,GameState world,MapPresentationState presentation,
        Func<GameState,SourceWorldContext?> capture,Func<Vector2I?> pointer,Action<int> selected,
        Func<Vector2I> renderExtent,Func<GameState,SourceWorldContext,SceneryMapHexSelection>? readPrimary=null,
        Func<bool>? inputAllowed=null,Func<int,bool>? canPress=null,
        AdministrationPageChrome.Resources? resources=null)
    {
        this.presentation=presentation;this.capture=capture;this.renderExtent=renderExtent;
        this.readPrimary=readPrimary;this.inputAllowed=inputAllowed;
        ownsResources=resources is null;
        assets=resources?.Assets??new(repository);
        font=resources?.Font??new(repository,locale);
        shader=resources?.SpriteShader??UiSourceSprite.CreateShader();
        var definitions=repository.LoadSceneries(fixedDataPath);
        Source=new(repository,scene,definitions,Current)
        {ReadProjectionClipBypass=_=>capturedClipBypass};
        Controller.Configure(presentation.AllocateLayoutIdentity(),Current(world),
            SceneryMapMarkerSources.Load(repository),assets,shader,font,MapTextPresets.Create(),
            MapSpritePalettes.Create(),AreaMapMarkerSources.LoadCurrentTc(repository),pointer,selected,canPress);
        RenderingServer.FramePostDraw+=AcceptGodotFrame;
        acceptFrameSubscribed=true;
    }
    private SourceWorldContext Current(GameState world)
    {
        if(!ReferenceEquals(currentWorld,world))
        {
            current=capture(world)??throw new InvalidDataException("Current map Scenery state is unavailable.");
            currentWorld=world;
            current.SceneryLayoutReader=Controller.ReadLayoutReference;
            current.SceneryLayoutDirtyWriter=Controller.WriteDirty;
        }
        return current!;
    }
    internal SceneryMapCapture Capture(GameState world,MainMapCameraModePacket mode,
        bool cameraStable,Vector2I actualRenderExtent,SceneryMapPresentation? capturedPresentation=null)
    {
        var c=Current(world);
        if(actualRenderExtent.X<=0||actualRenderExtent.Y<=0)
            throw new InvalidDataException("The actual map render target has no extent.");
        float w=actualRenderExtent.X,h=actualRenderExtent.Y;
        var viewport=new NMatrix(w/2,0,0,0,0,-h/2,0,0,0,0,1,0,w/2,h/2,0,1);
        var p=capturedPresentation??presentation.Capture();capturedClipBypass=p.ProjectionClipBypass;
        return new(world,c,mode.AcceptedMainFrame,Source.Capture(world,mode),p,
            viewport,new((uint)actualRenderExtent.X,(uint)actualRenderExtent.Y),cameraStable);
    }
    internal void Present(SceneryMapCapture frame,bool ordinaryGodot)
    {
        if(retired)return;
        if(inputAllowed?.Invoke()==false){Suspend();return;}
        Prepared=frame;acceptAfterDraw=ordinaryGodot;
        Controller.BindCurrentInput(frame.Current,readPrimary?.Invoke(frame.World,frame.Current)??new(false,null));
        var p=frame.Presentation;
        Controller.Present(frame.World,frame.Current,frame.Units,frame.Camera,p.Policy,
            frame.CameraStable,frame.Viewport,p.ProjectionClipBypass,frame.Surface,true,
            p.FieldRefreshSuppressed,p.AlphaRefreshSuppressed,Controller.ReadCurrentAlphaInputs);
    }
    internal void Accept(SceneryMapCapture frame)
    {
        if(retired||!ReferenceEquals(Prepared,frame))return;
        if(inputAllowed?.Invoke()==false){Suspend();return;}
        Controller.AcceptPrepared(frame.Camera);Accepted=frame;
    }
    private void AcceptGodotFrame()
    {
        if(!acceptAfterDraw||Prepared is not {} frame||renderExtent is null)return;
        var extent=renderExtent();
        // Layout can resize the actual SubViewport after camera preparation.
        // Do not admit clicks for that older target extent.
        if(frame.Surface.Width==(uint)extent.X&&frame.Surface.Height==(uint)extent.Y)Accept(frame);
    }
    internal SceneryLayoutReference ReadLayoutReference()=>Controller.ReadLayoutReference();
    internal void WriteDirty(SceneryLayoutReference target,int value)=>Controller.WriteDirty(target,value);
    internal void Abandon()
    {
        if(retired)return;
        retired=true;acceptAfterDraw=false;Prepared=null;Accepted=null;renderExtent=null;
        readPrimary=null;inputAllowed=null;
        // Configure may fail before the actual signal subscription exists.
        if(acceptFrameSubscribed)
        {
            acceptFrameSubscribed=false;
            RenderingServer.FramePostDraw-=AcceptGodotFrame;
        }
        Controller.Abandon();
    }
    internal void Suspend()
    {Prepared=null;acceptAfterDraw=false;Controller.SuspendPresentation();}
    public override void _ExitTree()
    {
        Abandon();Source?.Dispose();
        if(ownsResources){font?.Dispose();assets?.Dispose();shader?.Dispose();}
    }
}
