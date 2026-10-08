using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Stock screen initialization, with unit scales, white color, no alpha test/blend/custom VS.</summary>
public sealed record LoadedTerrainInitialControlResources(TerrainTextureBinding Binding,
    G1FieldIndexMap SourceMap, byte[] RgbaPixels, string ExecutableSha256,
    string ScreenVertexSha256, string ScreenPixelSha256)
{
    public const string StateProfile = "ce-stock-screen-initialization-v1";
}

public static class OriginalTerrainInitialControlResources
{
    public const string VertexSha256 = "F2E09A25DC65315D110B1E81383DAE8D411FE17BBAFA7196CC809AF2AACEEF80";
    public const string PixelSha256 = "F2C4CB1A2BE802F1E80CEEA3C27810BD102410687B9394029F8CE4D0816FBBD2";

    /// <summary>This initializes the replica's stock state; it does not sample mutable original context state.</summary>
    public static LoadedTerrainInitialControlResources LoadStockDefault(OriginalGameRepository repository,
        LoadedTerrainTextureGroup field, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(field);
        LoadedTerrainFieldIndexMap source = OriginalTerrainTextureResources.ReadFieldIndexMap(field);
        TerrainTextureBinding binding = source.Binding;
        LoadedRdbAsset bytes = binding.Resource.Source;
        if (Convert.ToHexString(SHA256.HashData(bytes.Bytes)) != bytes.DecodedSha256)
            throw new InvalidDataException($"Initial control source hash differs: {bytes.ResourceUri}.");
        G1TextureSamplerRecipe sampler = G1TextureSamplerReader.ReadRetainedSource(binding.Image);
        if (source.Map.Width != 2049 || source.Map.Height != 2049 || sampler.Filter != 0x14 ||
            sampler.AddressU is not (1 or 3) || sampler.AddressV is not (1 or 3) ||
            sampler.MipLodBiasBits != 0 || sampler.MinimumLodBits != 0 || sampler.MaximumLodBits != 0 ||
            sampler.MaxAnisotropy != 1 || (binding.Image.RawHeader[19] & 1) != 0)
            throw new InvalidDataException("Initial control requires the verified 2049-square linear UNORM source with one mip and stock addressing.");
        string executableSha = OriginalCeHexWorldCoordinates.VerifyInstalledExecutable(repository.SourceDirectory, locale);
        string path = Path.Combine(repository.SourceDirectory, $"SAN14CE_{locale}.exe");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
        int vs = locale == "SC" ? 0x14BD070 : 0x14BC190;
        int ps = locale == "SC" ? 0x14BD6D0 : 0x14BC7F0;
        ReadProgram(stream, pe, vs, 900, VertexSha256);
        ReadProgram(stream, pe, ps, 888, PixelSha256);
        return new(binding, source.Map, ConvertStockCenterBgraToRgba(source.Map.CopyRawMip()),
            executableSha, VertexSha256, PixelSha256);
    }

    // Pixel centers preserve the source RGB under the verified stock 1:1 viewport.
    // Stock PS1 writes white vertex alpha, independently of source texture alpha.
    public static byte[] ConvertStockCenterBgraToRgba(ReadOnlySpan<byte> bgra)
    {
        if (bgra.Length == 0 || bgra.Length % 4 != 0) throw new InvalidDataException("Incomplete initial control texels.");
        byte[] rgba = new byte[bgra.Length];
        for (int i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = bgra[i + 2]; rgba[i + 1] = bgra[i + 1]; rgba[i + 2] = bgra[i]; rgba[i + 3] = 255;
        }
        return rgba;
    }
    private static void ReadProgram(FileStream stream, PEReader pe, int rva, int count, string expected)
    {
        SectionHeader[] matches = pe.PEHeaders.SectionHeaders.Where(section =>
            rva >= section.VirtualAddress && (long)rva + count <= (long)section.VirtualAddress + section.SizeOfRawData).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("Embedded original screen program is outside its PE section.");
        SectionHeader section = matches[0];
        long offset = section.PointerToRawData + (long)rva - section.VirtualAddress;
        stream.Position = offset;
        byte[] bytes = new byte[count]; stream.ReadExactly(bytes);
        DxbcContainerReader.Read(bytes);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != expected)
            throw new InvalidDataException("Embedded original screen program bytes changed.");
    }
}
