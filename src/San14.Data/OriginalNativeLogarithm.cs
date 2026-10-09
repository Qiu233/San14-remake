using System.Runtime.CompilerServices;

namespace San14.Data;

public enum OriginalNativeLogDispatch { Sse, AvxFma }

/// <summary>Finite positive binary32 logarithm with separately rounded SSE
/// and fused AVX/FMA operation ordering. Dispatch is an explicit caller choice.</summary>
public static class OriginalNativeLogarithm
{
    public static float Log(float value,OriginalNativeLogTables tables,OriginalNativeLogDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if(!float.IsFinite(value)||value<=0||dispatch is not (OriginalNativeLogDispatch.Sse or OriginalNativeLogDispatch.AvxFma))
            throw new InvalidDataException("Native logarithm requires finite positive input and an explicit supported dispatch.");
        float Add(float a,float b)=>OriginalNativeFloat.Add(a,b);
        float Sub(float a,float b)=>OriginalNativeFloat.Subtract(a,b);
        float Mul(float a,float b)=>OriginalNativeFloat.Multiply(a,b);
        float F(uint bits)=>BitConverter.UInt32BitsToSingle(bits);
        bool fused=dispatch==OriginalNativeLogDispatch.AvxFma;
        uint word=BitConverter.SingleToUInt32Bits(value),mantissa=word&0x7FFFFF;
        int exponent=(int)(word>>23)-127;
        if(MathF.Abs(Sub(value,1))<0.0625f)
        {
            float t=Sub(value,1),r=Divide(t,Add(2,t)),product=Mul(t,r);
            r=Add(r,r);float z=Mul(r,r),cube=Mul(r,z);
            float polynomial=fused?MathF.FusedMultiplyAdd(z,1f/80,1f/12):
                Add(Mul(z,1f/80),1f/12);
            float tail=fused?MathF.FusedMultiplyAdd(polynomial,cube,-product):Sub(Mul(polynomial,cube),product);
            return Add(t,tail);
        }
        if(exponent==-127)
        {
            uint adjusted=BitConverter.SingleToUInt32Bits(Sub(F(mantissa|0x3F800000),1));
            exponent=(int)(adjusted>>23)-253;mantissa=adjusted&0x7FFFFF;word=mantissa;
        }
        uint rounded=(word&0x7F0000)+((word&0x8000)<<1);
        int index=(int)(rounded>>16);
        float ratio=Mul(Sub(F(rounded|0x3F000000),F(mantissa|0x3F000000)),F(tables.InverseCenter[index]));
        float correction,tailPart,head;
        if(fused)
        {
            float coefficient=MathF.FusedMultiplyAdd(1f/3,ratio,0.5f);
            correction=MathF.FusedMultiplyAdd(coefficient,Mul(ratio,ratio),ratio);
            tailPart=MathF.FusedMultiplyAdd(F(tables.LogLow[128]),exponent,-correction);
            head=MathF.FusedMultiplyAdd(F(tables.LogHigh[128]),exponent,F(tables.LogHigh[index]));
        }
        else
        {
            float polynomial=Mul(Add(Mul(ratio,1f/3),0.5f),Mul(ratio,ratio));
            correction=Add(ratio,polynomial);
            tailPart=Sub(Mul(exponent,F(tables.LogLow[128])),correction);
            head=Add(Mul(exponent,F(tables.LogHigh[128])),F(tables.LogHigh[index]));
        }
        tailPart=Add(tailPart,F(tables.LogLow[index]));
        return Add(head,tailPart);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static float Divide(float left,float right)=>left/right;
}
