using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace San14.Data;

public sealed class LoadedOriginalNativeSkyAtanSource
{
    internal LoadedOriginalNativeSkyAtanSource(string locale,string exe,string text,int rva,double[] values)
    {Locale=locale;ExecutableSha256=exe;DecodedTextSha256=text;NativeAtanRva=rva;Values=values;}
    public string Locale{get;}
    public string ExecutableSha256{get;}
    public string DecodedTextSha256{get;}
    public int NativeAtanRva{get;}
    internal double[] Values{get;}
    public string SourceProvenance=>$"san14://file/SAN14CE_{Locale}.exe@{ExecutableSha256}#finite-sky-atan-{NativeAtanRva:X8};nearest-even-SSE-binary64-kernel";
}

public static class OriginalNativeSkyAtanSource
{
    private static readonly (int Rva,bool LibmTable,ulong Bits)[] Constants=[
        (0x1727FD8,true,0x3FDDAC670561BB4FUL),(0x1727E10,true,0x3FE921FB54442D18UL),
        (0x1727FE8,true,0x3FEF730BD281F69BUL),(0x1724220,true,0x3FF921FB54442D18UL),
        (0x1727FB8,true,0x3F73476A758DA22AUL),(0x1727FC0,true,0x3FC89E173A81EE7FUL),
        (0x1727FC8,true,0x3FD2FA5316907834UL),(0x1727FD0,true,0x3FD327E3DF2CF2AAUL),
        (0x1727FF0,true,0x3FF1C58793DA6EA4UL),(0x1727FE0,true,0x3FEC777CA21053F0UL)];
    public static LoadedOriginalNativeSkyAtanSource Load(string sourceDirectory,string locale)
    {
        (string expected,string text,int rva)=locale switch{
            "SC"=>("CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88","D7F3831C369A76148D87370D91EF0A8A43487D5E13A383886C68A6D50591E8F6",0x1032E94),
            "TC"=>("0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464","FFBD504FC3DEA6A218EF4E8530A65761296C17191F7D675AB3C49DF64D950766",0x1032034),
            _=>throw new InvalidDataException("Finite source Sky atan requires its exact SC/TC image.")};
        byte[] exe=File.ReadAllBytes(Path.Combine(sourceDirectory,$"SAN14CE_{locale}.exe"));
        if(Convert.ToHexString(SHA256.HashData(exe))!=expected)throw new InvalidDataException("Sky atan source differs from its inspected executable revision.");
        using var pe=new PEReader(new MemoryStream(exe,false));
        if(pe.PEHeaders.CoffHeader.Machine!=Machine.Amd64||pe.PEHeaders.PEHeader is not{Magic:PEMagic.PE32Plus,ImageBase:0x140000000UL})
            throw new InvalidDataException("Sky atan requires the original AMD64 PE32+ source.");
        var values=new double[Constants.Length];
        for(int i=0;i<Constants.Length;i++)
        {
            var c=Constants[i];int atRva=c.Rva+(locale=="SC"?(c.LibmTable?0xC60:0x1000):0);
            var sections=pe.PEHeaders.SectionHeaders.Where(s=>s.Name==".rdata"&&atRva>=s.VirtualAddress&&
                (long)atRva+8<=(long)s.VirtualAddress+s.SizeOfRawData).ToArray();
            if(sections.Length!=1)throw new InvalidDataException("Sky atan coefficient is outside its exact raw rdata.");
            int at=checked(sections[0].PointerToRawData+atRva-sections[0].VirtualAddress);
            ulong bits=BinaryPrimitives.ReadUInt64LittleEndian(exe.AsSpan(at,8));
            if(bits!=c.Bits)throw new InvalidDataException("Sky atan coefficient changed from its pinned instruction operand.");
            values[i]=BitConverter.UInt64BitsToDouble(bits);
        }
        return new(locale,expected,text,rva,values);
    }
}

/// <summary>TC1032034/SC1032E94 finite source SSE atan. Input/result are
/// binary32, branch reduction and rational kernel are binary64. The Sky
/// transmittance caller's bounded abs≤8 domain never reaches exception/libm
/// status calls. No framework atan or alternate range reduction is used.</summary>
public static class OriginalNativeSkyAtan
{
    public static float Atan1032034(float input,LoadedOriginalNativeSkyAtanSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(!float.IsFinite(input)||MathF.Abs(input)>8f)
            throw new InvalidDataException("Original finite Sky atan requires abs(input)≤8.");
        bool negative=(BitConverter.SingleToUInt32Bits(input)&0x80000000)!=0;
        double x=negative?-(double)input:input,offset;var c=source.Values;
        if(x<.4375)offset=0;
        else if(x<.6875){offset=c[0];x=D(S(M(x,2),1),A(x,2));}
        else if(x<1.1875){offset=c[1];x=D(S(x,1),A(x,1));}
        else if(x<2.4375){offset=c[2];x=D(S(x,1.5),A(M(x,1.5),1));}
        else{offset=c[3];x=D(-1,x);}
        double square=M(x,x),cube=M(square,x);
        // Preserve 10321C6..1032209 grouping: add middle before second
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
