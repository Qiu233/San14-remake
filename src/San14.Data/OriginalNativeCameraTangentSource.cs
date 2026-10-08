using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace San14.Data;

public sealed record OriginalNativeCameraTangentConstantSource(string Name,int Rva,ulong Bits,string Sha256);

/// <summary>Read-only installed source provenance and exact coefficient words.
/// Private construction requires the pinned SC/TC executable and every named
/// raw .rdata range. No game executable is loaded or called.</summary>
public sealed class LoadedOriginalNativeCameraTangentSource
{
    public string Language { get; }
    public string ResourceUri { get; }
    public string PhysicalPath { get; }
    public string ExecutableSha256 { get; }
    public string DecodedTextSha256 { get; }
    public int NativeTanfRva { get; }
    public int DispatchFlagRva { get; }
    public IReadOnlyList<OriginalNativeCameraTangentConstantSource> Constants { get; }
    public bool CurrentNativeDispatchObserved=>false;
    public string RoundingContract=>"binary32 input/result; nearest-even binary64 scalar/FMA operations; no floating exception-state emulation";

    internal LoadedOriginalNativeCameraTangentSource(string language,string path,string exeHash,string textHash,
        int tanfRva,int dispatchRva,OriginalNativeCameraTangentConstantSource[] constants)
    {
        Language=language;PhysicalPath=path;ExecutableSha256=exeHash;DecodedTextSha256=textHash;
        NativeTanfRva=tanfRva;DispatchFlagRva=dispatchRva;Constants=Array.AsReadOnly(constants);
        ResourceUri=GameResourceId.File($"SAN14CE_{language}.exe").ToString()+"#camera-tangent";
    }
}

public static class OriginalNativeCameraTangentSource
{
    public static LoadedOriginalNativeCameraTangentSource Load(OriginalGameRepository repository,string language)
    {ArgumentNullException.ThrowIfNull(repository);return Load(repository.SourceDirectory,language);}

    public static LoadedOriginalNativeCameraTangentSource Load(string sourceDirectory,string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        (string expected,string text,int tanf,int dispatch,int shift)=language switch
        {
            "SC"=>("CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88",
                "D7F3831C369A76148D87370D91EF0A8A43487D5E13A383886C68A6D50591E8F6",0x1020A90,0x27F438C,0xC60),
            "TC"=>("0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464",
                "FFBD504FC3DEA6A218EF4E8530A65761296C17191F7D675AB3C49DF64D950766",0x101FB90,0x27F31CC,0),
            _=>throw new InvalidDataException("Original camera tangent source supports pinned SC/TC revisions only.")
        };
        string path=Path.GetFullPath(Path.Combine(sourceDirectory,$"SAN14CE_{language}.exe"));
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,128*1024,FileOptions.SequentialScan);
        if(stream.Length is <4096 or >128*1024*1024)throw new InvalidDataException("Original camera tangent executable size exceeds its source contract.");
        byte[] exe=new byte[checked((int)stream.Length)];stream.ReadExactly(exe);
        string hash=Convert.ToHexString(SHA256.HashData(exe));
        if(hash!=expected)throw new InvalidDataException("Original camera tangent executable differs from its pinned source revision.");
        using var pe=new PEReader(new MemoryStream(exe,false));
        var optional=pe.PEHeaders.PEHeader;
        if(optional is null||optional.Magic!=PEMagic.PE32Plus||optional.ImageBase!=0x140000000UL||pe.PEHeaders.CoffHeader.Machine!=Machine.Amd64)
            throw new InvalidDataException("Original camera tangent expects its AMD64 PE32+ source image.");
        var addresses=new Dictionary<string,int>
        {
            ["QuarterPi"]=0x1724C08,["TwoOverPi"]=0x1724B50,["Half"]=0x1724BE0,
            ["HalfPiHigh"]=0x1724BE8,["HalfPiLow"]=0x1724BF0,["Numerator2"]=0x1724B98,
            ["Numerator0"]=0x1724B90,["Denominator2"]=0x1724BB0,["Denominator1"]=0x1724BA8,
            ["Denominator0"]=0x1724BA0,["NegativeOne"]=0x1724C00,["OneThird"]=0x1724BC8,
            ["SmallInputThreshold"]=0x1724BD8,["TinyInputThreshold"]=0x1724BD0
        };
        var constants=new List<OriginalNativeCameraTangentConstantSource>();
        foreach(var address in addresses)
        {
            int rva=checked(address.Value+shift);
            var sections=pe.PEHeaders.SectionHeaders.Where(s=>rva>=s.VirtualAddress&&
                (long)rva+8<=(long)s.VirtualAddress+s.SizeOfRawData).ToArray();
            if(sections.Length!=1||sections[0].Name!=".rdata")throw new InvalidDataException("Original camera tangent coefficient is outside its one raw .rdata source range.");
            long at=(long)sections[0].PointerToRawData+rva-sections[0].VirtualAddress;
            if(at<0||at>exe.Length-8)throw new InvalidDataException("Original camera tangent coefficient exceeds source bytes.");
            var bytes=exe.AsSpan(checked((int)at),8);ulong bits=BinaryPrimitives.ReadUInt64LittleEndian(bytes);
            if(bits!=OriginalNativeCameraTangentConstants.Words[address.Key])throw new InvalidDataException("Original camera tangent coefficient differs from its pinned instruction operand.");
            constants.Add(new(address.Key,rva,bits,Convert.ToHexString(SHA256.HashData(bytes))));
        }
        return new(language,path,hash,text,tanf,dispatch,constants.ToArray());
    }
}
