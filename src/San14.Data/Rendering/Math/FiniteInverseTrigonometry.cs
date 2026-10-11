using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

/// <summary>Finite float ports retain each SSE operation and the native split
/// constants. No MathF.Acos/Asin substitution or unverified exception handling.</summary>
public static class FiniteInverseTrigonometry
{
    private static float F(uint bits)=>BitConverter.UInt32BitsToSingle(bits);
    private static double D(ulong bits)=>BitConverter.UInt64BitsToDouble(bits);
    public static float Acos(float input)
    {
        Domain(input);
        uint bits=BitConverter.SingleToUInt32Bits(input);bool negative=(bits&0x80000000)!=0;
        float abs=F(bits&0x7FFFFFFF);
        if(abs==1)return negative?F(0x40490FDB):0;
        if(abs<F(0x32800000))return F(0x3FC90FDB); // native exponent < -26
        bool large=abs>=.5f;float square=large?M(S(1,abs),.5f):M(abs,abs);
        float root=large?Sqrt(square):0,argument=large?root:abs;
        float ratio=Rational(square);
        double low=D(0x3C91A62633145C07),halfPi=D(0x3FF921FB54442D18);
        if(large)
        {
            if(negative)
                return (float)DS(D(0x400921FB54442D18),DM(DA(DS((double)M(ratio,argument),low),root),2));
            float truncated=F(BitConverter.SingleToUInt32Bits(root)&0xFFFF0000);
            float correction=V(S(square,M(truncated,truncated)),A(truncated,root));
            float tail=A(M(correction,2),M(M(argument,2),ratio));
            return A(tail,M(truncated,2));
        }
        // Small branch uses the original signed input in both products.
        return (float)DS(halfPi,DS((double)input,DS(low,(double)M(ratio,input))));
    }
    public static float Asin(float input)
    {
        Domain(input);
        uint bits=BitConverter.SingleToUInt32Bits(input);bool negative=(bits&0x80000000)!=0;
        float abs=F(bits&0x7FFFFFFF);
        if(abs==1)return negative?F(0xBFC90FDB):F(0x3FC90FDB);
        if(abs<F(0x38800000))return input; // exponent < -14
        bool large=abs>=.5f;float square=large?M(S(1,abs),.5f):M(abs,abs);
        float root=large?Sqrt(square):0,argument=large?root:abs,ratio=Rational(square),result;
        if(large)
        {
            float truncated=F(BitConverter.SingleToUInt32Bits(root)&0xFFFF0000);
            float correction=V(S(square,M(truncated,truncated)),A(truncated,root));
            float scaled=M(M(root,2),ratio);
            float tail=S(scaled,S(F(0x33A22168),M(correction,2)));
            tail=S(tail,S(F(0x3F490FDA),M(truncated,2)));
            result=S(F(0x3F490FDA),tail);
        }
        else result=A(M(ratio,argument),argument);
        return negative?F(BitConverter.SingleToUInt32Bits(result)^0x80000000):result;
    }
    private static float Rational(float square)
    {
        float numerator=S(F(0xBC5B3FE1),M(square,F(0x3B81CE6B)));
        numerator=S(M(numerator,square),F(0x3D678BDD));
        numerator=A(M(numerator,square),F(0x3E3C94DC));
        numerator=M(numerator,square);
        return V(numerator,S(F(0x3F8D6FA5),M(square,F(0x3F561F0D))));
    }
    internal static float Sqrt(float input)
    {
        if(!Sse.IsSupported||!float.IsFinite(input)||input<0)
            throw new InvalidDataException("Inverse trigonometry requires finite SSE square-root inputs.");
        return Sse.SqrtScalar(Vector128.CreateScalar(input)).GetElement(0);
    }
    private static void Domain(float input)
    {if(!float.IsFinite(input)||MathF.Abs(input)>1)throw new NotSupportedException("Inverse trigonometry supports finite inputs in [-1,1].");}
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float V(float a,float b)=>a/b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double DA(double a,double b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double DS(double a,double b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double DM(double a,double b)=>a*b;
}
