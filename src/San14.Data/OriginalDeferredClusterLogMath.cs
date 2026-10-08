using System.Runtime.CompilerServices;

namespace San14.Data;

public enum OriginalDeferredClusterFloatLogPath { Sse, AvxFma }

/// <summary>The original float-log positive normal paths at TC1032EA0.
/// Original CPU dispatch must be supplied explicitly. Subnormal, nonpositive
/// and nonfinite inputs are outside this finite client API's accepted domain.
/// No MathF.Log or native game execution is used.</summary>
public static partial class OriginalDeferredClusterLogMath
{
    public static OriginalDeferredClusterSourceLogarithms Prepare(float sourceStart,float sourceEnd,
        OriginalDeferredClusterFloatLogPath selectedOriginalPath) => new(
            Evaluate(2,selectedOriginalPath),Evaluate(sourceStart,selectedOriginalPath),Evaluate(sourceEnd,selectedOriginalPath));

    public static float Evaluate(float value,OriginalDeferredClusterFloatLogPath selectedOriginalPath)
    {
        if(selectedOriginalPath is not (OriginalDeferredClusterFloatLogPath.Sse or OriginalDeferredClusterFloatLogPath.AvxFma))
            throw new InvalidDataException("Original float-log CPU dispatch must be an explicit supported source path.");
        uint bits=BitConverter.SingleToUInt32Bits(value);
        if(bits<0x00800000 || bits>=0x7F800000)
            throw new InvalidDataException("Original float log requires a finite positive normal binary32 input.");
        bool fused=selectedOriginalPath==OriginalDeferredClusterFloatLogPath.AvxFma;
        float delta=Subtract(value,1);
        float absolute=BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(delta)&0x7FFFFFFF);
        if(absolute<.0625f)
        {
            float denominator=Add(2,delta);
            float quotient=Divide(delta,denominator);
            float correction=Multiply(delta,quotient);
            float doubled=Add(quotient,quotient);
            float square=Multiply(doubled,doubled);
            float cube=Multiply(doubled,square);
            float polynomial=fused ? Fused(square,FromBits(0x3C4CCCCD),FromBits(0x3DAAAAAB)) :
                Add(Multiply(square,FromBits(0x3C4CCCCD)),FromBits(0x3DAAAAAB));
            polynomial=fused ? Fused(polynomial,cube,Negate(correction)) : Subtract(Multiply(polynomial,cube),correction);
            return Add(delta,polynomial);
        }
        float exponent=(int)(bits>>23)-127;
        uint rounded=(bits&0x007F0000)+((bits&0x00008000)<<1);
        int index=(int)(rounded>>16);
        float mantissa=FromBits((bits&0x007FFFFF)|0x3F000000);
        float anchor=FromBits(rounded|0x3F000000);
        float residual=Multiply(Subtract(anchor,mantissa),Inverse[index]);
        float factor=fused ? Fused(FromBits(0x3EAAAAAB),residual,.5f) : Add(Multiply(residual,FromBits(0x3EAAAAAB)),.5f);
        float residualSquare=Multiply(residual,residual);
        residual=fused ? Fused(factor,residualSquare,residual) : Add(residual,Multiply(factor,residualSquare));
        float tail=fused ? Fused(FromBits(0x3805FDF4),exponent,Negate(residual)) : Subtract(Multiply(FromBits(0x3805FDF4),exponent),residual);
        float head=fused ? Fused(FromBits(0x3F317000),exponent,Head[index]) : Add(Multiply(FromBits(0x3F317000),exponent),Head[index]);
        tail=Add(tail,Tail[index]);
        return Add(head,tail);
    }
    private static float FromBits(uint bits)=>BitConverter.UInt32BitsToSingle(bits);
    private static float Negate(float value)=>FromBits(BitConverter.SingleToUInt32Bits(value)^0x80000000);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Multiply(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Divide(float a,float b)=>a/b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Add(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Subtract(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Fused(float a,float b,float c)=>MathF.FusedMultiplyAdd(a,b,c);
}
