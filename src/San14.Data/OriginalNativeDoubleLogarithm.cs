using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Binary64 ln for EA2's actual float-to-double input.
/// Float subnormals widen to normal doubles, so the native double-denormal
/// branch is unreachable here. Ordered SSE rounding and fused AVX operations
/// remain distinct. This is not a promise of ideal correctly rounded ln.</summary>
public static class OriginalNativeDoubleLogarithm
{
    public static double LogPositiveFloat(float value,LoadedOriginalNativeDoubleLogSource source,
        OriginalNativeLogDispatch selectedOriginalPath)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(!float.IsFinite(value)||value<=0||selectedOriginalPath is not(OriginalNativeLogDispatch.Sse or OriginalNativeLogDispatch.AvxFma))
            throw new InvalidDataException("EA2 double log requires its actual finite positive float and explicit native dispatch.");
        bool fused=selectedOriginalPath==OriginalNativeLogDispatch.AvxFma;
        double input=value;ulong bits=BitConverter.DoubleToUInt64Bits(input);
        double delta=Subtract(input,1);
        double absolute=BitConverter.UInt64BitsToDouble(BitConverter.DoubleToUInt64Bits(delta)&0x7FFFFFFFFFFFFFFFUL);
        if(absolute<1d/16)
        {
            double quotient=Divide(delta,Add(2,delta));
            double correction=Multiply(delta,quotient),doubled=Add(quotient,quotient);
            double square=Multiply(doubled,doubled);
            const double nearA0=0.083333333333331788,nearA1=0.012500000003771751;
            const double nearB0=0.0022321399879194482,nearB1=0.00043488777770761457;
            double a=fused?Fused(nearA1,square,nearA0):
                Add(Multiply(nearA1,square),nearA0);
            double b=fused?Fused(nearB1,square,nearB0):
                Add(Multiply(nearB1,square),nearB0);
            double cube=Multiply(square,doubled),first=Multiply(a,cube);
            double seventh=Multiply(Multiply(cube,cube),doubled);
            double polynomial=fused?Fused(b,seventh,first):Add(first,Multiply(b,seventh));
            return Add(delta,Subtract(polynomial,correction));
        }
        const ulong mantissaMask=0x000FFFFFFFFFFFFFUL;
        const ulong anchorMask=0x000FF00000000000UL,roundBit=0x0000080000000000UL;
        const ulong halfExponent=1022UL<<52;
        double exponent=(int)(bits>>52)-1023;
        ulong rounded=(bits&anchorMask)+((bits&roundBit)<<1);
        int index=checked((int)(rounded>>44));
        double anchor=BitConverter.UInt64BitsToDouble(rounded|halfExponent);
        double mantissa=BitConverter.UInt64BitsToDouble((bits&mantissaMask)|halfExponent);
        double residual=Multiply(Subtract(anchor,mantissa),Divide(512,256+index));
        double aPoly=fused?Fused(1d/6,residual,1d/5):
            Add(Multiply(1d/6,residual),1d/5);
        double bPoly=fused?Fused(1d/3,residual,1d/2):
            Add(Multiply(1d/3,residual),1d/2);
        aPoly=fused?Fused(aPoly,residual,1d/4):
            Add(Multiply(aPoly,residual),1d/4);
        double residualSquare=Multiply(residual,residual),fourth=Multiply(residualSquare,residualSquare);
        double correctionPoly=fused?Fused(bPoly,residualSquare,residual):Add(Multiply(bPoly,residualSquare),residual);
        correctionPoly=fused?Fused(aPoly,fourth,correctionPoly):Add(correctionPoly,Multiply(aPoly,fourth));
        double tail=fused?Fused(OriginalNativeDoubleLogTables.Low(256),exponent,Negate(correctionPoly)):
            Subtract(Multiply(OriginalNativeDoubleLogTables.Low(256),exponent),correctionPoly);
        tail=Add(OriginalNativeDoubleLogTables.Low(index),tail);
        double head=fused?Fused(exponent,OriginalNativeDoubleLogTables.High(256),OriginalNativeDoubleLogTables.High(index)):
            Add(OriginalNativeDoubleLogTables.High(index),Multiply(OriginalNativeDoubleLogTables.High(256),exponent));
        return Add(head,tail);
    }
    private static double Negate(double value)=>BitConverter.UInt64BitsToDouble(BitConverter.DoubleToUInt64Bits(value)^0x8000000000000000UL);
    [MethodImpl(MethodImplOptions.NoInlining)]private static double Add(double a,double b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double Subtract(double a,double b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double Multiply(double a,double b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double Divide(double a,double b)=>a/b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double Fused(double a,double b,double c)=>Math.FusedMultiplyAdd(a,b,c);
}
