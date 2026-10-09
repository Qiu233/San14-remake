using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Accepted prepared Camera and the current native viewport state.
/// Depth endpoints are backend+13C/+17C, independent of Camera Near/Far.
/// Height is backend+304C after the optional pass viewport override1139040.</summary>
public sealed record MapFinalOutputDofWeightInputs(long FrameNumber,
    ReadOnlyMemory<byte> PreparedCamera,int NativeViewportHeight,
    float NativeDepthMinimum,float NativeDepthMaximum,
    NativeCameraTangentDispatch TangentMode,string NativeContextSource);

public sealed class MapFinalOutputDofWeightPacket
{
    private readonly byte[] bytes;
    internal MapFinalOutputDofWeightPacket(MapFinalOutputDofWeightInputs input,
        byte[] pixels,float eyeToAt,float tangent,DeferredCameraInputs camera,string source)
    {FrameNumber=input.FrameNumber;bytes=pixels;EyeToAtLength=eyeToAt;TanHalfFov=tangent;
        Camera=camera;SourceResourceUri=source;NativeContextSource=input.NativeContextSource;}
    public long FrameNumber { get; }
    public ReadOnlyMemory<byte> Bytes=>bytes;
    public float EyeToAtLength { get; }
    public float TanHalfFov { get; }
    public DeferredCameraInputs Camera { get; }
    public string SourceResourceUri { get; }
    public string NativeContextSource { get; }
    private MapFinalOutputDofWeightPacket(MapFinalOutputDofWeightPacket source)
    {FrameNumber=source.FrameNumber;bytes=source.bytes.ToArray();EyeToAtLength=source.EyeToAtLength;TanHalfFov=source.TanHalfFov;
        Camera=source.Camera;SourceResourceUri=source.SourceResourceUri;NativeContextSource=source.NativeContextSource;}
    public MapFinalOutputDofWeightPacket CopyAcceptedSnapshot()=>new(this);
}

/// <summary>Source E1F4E0 CPU upload for the DOFMerge texWeight INPUT. This is
/// an alpha-only512x1 LUT, regenerated on every successful setup. It is never
/// substituted by Scene, a null image, or a synthesized zero texture.</summary>
public static class MapFinalOutputDofWeight
{
    public const int TextureIndex=30,Width=512,Height=1,NativePixelFormat=15,DxgiFormat=65;
    public const string TextureName="texWeight";
    public const uint TextureNameHash=0x1C773F95;
    public const string ProducerSamplerKeyHex="1514300000000000";
    private static readonly float Epsilon=BitConverter.UInt32BitsToSingle(0x3727C5AC);

    public static string SourceUri(LoadedMapFinalOutputResources source)
    {ValidateSource(source);return source.PostEffect.Source.ResourceUri+"#SPK/texture30/texWeight;"+
        "sha256="+MapFinalOutputResources.SpkSha256+";native=E21B80,96AFC0,E1F4E0,E1D660;A8_UNORM512x1";}

    public static MapFinalOutputDofWeightPacket Prepare(
        LoadedMapFinalOutputResources source,MapFinalOutputDofWeightInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);string uri=SourceUri(source);
        if(input.FrameNumber<=0||input.PreparedCamera.Length<0x160||input.NativeViewportHeight<=0||
            !float.IsFinite(input.NativeDepthMinimum)||!float.IsFinite(input.NativeDepthMaximum)||
            input.NativeDepthMinimum==input.NativeDepthMaximum||!Enum.IsDefined(input.TangentMode)||
            string.IsNullOrWhiteSpace(input.NativeContextSource))
            throw new InvalidDataException("DOF weight requires an accepted Camera, resolved viewport/depth state and explicit native CRT path.");
        byte[] camera=input.PreparedCamera.ToArray();
        float F(int offset)=>BinaryPrimitives.ReadSingleLittleEndian(camera.AsSpan(offset,4));
        // E6F6F9..E6F72B: named CAMERA_PARAM_EYE_TO_AT_LENGTH writer.
        float dx=Sub(F(0x20),F(0x10)),dy=Sub(F(0x24),F(0x14)),dz=Sub(F(0x28),F(0x18));
        float eyeToAt=MathF.Sqrt(Add(Add(Mul(dx,dx),Mul(dy,dy)),Mul(dz,dz)));
        float aperture=F(0x15C),near=F(0x140),far=F(0x144),fov=F(0x148);
        if(!float.IsFinite(eyeToAt)||eyeToAt<=0||!float.IsFinite(aperture)||aperture<0||
            !float.IsFinite(near)||near<=0||!float.IsFinite(far)||far<=near||!float.IsFinite(fov)||fov<=0)
            throw new InvalidDataException("DOF weight Camera semantics must describe a finite perspective lens and positive eye-to-at length.");
        // Same TC101FB90/SC1020A90 CRT helper as source Camera construction.
        float tangent=NativeCameraTangent.Tangent1020A90(Mul(fov,.5f),input.TangentMode);
        byte[] bytes=new byte[Width];float reciprocal=Div(1,Width-1);
        for(int i=0;i<Width;i++)
        {
            // Keep scalar SSE rounding and evaluation order from E1F760..E1F894.
            float scale=Div(1,Sub(input.NativeDepthMaximum,input.NativeDepthMinimum));
            float slope=Mul(Div(Sub(near,far),Mul(far,near)),scale);
            scale=Mul(scale,input.NativeDepthMinimum);
            slope=Mul(slope,Mul(i,reciprocal));
            scale=Mul(scale,Sub(far,near));
            scale=Div(scale,far);scale=Add(scale,1);scale=Mul(scale,Div(1,near));
            float distance=Add(Div(1,Add(slope,scale)),Epsilon);
            float ratio=Div(eyeToAt,distance);
            bool behindFocus=Sub(distance,eyeToAt)>0;
            float denominator=Mul(Mul(eyeToAt,2),tangent);
            float amount=Mul(MathF.Abs(Sub(1,ratio)),Div(Mul(input.NativeViewportHeight,aperture),denominator));
            float unblurred=Div(1,Add(amount,Epsilon));
            unblurred=unblurred>=1?1:MathF.Max(0,unblurred);
            float encoded=Mul(Add(Mul(Mul(Sub(1,unblurred),.5f),behindFocus?1:-1),.5f),255);
            encoded=encoded>=255?255:MathF.Max(0,encoded);
            // Native CVTTSS2SI64 produces an indefinite whose low byte is0 forNaN.
            bytes[i]=float.IsNaN(encoded)?(byte)0:unchecked((byte)(long)encoded);
        }
        return new(input,bytes,eyeToAt,tangent,
            DeferredSceneConstants.ReadPreparedCamera(camera.AsSpan(0x10,0x130)),uri);
    }

    public static MapFinalOutputDofWeightPacket PrepareSourceCreatedCamera(
        LoadedMapFinalOutputResources source,MapCameraConstructionPacket camera,
        int nativeViewportHeight,float nativeDepthMinimum,float nativeDepthMaximum,
        NativeCameraTangentDispatch tangentMode,string nativeContextSource)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if(string.IsNullOrWhiteSpace(nativeContextSource))
            throw new InvalidDataException("DOF source-created Camera requires the resolved current viewport/depth provenance.");
        return Prepare(source,new(camera.FrameNumber,camera.Prefix,nativeViewportHeight,
            nativeDepthMinimum,nativeDepthMaximum,tangentMode,
            camera.FrameProvenance+";"+nativeContextSource));
    }

    public static void ValidateSource(LoadedMapFinalOutputResources source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var fields=source.PostEffectObject.Properties.Where(p=>p.NameHash==0x7AE38475).ToArray();
        if(fields.Length!=1||fields[0].TypeCode!=1||fields[0].RawValue.Length!=116348||
            Convert.ToHexString(SHA256.HashData(fields[0].RawValue))!=MapFinalOutputResources.SpkSha256)
            throw new InvalidDataException("DOF weight requires the exact source A1C83E34 SPK.");
        var data=fields[0].RawValue.AsSpan();int texture=0x2A0C+TextureIndex*24,allocation=0x2D0C+TextureIndex*24;
        uint U(int at)=>BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue.AsSpan(at,4));
        int name=checked((int)U(texture)+100);
        if(!data.Slice(name,10).SequenceEqual("texWeight\0"u8)||U(texture+8)!=TextureNameHash||
            U(texture+12)!=0x942A6D9F||U(texture+16)!=5||U(texture+20)!=TextureIndex||
            U(allocation)!=NativePixelFormat||U(allocation+4)!=Width||U(allocation+8)!=Height||
            U(allocation+12)!=0||U(allocation+16)!=0||U(allocation+20)!=1)
            throw new InvalidDataException("DOF weight source INPUT kind5 and A8 allocation differ from the native contract.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]private static float Add(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Sub(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Mul(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Div(float a,float b)=>a/b;
}
