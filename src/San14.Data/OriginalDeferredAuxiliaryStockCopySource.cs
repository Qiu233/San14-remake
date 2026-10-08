using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The actual stock VS/default3 PS used by the node137 mip0 copy
/// fallback. This source owns programs, not the current HDR attachment pixels.</summary>
public sealed record LoadedOriginalDeferredAuxiliaryStockCopySource(
    LoadedOriginalDeferredSamplerNativeSource Native,
    OriginalDeferredAuxiliaryDepthCopyProgram VertexProgram,
    OriginalDeferredAuxiliaryDepthCopyProgram PixelProgram);

public static class OriginalDeferredAuxiliaryStockCopySource
{
    public const string VertexSha256=OriginalMapExposureSource.VertexSha256;
    public const string PixelSha256=OriginalMapExposureSource.PixelSha256;
    public const ulong EqualRectangleSamplerKey=0x0000000000300015;
    public const ulong ResizedRectangleSamplerKey=0x0000000000301415;

    public static LoadedOriginalDeferredAuxiliaryStockCopySource ReadCurrent(string sourceDirectory,string locale)
    {
        var native=OriginalDeferredSamplerSources.Load(sourceDirectory,locale);
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);
        if(Hash(exe)!=native.ExecutableSha256)
            throw new InvalidDataException("Stock copy executable changed while reading its actual programs.");
        using var pe=new PEReader(new MemoryStream(exe,false));
        OriginalDeferredAuxiliaryDepthCopyProgram Read(int rva,int count,string hash,bool vertex)
        {
            var section=pe.PEHeaders.SectionHeaders.Single(s=>s.Name==".rdata"&&rva>=s.VirtualAddress&&
                (long)rva+count<=(long)s.VirtualAddress+s.SizeOfRawData);
            byte[] bytes=exe.AsSpan(checked(section.PointerToRawData+rva-section.VirtualAddress),count).ToArray();
            if(Hash(bytes)!=hash)throw new InvalidDataException("Stock copy embedded program differs from its pinned source bytes.");
            var container=DxbcContainerReader.Read(bytes);
            var rdef=container.Parts.Single(p=>p.FourCc=="RDEF").PayloadRange.Slice(bytes).ToArray();
            var definitions=vertex?OriginalDeferredAuxiliaryDepthCopySource.ReadVertexDefinitions(bytes):ReadPixelDefinitions(bytes);
            return new(bytes,rva,hash,container,rdef,definitions,DxbcSignatureReader.Read(bytes));
        }
        return new(native,Read(locale=="SC"?0x14BD070:0x14BC190,900,VertexSha256,true),
            Read(locale=="SC"?0x14BDDF0:0x14BCF10,896,PixelSha256,false));
    }

    /// <summary>Finite original SM4 reflection for the exact default3 PS. Raw
    /// declarations retain their actual names, flags, ranges and 4-byte vATest.
    /// SM4 absent texture/sampler-tail fields retain absent-index sentinels.</summary>
    public static DxbcResourceDefinitions ReadPixelDefinitions(ReadOnlySpan<byte> program)
    {
        if(Hash(program)!=PixelSha256)throw new InvalidDataException("Stock copy PS reflection requires its exact original program.");
        var part=DxbcContainerReader.Read(program).Parts.Single(p=>p.FourCc=="RDEF");
        byte[] data=part.PayloadRange.Slice(program).ToArray();
        DxbcRdefString Name(int offset)
        {
            int count=data.AsSpan(offset).IndexOf((byte)0);
            return new(Encoding.UTF8.GetString(data.AsSpan(offset,count)),new(offset,count+1));
        }
        var globals=Name(132);
        var bindings=new DxbcRdefBinding[]{
            new(0,new(28,32),Name(124),3,0,0,0,0,1,1),
            new(1,new(60,32),Name(128),2,5,4,uint.MaxValue,0,1,13),
            new(2,new(92,32),globals,0,0,0,0,0,1,0)};
        var variable=new DxbcRdefVariable(0,new(168,24),Name(192),0,4,2,200,null,uint.MaxValue,0,uint.MaxValue,0);
        var buffers=new[]{new DxbcRdefBuffer(0,new(144,24),globals,16,0,0,new(168,24),Array.AsReadOnly(new[]{variable}))};
        var types=new[]{new DxbcRdefType(200,new(200,16),0,3,1,1,0,new(0,0),
            Array.Empty<DxbcRdefMember>(),Array.Empty<byte>(),null)};
        return new(program.Length,part.PayloadRange,4,0,0xFFFF,0x100,0,data.AsSpan(0,28).ToArray(),
            Name(216),new(144,24),new(28,96),Array.AsReadOnly(buffers),Array.AsReadOnly(bindings),Array.AsReadOnly(types));
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
}
