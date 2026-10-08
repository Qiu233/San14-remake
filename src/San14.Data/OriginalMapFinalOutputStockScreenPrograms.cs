using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Additional exact installed stock PS1 used by the LightScene copy.
/// Reads inert PE bytes only. Mode3 remains supplied by the Exposure source.</summary>
public static class OriginalMapFinalOutputStockScreenPrograms
{
    public static byte[] ReadMode1(LoadedOriginalDeferredSamplerNativeSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        int rva=source.Locale switch{"SC"=>0x14BD6D0,"TC"=>0x14BC7F0,_=>throw new InvalidDataException("Stock screen PS1 requires a verified source locale.")};
        using var stream=new FileStream(source.PhysicalPath,FileMode.Open,FileAccess.Read,FileShare.Read);
        using var pe=new PEReader(stream,PEStreamOptions.LeaveOpen);
        var sections=pe.PEHeaders.SectionHeaders.Where(s=>rva>=s.VirtualAddress&&(long)rva+888<=(long)s.VirtualAddress+s.SizeOfRawData).ToArray();
        if(sections.Length!=1)throw new InvalidDataException("Source stock PS1 is outside its exact PE section.");
        stream.Position=sections[0].PointerToRawData+(long)rva-sections[0].VirtualAddress;
        byte[] bytes=new byte[888];stream.ReadExactly(bytes);DxbcContainerReader.Read(bytes);
        if(Convert.ToHexString(SHA256.HashData(bytes))!=OriginalTerrainInitialControlResources.PixelSha256)
            throw new InvalidDataException("Source stock PS1 bytes changed.");
        return bytes;
    }
}
