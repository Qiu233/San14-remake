using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

public readonly record struct OriginalMainMapCameraClientPoint(int X,int Y);
public readonly record struct OriginalMainMapCameraCanonicalPoint(int X34,int Y38);
public readonly record struct OriginalMainMapCameraPointerDelta(int X44,int Y48);

public static partial class OriginalMainMapCameraController
{
    /// <summary>WM_MOUSEMOVE uses signed16 low/high LPARAM lanes.</summary>
    public static OriginalMainMapCameraClientPoint ReadMouseMoveClientPoint(ulong lParam) =>
        new(unchecked((short)lParam),unchecked((short)(lParam>>16)));

    /// <summary>SC3F67B0 / TC3F6170 canonical cursor writer. Dimensions are
    /// the actual selected client and target records; no DPI factor is added.
    /// Zero client width retains the raw pair. CVTT retains its native indefinite
    /// INT_MIN for NaN, infinity or a value outside signed32 range.</summary>
    public static OriginalMainMapCameraCanonicalPoint CanonicalCursor(
        OriginalMainMapCameraClientPoint client,int clientWidth,int clientHeight,int targetWidth,int targetHeight)
    {
        if(clientWidth==0)return new(client.X,client.Y);
        float w=targetWidth,h=targetHeight;
        float x=D(M(client.X,w),clientWidth),y=D(M(client.Y,h),clientHeight);
        float nineW=M(w,9f),sixteenH=M(h,16f);
        if(nineW>sixteenH)
        {
            float offset=M(S(w,D(M(h,16f),9f)),.5f);
            x=D(M(S(x,offset),1080f),h);
            y=D(M(y,1080f),h);
        }
        else if(nineW<sixteenH)
        {
            x=D(M(x,1920f),w);
            float offset=M(S(h,M(M(w,9f),.0625f)),.5f);
            y=D(M(S(y,offset),1920f),w);
        }
        else
        {
            x=D(M(x,1920f),w);
            y=D(M(y,1080f),h);
        }
        if(!Sse.IsSupported)throw new PlatformNotSupportedException("Original cursor conversion requires scalar SSE CVTT.");
        return new(Sse.ConvertToInt32WithTruncation(Vector128.CreateScalar(x)),
            Sse.ConvertToInt32WithTruncation(Vector128.CreateScalar(y)));
    }

    /// <summary>Source+44/+48 are wrapped signed32 current-minus-previous
    /// canonical words. Disabled source+64 writes zero.</summary>
    public static OriginalMainMapCameraPointerDelta CanonicalDelta(
        OriginalMainMapCameraCanonicalPoint current,OriginalMainMapCameraCanonicalPoint previous,bool inputEnabled64) =>
        inputEnabled64?new(unchecked(current.X34-previous.X34),unchecked(current.Y38-previous.Y38)):new(0,0);
}
