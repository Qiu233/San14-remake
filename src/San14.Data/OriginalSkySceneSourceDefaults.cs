using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalSkySceneCameraReferences(uint CloudNoise, uint CloudCirrus,
    uint PlaneSta, uint SkylightSta, uint Sky2Controller);

/// <summary>An explicit original source object after property application.
/// This immutable projection describes a newly created source profile; it is
/// not a snapshot of edits to an existing original Camera.</summary>
public sealed record OriginalSkySceneCameraSourceProfile(string ResourceUri, string SourceSha256,
    bool IsOverride, uint ObjectId, float Near, float Far, float VerticalFovRadians, float Aspect,
    OriginalSkylightParameters Skylight, OriginalCameraLightingController Lighting,
    OriginalSkySceneCameraReferences References, float FogMaxDistanceToSky278);

public sealed record OriginalSkySceneStarInitializer(float Scalar15C0, float Scalar15C4,
    bool GeometryOwnerPresent, bool StarTexturePresent);
public sealed record OriginalSkySceneCloudInputInitializer(Vector4 Dimensions60);
public sealed record OriginalSkySceneSharedContextInitializer(bool FogEnabled120,
    bool ScatteringPending121, byte Selector122, int RendererFogMode12C, int Profile130);
public sealed record OriginalSkySceneSourceRequirement(string Input, string NativeSource, string Reason);
public sealed record OriginalSkySceneCubeFaceSource(int Face, Vector3 Direction, Vector4 Up);
public sealed record OriginalSkySceneControllerSourceProfile(string SourceProvenance,
    float Scalar38, float Scalar1F0, float SunSize60, float MoonSize64, float MilkyStrength68,
    float MonthDay48, float DateTime4C, float MoonAge70, float MoonObliquity74, Matrix4x4 Matrix170);
public sealed record OriginalSkyScenePreparedController(OriginalSkySceneSky2Controller Controller,
    float MilkyStrength1484, Vector4? SunPosition, Vector4? MoonPosition,
    Matrix4x4 Matrix130, Matrix4x4 MatrixF0);

/// <summary>Closed source construction defaults and the D3A850 cube-camera
/// producer. Every frame owner, texture, light, shared atmosphere and target
/// format is explicit. Source declarations do not select a current frame.</summary>
public static class OriginalSkySceneSourceDefaults
{
    public const string Sky2InitializerProvenance =
        "Original successful Sky2 initializer TC118CFA0/SC118DFD0; source construction before setters";
    public const string CubeCameraProvenance =
        "Original D3A850 face loop ->1198BA0 ->AB5BD0; package+A0=faceCamera+10";

    public static IReadOnlyList<OriginalSkySceneCubeFaceSource> CubeFaces { get; } = Array.AsReadOnly(new[] {
        new OriginalSkySceneCubeFaceSource(0, new(1,0,0), new(0,1,0,0)),
        new OriginalSkySceneCubeFaceSource(1, new(-1,0,0), new(0,1,0,0)),
        new OriginalSkySceneCubeFaceSource(2, new(0,1,0), new(0,0,-1,0)),
        new OriginalSkySceneCubeFaceSource(3, new(0,-1,0), new(0,0,1,0)),
        new OriginalSkySceneCubeFaceSource(4, new(0,0,1), new(0,1,0,0)),
        new OriginalSkySceneCubeFaceSource(5, new(0,0,-1), new(0,1,0,0)) });

    public static IReadOnlyList<OriginalSkySceneSourceRequirement> RequiredFrameInputs { get; } = Array.AsReadOnly(new[] {
        new OriginalSkySceneSourceRequirement("Prepared main Camera prefix", "D3A850 selected View Camera; Eye10/Target20/Near140/Far144/Flags158/Altitude210",
            "Resolve the source Camera, clock and modifiers before deriving six face cameras."),
        new OriginalSkySceneSourceRequirement("Native target format and dimensions", "95B0C0 target+48/+50/+54",
            "Use the actual original target declaration; no GPU enum cast or inferred viewport size."),
        new OriginalSkySceneSourceRequirement("Sky2 controller or explicit absence", "shader+1440; Matrix1B0/Scalar38/SkyInfo7C78/Scalar1F0",
            "Prepare the constructor or explicit edited controller profile with PrepareController; absence remains a separate source branch."),
        new OriginalSkySceneSourceRequirement("Sun, Moon and package light", "shader+1448/+1450; 118DA90/118D7E0 setters; package+10",
            "Use newly produced light fields or retained caches, including original RGB maxima and raw positions."),
        new OriginalSkySceneSourceRequirement("Texture presence and bindings", "shader+1678/+1688/+1698 and original STA owner",
            "Constructor-null descriptors do not identify Earth/Milky/Moon resources after source preparation."),
        new OriginalSkySceneSourceRequirement("Shared Fog and Scat", "package+40 record96; context+120/+12C/+130 and Scat pending121",
            "Provide the named source writer result and retained Scat state; context controls are not final constant buffers."),
        new OriginalSkySceneSourceRequirement("Retained six Sky2 caches", "shader parameter group+30 before118DC30",
            "Absent lights/controller retain source cache fields; construction defaults do not fill unproved token bytes."),
        new OriginalSkySceneSourceRequirement("Cloud packets when noise is present", "prepared+118 ->input+18;1193A90",
            "Cloud input dimensions32/128/32/8 alone do not supply the complete VS160/PS448 source packet."),
        new OriginalSkySceneSourceRequirement("Star prepared packet", "controller+8 geometry;118EF60/118F040/118FCD0",
            "The real point stream is closed separately; phase/table/E2C/point-scale writers and Star texture binding stay explicit."),
        new OriginalSkySceneSourceRequirement("Pass, variant and generation policy", "desired/committed bools;shader+1400;SkyCPU+168",
            "No current selection, elapsed clock, phase, output index or work budget is inferred from constructor defaults.") });

    /// <summary>118CFA0 literals, only for a successfully initialized fresh
    /// actual Sky2 instance. Original target format is required separately.</summary>
    public static OriginalSkySceneSky2Parameters CreateSky2Parameters(int explicitNativeTargetFormat)
    {
        if (explicitNativeTargetFormat is < 0 or > 127)
            throw new NotSupportedException("Original Sky2 source profile requires a supported explicit native target format.");
        var vectors = OriginalSky2ParameterPreparation.OriginalInitializer();
        return new(Sky2InitializerProvenance, F(0x401BFC82), F(0x3F1ED619), F(0x40490FDB), F(0x41200000),
            vectors.AltitudeInfo, vectors.StableDimensions, 1, 1, 1, 1, 1, F(0x3A31AD2E), false,
            Vector4.Zero, explicitNativeTargetFormat);
    }

    public static OriginalSkySceneStarInitializer StarInitializer() => new(1,8,false,false);
    public static OriginalSkySceneCloudInputInitializer CloudInputInitializer() => new(new(32,128,32,8));
    public static OriginalSkySceneSharedContextInitializer SharedContextInitializer() => new(true,false,1,0,1);

    /// <summary>1176AC0 geometry/controller construction, before width, clamp,
    /// clock/phase or light setters. Matrix170 is its original fixed rotation.</summary>
    public static OriginalSkySceneControllerSourceProfile CreateControllerConstructor()
    {
        var rotation=OriginalSky2GeometryPreparation.PrepareSkyTransform(F(0xBF82E925),F(0xBF0149BF),F(0xC094C9EF));
        Matrix4x4 geometry=Matrix(rotation);geometry.M41=geometry.M42=geometry.M43=0;
        return new("Original controller constructor TC1176AC0/SC1177B90 before setters",
            F(0x3C23D70A),0,1,1,M(F(0x3C23D70A),F(0x3D8ACF4C)),F(0x406CCCCD),F(0x3F333333),.5f,0,geometry);
    }

    /// <summary>D3AB9F..D3ABB2 copies Camera+278 times100 to controller1F0.
    /// The scalar's additional meaning is not inferred from this source copy.</summary>
    public static OriginalSkySceneControllerSourceProfile ApplyPreparedMainControllerInputs(
        OriginalSkySceneControllerSourceProfile explicitController,ReadOnlySpan<byte> explicitPreparedMainPrefix)
    {
        ArgumentNullException.ThrowIfNull(explicitController);
        if(explicitPreparedMainPrefix.Length!=OriginalPreparedCameraLighting.PrefixByteLength)
            throw new InvalidDataException("Controller input copy requires the complete explicit main Camera prefix.");
        return explicitController with{Scalar1F0=M(Read(explicitPreparedMainPrefix,0x278),100),
            SourceProvenance=explicitController.SourceProvenance+"; explicit prepared Camera278 viaD3AB9F"};
    }

    /// <summary>Fresh source Camera property A48ECB2A/ordinal934 is copied
    /// CPU120→Camera278 by C409C0, then multiplied by100 for controller1F0.
    /// This entry applies that exact source branch without a fabricated prefix.</summary>
    public static OriginalSkySceneControllerSourceProfile ApplySourceCameraControllerInputs(
        OriginalSkySceneControllerSourceProfile explicitController,OriginalSkySceneCameraSourceProfile explicitCamera)
    {
        ArgumentNullException.ThrowIfNull(explicitController);ArgumentNullException.ThrowIfNull(explicitCamera);
        Finite(explicitCamera.FogMaxDistanceToSky278);
        return explicitController with{Scalar1F0=M(explicitCamera.FogMaxDistanceToSky278,100),
            SourceProvenance=$"{explicitController.SourceProvenance}; source Camera{explicitCamera.ObjectId:X8} {explicitCamera.ResourceUri} SHA{explicitCamera.SourceSha256} fieldA48ECB2A"};
    }

    /// <summary>11775C0→1177750 prepares1B0/7C78 from explicit source fields.
    /// The original call overwrites each present Sun/Moon position; callers must
    /// use those outputs in the light packet. Missing lights return no update.
    /// Eclipse cosine supports the closed small-angle finite branch.</summary>
    public static OriginalSkyScenePreparedController PrepareController(
        OriginalSkySceneControllerSourceProfile explicitController,
        OriginalSkySceneSky2Parameters explicitSky2Parameters,
        bool explicitSunPresent, bool explicitMoonPresent)
    {
        ArgumentNullException.ThrowIfNull(explicitController);ArgumentNullException.ThrowIfNull(explicitSky2Parameters);
        var c=explicitController;var p=explicitSky2Parameters;
        if(string.IsNullOrWhiteSpace(c.SourceProvenance))throw new InvalidDataException("Controller source provenance is required.");
        foreach(float v in new[]{c.Scalar38,c.Scalar1F0,c.SunSize60,c.MoonSize64,c.MilkyStrength68,c.MonthDay48,c.DateTime4C,c.MoonAge70,c.MoonObliquity74})Finite(v);
        foreach(float v in new[]{c.Matrix170.M11,c.Matrix170.M12,c.Matrix170.M13,c.Matrix170.M21,c.Matrix170.M22,c.Matrix170.M23,
            c.Matrix170.M31,c.Matrix170.M32,c.Matrix170.M33})Finite(v);
        float season=M(S(c.MonthDay48,F(0x406CCCCD)),F(0x3F060A92));
        float hour=S(A(M(S(A(c.DateTime4C,c.DateTime4C),1),F(0x40490FDB)),season),p.Angle1430);
        var quarter=OriginalNativeTrigonometry.SinCos014590(F(0x3FC90FDB));
        Matrix4x4 celestial=new(1,0,0,0,0,quarter.Cos,quarter.Sin,0,0,Neg(quarter.Sin),quarter.Cos,0,0,0,0,1);
        celestial=LeftY(celestial,F(0x40490FDB));celestial=RightY(celestial,hour);
        Matrix4x4 geographic=Matrix(OriginalSky2GeometryPreparation.PrepareSkyTransform(p.Angle1430,p.Angle1434,p.Angle143C));
        Matrix4x4 inverse=OriginalNativeMatrixMath.Inverse3F6EB0(Combine3x3(celestial,geographic),out _);
        Vector4? sun=null,moon=null;
        if(explicitSunPresent) sun=CelestialDirection(season,F(0x3ED17496),inverse);
        if(explicitMoonPresent)
        {
            //117778A0/117778B7: additional obliquity74 scales then adds
            //the same fixed Sun obliquity; source initializer74 is0.
            float obliquity=A(M(c.MoonObliquity74,F(0x3DB75D14)),F(0x3ED17496));
            float phase=A(M(A(c.MoonAge70,c.MoonAge70),F(0x40490FDB)),season);
            moon=CelestialDirection(phase,obliquity,inverse);
        }
        float centered=S(c.MoonAge70,.5f);float distance=MathF.Abs(centered);
        float strength=S(1,A(distance,distance));strength=M(strength,strength);strength=M(strength,strength);
        float eclipse=1;
        if(sun is Vector4 s && moon is Vector4 m)
        {
            float dot=A(A(M(s.Y,m.Y),M(s.X,m.X)),M(s.Z,m.Z));
            if(dot>=1)dot=1;
            //118D9D0 copies the Sky2 sizes to controller60/64 before update.
            float cosine=CosineSmall1C2E80(M(p.SunSize1490,F(0x3C187400)));
            if(dot>cosine)
            {
                float v=D(S(dot,cosine),S(1,cosine));v=M(v,F(0x3C142917));v=D(v,F(0x3C187400));
                eclipse=S(1,M(v,v));
            }
        }
        Matrix4x4 localToGlobal=Combine3x3(c.Matrix170,celestial);
        Finite(strength);Finite(eclipse);
        foreach(float v in new[]{localToGlobal.M11,localToGlobal.M12,localToGlobal.M13,localToGlobal.M21,localToGlobal.M22,localToGlobal.M23,
            localToGlobal.M31,localToGlobal.M32,localToGlobal.M33})Finite(v);
        return new(new($"{c.SourceProvenance}; prepared11775C0/1177750",localToGlobal,c.Scalar38,new(eclipse,strength),c.Scalar1F0),
            c.MilkyStrength68,sun,moon,celestial,inverse);
    }

    /// <summary>Read a caller-selected exact Perspective DOK. The source
    /// controller and cloud/STA declarations retain their separate meanings.</summary>
    public static OriginalSkySceneCameraSourceProfile ReadPerspectiveCamera(LoadedObjects resource, uint objectId)
    {
        var source = OriginalPreparedCameraLightingSource.ReadPerspective(resource, objectId);
        DokObject camera = source.Source.Camera;
        float near = Scalar(camera,0x06741408), far = Scalar(camera,0x003546E9),
            fov = Scalar(camera,0xBA7543EA), aspect = Scalar(camera,0xA70685F8);
        if (near<=0 || far<=near || fov<=0 || fov>=MathF.PI || aspect<=0)
            throw new InvalidDataException("Source Perspective Camera lens is outside the finite supported domain.");
        var refs = new OriginalSkySceneCameraReferences(Key(camera,0x4260B9DB),Key(camera,0x8997E075),
            Key(camera,0xE67B59E3),Key(camera,0x063D4BE7),Key(camera,0x8D316B8D));
        return new(resource.Source.ResourceUri,resource.Source.DecodedSha256,resource.Source.OverrideSource is not null,
            objectId,near,far,fov,aspect,source.Source.Parameters,source.Controller,refs,Scalar(camera,0xA48ECB2A));
    }

    /// <summary>Original six-face camera production from a caller-prepared
    /// 3C0-byte main Camera prefix. Ordinary Perspective clip profile2 is closed;
    /// near/far and reverse-depth flags are inherited, while cube aspect is -1.
    /// Altitude210 is kilometres: the native47C35000 multiplier is100000 cm/km.</summary>
    public static IReadOnlyList<OriginalSkyScenePreparedCamera> PrepareCubeCameras(
        ReadOnlySpan<byte> explicitPreparedMainPrefix, string explicitMainProvenance,
        int explicitNativeClipProfile, int explicitTargetWidth, int explicitTargetHeight)
    {
        if (explicitPreparedMainPrefix.Length != OriginalPreparedCameraLighting.PrefixByteLength ||
            string.IsNullOrWhiteSpace(explicitMainProvenance) || explicitTargetWidth<=0 || explicitTargetHeight<=0)
            throw new InvalidDataException("Original sky cube cameras require an explicit complete main Camera and target provenance.");
        if (explicitNativeClipProfile!=2)
            throw new NotSupportedException("Original sky cube Perspective producer currently closes only native clip profile2.");
        uint flags=BinaryPrimitives.ReadUInt32LittleEndian(explicitPreparedMainPrefix.Slice(0x158,4));
        if ((flags&~2u)!=0)
            throw new NotSupportedException("Original sky cube producer requires the ordinary Perspective source flags0 or2.");
        float near=Read(explicitPreparedMainPrefix,0x140), far=Read(explicitPreparedMainPrefix,0x144);
        if (near<=0 || far<=near) throw new InvalidDataException("Original sky cube inherited lens is degenerate.");
        Vector4 eye=Vector(explicitPreparedMainPrefix,0x10), mainTarget=Vector(explicitPreparedMainPrefix,0x20);
        float dx=S(mainTarget.X,eye.X), dy=S(mainTarget.Y,eye.Y), dz=S(mainTarget.Z,eye.Z);
        // D3B11C begins Y^2+X^2, then adds Z^2 before the native SQRTSS.
        float length=SquareRoot(A(A(M(dy,dy),M(dx,dx)),M(dz,dz)));
        if (length<=0) throw new InvalidDataException("Original main Camera eye/target distance is zero.");
        float offset=M(Read(explicitPreparedMainPrefix,0x210),F(0x47C35000));
        Vector4 faceEye=new(eye.X,A(eye.Y,offset),eye.Z,eye.W);
        Matrix4x4 projection=CubeProjection(near,far,(flags&2u)!=0);
        var output=new OriginalSkyScenePreparedCamera[6];
        for (int face=0;face<6;face++)
        {
            var axis=CubeFaces[face];
            //1198BA0's seed is (0,0,0,1); BF46E0 normalizes its
            // exact unit-axis target, retaining W1. D3A850 then replaces XYZ.
            Vector4 target=new(A(M(axis.Direction.X,length),eye.X),
                A(A(M(axis.Direction.Y,length),eye.Y),offset),A(M(axis.Direction.Z,length),eye.Z),1);
            Matrix4x4 view=LookTo97DCC0(faceEye,target,axis.Up);
            output[face]=new($"{CubeCameraProvenance}; face{face}; {explicitMainProvenance}",view,projection,
                TerrainFrustumMath.Multiply(view,projection),faceEye,F(0x3FC90FDB),-1,flags,
                explicitNativeClipProfile,explicitTargetWidth,explicitTargetHeight);
        }
        return Array.AsReadOnly(output);
    }

    private static Matrix4x4 CubeProjection(float near,float far,bool reverse)
    {
        // Original tanf(3FC90FDB*.5) is exactly1 for both audited SSE/AVX
        // paths. Aspect=-1 therefore gives exact X=-1 and Y=1.
        float range=reverse?S(far,near):S(near,far), z=D(reverse?near:far,range);
        return new(-1,0,0,0,0,1,0,0,0,0,z,-1,0,0,M(z,reverse?far:near),0);
    }

    private static Matrix4x4 LookTo97DCC0(Vector4 eye,Vector4 target,Vector4 up)
    {
        Vector3 z=NormalizeDirect(new(S(eye.X,target.X),S(eye.Y,target.Y),S(eye.Z,target.Z)));
        // Native cross multiplication order: Z.z*Up.y - Z.y*Up.z, etc.
        Vector3 x=NormalizeDirect(new(S(M(z.Z,up.Y),M(z.Y,up.Z)),S(M(z.X,up.Z),M(z.Z,up.X)),S(M(z.Y,up.X),M(z.X,up.Y))));
        Vector3 y=new(S(M(x.Z,z.Y),M(x.Y,z.Z)),S(M(x.X,z.Z),M(x.Z,z.X)),S(M(x.Y,z.X),M(x.X,z.Y)));
        Vector3 minusEye=new(S(0,eye.X),S(0,eye.Y),S(0,eye.Z));
        float Dot(Vector3 axis)=>A(A(M(minusEye.X,axis.X),M(minusEye.Y,axis.Y)),M(minusEye.Z,axis.Z));
        return new(x.X,y.X,z.X,0,x.Y,y.Y,z.Y,0,x.Z,y.Z,z.Z,0,Dot(x),Dot(y),Dot(z),1);
    }
    private static Vector3 NormalizeDirect(Vector3 v)
    {
        float length=SquareRoot(A(A(M(v.X,v.X),M(v.Y,v.Y)),M(v.Z,v.Z)));
        if (length<=0) throw new InvalidDataException("Original cube camera normalization is degenerate.");
        return new(D(v.X,length),D(v.Y,length),D(v.Z,length));
    }
    private static float SquareRoot(float value)
    {
        if (!float.IsFinite(value) || value<0) throw new InvalidDataException("Original cube camera source exceeds finite arithmetic.");
        if (!Sse.IsSupported) throw new PlatformNotSupportedException("Original cube camera requires SSE SQRTSS.");
        return Sse.SqrtScalar(Vector128.CreateScalar(value)).GetElement(0);
    }
    private static DokProperty Field(DokObject o,uint hash,byte type)
    {
        var rows=o.Properties.Where(p=>p.NameHash==hash).ToArray();
        if(rows.Length!=1 || rows[0].TypeCode!=type || rows[0].ElementCount!=1 || rows[0].ElementSize!=4 || rows[0].RawValue.Length!=4)
            throw new InvalidDataException($"Original source Camera property {hash:X8} requires one exact typed scalar.");
        return rows[0];
    }
    private static uint Key(DokObject o,uint hash)=>BinaryPrimitives.ReadUInt32LittleEndian(Field(o,hash,5).RawValue);
    private static float Scalar(DokObject o,uint hash)
    { float value=BinaryPrimitives.ReadSingleLittleEndian(Field(o,hash,8).RawValue);Finite(value);return value; }
    private static Vector4 Vector(ReadOnlySpan<byte> bytes,int at)=>new(Read(bytes,at),Read(bytes,at+4),Read(bytes,at+8),Read(bytes,at+12));
    private static float Read(ReadOnlySpan<byte> bytes,int at)
    { float value=BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(at,4));Finite(value);return value; }
    private static void Finite(float value)
    { if(!float.IsFinite(value))throw new InvalidDataException("Original Sky source field is nonfinite."); }
    private static float F(uint bits)=>BitConverter.UInt32BitsToSingle(bits);
    private static float Neg(float value)=>F(BitConverter.SingleToUInt32Bits(value)^0x80000000);
    private static Matrix4x4 Matrix(ReadOnlySpan<float> v)=>new(v[0],v[1],v[2],v[3],v[4],v[5],v[6],v[7],v[8],v[9],v[10],v[11],v[12],v[13],v[14],v[15]);
    private static float CosineSmall1C2E80(float angle)
    {
        Finite(angle);
        if(angle<0 || angle>=F(0x3F490FDB))throw new NotSupportedException("Controller Sun-size cosine requires the closed original small-angle branch.");
        if(angle==0)return 1;
        float x=M(angle,angle), y=A(M(x,x),360);y=M(y,S(30,x));y=S(y,10080);return M(y,F(0x3AB60B61));
    }
    private static Matrix4x4 LeftY(Matrix4x4 m,float angle)
    {
        var t=OriginalNativeTrigonometry.SinCos014590(angle);
        return new(S(M(m.M11,t.Cos),M(m.M31,t.Sin)),S(M(m.M12,t.Cos),M(m.M32,t.Sin)),S(M(m.M13,t.Cos),M(m.M33,t.Sin)),0,
            m.M21,m.M22,m.M23,0,A(M(m.M11,t.Sin),M(m.M31,t.Cos)),A(M(m.M32,t.Cos),M(m.M12,t.Sin)),A(M(m.M33,t.Cos),M(m.M13,t.Sin)),0,0,0,0,1);
    }
    private static Matrix4x4 RightY(Matrix4x4 m,float angle)
    {
        var t=OriginalNativeTrigonometry.SinCos014590(angle);
        return new(A(M(m.M13,t.Sin),M(m.M11,t.Cos)),m.M12,S(M(m.M13,t.Cos),M(m.M11,t.Sin)),m.M14,
            A(M(m.M23,t.Sin),M(m.M21,t.Cos)),m.M22,S(M(m.M23,t.Cos),M(m.M21,t.Sin)),m.M24,
            A(M(m.M33,t.Sin),M(m.M31,t.Cos)),m.M32,S(M(m.M33,t.Cos),M(m.M31,t.Sin)),m.M34,
            A(M(m.M43,t.Sin),M(m.M41,t.Cos)),m.M42,S(M(m.M43,t.Cos),M(m.M41,t.Sin)),m.M44);
    }
    // A901E0(out,a,b) returns b*a, preserving Y+X then Z.
    private static Matrix4x4 Combine3x3(Matrix4x4 a,Matrix4x4 b)
    {
        float Dot(float x,float y,float z,float l,float m,float n)=>A(A(M(y,m),M(x,l)),M(z,n));
        return new(Dot(b.M11,b.M12,b.M13,a.M11,a.M21,a.M31),Dot(b.M11,b.M12,b.M13,a.M12,a.M22,a.M32),Dot(b.M11,b.M12,b.M13,a.M13,a.M23,a.M33),0,
            Dot(b.M21,b.M22,b.M23,a.M11,a.M21,a.M31),Dot(b.M21,b.M22,b.M23,a.M12,a.M22,a.M32),Dot(b.M21,b.M22,b.M23,a.M13,a.M23,a.M33),0,
            Dot(b.M31,b.M32,b.M33,a.M11,a.M21,a.M31),Dot(b.M31,b.M32,b.M33,a.M12,a.M22,a.M32),Dot(b.M31,b.M32,b.M33,a.M13,a.M23,a.M33),0,0,0,0,1);
    }
    private static Vector4 CelestialDirection(float phase,float obliquity,Matrix4x4 inverse)
    {
        var t=OriginalNativeTrigonometry.SinCos014590(obliquity);Vector4 axis=new(Neg(t.Sin),t.Cos,0,0);
        var turn=OriginalNativeTrigonometry.SinCos014590(phase);Vector4 v=new(0,0,-1,0);
        float dot=A(A(M(axis.X,v.X),M(axis.Y,v.Y)),M(axis.Z,v.Z)),k=M(dot,S(1,turn.Cos));
        Vector4 r=new(A(A(M(axis.X,k),M(v.X,turn.Cos)),M(S(M(v.Z,axis.Y),M(axis.Z,v.Y)),turn.Sin)),
            A(A(M(k,axis.Y),M(turn.Cos,v.Y)),M(S(M(axis.Z,v.X),M(v.Z,axis.X)),turn.Sin)),
            A(A(M(k,axis.Z),M(turn.Cos,v.Z)),M(S(M(axis.X,v.Y),M(v.X,axis.Y)),turn.Sin)),0);
        return new(A(A(A(M(r.Y,inverse.M21),M(r.X,inverse.M11)),M(r.Z,inverse.M31)),M(r.W,inverse.M41)),
            A(A(A(M(r.X,inverse.M12),M(r.Y,inverse.M22)),M(r.Z,inverse.M32)),M(r.W,inverse.M42)),
            A(A(A(M(r.X,inverse.M13),M(r.Y,inverse.M23)),M(r.Z,inverse.M33)),M(r.W,inverse.M43)),r.W);
    }
    private static float A(float a,float b)=>OriginalNativeFloat.Add(a,b);
    private static float S(float a,float b)=>OriginalNativeFloat.Subtract(a,b);
    private static float M(float a,float b)=>OriginalNativeFloat.Multiply(a,b);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a,float b)=>a/b;
}
