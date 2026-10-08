using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Finite original PS16 translation boundary. Its RDEF retains a
/// complete160B native buffer, while its original dcl CB0[9] and translated
/// shader address only144B. The complete source upload is never truncated.</summary>
public static class OriginalMapFinalOutputSourceBindings
{
    public const string StarPixelSha256="D2373CE632D7788DD4DFE2C2048D289D181617A76E4FB6101E1640B94BC5A402";
    public static OriginalTranslatedShaderBindings Read(ReadOnlySpan<byte> sourceProgram,ReadOnlySpan<byte> spirv)
    {
        var source=DxbcResourceDefinitionReader.Read(sourceProgram);
        if(Convert.ToHexString(SHA256.HashData(sourceProgram))!=StarPixelSha256)
            return OriginalTranslatedShaderBindingReader.Read(source,spirv);
        var device=SpirvShaderInterfaceReader.Read(spirv);var cb=device.Descriptors.Where(d=>d.StorageClass==2).ToArray();
        if(source.Buffers.Count!=1||source.Buffers[0].Name.Text!="$Globals"||source.Buffers[0].ByteSize!=160||
            cb.Length!=1||cb[0].RequiredBufferBytes!=144||cb[0].Name!="cb0_0"||
            !source.Buffers[0].Variables.Select(v=>(v.Name.Text,v.StartOffset,v.ByteSize)).SequenceEqual(new[]{
                ("g_vStarWeight",0,128),("g_fStarMergeWeight",128,4),("SimulateHDRParams",132,8),("fKIDSToneCurveType",140,4),("fKIDSTrueHDR",144,4)}))
            throw new InvalidDataException("Original PS16 inactive constant-buffer tail differs from its pinned source/translation.");
        var normalized=source with{Buffers=new[]{source.Buffers[0] with{ByteSize=144}}};
        var matched=OriginalTranslatedShaderBindingReader.Read(normalized,spirv);
        return matched with{OriginalDefinitions=source,Bindings=Array.AsReadOnly(matched.Bindings.Select(b=>b.ConstantBuffer is null?b:
            b with{ConstantBuffer=source.Buffers[0]}).ToArray())};
    }
}
