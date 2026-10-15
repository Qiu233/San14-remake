using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Untextured controller-point preparation for the translated backend.
/// Selected resources and material destinations are imported once. The owned
/// backend clock is explicit. Phase candidates commit only
/// after a complete caller output succeeds, so failed output retries do not
/// advance the accepted animation twice.</summary>
public sealed class SkyAppliedSourceStarOwner
{
    private readonly GameResourceRepository repository;
    private readonly SkyStarResources resources;
    private readonly SkyWeatherNativePowerSource power;
    private readonly float[] table;
    private readonly string recipe;
    private float committedPhase;
    private long committedFrame;
    private SkyAppliedSourceStarFrame? candidate;
    private SkyAppliedSourceStarOwner(GameResourceRepository repository,SkyStarResources resources,
        SkyWeatherNativePowerSource power,string owner,string recipe)
    {this.repository=repository;this.resources=resources;this.power=power;OwnerIdentity=owner;this.recipe=recipe;table=SkyStarTwinkle.CreateTable();Program=resources.Select(0,false);}
    public string OwnerIdentity{get;}
    public float CommittedPhase=>committedPhase;
    public LoadedShaderContainer Shader=>resources.Shader;
    public SkyStarProgramSelection Program{get;}
    public SkyStarGeometry? Geometry=>resources.Geometry;
    public static SkyAppliedSourceStarOwner CreateFresh(GameResourceRepository repository,string locale,string ownedStarIdentity,
        NamedViewRendererResources renderer,MapSeasonCameraPackage package)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentException.ThrowIfNullOrWhiteSpace(ownedStarIdentity);
        var resources=SkyStarResources.LoadControllerPass(repository,renderer,package)??
            throw new NotSupportedException("This optional Star producer requires a selected Star resource owner.");
        return new(repository,resources,SkyWeatherNativePowerSource.Load(repository,locale),ownedStarIdentity,
            RenderSamplerSourceAdapters.ReadAppliedStarNativeRecipe(repository.SourceDirectory,locale));
    }
    public SkyAppliedSourceStarFrame PrepareCurrent(SkySceneSourceFramePacket sourceFrame,
        MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages,
        MainSceneNode51Execution actualSelectedView,float explicitOwnedClockDelta,string ownedClockProvenance)
    {
        ArgumentNullException.ThrowIfNull(sourceFrame);ArgumentNullException.ThrowIfNull(present);
        ArgumentNullException.ThrowIfNull(stages);ArgumentNullException.ThrowIfNull(actualSelectedView);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedClockProvenance);
        actualSelectedView.RequireSelectedView(present.FrameNumber,sourceFrame.Binding);
        var lighting=stages.AcceptedLighting;var selected=present.AcceptedSeasonCamera.Inputs.Source.Selection;
        stages.ValidateCurrent(lighting,selected,present.CurrentSkyCamera);
        if(!ReferenceEquals(stages.AcceptedPresent,present)||sourceFrame.FrameNumber!=present.FrameNumber||
            sourceFrame.Binding!=lighting.Binding||present.FrameNumber<=committedFrame||
            present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale!=power.Locale||
            !float.IsFinite(explicitOwnedClockDelta)||explicitOwnedClockDelta<0)
            throw new InvalidDataException("Star current packet requires the same accepted source frame and finite explicit owned clock.");
        var package=selected.FirstPackage;
        resources.RequireCompatibleController(repository,package);
        // C5197F/86 and C519E1/E8 load CPU56C/588; C419BD/C41A11
        // project them to Camera7F8/814. D4BD30's current packet has a
        // distinct -1 branch for nonprimary View, never HDRI setter slot3/4.
        var settings=SkyStarSettings.Import(package);
        float geometry40=actualSelectedView.SelectedViewIndex==0?settings.ExposureScale:-1f;
        float clockScale=settings.BlinkSpeed;
        float geometryDelta=M(clockScale,explicitOwnedClockDelta);
        var frame=sourceFrame.SourceFrame;
        if(resources.Geometry is not { PointCount: > 0 })
            return candidate=new(this,sourceFrame,present,stages,actualSelectedView,[],committedPhase,committedPhase,
                geometry40,geometryDelta,recipe+";no-controller-points;phase-retained;owner="+OwnerIdentity);
        if(frame.SixCameras.Count!=6||frame.Controller is null||!frame.Lights.SunPresent||!frame.Lights.MoonPresent)
            throw new NotSupportedException("Actual source Star requires six current cameras and the complete same-frame controller/light path.");
        var controller=SkySceneSourceDefaults.PrepareController(present.CurrentSkyCamera.Controller,
            present.CurrentSkyCamera.Parameters,frame.Lights.SunPresent,frame.Lights.MoonPresent);
        if(controller.Controller!=frame.Controller)
            throw new InvalidDataException("Star source geometry matrix/controller differs from the actual current Sky source.");
        var packets=new SkyStarConstantPacket[6];float phase=committedPhase;
        for(int face=0;face<6;face++)
        {
            var c=frame.SixCameras[face];
            if(c.TargetWidth<=0||c.TargetHeight<=0||c.Scalar138<=0||!float.IsFinite(geometry40)||geometry40==0)
                throw new InvalidDataException("Star source dimensions/FOV/geometry40 are outside the finite native writer branch.");
            // E40540 face3 bypasses the Star draw and its11902E0 writer.
            // The unused packet is still fully prepared; no phase step occurs.
            if(face!=3)
            {
                float heightTerm=Math.Max(0,S(F(0x479C4000),c.Position.Y));
                float step=M(D(M(heightTerm,geometryDelta),F(0x479C4000)),.5f);
                phase=A(phase,step);if(phase>=1)phase=A(phase,-1);
            }
            float angular=D(c.Scalar138,c.TargetWidth);angular=M(angular,angular);
            float intensity=M(SkyWeatherNativePower.PowSse(10,F(0xC10126EA),power),1);
            intensity=M(intensity,controller.Controller.Scalar38);intensity=M(intensity,F(0x3FA2F983));
            intensity=D(intensity,angular);intensity=D(intensity,F(0x3E5EDC7F));
            float size=M(8,.5f);intensity=M(intensity,D(256,M(size,size)));
            Vector4 info=new(intensity,size,D(F(0x3B000000),geometry40),0);
            packets[face]=Program.MaterialLayout.EncodeController(
                TerrainFrustumMath.Multiply(controller.MatrixF0,c.MatrixF0),new(phase,16,3,0),table,
                info,new(D(1,c.TargetWidth),D(1,c.TargetHeight),0,0));
        }
        return candidate=new(this,sourceFrame,present,stages,actualSelectedView,packets,committedPhase,phase,
            geometry40,geometryDelta,recipe+";"+sourceFrame.SourceProvenance+";"+ownedClockProvenance+
            ";selected-Star-resources;fresh-diameter=8;texture=null;"+
            "native-Star-faces0/1/2/4/5;pure-phase-candidate-until-complete-output-commit;owner="+OwnerIdentity);
    }
    public void CommitCompleteFrame(SkyAppliedSourceStarFrame frame)
    {RequireCandidate(frame);committedPhase=frame.CandidatePhase;committedFrame=frame.FrameNumber;candidate=null;}
    internal void RequireCandidate(SkyAppliedSourceStarFrame frame)
    {if(!ReferenceEquals(candidate,frame))throw new InvalidDataException("Star candidate is stale, superseded or already committed.");}
    private static float F(uint b)=>BitConverter.UInt32BitsToSingle(b);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}

public sealed class SkyAppliedSourceStarFrame
{
    private readonly SkyAppliedSourceStarOwner owner;
    private readonly SkyStarConstantPacket[] packets;
    internal SkyAppliedSourceStarFrame(SkyAppliedSourceStarOwner owner,SkySceneSourceFramePacket source,
        MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages,MainSceneNode51Execution view,
        SkyStarConstantPacket[] packets,float prior,float next,float scale,float delta,string provenance)
    {this.owner=owner;SourceFrame=source;AcceptedPresent=present;Stages=stages;SelectedView=view;this.packets=packets;
        PriorAcceptedPhase=prior;CandidatePhase=next;Geometry40=scale;OwnedGeometryDelta34=delta;SourceProvenance=provenance;}
    public SkySceneSourceFramePacket SourceFrame{get;}
    public MapSourceCreatedSeasonLightingFrame AcceptedPresent{get;}
    public SkyAppliedSourceStageFrame Stages{get;}
    public MainSceneNode51Execution SelectedView{get;}
    public long FrameNumber=>AcceptedPresent.FrameNumber;
    public float PriorAcceptedPhase{get;}
    public float CandidatePhase{get;}
    public float Geometry40{get;}
    public float OwnedGeometryDelta34{get;}
    public float NativeScalar15C0=>1;
    public float NativeScalar15C4=>8;
    public bool SourceStarTextureAbsent=>true;
    public string SourceProvenance{get;}
    public IReadOnlyList<SkyStarConstantPacket> SixPackets=>Array.AsReadOnly(packets.Select(p=>new SkyStarConstantPacket(p.VertexBytes.ToArray(),p.GeometryBytes.ToArray(),p.PixelBytes.ToArray())).ToArray());
    public void ValidateCandidate(SkySceneSourceFramePacket exactSource,MapSourceCreatedSeasonLightingFrame exactPresent)
    {if(!ReferenceEquals(SourceFrame,exactSource)||!ReferenceEquals(AcceptedPresent,exactPresent))throw new InvalidDataException("Star candidate belongs to another current source frame.");owner.RequireCandidate(this);}
}
