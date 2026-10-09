using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Accepted Camera and the actual resolved Apply arguments. The
/// HDR luminance is EB5F50's global-object+18 input, distinct from Exposure's
/// selected luminance. TrueHDR, tone and DOF modes remain resolved route values.
/// LightShaftPower and LUT height come from their actual source producers.</summary>
public sealed record MapFinalOutputSceneConstantInputs(long FrameNumber,
    ReadOnlyMemory<byte> PreparedCameraPrefix,uint ApplyWidth,uint ApplyHeight,
    float LimbDarkeningStart,float LimbDarkeningEnd,float ResolvedDofType,
    Vector4 LightShaftPower,float SimulatedHdrLuminanceArgument,
    float ResolvedToneCurveType,bool ResolvedTrueHdr,ushort ColorCorrectionTextureHeight,
    NativeCameraTangentDispatch TangentMode,string NativeContextSource);

/// <summary>Recovered E6F580 screen/lens writers and EB5D50 SIMHDR argument
/// preparation for the exact resolved Tone2 profile. Complete retained CB padding and unrelated fields survive.
/// Every scalar operation follows the original float32 instruction order.</summary>
public static class MapFinalOutputSceneConstants
{
    /// <summary>Read source-created LimbStart/End and the exact unsigned DOF
    /// getter from the same accepted Camera. The remaining Apply route/producer
    /// arguments are retained explicitly. Lighting preparation may update the
    /// later prefix; the optical/pose prefix must still match this Camera.</summary>
    public static MapFinalOutputConstantPacket ApplySourceCreatedCamera(
        MapFinalOutputProgramSource program,ReadOnlySpan<byte> completeRetainedTemplate,
        MapCameraConstructionPacket camera,MapFinalOutputSceneConstantInputs input)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(input);
        if(camera.FrameNumber!=input.FrameNumber||input.PreparedCameraPrefix.Length!=0x3C0||
            !camera.Prefix.Span[..0x170].SequenceEqual(input.PreparedCameraPrefix.Span[..0x170]))
            throw new InvalidDataException("Final scene source scalar copies differ from the accepted Camera frame/optical prefix.");
        uint dof=camera.Source.CpuWord(0xBF0);
        return Apply(program,completeRetainedTemplate,input with{
            LimbDarkeningStart=BitConverter.UInt32BitsToSingle(camera.Source.CpuWord(0x51C)),
            LimbDarkeningEnd=BitConverter.UInt32BitsToSingle(camera.Source.CpuWord(0x520)),
            ResolvedDofType=dof<=2?dof:0});
    }
    public static MapFinalOutputConstantPacket Apply(
        MapFinalOutputProgramSource program,ReadOnlySpan<byte> completeRetainedTemplate,
        MapFinalOutputSceneConstantInputs input)
    {
        ArgumentNullException.ThrowIfNull(program);ArgumentNullException.ThrowIfNull(input);
        if(program.PixelSha256!=MapFinalOutputConstants.PixelSha256||
            Convert.ToHexString(SHA256.HashData(program.PixelBytes.Span))!=MapFinalOutputConstants.PixelSha256||completeRetainedTemplate.Length!=240||
            input.PreparedCameraPrefix.Length!=0x3C0||input.FrameNumber<=0||input.ApplyWidth==0||input.ApplyHeight==0||
            !Enum.IsDefined(input.TangentMode)||string.IsNullOrWhiteSpace(input.NativeContextSource))
            throw new InvalidDataException("Final scene writers require source PS28, complete CB, accepted3C0 Camera and explicit Apply arguments.");
        float[] current=[input.LimbDarkeningStart,input.LimbDarkeningEnd,input.ResolvedDofType,
            input.LightShaftPower.X,input.LightShaftPower.Y,input.LightShaftPower.Z,input.LightShaftPower.W,
            input.SimulatedHdrLuminanceArgument,input.ResolvedToneCurveType];
        if(current.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Final scene arguments must be finite resolved native values.");
        if(BitConverter.SingleToUInt32Bits(input.ResolvedToneCurveType)!=0x3E4CCCCD)
            throw new NotSupportedException("This recovered scene-argument producer supports original Tone2 only; other native tone profiles require their actual SIMHDR preparation.");
        var fields=DxbcResourceDefinitionReader.Read(program.PixelBytes.Span).Buffers.Single(b=>b.ByteSize==240).Variables.ToDictionary(v=>v.Name.Text);
        byte[] camera=input.PreparedCameraPrefix.ToArray(),bytes=completeRetainedTemplate.ToArray();var writes=new List<string>();
        float C(int at)=>BinaryPrimitives.ReadSingleLittleEndian(camera.AsSpan(at,4));
        float near=C(0x140),far=C(0x144),fov=C(0x148),aspect=C(0x14C);
        if(!float.IsFinite(near)||!float.IsFinite(far)||!float.IsFinite(fov)||!float.IsFinite(aspect)||near<=0||far<=near||fov<=0||aspect<=0)
            throw new InvalidDataException("Final scene lens writers require the accepted finite perspective Camera.");
        void Write(string name,params float[] values)
        {
            var field=fields[name];if(field.ByteSize!=values.Length*4)throw new InvalidDataException("Final scene writer differs from its source reflected field.");
            for(int i=0;i<values.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(field.StartOffset+i*4,4),values[i]);writes.Add(name);
        }
        float w=input.ApplyWidth,h=input.ApplyHeight,rw=Div(1,w),rh=Div(1,h);
        Write("vScreenSize",w,h);Write("vRecipScreenSize",rw,rh,Mul(rw,.5f),Mul(rh,.5f));
        bool reverse=(BinaryPrimitives.ReadUInt32LittleEndian(camera.AsSpan(0x158,4))&2)!=0;
        Write("vKIDSReverseZ",reverse?-1:1,reverse?1:0);
        float tan=NativeCameraTangent.Tangent1020A90(Mul(fov,.5f),input.TangentMode);
        Write("vViewInfo",Mul(Mul(tan,2),aspect),Mul(tan,-2),Div(far,Sub(near,far)),Div(Mul(far,near),Sub(far,near)));
        Write("fFar",far);Write("fKIDSDOFType",input.ResolvedDofType);
        float halfW=Mul(w,.5f),halfH=Mul(h,.5f);
        Write("vSpotParams",halfW,halfH,Mul(halfW,input.LimbDarkeningStart),Mul(halfW,input.LimbDarkeningEnd));
        float diameter=Mul(tan,4);Write("fLimbDarkening",Div(Mul(h,h),Mul(diameter,diameter)));
        Write("vLightShaftPower",input.LightShaftPower.X,input.LightShaftPower.Y,input.LightShaftPower.Z,input.LightShaftPower.W);
        float l=input.SimulatedHdrLuminanceArgument;
        float numerator=Add(Mul(Add(Mul(.15f,l),.05f),l),.004f);
        float denominator=Add(Mul(Add(Mul(.15f,l),.5f),l),.06f);
        float q=Sub(Div(numerator,denominator),.067f);
        Write("SimulateHDRParams",q>0?Div(1,q):0,q>0?q:0);
        Write("fKIDSToneCurveType",input.ResolvedToneCurveType);Write("fKIDSTrueHDR",input.ResolvedTrueHdr?1:0);
        Write("fColorCorrectionTexHeight",input.ColorCorrectionTextureHeight);
        return new(bytes,writes.AsReadOnly(),Array.AsReadOnly(fields.Keys.Where(k=>!writes.Contains(k)).ToArray()),input.FrameNumber,input.NativeContextSource);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Add(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Sub(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Mul(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Div(float a,float b)=>a/b;
}
