using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed class LoadedNativeSkyAtanSource
{
    internal LoadedNativeSkyAtanSource(string locale) => Locale = locale;
    public string Locale { get; }
    public string RuleVersion => "finite-sky-atan-rational-v1";
    // atan(1/2), pi/4, atan(3/2), pi/2, then rational approximation coefficients.
    internal ReadOnlySpan<double> Values =>
    [
        0.46364760900080609d, 0.78539816339744828d,
        0.98279372324732905d, 1.5707963267948966d,
        0.004706779342861492d, 0.19232454640210858d,
        0.29652859881923921d, 0.29930969995965972d,
        1.1107249999539954d, 0.88958579686243233d
    ];
    public string SourceProvenance => "san14-remake:" + RuleVersion + ";nearest-even-SSE-binary64-kernel";
}

public static class NativeSkyAtanSource
{
    private static readonly LoadedNativeSkyAtanSource simplified = new("SC");
    private static readonly LoadedNativeSkyAtanSource traditional = new("TC");
    public static LoadedNativeSkyAtanSource Create(string locale) => locale switch
    {
        "SC" => simplified,
        "TC" => traditional,
        _ => throw new InvalidDataException("Finite Sky atan parameter language must be SC or TC.")
    };
    public static LoadedNativeSkyAtanSource Load(string sourceDirectory, string locale)
        => Create(locale);
}

/// <summary>Finite SSE atan. Input/result are binary32; branch reduction and
/// rational kernel are binary64. The transmittance domain is abs(input) &lt;= 8.</summary>
public static class NativeSkyAtan
{
    public static float Atan1032034(float input,LoadedNativeSkyAtanSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(!float.IsFinite(input)||MathF.Abs(input)>8f)
            throw new InvalidDataException("Finite Sky atan requires abs(input) <= 8.");
        bool negative=(BitConverter.SingleToUInt32Bits(input)&0x80000000)!=0;
        double x=negative?-(double)input:input,offset;var c=source.Values;
        if(x<.4375)offset=0;
        else if(x<.6875){offset=c[0];x=D(S(M(x,2),1),A(x,2));}
        else if(x<1.1875){offset=c[1];x=D(S(x,1),A(x,1));}
        else if(x<2.4375){offset=c[2];x=D(S(x,1.5),A(M(x,1.5),1));}
        else{offset=c[3];x=D(-1,x);}
        double square=M(x,x),cube=M(square,x);
        // Preserve rounding order: add middle before second
        // multiply, then add low coefficient. No algebraic regrouping/FMA.
        double numerator=A(M(A(M(square,c[4]),c[5]),square),c[6]);
        double denominator=A(M(A(M(square,c[7]),c[8]),square),c[9]);
        double correction=S(D(M(numerator,cube),denominator),x);
        double result=S(offset,correction);
        if(negative)result=-result;
        return (float)result;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static double A(double a,double b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double S(double a,double b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double M(double a,double b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double D(double a,double b)=>a/b;
}
