using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Explicit owned presence and C09270 descriptor extent of Apply's
/// PostEffect3 input. Absence is a caller fact; it is not inferred from an
/// unimplemented effect or from the source object's name.</summary>
public sealed class OriginalMapFinalOutputPostEffect3Owner
{
    private OriginalMapFinalOutputPostEffect3Owner(string identity,string proof,bool present,int width,int height)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);ArgumentException.ThrowIfNullOrWhiteSpace(proof);
        if(present&&(width<=0||height<=0))throw new InvalidDataException("Present PostEffect3 needs its actual descriptor extent.");
        OwnerIdentity=identity;SourceEvidence=proof;Present=present;Width=width;Height=height;
    }
    public string OwnerIdentity{get;}
    public string SourceEvidence{get;}
    public bool Present{get;}
    public int Width{get;}
    public int Height{get;}
    public static OriginalMapFinalOutputPostEffect3Owner CreateAbsent(string ownerIdentity,string callerEvidence)=>
        new(ownerIdentity,callerEvidence,false,0,0);
    public static OriginalMapFinalOutputPostEffect3Owner CreatePresent(string ownerIdentity,int width,int height,string typedSpkDescriptorEvidence)=>
        new(ownerIdentity,typedSpkDescriptorEvidence,true,width,height);
}

/// <summary>Pure EB62D4..EB6340 / EB6899..EB6927 projection. This describes
/// Apply's TrueHDR parameter, independently of Q.v80's output-HDR predicate.
/// It neither installs a runtime proxy nor implements EB638D's conversion.</summary>
public sealed class OriginalMapFinalOutputHdrRoute
{
    public const ulong LowFloatFormatMask=0x000000018000781CUL;
    public const ulong HighFloatFormatMask=0x01C03E204000F000UL;
    public const string NativeBodySha256="D188F62871633D524939D157A34B77232EF9A24DFEAD1061F516E561EFFF939E";
    private readonly OriginalRenderTextureRuntimeDescriptor working;
    private OriginalMapFinalOutputHdrRoute(OriginalRenderTextureRuntimeDescriptor target,int width,int height,
        int mode,OriginalMapFinalOutputPostEffect3Owner effect,long frame,string caller)
    {
        working=target;Width=width;Height=height;CurrentCommonMode3C0=mode;PostEffect3=effect;
        FrameNumber=frame;CallerOwnerIdentity=caller;
        FormatTrueHdr=IsFloatFormat(target.PixelFormat);
        PostEffect3OverrideApplied=mode==1&&effect.Present;
        ResolvedTrueHdr=Resolve(target.PixelFormat,width,height,mode,effect);
    }
    public int Width{get;}
    public int Height{get;}
    public int CurrentCommonMode3C0{get;}
    public OriginalMapFinalOutputPostEffect3Owner PostEffect3{get;}
    public long FrameNumber{get;}
    public string CallerOwnerIdentity{get;}
    public string WorkingTargetOwnerIdentity=>working.OwnerIdentity;
    public byte EffectiveNativeCode=>working.PixelFormat;
    public bool FormatTrueHdr{get;}
    public bool PostEffect3OverrideApplied{get;}
    public bool ResolvedTrueHdr{get;}
    public string SourceEvidence=>"TC:EB5D50@"+NativeBodySha256+";EB62D4-two-format-masks;EB6899-mode1-present-SPK-extent;"+working.SourceProvenance+";"+PostEffect3.SourceEvidence;
    public static bool IsFloatFormat(ushort rawNativeCode)
    {
        int code=rawNativeCode&0x7F;
        return code<64?(LowFloatFormatMask&(1UL<<code))!=0:(HighFloatFormatMask&(1UL<<(code-64)))!=0;
    }
    public static bool Resolve(ushort rawNativeCode,int width,int height,int currentCommonMode3C0,
        OriginalMapFinalOutputPostEffect3Owner explicitPostEffect3)
    {
        ArgumentNullException.ThrowIfNull(explicitPostEffect3);
        if(width<=0||width>ushort.MaxValue||height<=0||height>ushort.MaxValue)
            throw new InvalidDataException("Apply working wrapper dimensions are nonzero native u16 values.");
        if(currentCommonMode3C0==1&&explicitPostEffect3.Present)
            return explicitPostEffect3.Width!=width||explicitPostEffect3.Height!=height;
        return IsFloatFormat(rawNativeCode);
    }
    public static OriginalMapFinalOutputHdrRoute Capture(OriginalRenderTextureRuntimeDescriptor actualWorkingTarget,
        int actualWidth,int actualHeight,int currentCommonMode3C0,OriginalMapFinalOutputPostEffect3Owner effectOwner,
        long frameNumber,string callerOwnerIdentity)
    {
        ArgumentNullException.ThrowIfNull(actualWorkingTarget);ArgumentNullException.ThrowIfNull(effectOwner);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerOwnerIdentity);
        if(frameNumber<=0)throw new InvalidDataException("HDR route needs its actual owned frame.");
        _=Resolve(actualWorkingTarget.PixelFormat,actualWidth,actualHeight,currentCommonMode3C0,effectOwner);
        OriginalRenderTextureResolutionFrame.ValidateDeclaration(actualWorkingTarget.Declaration);
        return new(actualWorkingTarget,actualWidth,actualHeight,currentCommonMode3C0,effectOwner,frameNumber,callerOwnerIdentity);
    }
    public void ValidateSameWorkingTarget(OriginalRenderTextureRuntimeDescriptor target,int width,int height,
        long frameNumber,string callerOwnerIdentity)
    {
        if(!ReferenceEquals(target,working)||width!=Width||height!=Height||frameNumber!=FrameNumber||callerOwnerIdentity!=CallerOwnerIdentity||
            ResolvedTrueHdr!=Resolve(working.PixelFormat,Width,Height,CurrentCommonMode3C0,PostEffect3))
            throw new InvalidDataException("HDR route must retain the same resolved working owner, extent and frame.");
        OriginalRenderTextureResolutionFrame.ValidateDeclaration(working.Declaration);
    }
}

/// <summary>EB6CF1/EB6CF7 and EB7133..EB71A8 produce the value passed at
/// EB9654 rsp+30 to E6F580's fMaxLum writer. Camera offsets are whole prepared
/// Camera offsets (outside the 3C0 prefix). EB5F50 ApplyReference+18 is a
/// different earlier value and is not an input to this projection.</summary>
public static class OriginalMapFinalOutputBloomCaller
{
    public static float Resolve(float currentCamera1384,float currentCamera1398,
        bool resolvedCamera1830Present,bool resolvedCamera1840Present)
    {
        if(!float.IsFinite(currentCamera1384)||!float.IsFinite(currentCamera1398))
            throw new InvalidDataException("Bloom caller needs finite current whole-Camera scalar values.");
        if(!resolvedCamera1830Present&&!resolvedCamera1840Present)return 0f;
        if(currentCamera1398<=0f)return resolvedCamera1830Present?currentCamera1384:0f;
        if(currentCamera1398>=1f)return resolvedCamera1840Present?currentCamera1384:0f;
        if(!resolvedCamera1830Present)return Multiply(currentCamera1384,currentCamera1398);
        if(!resolvedCamera1840Present)return Multiply(currentCamera1384,Subtract(1f,currentCamera1398));
        return currentCamera1384;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Multiply(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Subtract(float a,float b)=>a-b;
}
