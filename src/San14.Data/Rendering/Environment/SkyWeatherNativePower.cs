using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Project-owned scalar power rules and mathematical anchor tables.</summary>
public sealed class SkyWeatherNativePowerSource
{
    private static readonly SkyWeatherNativePowerSource simplified = new("SC");
    private static readonly SkyWeatherNativePowerSource traditional = new("TC");
    private SkyWeatherNativePowerSource(string locale) => Locale = locale;
    public string Locale { get; }
    public string RuleVersion => "binary64-sse-power-256-log-64-exp-v1";
    public string SourceProvenance => "san14-remake:" + RuleVersion + ";finite-nonnegative-binary32-input";
    internal double Reciprocal(int index) => 512d / (256 + index);
    internal double Logarithm(int index) => SkyWeatherPowerTables.Logarithm(index);
    internal double Exponential(int index) => SkyWeatherPowerTables.Exponential(index);
    internal double Constant(int index) => SkyWeatherNativePower.Constant(index);

    public static SkyWeatherNativePowerSource Create(string locale) => locale switch
    {
        "SC" => simplified,
        "TC" => traditional,
        _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Power parameter language must be SC or TC.")
    };
    public static SkyWeatherNativePowerSource Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return Create(locale);
    }
}

/// <summary>Scalar SSE power with independently rounded binary64 operations.
/// AVX/FMA, NaN, negative bases and overflow/underflow reporting are separate branches.</summary>
public static class SkyWeatherNativePower
{
    private const double Third = 1d / 3, Sixth = 1d / 6;
    private const double Ln2 = 0.6931471805599453d;
    private const double UpperLogLimit = 88.72283935546875d, LowerLogLimit = -103.2789306640625d;
    private const double Ln2Over64 = 0.010830424696249145d, SixtyFourOverLn2 = 92.33248261689366d;
    private const double NearCubicA = 0.012500000003771751d, NearCubicB = 0.00043488777770761457d;
    private const double NearLinearA = 0.083333333333331788d, NearLinearB = 0.0022321399879194482d;

    // The Native power input packet uses this coefficient order.
    internal static double Constant(int index) => index switch
    {
        0 => Third,
        1 => Ln2,
        2 => UpperLogLimit,
        3 => LowerLogLimit,
        4 => Ln2Over64,
        5 => Sixth,
        6 => SixtyFourOverLn2,
        7 => NearCubicA,
        8 => NearCubicB,
        9 => NearLinearA,
        10 => NearLinearB,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    public static float PowSse(float value,float exponent,SkyWeatherNativePowerSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(!float.IsFinite(value)||!float.IsFinite(exponent)||
            (BitConverter.SingleToUInt32Bits(value)&0x80000000)!=0)
            throw new InvalidDataException("Sky weather power supports finite nonnegative current inputs.");
        if(exponent==0)return 1;
        if(exponent==1)return value;
        if(value==0)
        {
            if(exponent>0)return 0;
            throw new NotSupportedException("Native pow zero-base negative exponent reaches its error-reporting branch.");
        }
        double x=value,log;
        double t=S(x,1);
        // The near-one threshold is binary64 1/16.
        if(value<1.0625f&&Math.Abs(t)<.0625)
        {
            double ratio=D(t,A(t,2)),product=M(ratio,t);
            ratio=A(ratio,ratio);
            double square=M(ratio,ratio),cube=M(ratio,square),fifth=M(M(square,square),cube);
            double p=M(square,NearCubicA),q=M(square,NearCubicB);
            p=A(p,NearLinearA);q=A(q,NearLinearB);
            double correction=A(M(cube,p),M(fifth,q));
            correction=S(correction,product);log=A(t,correction);
        }
        else
        {
            ulong bits=BitConverter.DoubleToUInt64Bits(x);
            ulong mantissa=bits&0x000FFFFFFFFFFFFFul;
            int i=checked((int)((mantissa>>44)+((mantissa>>43)&1)));
            double normalized=BitConverter.UInt64BitsToDouble(mantissa|0x3FE0000000000000ul);
            double center=BitConverter.UInt64BitsToDouble(((ulong)i|0x3FE00ul)<<44);
            double r=M(S(center,normalized),source.Reciprocal(i));
            double poly=M(r,Third);poly=A(poly,.5);
            poly=M(M(r,r),poly);poly=A(poly,r);
            int e=(int)(bits>>52)-1023;
            log=S(A(M(e,Ln2),source.Logarithm(i)),poly);
        }
        double y=M(exponent,log);
        if(y>UpperLogLimit||y<=LowerLogLimit)
            throw new NotSupportedException("Sky weather power reaches its separately unported overflow/underflow branch.");
        double scaled=M(y,SixtyFourOverLn2);
        int n=checked((int)Math.Round(scaled,MidpointRounding.ToEven));
        double remainder=S(y,M(n,Ln2Over64));
        double ep=M(remainder,Sixth);ep=A(ep,.5);
        ep=M(M(remainder,remainder),ep);ep=A(ep,remainder);
        double centerExp=source.Exponential(n&63);
        double result=A(M(ep,centerExp),centerExp);
        ulong resultBits=unchecked(BitConverter.DoubleToUInt64Bits(result)+((ulong)(long)(n>>6)<<52));
        float final=(float)BitConverter.UInt64BitsToDouble(resultBits);
        if(!float.IsFinite(final))throw new InvalidDataException("Original weather power result is nonfinite.");
        return final;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static double A(double a,double b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double S(double a,double b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double M(double a,double b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double D(double a,double b)=>a/b;
}
