using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace San14.Data;

public sealed record OriginalNativeDoubleLogRegionSource(string ResourceUri,int Rva,int ByteCount,string Sha256);

/// <summary>Original TC1035520 coefficients and257-entry tables. Reads inert
/// verified PE data only. Dispatch is an explicit caller choice, not a read
/// of the original mutable CPU feature flag27F31CC.</summary>
public sealed class LoadedOriginalNativeDoubleLogSource
{
    private readonly ulong[] coefficients,inverse,high,low;
    private LoadedOriginalNativeDoubleLogSource(ulong[] c,ulong[] i,ulong[] h,ulong[] l,
        OriginalNativeDoubleLogRegionSource[] sources,IReadOnlyList<OriginalNativeDoubleLogRegionSource> code,LoadedOriginalDeferredSamplerNativeSource native)
    {coefficients=c;inverse=i;high=h;low=l;Sources=Array.AsReadOnly(sources);CodeWindows=code;Native=native;}
    public LoadedOriginalDeferredSamplerNativeSource Native{get;}
    public string Locale=>Native.Locale;
    public const string PinnedExecutableSha256="0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464";
    public string ExecutableSha256=>Native.ExecutableSha256;
    public string DecodedTextSha256=>Native.DecodedTextSha256;
    public string PhysicalPath=>Native.PhysicalPath;
    public IReadOnlyList<OriginalNativeDoubleLogRegionSource> Sources{get;}
    public IReadOnlyList<OriginalNativeDoubleLogRegionSource> CodeWindows{get;}
    public bool CurrentNativeDispatchObserved=>false;
    public string SourceEvidence=>$"{Locale}{CodeWindows[0].Rva:X8}-natural-log;finitepositive-binary32-CVTPS2PD-input;explicit-original-SSE/AVX-FMA-dispatch;"+
        Native.ResourceUri+"@"+ExecutableSha256+";decoded-text@"+DecodedTextSha256+";"+
        string.Join(";",Sources.Select(s=>$"rdata{s.Rva:X}@{s.Sha256}"));
    internal ulong Word(int rva)=>rva>=0x1728920&&rva<=0x1728AC8&&(rva&7)==0?
        coefficients[(rva-0x1728920)/8]:throw new InvalidDataException("Double-log coefficient is outside its verified source window.");
    internal double Coefficient(int rva)=>BitConverter.UInt64BitsToDouble(Word(rva));
    internal double Inverse(int index)=>BitConverter.UInt64BitsToDouble(inverse[index]);
    internal double High(int index)=>BitConverter.UInt64BitsToDouble(high[index]);
    internal double Low(int index)=>BitConverter.UInt64BitsToDouble(low[index]);
    public double LogPositiveFloat(float value,OriginalNativeLogDispatch selectedOriginalPath)
        =>OriginalNativeDoubleLogarithm.LogPositiveFloat(value,this,selectedOriginalPath);

    public static LoadedOriginalNativeDoubleLogSource Load(OriginalGameRepository repository,string locale)
    {ArgumentNullException.ThrowIfNull(repository);return Load(repository.SourceDirectory,locale);}
    public static LoadedOriginalNativeDoubleLogSource Load(string sourceDirectory,string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        var native=OriginalDeferredSamplerSources.Load(sourceDirectory,locale);
        string path=native.PhysicalPath;
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,128*1024,FileOptions.SequentialScan);
        if(stream.Length is <4096 or >128*1024*1024)throw new InvalidDataException("Double-log source executable exceeds its finite PE bound.");
        byte[] exe=new byte[checked((int)stream.Length)];stream.ReadExactly(exe);
        if(Convert.ToHexString(SHA256.HashData(exe))!=native.ExecutableSha256)
            throw new InvalidDataException("Double-log source executable differs from its actual pinned revision.");
        var code=OriginalDeferredSamplerSources.VerifyDoubleLogNativeCode(exe,locale);
        using var pe=new PEReader(new MemoryStream(exe,false));
        if(pe.PEHeaders.PEHeader is not{Magic:PEMagic.PE32Plus,ImageBase:0x140000000UL}||
            pe.PEHeaders.CoffHeader.Machine!=Machine.Amd64)
            throw new InvalidDataException("Double-log source requires its original AMD64 PE32+ image.");
        var sources=new List<OriginalNativeDoubleLogRegionSource>();
        ulong[] Read(int rva,int count,string hash,string role)
        {
            rva=OriginalMapPostEffect3NativeSourceProfile.DataRva(locale,"OriginalNativeDoubleLogSource.cs",rva);
            int size=checked(count*8);
            var section=pe.PEHeaders.SectionHeaders.Single(s=>rva>=s.VirtualAddress&&(long)rva+size<=(long)s.VirtualAddress+s.SizeOfRawData);
            if(section.Name!=".rdata")throw new InvalidDataException("Double-log coefficients are not original file-backed rdata.");
            var raw=exe.AsSpan(checked(section.PointerToRawData+rva-section.VirtualAddress),size);
            if(Convert.ToHexString(SHA256.HashData(raw))!=hash)throw new InvalidDataException("Double-log source region changed: "+role);
            var words=new ulong[count];for(int n=0;n<count;n++)words[n]=BinaryPrimitives.ReadUInt64LittleEndian(raw.Slice(n*8,8));
            sources.Add(new(native.ResourceUri+"#native-doublelog/"+role,rva,size,hash));return words;
        }
        var c=Read(0x1728920,54,"970639EBAB6C1F5FBDB16518C2959B838A7956949C99820B8AF80B819F41ABF6","coefficients");
        var i=Read(0x1729A30,257,"229D2459BBB3CB9C459844B78A6ABDD8FDDF1BDAEFDB4057F4EA6679BF6CDE24","inverse");
        var h=Read(0x172A320,257,"6559E5FE3D58ADC08589727E4BA0292C5F023079394A66BD93BE043B4D607A0E","high");
        var l=Read(0x172AB30,257,"8565D9011BDABCB30AC135C867E272E0EB8BDD1DED538456EF36F5785E744488","low");
        return new(c,i,h,l,sources.ToArray(),code,native);
    }
}
