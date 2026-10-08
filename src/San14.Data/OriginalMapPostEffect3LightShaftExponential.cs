using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Read-only literal tables for the actual1039540 SSE exponential.
/// Its AVX dispatch and overflow/underflow handlers are separate source paths.
/// The admitted Gaussian exponent range is binary32 [-112.5,0].</summary>
public sealed class OriginalMapPostEffect3LightShaftExponential
{
    private readonly double[] constants,scale,head,tail;
    private OriginalMapPostEffect3LightShaftExponential(string locale,string evidence,
        double[] constants,double[] scale,double[] head,double[] tail)
    {Locale=locale;SourceEvidence=evidence;this.constants=constants;this.scale=scale;this.head=head;this.tail=tail;}
    public string Locale{get;}
    public string SourceEvidence{get;}
    public static OriginalMapPostEffect3LightShaftExponential LoadSseSource(OriginalGameRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        (string sha,int delta)=locale switch
        {
            "TC"=>("0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464",0),
            "SC"=>("CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88",0xC60),
            _=>throw new ArgumentOutOfRangeException(nameof(locale))
        };
        byte[] exe=File.ReadAllBytes(Path.Combine(repository.SourceDirectory,$"SAN14CE_{locale}.exe"));
        if(Convert.ToHexString(SHA256.HashData(exe))!=sha)
            throw new InvalidDataException("LightShaft exponential needs its pinned native executable source.");
        using var pe=new PEReader(new MemoryStream(exe,false));var proofs=new List<string>();
        double[] Read(int originalRva,int count)
        {
            int rva=originalRva+delta,n=checked(count*8);
            var s=pe.PEHeaders.SectionHeaders.Single(s=>s.Name==".rdata"&&rva>=s.VirtualAddress&&
                (long)rva+n<=(long)s.VirtualAddress+s.SizeOfRawData);
            var bytes=exe.AsSpan(s.PointerToRawData+rva-s.VirtualAddress,n);double[] values=new double[count];
            for(int i=0;i<count;i++)values[i]=BinaryPrimitives.ReadDoubleLittleEndian(bytes.Slice(i*8,8));
            if(values.Any(v=>!double.IsFinite(v)))throw new InvalidDataException("Native exp literal is nonfinite.");
            proofs.Add($"{rva:X}/{n}@{Convert.ToHexString(SHA256.HashData(bytes))}");return values;
        }
        int[] sites=[0x17297A0,0x1729758,0x1729780,0x1729788,0x17296D0,0x17296E0,0x1729710,0x17296F0,0x1729700,0x1729798];
        var constants=sites.Select(r=>Read(r,1)[0]).ToArray();
        var scale=Read(0x1729830,64);var head=Read(0x172B540,64);var tail=Read(0x172B340,64);
        return new(locale,$"SAN14CE_{locale}.exe@{sha};TC1039540/SC103A3B0-SSE-exp-kernel;"+
            "explicit-SSE-port-not-current-native-dispatch-observation;"+string.Join(";",proofs),constants,scale,head,tail);
    }
    public float EvaluateSse(float binary32Exponent)
    {
        if(!float.IsFinite(binary32Exponent)||binary32Exponent>0||binary32Exponent< -112.5f)
            throw new NotSupportedException("LightShaft exponential is outside the closed finite Gaussian SSE domain.");
        double x=binary32Exponent;
        if(Math.Abs(x)<=constants[0])return (float)A(x,constants[9]);
        int n=(int)M(x,constants[1]); //CVTTPD2DQ truncation, not rounding-to-nearest.
        double a=M(n,constants[2]),b=M(n,constants[3]);
        double r=A(b,A(x,a)),r2=M(r,r),r4=M(r2,r2);
        double p=M(constants[4],r);p=A(p,constants[5]);p=M(p,r);p=A(p,constants[6]);p=M(p,r4);
        double q=M(constants[7],r);q=A(q,constants[8]);q=M(q,r2);q=A(q,r);q=A(q,p);
        int index=n&63;q=M(q,scale[index]);q=A(q,head[index]);q=A(q,tail[index]);
        ulong bits=unchecked(BitConverter.DoubleToUInt64Bits(q)+((ulong)(long)(n>>6)<<52));
        return (float)BitConverter.UInt64BitsToDouble(bits);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static double A(double a,double b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double M(double a,double b)=>a*b;
}
