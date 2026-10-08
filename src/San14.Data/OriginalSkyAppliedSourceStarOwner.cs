using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual fresh Star constructor/table and current11902E0 writer.
/// The explicit owned backend clock is an input; no original Clock254 or
/// original current shader variant is inferred. Phase candidates commit only
/// after a complete caller output succeeds, so failed output retries do not
/// advance the accepted animation twice.</summary>
public sealed class OriginalSkyAppliedSourceStarOwner
{
    private readonly LoadedShaderContainer shader;
    private readonly OriginalSkyStarGeometry geometry;
    private readonly OriginalSkyWeatherNativePowerSource power;
    private readonly float[] table;
    private readonly string recipe;
    private float committedPhase;
    private long committedFrame;
    private OriginalSkyAppliedSourceStarFrame? candidate;
    private OriginalSkyAppliedSourceStarOwner(LoadedShaderContainer shader,OriginalSkyStarGeometry geometry,
        OriginalSkyWeatherNativePowerSource power,string owner,string recipe)
    {this.shader=shader;this.geometry=geometry;this.power=power;OwnerIdentity=owner;this.recipe=recipe;table=CreateTable();}
    public string OwnerIdentity{get;}
    public float CommittedPhase=>committedPhase;
    public LoadedShaderContainer Shader=>shader;
    public OriginalSkyStarGeometry Geometry=>geometry with{VertexBytes=geometry.VertexBytes.ToArray()};
    public static OriginalSkyAppliedSourceStarOwner CreateFresh(OriginalGameRepository repository,string locale,string ownedStarIdentity)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentException.ThrowIfNullOrWhiteSpace(ownedStarIdentity);
        var shader=repository.LoadShaderContainer("system",0x7180A527);
        RequireSource(shader.Source,"san14://rdb/system/7180A527","0918D4ABA52CC627D454C7376A8E72645ABF56B8194481193B938AE5CD9CF918");
        var definitions=repository.LoadObjects("system",0xDAB638B2);
        RequireSource(definitions.Source,"san14://rdb/system/DAB638B2","357F6A380CF19EB2A86B439AF24CF2BACDD38074AAF9BC3AE99F0EAD21EE9271");
        var controller=DokReader.Read(definitions.Source.Bytes).Objects.Single(o=>o.ObjectId==0x6DDC0D75);
        if(controller.ClassId!=0x12BD9185||controller.Properties.Count!=1||
            Scalar(controller,0,0x78051B9F,5)!=0xAF8EBDD2)
            throw new InvalidDataException("Actual source Sky2Controller geometry declaration changed.");
        var points=repository.LoadRdbAsset("system",0xAF8EBDD2);
        RequireSource(points,"san14://rdb/system/AF8EBDD2","815777E13CB07567FB9B08AB38C10147ABF2E9AC42AE37321C07DB4F67A0005D");
        var geometry=OriginalSkyStarGeometry.Read(points);
        if(geometry.PointCount!=121318)throw new InvalidDataException("Actual Star point stream count changed.");
        return new(shader,geometry,OriginalSkyWeatherNativePowerSource.Load(repository,locale),ownedStarIdentity,
            OriginalDeferredSamplerSources.ReadAppliedStarNativeRecipe(repository.SourceDirectory,locale));
    }
    public OriginalSkyAppliedSourceStarFrame PrepareCurrent(OriginalSkySceneSourceFramePacket sourceFrame,
        OriginalMapSourceCreatedSeasonLightingFrame present,OriginalSkyAppliedSourceStageFrame stages,
        OriginalMainSceneNode51Execution actualSelectedView,float explicitOwnedClockDelta,string ownedClockProvenance)
    {
        ArgumentNullException.ThrowIfNull(sourceFrame);ArgumentNullException.ThrowIfNull(present);
        ArgumentNullException.ThrowIfNull(stages);ArgumentNullException.ThrowIfNull(actualSelectedView);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedClockProvenance);
        RequireSource(shader.Source,"san14://rdb/system/7180A527","0918D4ABA52CC627D454C7376A8E72645ABF56B8194481193B938AE5CD9CF918");
        actualSelectedView.RequireSelectedView(present.FrameNumber,sourceFrame.Binding);
        var lighting=stages.AcceptedLighting;var selected=present.AcceptedSeasonCamera.Inputs.Source.Selection;
        stages.ValidateCurrent(lighting,selected,present.CurrentSkyCamera);
        if(!ReferenceEquals(stages.AcceptedPresent,present)||sourceFrame.FrameNumber!=present.FrameNumber||
            sourceFrame.Binding!=lighting.Binding||present.FrameNumber<=committedFrame||
            present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale!=power.Locale||
            !float.IsFinite(explicitOwnedClockDelta)||explicitOwnedClockDelta<0)
            throw new InvalidDataException("Star current packet requires the same accepted source frame and finite explicit owned clock.");
        var package=OriginalSkySeasonPackageTextures.Read(selected.FirstPackage).Package;
        var sourceCamera=OriginalSkySceneSourceDefaults.ReadPerspectiveCamera(package.Objects,package.Camera.ObjectId);
        if(sourceCamera.References.Sky2Controller!=0x6DDC0D75||
            !OriginalSkySeasonPackageTextures.Read(package).Star.IsSourceReferenceZero)
            throw new NotSupportedException("Applied source Star requires its actual loaded controller and fresh null Star descriptor.");
        // C5197F/86 and C519E1/E8 load CPU56C/588; C419BD/C41A11
        // project them to Camera7F8/814. D4BD30's current packet has a
        // distinct -1 branch for nonprimary View, never HDRI setter slot3/4.
        float geometry40=actualSelectedView.SelectedViewIndex==0?
            F(Scalar(package.Camera,614,0x59633E4E,8)):-1f;
        float clockScale=F(Scalar(package.Camera,621,0x4C16B0AE,8));
        float geometryDelta=M(clockScale,explicitOwnedClockDelta);
        var frame=sourceFrame.SourceFrame;
        if(frame.SixCameras.Count!=6||frame.Controller is null||!frame.Lights.SunPresent||!frame.Lights.MoonPresent)
            throw new NotSupportedException("Actual source Star requires six current cameras and the complete same-frame controller/light path.");
        var controller=OriginalSkySceneSourceDefaults.PrepareController(present.CurrentSkyCamera.Controller,
            present.CurrentSkyCamera.Parameters,frame.Lights.SunPresent,frame.Lights.MoonPresent);
        if(controller.Controller!=frame.Controller)
            throw new InvalidDataException("Star source geometry matrix/controller differs from the actual current Sky source.");
        var packets=new OriginalSkyStarConstantPacket[6];float phase=committedPhase;
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
            float intensity=M(OriginalSkyWeatherNativePower.PowSse(10,F(0xC10126EA),power),1);
            intensity=M(intensity,controller.Controller.Scalar38);intensity=M(intensity,F(0x3FA2F983));
            intensity=D(intensity,angular);intensity=D(intensity,F(0x3E5EDC7F));
            float size=M(8,.5f);intensity=M(intensity,D(256,M(size,size)));
            Vector4 info=new(intensity,size,D(F(0x3B000000),geometry40),0);
            byte[] vs=new byte[352],gs=new byte[16],ps=new byte[16];
            Matrix(vs,TerrainFrustumMath.Multiply(controller.MatrixF0,c.MatrixF0));
            Vector(vs,64,new(phase,16,3,0));
            for(int i=0;i<64;i++)Float(vs,80+i*4,table[i]);
            Vector(vs,336,info);Vector(gs,0,new(D(1,c.TargetWidth),D(1,c.TargetHeight),0,0));Vector(ps,0,info);
            packets[face]=OriginalSkyStarConstants.Read(shader.ReadResourceDefinitions(0),shader.ReadResourceDefinitions(5),
                shader.ReadResourceDefinitions(4),vs,gs,ps);
        }
        return candidate=new(this,sourceFrame,present,stages,actualSelectedView,packets,committedPhase,phase,
            geometry40,geometryDelta,recipe+";"+sourceFrame.SourceProvenance+";"+ownedClockProvenance+
            ";source614/621->Camera7F8/814->packet38/54;controller6DDC0D75/AF8EBDD2;fresh-Star15C0=1/15C4=8/16B2=0/texture=null;"+
            "native-Star-faces0/1/2/4/5;pure-phase-candidate-until-complete-output-commit;owner="+OwnerIdentity);
    }
    public void CommitCompleteFrame(OriginalSkyAppliedSourceStarFrame frame)
    {RequireCandidate(frame);committedPhase=frame.CandidatePhase;committedFrame=frame.FrameNumber;candidate=null;}
    internal void RequireCandidate(OriginalSkyAppliedSourceStarFrame frame)
    {if(!ReferenceEquals(candidate,frame))throw new InvalidDataException("Star candidate is stale, superseded or already committed.");}
    private static float[] CreateTable()
    {
        var samples=new float[48];
        for(int n=0;n<48;n++)
        {
            float angle=D(M(n,F(0x40490FDB)),48);
            float amplitude=M(A(OriginalArmyAnimationSampler.SourceSinCos(angle).Sin,2),F(0x3D75C28F));
            float value=F(0x3F4CCCCD);
            for(int j=0;j<8;j++)
            {angle=A(angle,angle);amplitude=M(amplitude,F(0x3F59999A));value=A(value,M(OriginalArmyAnimationSampler.SourceSinCos(angle).Sin,amplitude));}
            samples[n]=value;
        }
        return Enumerable.Range(0,64).Select(i=>samples[((i/4)*3+i%4)%48]).ToArray();
    }
    private static uint Scalar(DokObject o,int ordinal,uint hash,uint type)
    {
        var p=o.Properties[ordinal];if(p.Index!=ordinal||p.NameHash!=hash||p.TypeCode!=type||p.ElementCount!=1||p.ElementSize!=4||p.RawValue.Length!=4)
            throw new InvalidDataException("Applied Star exact typed source property changed.");
        return BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue);
    }
    private static void RequireSource(LoadedRdbAsset s,string uri,string sha)
    {if(s.ResourceUri!=uri||s.OverrideSource is not null||s.DecodedSha256!=sha||Convert.ToHexString(SHA256.HashData(s.Bytes))!=sha)throw new InvalidDataException("Applied Star exact source URI/hash changed.");}
    private static float F(uint b)=>BitConverter.UInt32BitsToSingle(b);
    private static void Float(byte[] b,int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(at,4),value);
    private static void Vector(byte[] b,int at,Vector4 v){Float(b,at,v.X);Float(b,at+4,v.Y);Float(b,at+8,v.Z);Float(b,at+12,v.W);}
    private static void Matrix(byte[] b,Matrix4x4 m)
    {float[] f=[m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];for(int i=0;i<16;i++)Float(b,i*4,f[i]);}
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}

public sealed class OriginalSkyAppliedSourceStarFrame
{
    private readonly OriginalSkyAppliedSourceStarOwner owner;
    private readonly OriginalSkyStarConstantPacket[] packets;
    internal OriginalSkyAppliedSourceStarFrame(OriginalSkyAppliedSourceStarOwner owner,OriginalSkySceneSourceFramePacket source,
        OriginalMapSourceCreatedSeasonLightingFrame present,OriginalSkyAppliedSourceStageFrame stages,OriginalMainSceneNode51Execution view,
        OriginalSkyStarConstantPacket[] packets,float prior,float next,float scale,float delta,string provenance)
    {this.owner=owner;SourceFrame=source;AcceptedPresent=present;Stages=stages;SelectedView=view;this.packets=packets;
        PriorAcceptedPhase=prior;CandidatePhase=next;Geometry40=scale;OwnedGeometryDelta34=delta;SourceProvenance=provenance;}
    public OriginalSkySceneSourceFramePacket SourceFrame{get;}
    public OriginalMapSourceCreatedSeasonLightingFrame AcceptedPresent{get;}
    public OriginalSkyAppliedSourceStageFrame Stages{get;}
    public OriginalMainSceneNode51Execution SelectedView{get;}
    public long FrameNumber=>AcceptedPresent.FrameNumber;
    public float PriorAcceptedPhase{get;}
    public float CandidatePhase{get;}
    public float Geometry40{get;}
    public float OwnedGeometryDelta34{get;}
    public float NativeScalar15C0=>1;
    public float NativeScalar15C4=>8;
    public bool SourceStarTextureAbsent=>true;
    public string SourceProvenance{get;}
    public IReadOnlyList<OriginalSkyStarConstantPacket> SixPackets=>Array.AsReadOnly(packets.Select(p=>new OriginalSkyStarConstantPacket(p.VertexBytes.ToArray(),p.GeometryBytes.ToArray(),p.PixelBytes.ToArray())).ToArray());
    public void ValidateCandidate(OriginalSkySceneSourceFramePacket exactSource,OriginalMapSourceCreatedSeasonLightingFrame exactPresent)
    {if(!ReferenceEquals(SourceFrame,exactSource)||!ReferenceEquals(AcceptedPresent,exactPresent))throw new InvalidDataException("Star candidate belongs to another current source frame.");owner.RequireCandidate(this);}
}
