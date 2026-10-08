using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalMapFinalOutputToneShaderProgram(int Rva,string Sha256,
    ReadOnlyMemory<byte> Bytes,DxbcContainer Container,DxbcResourceDefinitions Definitions,
    DxbcProgramSignatures Signatures);

/// <summary>Exact embedded PS published to previously empty Common+F8 by
/// successful renderer initialization, together with the original stock VS.
/// This immutable source pair does not observe or select a current native Aux.</summary>
public sealed record LoadedOriginalMapFinalOutputToneShaderSource(string Locale,string PhysicalPath,
    string ExecutableSha256,string SourceResourceUri,OriginalMapFinalOutputToneShaderProgram Vertex,
    OriginalMapFinalOutputToneShaderProgram Pixel,string SourceEvidence)
{
    public bool ActiveOriginalSelectionObserved=>false;
    public int PixelConstantBufferBytes=>32;
    public bool DeclaresTexture3DLut=>true;
    /// <summary>The source index0 here names one explicitly selected embedded
    /// pair, not an A1/3SPK program index. No native pointer is manufactured.</summary>
    public OriginalMapFinalOutputProgramSource ToProgramSource()=>new(SourceResourceUri,0,0,
        Vertex.Bytes.ToArray(),Pixel.Bytes.ToArray(),Vertex.Sha256,Pixel.Sha256);
}

public static class OriginalMapFinalOutputToneShaderSource
{
    public const string VertexSha256="F2E09A25DC65315D110B1E81383DAE8D411FE17BBAFA7196CC809AF2AACEEF80";
    public const string PixelSha256="1DFBAF0D64B6A4F3CAB6CA8E66B56DB4002A7B4C1BE3937E1CEC0079E66668F4";
    public const int VertexByteCount=900,PixelByteCount=2612;

    /// <summary>Locale must be explicit: resource.StockScreen may use a
    /// different locale from the accepted camera/renderer source owner.</summary>
    public static LoadedOriginalMapFinalOutputToneShaderSource ReadCurrent(string sourceDirectory,string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        var profile=locale switch {
            "TC"=>(Exe:"0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464",Vs:0x14BC190,Ps:0x1699DB0,
                Evidence:"TC:BF93D0/BFBDBC-settings7F0;BFBDCC-F8-empty;BFBDDD-1699DB0/A34;BFBDEA-94DE50;BFBE12-publish;D1ECCD/D1ECD4-Aux-copy;EC377F;959250-stockVS"),
            "SC"=>(Exe:"CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88",Vs:0x14BD070,Ps:0x169AA80,
                Evidence:"SC:BFA8C0/BFD28C-settings7F0;BFD29C-F8-empty;BFD2AD-169AA80/A34;BFD2BA-94EF20;BFD2E2-publish;D1F9FD/D1FA04-Aux-copy;stockVS14BD070"),
            _=>throw new ArgumentOutOfRangeException(nameof(locale),locale,"Tone source supports exact installed SC and TC only.")};
        // Keep the whole-file revision gate before any embedded RVA read.
        string path=Path.GetFullPath(Path.Combine(sourceDirectory,$"SAN14CE_{locale}.exe"));
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,128*1024,FileOptions.SequentialScan);
        if(stream.Length is <4096 or >128*1024*1024)throw new InvalidDataException("Tone executable exceeds supported PE size bounds.");
        string hash=Convert.ToHexString(SHA256.HashData(stream));
        if(hash!=profile.Exe)throw new InvalidDataException("Tone executable differs from the pinned installed revision.");
        stream.Position=0;using var pe=new PEReader(stream,PEStreamOptions.LeaveOpen);
        if(pe.PEHeaders.PEHeader is not {Magic:PEMagic.PE32Plus,ImageBase:0x140000000UL}||pe.PEHeaders.CoffHeader.Machine!=Machine.Amd64)
            throw new InvalidDataException("Tone source expects the exact AMD64 PE32+ image.");
        OriginalMapFinalOutputToneShaderProgram Read(int rva,int length,string sha,uint stage)
        {
            var section=pe.PEHeaders.SectionHeaders.Single(s=>s.Name==".rdata"&&rva>=s.VirtualAddress&&
                (long)rva+length<=(long)s.VirtualAddress+s.SizeOfRawData);
            long offset=section.PointerToRawData+(long)rva-section.VirtualAddress;
            if(offset<0||offset>stream.Length-length)throw new InvalidDataException("Tone embedded program exceeds the source file.");
            stream.Position=offset;byte[] bytes=new byte[length];stream.ReadExactly(bytes);
            if(Convert.ToHexString(SHA256.HashData(bytes))!=sha)throw new InvalidDataException("Tone embedded program SHA-256 differs.");
            var container=DxbcContainerReader.Read(bytes);var tokens=container.Parts.Where(p=>p.TokenHeader is not null).ToArray();
            if(tokens.Length!=1||tokens[0].TokenHeader!.RawVersionToken!=stage)throw new InvalidDataException("Tone source program stage differs.");
            return new(rva,sha,bytes,container,stage==0x10040?OriginalDeferredAuxiliaryDepthCopySource.ReadVertexDefinitions(bytes):
                DxbcResourceDefinitionReader.Read(bytes),DxbcSignatureReader.Read(bytes));
        }
        var vertex=Read(profile.Vs,VertexByteCount,VertexSha256,0x10040);
        var pixel=Read(profile.Ps,PixelByteCount,PixelSha256,0x50);
        var defs=pixel.Definitions;
        if(defs.Buffers is not [var globals]||globals.Name.Text!="$Globals"||globals.ByteSize!=32||
            globals.Variables.Select(v=>(v.Name.Text,v.StartOffset,v.ByteSize)).SequenceEqual(new[]{("vToneMapParams",0,16),("fGamma",16,4),("fToneCurveType",20,4)})!=true)
            throw new InvalidDataException("Tone source global constant layout differs.");
        foreach(var (name,slot,dimension) in new[]{("t0",0u,4u),("t1",1u,4u),("t2",2u,8u)})
            if(!defs.Bindings.Any(b=>b.Name.Text==name&&b.InputType==2&&b.BindPoint==slot&&b.Dimension==dimension&&b.BindCount==1))
                throw new InvalidDataException("Tone source requires both original 2D inputs and its declared 3D LUT.");
        if(defs.Bindings.Count(b=>b.InputType==3&&b.BindCount==1&&b.BindPoint<=2)!=3)
            throw new InvalidDataException("Tone source sampler registers differ.");
        string uri=GameResourceId.File($"SAN14CE_{locale}.exe")+$"#Common-F8-tone/VS-{profile.Vs:X}-PS-{profile.Ps:X}@{PixelSha256}";
        return new(locale,path,hash,uri,vertex,pixel,profile.Evidence);
    }
}
