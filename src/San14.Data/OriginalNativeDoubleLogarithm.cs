using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Finite TC1035520 ln port for EA2's actual float-to-double input.
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
        double C(int rva)=>source.Coefficient(rva);
        double delta=Subtract(input,C(0x1728980));
        double absolute=BitConverter.UInt64BitsToDouble(BitConverter.DoubleToUInt64Bits(delta)&source.Word(0x1728A60));
        if(absolute<C(0x1728A50))
        {
            double quotient=Divide(delta,Add(C(0x1728970),delta));
            double correction=Multiply(delta,quotient),doubled=Add(quotient,quotient);
            double square=Multiply(doubled,doubled);
            double a=fused?Fused(C(0x1728A80),square,C(0x1728A70)):
                Add(Multiply(C(0x1728A80),square),C(0x1728A70));
            double b=fused?Fused(C(0x1728AA0),square,C(0x1728A90)):
                Add(Multiply(C(0x1728AA0),square),C(0x1728A90));
            double cube=Multiply(square,doubled),first=Multiply(a,cube);
            double seventh=Multiply(Multiply(cube,cube),doubled);
            double polynomial=fused?Fused(b,seventh,first):Add(first,Multiply(b,seventh));
            return Add(delta,Subtract(polynomial,correction));
        }
        double exponent=(int)(bits>>52)-unchecked((int)source.Word(0x1728930));
        ulong rounded=(bits&source.Word(0x1728950))+((bits&source.Word(0x1728960))<<1);
        int index=checked((int)(rounded>>44));
        if(index is <0 or >256)throw new InvalidDataException("Native double-log mantissa index escaped its exact257-entry table.");
        double anchor=BitConverter.UInt64BitsToDouble(rounded|source.Word(0x17289B0));
        double mantissa=BitConverter.UInt64BitsToDouble((bits&source.Word(0x1728920))|source.Word(0x17289B0));
        double residual=Multiply(Subtract(anchor,mantissa),source.Inverse(index));
        double aPoly=fused?Fused(C(0x1728A20),residual,C(0x1728A10)):
            Add(Multiply(C(0x1728A20),residual),C(0x1728A10));
        double bPoly=fused?Fused(C(0x17289F0),residual,C(0x17289E0)):
            Add(Multiply(C(0x17289F0),residual),C(0x17289E0));
        aPoly=fused?Fused(aPoly,residual,C(0x1728A00)):
            Add(Multiply(aPoly,residual),C(0x1728A00));
        double residualSquare=Multiply(residual,residual),fourth=Multiply(residualSquare,residualSquare);
        double correctionPoly=fused?Fused(bPoly,residualSquare,residual):Add(Multiply(bPoly,residualSquare),residual);
        correctionPoly=fused?Fused(aPoly,fourth,correctionPoly):Add(correctionPoly,Multiply(aPoly,fourth));
        double tail=fused?Fused(C(0x1728AC0),exponent,Negate(correctionPoly)):
            Subtract(Multiply(C(0x1728AC0),exponent),correctionPoly);
        tail=Add(source.Low(index),tail);
        double head=fused?Fused(exponent,C(0x1728AB0),source.High(index)):
            Add(source.High(index),Multiply(C(0x1728AB0),exponent));
        return Add(head,tail);
    }
    private static double Negate(double value)=>BitConverter.UInt64BitsToDouble(BitConverter.DoubleToUInt64Bits(value)^0x8000000000000000UL);
    [MethodImpl(MethodImplOptions.NoInlining)]private static double Add(double a,double b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double Subtract(double a,double b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double Multiply(double a,double b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double Divide(double a,double b)=>a/b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double Fused(double a,double b,double c)=>Math.FusedMultiplyAdd(a,b,c);
}
