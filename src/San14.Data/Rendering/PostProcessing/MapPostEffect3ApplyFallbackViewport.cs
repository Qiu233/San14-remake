using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Resolved inputs only. Extents are backend95B0C0's target
/// descriptor50/54, not the source texture extent. Camera fields are from the
/// actual local snapshot prepared at rbp-70. Apply28Present remains unknown
/// when the special rectangle has no positive area: either pointer branch
/// reaches the same ordinary draw. No null pointer is inferred.</summary>
public sealed record MapPostEffect3ApplyFallbackViewportInputs(long FrameNumber,string OwnerIdentity,
    int ActualTargetWidth50,int ActualTargetHeight54,uint ActualTargetColorSpace60,
    ushort ActualSourceFlags36,ushort ActualSourceSamplerFlags38,uint ActualCamera1CC,
    Vector4 ActualCameraViewport1540,Vector4 ActualCameraSpecialRectangle152C,
    bool? ActualApply28Present,float ActualFifthFloatArgument,string SourceEvidence)
{
    /// <summary>Raw native descriptor60 classification; this API only uses its
    /// actual0/1 branches and does not infer it from a Godot image format.</summary>
    public uint ActualTargetKind60=>ActualTargetColorSpace60;
}

public readonly record struct MapPostEffect3ApplyFallbackPixelRectangle(int Left,int Top,int Right,int Bottom);

/// <summary>Facts from the context's actual selected Camera and backend Q.v80.
/// Unknown optional facts are explicit null, not default false/zero. Q.v80
/// is not needed on targetkind60==1's native short-circuit branch.</summary>
public sealed record MapPostEffect3ApplyFallbackGammaInputs(long FrameNumber,string OwnerIdentity,
    bool ActualContextCameraSelected,float? ActualSelectedCameraGamma174,
    uint ActualTargetKind60,bool? ActualQVirtual80Predicate,string SourceEvidence);

public sealed record MapPostEffect3ApplyFallbackGammaPlan(long FrameNumber,string OwnerIdentity,
    float SelectedGammaOrSourceDefault,float AdjustedGamma,float ShaderReciprocalGamma,
    bool UsedSourceGammaDefault,bool DivisionBySourceGammaApplied,string SourceEvidence);

/// <summary>Native49 draw packet emitted by→. Stores pixel-space
/// rect, UV rect, packed colorFFFFFFFF and the actual fifth draw argument0.
/// The GPU adapter still supplies its source-proven programs and resources.</summary>
public sealed class MapPostEffect3ApplyFallbackViewportPlan
{
    private readonly byte[] packet;
    internal MapPostEffect3ApplyFallbackViewportPlan(MapPostEffect3ApplyFallbackViewportInputs input,
        MapPostEffect3ApplyFallbackPixelRectangle rectangle,Vector4 uv,float scalar,
        bool direct,bool disableView,bool special,ushort drawingSampler,byte[] bytes)
    {Inputs=input;PixelRectangle=rectangle;UvRectangle=uv;FifthScalarXmm8=scalar;
     UsesDirectTextureDrawPath=direct;RequestsSourceViewBit15Clear=disableView;
     SpecialRectangleApplied=special;DrawingSamplerFlags38=drawingSampler;packet=bytes;}
    public MapPostEffect3ApplyFallbackViewportInputs Inputs{get;}
    public long FrameNumber=>Inputs.FrameNumber;
    public MapPostEffect3ApplyFallbackPixelRectangle PixelRectangle{get;}
    public Vector4 UvRectangle{get;}
    public float FifthScalarXmm8{get;}
    public bool UsesDirectTextureDrawPath{get;}
    public bool RequiresGenericCopyShader=>!UsesDirectTextureDrawPath;
    public bool RequestsSourceViewBit15Clear{get;}
    public bool SourceViewBit15Before=>(Inputs.ActualSourceFlags36&0x8000)!=0;
    /// <summary>Requested draw state, not a claim that native succeeded.</summary>
    public ushort RequestedDrawingSourceFlags36=>RequestsSourceViewBit15Clear?
        (ushort)(Inputs.ActualSourceFlags36&0x7FFF):Inputs.ActualSourceFlags36;
    public ushort RestoredSourceFlags36=>Inputs.ActualSourceFlags36;
    public bool SpecialRectangleApplied{get;}
    public ushort DrawingSamplerFlags38{get;}
    public ushort RestoredSamplerFlags38=>Inputs.ActualSourceSamplerFlags38;
    public uint PackedColorR9=>uint.MaxValue;
    public float FifthDrawArgument=>0;
    public ReadOnlyMemory<byte> Native49Packet=>packet;
    public string SourceEvidence=>"TC"+Inputs.SourceEvidence;
    public bool IsCompleteForExecution=>false;
}

public static class MapPostEffect3ApplyFallbackViewport
{
    public static MapPostEffect3ApplyFallbackGammaPlan ResolveShaderGamma(
        MapPostEffect3ApplyFallbackGammaInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentException.ThrowIfNullOrWhiteSpace(input.OwnerIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SourceEvidence);
        if(input.FrameNumber<=0)throw new InvalidDataException("Fallback gamma requires an owned frame.");
        float sourceGamma=BitConverter.UInt32BitsToSingle(0x400CCCCD);
        float gamma=sourceGamma;
        if(input.ActualContextCameraSelected)
        {
            if(input.ActualSelectedCameraGamma174 is not float actual||!float.IsFinite(actual))
                throw new InvalidDataException("Selected context Camera gamma174 is unresolved/nonfinite.");
            gamma=actual;
        }
        else if(input.ActualSelectedCameraGamma174 is not null)
            throw new InvalidDataException("Absent selected context Camera cannot carry a different Camera's gamma.");
        bool divide=input.ActualTargetKind60==1;
        if(!divide)
            divide=input.ActualQVirtual80Predicate??throw new InvalidDataException("Fallback Q.v80 branch is unresolved.");
        float adjusted=divide?D(gamma,sourceGamma):gamma;
        float reciprocal=D(1,adjusted);
        if(!float.IsFinite(adjusted)||!float.IsFinite(reciprocal))
            throw new NotSupportedException("Fallback gamma reaches an unadmitted zero/nonfinite native shader result.");
        return new(input.FrameNumber,input.OwnerIdentity,gamma,adjusted,reciprocal,
            !input.ActualContextCameraSelected,divide,
            "TC;source-literal400CCCCD;actual-context-selected-Camera174"+input.SourceEvidence);
    }
    public static MapPostEffect3ApplyFallbackViewportPlan Prepare(
        MapPostEffect3ApplyFallbackViewportInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentException.ThrowIfNullOrWhiteSpace(input.OwnerIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SourceEvidence);
        if(input.FrameNumber<=0||input.ActualTargetWidth50<=0||input.ActualTargetHeight54<=0||
            !Finite(input.ActualCameraViewport1540)||!Finite(input.ActualCameraSpecialRectangle152C)||
            !float.IsFinite(input.ActualFifthFloatArgument))
            throw new InvalidDataException("Fallback viewport requires owned finite actual descriptor/Camera/caller inputs.");
        bool originalView=(input.ActualSourceFlags36&0x8000)!=0;
        bool forceViewOff=input.ActualTargetColorSpace60==0&&originalView;
        bool direct=false;float scalar=input.ActualFifthFloatArgument;
        if(input.ActualTargetColorSpace60==0&&!originalView)
        {
            // uses the very same two source float-format masks as Apply.
            direct=!MapFinalOutputHdrRoute.IsFloatFormat(input.ActualSourceFlags36);
            if(input.ActualCamera1CC==0)
            {scalar=BitConverter.UInt32BitsToSingle(0x3E99999A);direct=false;}
        }
        Vector4 viewport=input.ActualCameraViewport1540;
        int left=FloorProduct(input.ActualTargetWidth50,viewport.X);
        int top=FloorProduct(input.ActualTargetHeight54,viewport.Y);
        int right=checked(left+FloorProduct(input.ActualTargetWidth50,viewport.Z));
        int bottom=checked(top+FloorProduct(input.ActualTargetHeight54,viewport.W));
        var rect=new MapPostEffect3ApplyFallbackPixelRectangle(left,top,right,bottom);
        Vector4 uv=new(0,0,viewport.Z,viewport.W);
        Vector4 special=input.ActualCameraSpecialRectangle152C;
        //Native tests Apply+28 first, then test
        //positive Z/W. If either extent is nonpositive, both possible pointer
        //branches converge at with unchanged rect/UV/sampler. This is
        //a proved result invariant, not a claim that native never reads+28.
        bool useSpecial=special.Z>0&&special.W>0&&
            !(input.ActualApply28Present??throw new InvalidDataException(
                "Positive special rectangle requires the actual Apply28 presence."));
        ushort sampler=input.ActualSourceSamplerFlags38;
        if(useSpecial)
        {
            float u0=S(special.X,viewport.X),v0=S(special.Y,viewport.Y);
            uv=new(u0,v0,A(special.Z,u0),A(special.W,v0));
            rect=new(0,0,input.ActualTargetWidth50,input.ActualTargetHeight54);
            //..: both address modes1, clear upper bits14/15.
            sampler=(ushort)((sampler&0x03FF)|0x1400);
        }
        if(!Finite(uv))throw new InvalidDataException("Fallback UV expression is outside the finite admitted source domain.");
        byte[] packet=new byte[0x2C];BinaryPrimitives.WriteUInt32LittleEndian(packet,0x49);
        F(4,rect.Left);F(8,rect.Top);F(12,rect.Right);F(16,rect.Bottom);
        F(20,uv.X);F(24,uv.Y);F(28,uv.Z);F(32,uv.W);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(36),uint.MaxValue);F(40,0);
        return new(input,rect,uv,scalar,direct,forceViewOff,useSpecial,sampler,packet);
        void F(int offset,float value)=>BinaryPrimitives.WriteSingleLittleEndian(packet.AsSpan(offset),value);
    }
    private static int FloorProduct(int dimension,float normalized)
    {
        float value=NativeFloatRounding.Floor101EB80(M(dimension,normalized));
        //CVTTSS2SI overflow returns INT_MIN; no meaningful owned quad may use
        //that invalid sentinel. This explicit domain boundary is not a clamp.
        if(!float.IsFinite(value)||value<int.MinValue||(double)value>int.MaxValue)
            throw new InvalidDataException("Fallback pixel conversion reaches the unadmitted native invalid-int32 sentinel.");
        return (int)value;
    }
    private static bool Finite(Vector4 value)=>float.IsFinite(value.X)&&float.IsFinite(value.Y)&&float.IsFinite(value.Z)&&float.IsFinite(value.W);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
