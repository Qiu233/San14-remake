using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace San14.Data;

public sealed record OriginalNativeLogTableSource(string ResourceUri, int Rva, string Sha256);

/// <summary>The three 129-entry tables used by the original scalar logarithm,
/// read on demand from a verified local executable. No original code is loaded.</summary>
public sealed class OriginalNativeLogTables
{
    public IReadOnlyList<uint> InverseCenter { get; }
    public IReadOnlyList<uint> LogHigh { get; }
    public IReadOnlyList<uint> LogLow { get; }
    public IReadOnlyList<OriginalNativeLogTableSource> Sources { get; }
    public string ExecutableSha256 { get; }
    public string PhysicalPath { get; }

    private OriginalNativeLogTables(uint[] inverse, uint[] high, uint[] low,
        OriginalNativeLogTableSource[] sources, string executableSha, string path)
        => (InverseCenter,LogHigh,LogLow,Sources,ExecutableSha256,PhysicalPath) =
            (Array.AsReadOnly(inverse),Array.AsReadOnly(high),Array.AsReadOnly(low),Array.AsReadOnly(sources),executableSha,path);

    public static OriginalNativeLogTables Load(string sourceDirectory,string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        (string expectedSha,int inverse,int high,int low) = locale switch {
            "SC"=>("CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88",0x172EB60,0x172E740,0x172E950),
            "TC"=>("0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464",0x172DF00,0x172DAE0,0x172DCF0),
            _=>throw new ArgumentOutOfRangeException(nameof(locale),locale,"Native log supports verified SC/TC revisions only.") };
        string path=Path.GetFullPath(Path.Combine(sourceDirectory,$"SAN14CE_{locale}.exe"));
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,128*1024,FileOptions.SequentialScan);
        if(stream.Length is <4096 or >128*1024*1024)throw new InvalidDataException("Native log executable exceeds the supported size bound.");
        byte[] executable=new byte[(int)stream.Length];stream.ReadExactly(executable);
        string sha=Convert.ToHexString(SHA256.HashData(executable));
        if(sha!=expectedSha)throw new InvalidDataException("Native log executable differs from the verified source revision.");
        using var pe=new PEReader(new MemoryStream(executable,writable:false));
        var optional=pe.PEHeaders.PEHeader;
        if(optional is null||optional.Magic!=PEMagic.PE32Plus||optional.ImageBase!=0x140000000UL||pe.PEHeaders.CoffHeader.Machine!=Machine.Amd64)
            throw new InvalidDataException("Native log expects the verified AMD64 PE32+ source.");
        var sources=new List<OriginalNativeLogTableSource>();
        uint[] ReadTable(int rva,string expected,string role)
        {
            const int count=129*4;
            var sections=pe.PEHeaders.SectionHeaders.Where(s=>rva>=s.VirtualAddress &&
                (long)rva+count<=(long)s.VirtualAddress+s.SizeOfRawData).ToArray();
            if(sections.Length!=1||sections[0].Name!=".rdata")throw new InvalidDataException("Native log table is not one raw .rdata range.");
            long offset=(long)sections[0].PointerToRawData+rva-sections[0].VirtualAddress;
            if(offset<0||offset>executable.Length-count)throw new InvalidDataException("Native log table exceeds executable bytes.");
            ReadOnlySpan<byte> bytes=executable.AsSpan((int)offset,count);
            string hash=Convert.ToHexString(SHA256.HashData(bytes));
            if(hash!=expected)throw new InvalidDataException("Native log table differs from its original source bytes.");
            var values=new uint[129];
            for(int i=0;i<values.Length;i++)values[i]=BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(i*4,4));
            sources.Add(new(GameResourceId.File($"SAN14CE_{locale}.exe")+$"#rva-{rva:X}/native-log/{role}",rva,hash));
            return values;
        }
        uint[] inv=ReadTable(inverse,"8C1E082036B2ADC3A3BBEC223FBEB32E62C311D5E5C2B935F9692B5640AD1794","inverse-center");
        uint[] hi=ReadTable(high,"4C76320E09085B9EB463C242E121DA58E26A1F4FB30178D17EF20C9CE5441582","high");
        uint[] lo=ReadTable(low,"F3749403D5D1F6F9C1ACA320AEF40A3EEA25D128636A25FCE91D330FF1B95211","low");
        return new(inv,hi,lo,sources.ToArray(),sha,path);
    }
}
