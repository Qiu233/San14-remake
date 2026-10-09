using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Positive lookup selection supplied by its actual View owner.
/// The private snapshot retains a complete produced LightData. Loading a light
/// declaration or enabling shafts does not create this selection.</summary>
public sealed class OriginalMapPostEffect3ApplyUncontrolledPresentLightInputs
{
    private readonly OriginalMapSourceCreatedLightingPacket light;
    private OriginalMapPostEffect3ApplyUncontrolledPresentLightInputs(OriginalMapSourceCreatedLightingPacket selected,
        string owner,string evidence,bool shaft)
        =>(light,ActualLookupOwnerIdentity,LookupEvidence,ActualLightShaftArgument)=(selected.CopyAcceptedSnapshot(),owner,evidence,shaft);
    public long FrameNumber=>light.FrameNumber;
    public string ActualLookupOwnerIdentity{get;}
    public string LookupEvidence{get;}
    public bool ActualLightShaftArgument{get;}
    public string SourceEvidence=>light.SourceProvenance+";"+LookupEvidence;
    internal OriginalMapSourceCreatedLightingPacket Light=>light;
    public static OriginalMapPostEffect3ApplyUncontrolledPresentLightInputs CaptureSelectedLookup(
        OriginalMapSourceCreatedLightingPacket actualSelectedLight,string actualLookupOwnerIdentity,
        string actualSuccessfulContextAndViewMaskLookupEvidence,bool actualLightShaftArgument)
    {
        ArgumentNullException.ThrowIfNull(actualSelectedLight);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualLookupOwnerIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualSuccessfulContextAndViewMaskLookupEvidence);
        if(actualSelectedLight.FrameNumber<=0||actualSelectedLight.LocalLightData.Length!=0x10C0)
            throw new InvalidDataException("Present light requires its actual same-frame complete selected LightData.");
        return new(actualSelectedLight,actualLookupOwnerIdentity,actualSuccessfulContextAndViewMaskLookupEvidence,actualLightShaftArgument);
    }
}

public sealed record OriginalMapPostEffect3PresentLightCalculatedValues(float WhitePoint,Vector3 SunProjPos,
    Vector2 SunScale,Vector3 SunColor,bool IntoSunLight,int LsGaussBlur,int LsStepLength);

/// <summary>Complete present-light writers. These are computed after the
/// actual positive lookup and before the direct Camera LS scalar writes.</summary>
public static class OriginalMapPostEffect3ApplyUncontrolledPresentLightWriters
{
    public static IReadOnlyList<OriginalMapPostEffect3ApplyValueWrite> PrepareFreshSource(
        OriginalMapPostEffect3ApplyPreparationSelection selection,OriginalMapCameraConstructionPacket camera,
        OriginalMapPostEffect3ApplyUncontrolledPresentLightInputs input,
        OriginalMapPostEffect3PresentLightMathSource math,LoadedOriginalNativeCameraTangentSource tangent,
        OriginalNativeCameraTangentDispatch explicitDispatch)
    {
        ArgumentNullException.ThrowIfNull(selection);ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(math);ArgumentNullException.ThrowIfNull(tangent);
        if(selection.Variant!=OriginalMapPostEffect3ApplyUncontrolledVariant.LensPresetCameraFields)
            throw new NotSupportedException("Present light preparation requires the actual  descriptor route");
        if(input.FrameNumber!=camera.FrameNumber||math.Locale!=camera.ExecutableLocale||tangent.Language!=camera.ExecutableLocale||
            input.Light.SourceIdentity.CameraObjectId!=camera.Source.Camera.ObjectId||
            !input.Light.PreparedCameraPrefix.Span.SequenceEqual(camera.Prefix.Span))
            throw new InvalidDataException("Present light does not belong to this exact prepared Camera frame and native math locale.");
        var source=OriginalMapCameraConstructionSource.ReadPerspective(camera.Source.Resource,camera.Source.Camera.ObjectId);
        float P(int ordinal,uint hash,int component=0,int count=1)
        {
            var p=source.Camera.Properties[ordinal];
            if(p.Index!=ordinal||p.NameHash!=hash||p.TypeCode!=8||p.ElementCount!=count||p.RawValue.Length!=count*4)
                throw new InvalidDataException("Present light fresh Camera field differs from its source loader binding.");
            return BinaryPrimitives.ReadSingleLittleEndian(p.RawValue.AsSpan(component*4,4));
        }
        Vector3 V3(int offset)=>new(BinaryPrimitives.ReadSingleLittleEndian(input.Light.LocalLightData.Span.Slice(offset,4)),
            BinaryPrimitives.ReadSingleLittleEndian(input.Light.LocalLightData.Span.Slice(offset+4,4)),
            BinaryPrimitives.ReadSingleLittleEndian(input.Light.LocalLightData.Span.Slice(offset+8,4)));
        // .. copies CPU B88..B98 into embedded Camera E18..E28.
        var values=CalculateExplicitCurrentFields(camera.Prefix,V3(0),V3(0xFA0),P(134,0xEBCADD79),
            new(P(135,0x41C4B441,0,3),P(135,0x41C4B441,1,3),P(135,0x41C4B441,2,3)),
            P(136,0xB2EB70EA),P(290,0xEB8B5927),math,tangent,explicitDispatch);
        string evidence=selection.SourceEvidence+";"+input.SourceEvidence+";"+math.SourceEvidence+
            "TC;SC;selected-positive;fresh-Camera-E18/E1C/E28/1318";
        OriginalMapPostEffect3ApplyValueWrite W(string name,OriginalMapPostEffect3WriteStage stage,params float[] fields)
        {byte[] b=new byte[fields.Length*4];for(int i=0;i<fields.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(i*4,4),fields[i]);return new(name,stage,b,evidence);}
        OriginalMapPostEffect3ApplyValueWrite Int(string name,OriginalMapPostEffect3WriteStage stage,int value)
        {byte[] b=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(b,value);return new(name,stage,b,evidence);}
        return Array.AsReadOnly(new[]{W("WhitePoint",OriginalMapPostEffect3WriteStage.LightWhitePointUpdated,values.WhitePoint),
            W("SunProjPos",OriginalMapPostEffect3WriteStage.LightSunProjPos,values.SunProjPos.X,values.SunProjPos.Y,values.SunProjPos.Z),
            Int("IntoSunLight",OriginalMapPostEffect3WriteStage.LightIntoSunLight,values.IntoSunLight?1:0),W("SunScale",OriginalMapPostEffect3WriteStage.LightSunScale,values.SunScale.X,values.SunScale.Y),
            W("SunColor",OriginalMapPostEffect3WriteStage.LightSunColor,values.SunColor.X,values.SunColor.Y,values.SunColor.Z),
            Int("LightShaftEnable",OriginalMapPostEffect3WriteStage.LightLightShaftEnable,input.ActualLightShaftArgument?1:0),
            Int("LSGaussBlur",OriginalMapPostEffect3WriteStage.LightLSGaussBlur,values.LsGaussBlur),Int("LSStepLength",OriginalMapPostEffect3WriteStage.LightLSStepLength,values.LsStepLength)});
    }

    /// <summary>Pure scalar primitive for explicit current fields and native
    /// arithmetic verification. It does not establish a successful light lookup,
    /// publish named scalar writes or infer a missing Camera/light as zero.</summary>
    public static OriginalMapPostEffect3PresentLightCalculatedValues CalculateExplicitCurrentFields(
        ReadOnlyMemory<byte> currentCameraPrefix,Vector3 nativeLightDirection0,Vector3 nativeLightRgbFa0,
        float cameraE18Length,Vector3 cameraE1cColor,float cameraE28Step,float camera1318Radius,
        OriginalMapPostEffect3PresentLightMathSource math,LoadedOriginalNativeCameraTangentSource tangent,
        OriginalNativeCameraTangentDispatch explicitDispatch)
    {
        ArgumentNullException.ThrowIfNull(math);ArgumentNullException.ThrowIfNull(tangent);
        if(currentCameraPrefix.Length!=0x3C0||math.Locale!=tangent.Language||explicitDispatch!=OriginalNativeCameraTangentDispatch.Sse)
            throw new NotSupportedException("Present light requires a complete actual Camera and matching finite SSE source arithmetic.");
        if(new[]{nativeLightDirection0.X,nativeLightDirection0.Y,nativeLightDirection0.Z,nativeLightRgbFa0.X,nativeLightRgbFa0.Y,nativeLightRgbFa0.Z,
            cameraE18Length,cameraE1cColor.X,cameraE1cColor.Y,cameraE1cColor.Z,cameraE28Step,camera1318Radius}.Any(x=>!float.IsFinite(x)))
            throw new InvalidDataException("Present light contains nonfinite consumed current fields.");
        float C(int offset)
        {float x=BinaryPrimitives.ReadSingleLittleEndian(currentCameraPrefix.Span.Slice(offset,4));if(!float.IsFinite(x))throw new InvalidDataException("Present light Camera field is not finite.");return x;}
        float epsilon=BitConverter.UInt32BitsToSingle(0x37480000);
        float white=A(A(M(nativeLightRgbFa0.Y,F(0x3F1645A2)),M(nativeLightRgbFa0.X,F(0x3E991687))),M(nativeLightRgbFa0.Z,F(0x3DE978D5)));
        float x=A(M(-cameraE18Length,nativeLightDirection0.X),C(0x10));
        float y=A(M(-cameraE18Length,nativeLightDirection0.Y),C(0x14));
        float z=A(M(-cameraE18Length,nativeLightDirection0.Z),C(0x18));
        float Clip(int component,bool reverseFirst=false)
        {
            float first=reverseFirst?A(M(y,C(0x110+component*4)),M(x,C(0x100+component*4))):A(M(x,C(0x100+component*4)),M(y,C(0x110+component*4)));
            return A(A(first,M(z,C(0x120+component*4))),M(1,C(0x130+component*4)));
        }
        float clipX=Clip(0,true),clipY=Clip(1),clipZ=Clip(2),clipW=Clip(3),inverse=V(1,clipW);
        var projected=new Vector3(M(inverse,clipX),M(inverse,clipY),M(inverse,clipZ));
        if((BinaryPrimitives.ReadUInt32LittleEndian(currentCameraPrefix.Span.Slice(0x158,4))&2)!=0)projected.Z=S(1,projected.Z);
        float eyeX=S(C(0x10),C(0x20)),eyeY=S(C(0x14),C(0x24)),eyeZ=S(C(0x18),C(0x28));
        float norm=OriginalMapPostEffect3PresentLightMath.Sqrt(A(A(M(eyeY,eyeY),M(eyeX,eyeX)),M(eyeZ,eyeZ)));
        float eyeInverse=V(1,norm);
        float dot=A(A(M(M(eyeX,eyeInverse),nativeLightDirection0.X),M(M(eyeY,eyeInverse),nativeLightDirection0.Y)),M(M(eyeZ,eyeInverse),nativeLightDirection0.Z));
        float attenuation=MathF.Abs(M(M(dot,dot),dot));
        var color=new Vector3(M(attenuation,cameraE1cColor.X),M(attenuation,cameraE1cColor.Y),M(attenuation,cameraE1cColor.Z));
        float viewX=A(A(M(nativeLightDirection0.Y,C(0x90)),M(nativeLightDirection0.X,C(0x80))),M(nativeLightDirection0.Z,C(0xA0)));
        float viewY=A(A(M(nativeLightDirection0.Y,C(0x94)),M(nativeLightDirection0.X,C(0x84))),M(nativeLightDirection0.Z,C(0xA4)));
        float viewZ=A(A(M(nativeLightDirection0.Y,C(0x98)),M(nativeLightDirection0.X,C(0x88))),M(nativeLightDirection0.Z,C(0xA8)));
        // .. and .. round the reciprocal first,
        // then multiply Z. Combining these into Z length changes bits.
        float vertical=M(V(1,Max(OriginalMapPostEffect3PresentLightMath.Sqrt(A(A(M(viewY,viewY),0),M(viewZ,viewZ))),epsilon)),viewZ);
        float halfField=Max(Tan(M(C(0x148),.5f),tangent),epsilon),horizontalField=M(halfField,C(0x14C));
        float angularRadius=OriginalMapPostEffect3PresentLightMath.Asin(V(camera1318Radius,Max(epsilon,cameraE18Length)),math);
        float horizontal=M(V(1,Max(OriginalMapPostEffect3PresentLightMath.Sqrt(A(A(M(viewX,viewX),0),M(viewZ,viewZ))),epsilon)),viewZ);
        float Scale(float direction,float field)
        {
            float angle=OriginalMapPostEffect3PresentLightMath.Acos(direction,math);
            float upper=Tan(A(angle,angularRadius),tangent),lower=Tan(S(angle,angularRadius),tangent);
            float clippedUpper=upper>=field?field:Max(0,upper),clippedLower=lower>=field?field:Max(0,lower);
            return V(M(S(clippedUpper,clippedLower),.5f),field);
        }
        var scale=new Vector2(Scale(horizontal,horizontalField),Scale(vertical,halfField));
        bool into=(scale.X>0&&scale.Y>0)||color.X>=epsilon||color.Y>=epsilon||color.Z>=epsilon;
        float floored=MathF.Floor(cameraE28Step); // clears fractional bits; negatives subtract 1.
        int step=floored>=2147483648f||floored< -2147483648f?int.MinValue:(int)floored;
        if(new[]{white,projected.X,projected.Y,projected.Z,scale.X,scale.Y,color.X,color.Y,color.Z}.Any(v=>!float.IsFinite(v)))
            throw new NotSupportedException("Present light exceptional projection/normalization requires its separate native path.");
        return new(white,projected,scale,color,into,7,step);
    }
    private static float Tan(float angle,LoadedOriginalNativeCameraTangentSource source)
    {
        // fast reduction supports these bounded signed angles as well
        // as the existing positive half-FOV caller. The same source constants
        // and odd-quadrant reciprocal are retained; no pi subtraction shortcut.
        if(!float.IsFinite(angle)||MathF.Abs(angle)>4)throw new NotSupportedException("Present light tangent left the verified finite fast-reduction domain.");
        uint bits=BitConverter.SingleToUInt32Bits(angle);float abs=F(bits&0x7FFFFFFF);
        if(abs==0)return angle;
        var c=OriginalNativeCameraTangentConstants.Values;double x=abs;int count=0;
        if((bits&0x7FFFFFFF)<OriginalNativeCameraTangentConstants.TinyInputThresholdBits)return angle;
        if(x<c.SmallInputThreshold)
        {
            float tiny=(float)DA(DM(DM(DM(x,x),x),c.OneThird),x);
            return (bits&0x80000000)!=0?F(BitConverter.SingleToUInt32Bits(tiny)^0x80000000):tiny;
        }
        if(x>c.QuarterPi)
        {count=(int)DA(DM(x,c.TwoOverPi),c.Half);x=DS(DS(x,DM(count,c.HalfPiHigh)),DM(count,c.HalfPiLow));}
        double q=DM(x,x),numerator=DA(DM(c.Numerator2,q),c.Numerator0);
        double denominator=DA(DM(DA(DM(c.Denominator2,q),c.Denominator1),q),c.Denominator0);
        double result=DA(x,DM(DM(q,x),DV(numerator,denominator)));
        if(count%2!=0)result=DV(c.NegativeOne,result);
        float value=(float)result;return (bits&0x80000000)!=0?F(BitConverter.SingleToUInt32Bits(value)^0x80000000):value;
    }
    private static float F(uint x)=>BitConverter.UInt32BitsToSingle(x);
    private static float Max(float a,float b)=>a>b?a:b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float V(float a,float b)=>a/b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double DA(double a,double b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double DS(double a,double b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double DM(double a,double b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double DV(double a,double b)=>a/b;
}
