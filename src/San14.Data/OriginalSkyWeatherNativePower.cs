using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>The original positive finite SSE pow kernel's literal and table
/// storage. The original executable is read as data; no native code executes.</summary>
public sealed class OriginalSkyWeatherNativePowerSource
{
    private readonly double[] reciprocal, logarithms, exponentials, constants;
    internal OriginalSkyWeatherNativePowerSource(string locale,string provenance,
        double[] reciprocal,double[] logarithms,double[] exponentials,double[] constants)
    {Locale=locale;SourceProvenance=provenance;this.reciprocal=reciprocal;
        this.logarithms=logarithms;this.exponentials=exponentials;this.constants=constants;}
    public string Locale {get;}
    public string SourceProvenance {get;}
    internal double Reciprocal(int i)=>reciprocal[i];
    internal double Logarithm(int i)=>logarithms[i];
    internal double Exponential(int i)=>exponentials[i];
    internal double Constant(int i)=>constants[i];

    public static OriginalSkyWeatherNativePowerSource Load(OriginalGameRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        (string expected,int shift)=locale switch{
            "TC"=>("0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464",0),
            "SC"=>("CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88",0xC60),
            _=>throw new ArgumentOutOfRangeException(nameof(locale))};
        string path=Path.Combine(repository.SourceDirectory,$"SAN14CE_{locale}.exe");
        byte[] exe=File.ReadAllBytes(path);
        if(Convert.ToHexString(SHA256.HashData(exe))!=expected)
            throw new InvalidDataException("Sky weather native power requires its pinned original executable revision.");
        using var pe=new PEReader(new MemoryStream(exe,false));
        if(pe.PEHeaders.PEHeader?.Magic!=PEMagic.PE32Plus||
            pe.PEHeaders.CoffHeader.Machine!=Machine.Amd64)
            throw new InvalidDataException("Sky weather power requires original AMD64 PE32+ data.");
        var proofs=new List<string>();
        double[] Read(int rva,int count,string role)
        {
            int length=checked(count*8);rva+=shift;
            var section=pe.PEHeaders.SectionHeaders.Single(s=>s.Name==".rdata"&&
                rva>=s.VirtualAddress&&(long)rva+length<=(long)s.VirtualAddress+s.SizeOfRawData);
            var bytes=exe.AsSpan(section.PointerToRawData+rva-section.VirtualAddress,length);
            var result=new double[count];
            for(int i=0;i<count;i++)result[i]=BinaryPrimitives.ReadDoubleLittleEndian(bytes.Slice(i*8,8));
            if(result.Any(x=>!double.IsFinite(x)))throw new InvalidDataException("Native power literal/table contains nonfinite data.");
            proofs.Add($"{role}={rva:X}/{length}@{Convert.ToHexString(SHA256.HashData(bytes))}");
            return result;
        }
        var reciprocal=Read(0x1729A30,257,"reciprocal");
        var logarithms=Read(0x1724330,257,"logarithm");
        var exponentials=Read(0x1729830,64,"exponential");
        var constants=new[]{Read(0x1724264,1,"third")[0],Read(0x172426C,1,"ln2")[0],
            Read(0x1724274,1,"upper")[0],Read(0x172427C,1,"lower")[0],
            Read(0x1724284,1,"ln2-over64")[0],Read(0x172428C,1,"sixth")[0],
            Read(0x17242F0,1,"64-over-ln2")[0],Read(0x17242C0,2,"near-cubic")[0],
            Read(0x17242C0,2,"near-cubic")[1],Read(0x17242D0,2,"near-linear")[0],
            Read(0x17242D0,2,"near-linear")[1]};
        return new(locale,$"san14://file/SAN14CE_{locale}.exe@{expected};"+
            "original-SSE-pow-positive-finite;"+string.Join(";",proofs),
            reciprocal,logarithms,exponentials,constants);
    }
}

/// <summary>Scalar port of actual SSE pow. Each original binary64 arithmetic
/// operation rounds independently. AVX/FMA, NaN, negative bases and native
/// overflow/underflow reporting require their own source branches.</summary>
public static class OriginalSkyWeatherNativePower
{
    public static float PowSse(float value,float exponent,OriginalSkyWeatherNativePowerSource source)
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
        // 101EE15: the near-one threshold is double binary64 .0625.
        if(value<1.0625f&&Math.Abs(t)<.0625)
        {
            double ratio=D(t,A(t,2)),product=M(ratio,t);
            ratio=A(ratio,ratio);
            double square=M(ratio,ratio),cube=M(ratio,square),fifth=M(M(square,square),cube);
            double p=M(square,source.Constant(7)),q=M(square,source.Constant(8));
            p=A(p,source.Constant(9));q=A(q,source.Constant(10));
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
            double poly=M(r,source.Constant(0));poly=A(poly,.5);
            poly=M(M(r,r),poly);poly=A(poly,r);
            int e=(int)(bits>>52)-1023;
            log=S(A(M(e,source.Constant(1)),source.Logarithm(i)),poly);
        }
        double y=M(exponent,log);
        if(y>source.Constant(2)||y<=source.Constant(3))
            throw new NotSupportedException("Sky weather power reaches its separately unported overflow/underflow branch.");
        double scaled=M(y,source.Constant(6));
        int n=checked((int)Math.Round(scaled,MidpointRounding.ToEven));
        double remainder=S(y,M(n,source.Constant(4)));
        double ep=M(remainder,source.Constant(5));ep=A(ep,.5);
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
