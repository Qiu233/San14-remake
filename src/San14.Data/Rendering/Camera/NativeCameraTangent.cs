using System.Runtime.CompilerServices;

namespace San14.Data;

public enum NativeCameraTangentDispatch { Sse, AvxFma }

/// <summary>Finite camera half-FOV port of SC1020A90 / TC101FB90. Input and
/// result are binary32; the original approximation and reduction use binary64.
/// SSE rounds each scalar operation separately, AVX retains the native FMA
/// sites. Caller selects dispatch and assumes nearest-even arithmetic.
/// Large angle reduction, special inputs and floating exception flags are
/// outside this camera contract.</summary>
public static class NativeCameraTangent
{
    public const uint ExclusiveHalfFovLimitBits=0x3FC90FDB;
    public static float Tangent1020A90(float halfFovRadians,NativeCameraTangentDispatch dispatch)
    {
        uint input=BitConverter.SingleToUInt32Bits(halfFovRadians);
        if(input==0||input>=ExclusiveHalfFovLimitBits||dispatch is not
            (NativeCameraTangentDispatch.Sse or NativeCameraTangentDispatch.AvxFma))
            throw new InvalidDataException("Original camera tangent requires a positive finite half-FOV below float pi/2 and an explicit SSE/AVX-FMA dispatch.");
        var c=NativeCameraTangentConstants.Values;
        bool fused=dispatch==NativeCameraTangentDispatch.AvxFma;
        // The native tiny branch performs exception-status arithmetic in xmm1
        // and returns the untouched xmm0. Returning the input also retains
        // subnormal binary32 bits without relying on the host DAZ setting.
        if(input<NativeCameraTangentConstants.TinyInputThresholdBits)return halfFovRadians;
        double x=halfFovRadians;
        if(x<c.QuarterPi)
        {
            if(x<c.SmallInputThreshold)
            {
                double cube=Multiply(Multiply(x,x),x);
                return Convert(fused?Math.FusedMultiplyAdd(cube,c.OneThird,x):Add(Multiply(cube,c.OneThird),x));
            }
            return Convert(Kernel(x,c,fused));
        }
        if(x==c.QuarterPi)return Convert(Kernel(x,c,fused));
        // The complete proven camera domain reaches only quadrant1 of the
        // source fast reduction. Retain its count arithmetic and split pi/2.
        double countInput=fused?Math.FusedMultiplyAdd(x,c.TwoOverPi,c.Half):Add(Multiply(x,c.TwoOverPi),c.Half);
        int count=(int)countInput;
        if(count!=1)throw new InvalidDataException("Original camera tangent left the verified quadrant1 reduction domain.");
        double first=fused?Math.FusedMultiplyAdd(-count,c.HalfPiHigh,x):Subtract(x,Multiply(count,c.HalfPiHigh));
        double reduced=Subtract(first,Multiply(count,c.HalfPiLow));
        double tangent=Kernel(reduced,c,fused);
        return Convert(Divide(c.NegativeOne,tangent));
    }

    /// <summary>Apply the immutable tangent rule set with the requested rounding path.</summary>
    public static float Tangent1020A90(float halfFovRadians,LoadedNativeCameraTangentSource source,
        NativeCameraTangentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Tangent1020A90(halfFovRadians,dispatch);
    }

    private static double Kernel(double x,NativeCameraTangentConstantValues c,bool fused)
    {
        double square=Multiply(x,x);
        double numerator=fused?Math.FusedMultiplyAdd(c.Numerator2,square,c.Numerator0):Add(Multiply(c.Numerator2,square),c.Numerator0);
        double denominator=fused?Math.FusedMultiplyAdd(c.Denominator2,square,c.Denominator1):Add(Multiply(c.Denominator2,square),c.Denominator1);
        denominator=fused?Math.FusedMultiplyAdd(denominator,square,c.Denominator0):Add(Multiply(denominator,square),c.Denominator0);
        double ratio=Divide(numerator,denominator);
        double cube=Multiply(square,x);
        return fused?Math.FusedMultiplyAdd(cube,ratio,x):Add(x,Multiply(cube,ratio));
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static double Multiply(double a,double b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static double Add(double a,double b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static double Subtract(double a,double b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static double Divide(double a,double b)=>a/b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Convert(double value)=>(float)value;
}
