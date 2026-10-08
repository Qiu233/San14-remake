using System.Buffers.Binary;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalDeferredAuxiliaryDepthCopyProgram(byte[] Dxbc, int Rva,
    string Sha256, DxbcContainer Container, byte[] ResourceDefinitionBytes,
    DxbcResourceDefinitions Definitions, DxbcProgramSignatures Signatures);

/// <summary>The exact installed embedded programs used by node97's depth target branch.
/// The caller supplies the accepted main-depth image and current viewport. These
/// programs do not establish that the original game's current draw ran.</summary>
public sealed record LoadedOriginalDeferredAuxiliaryDepthCopySource(string Locale,
    string PhysicalPath, string ResourceUri, string ExecutableSha256,
    OriginalDeferredAuxiliaryDepthCopyProgram VertexProgram,
    OriginalDeferredAuxiliaryDepthCopyProgram PixelProgram)
{
    public uint SourceDepthObjectId => 0xE13DDB4F;
    public uint DestinationDepthObjectId => 0xB6169FDB;
    public int SourceDepthMip => 0;
    public int NativeTextureStage => 4;
    public int TextureSlot => 0;
    public int StockVertexConstantByteCount => 32;
    public bool PixelUsesSampler => false;
    public bool PixelWritesDepth => true;
}

/// <summary>Reads inert, hash-pinned PE .rdata only. Native initialization fills
/// CommonViewSetting+1C8 with the embedded PS; node97 binds it in screen mode5.</summary>
public static class OriginalDeferredAuxiliaryDepthCopySource
{
    public const string VertexSha256 = "F2E09A25DC65315D110B1E81383DAE8D411FE17BBAFA7196CC809AF2AACEEF80";
    public const string PixelSha256 = "BE664BE4E92FA98ACDD90C84AF00EB740E81E6941D52087C29D70D8B0F9FAFC0";
    public const int VertexByteCount = 900, PixelByteCount = 704;

    /// <summary>TC95E54A..95E5F6 / SC95F5FA..95F6A6. Both retained
    /// backend screen scales and color scale are explicit inputs. No identity
    /// scale or color is inferred from the stock program or source rectangle.</summary>
    public static byte[] BuildStockVertexConstants(int currentViewportWidth, int currentViewportHeight,
        float currentScreenScale29E0, float currentScreenScale29E4, Vector4 currentColorScale29F0)
    {
        if (currentViewportWidth <= 0 || currentViewportHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(currentViewportWidth), "Depth-copy viewport dimensions must be positive.");
        float[] values = [(currentScreenScale29E0 + currentScreenScale29E0) / (float)currentViewportWidth,
            (currentScreenScale29E4 * -2.0f) / (float)currentViewportHeight, -1.0f, 1.0f,
            currentColorScale29F0.X,currentColorScale29F0.Y,currentColorScale29F0.Z,currentColorScale29F0.W];
        byte[] bytes = new byte[32];
        for (int i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4), BitConverter.SingleToInt32Bits(values[i]));
        return bytes;
    }

    public static LoadedOriginalDeferredAuxiliaryDepthCopySource ReadCurrent(string sourceDirectory, string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        var profile = locale switch
        {
            "SC" => (Exe: "CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88", Vs: 0x14BD070, Ps: 0x16AC1F0),
            "TC" => (Exe: "0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464", Vs: 0x14BC190, Ps: 0x16AB520),
            _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Depth copy supports the pinned SC and TC revisions only.")
        };
        string path = Path.GetFullPath(Path.Combine(sourceDirectory, $"SAN14CE_{locale}.exe"));
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.SequentialScan);
        if (stream.Length is < 4096 or > 128 * 1024 * 1024)
            throw new InvalidDataException("Depth-copy source executable is outside the supported size bound.");
        string executableHash = Convert.ToHexString(SHA256.HashData(stream));
        if (executableHash != profile.Exe)
            throw new InvalidDataException("Depth-copy source executable SHA-256 differs from the pinned revision.");
        stream.Position = 0;
        using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
        if (pe.PEHeaders.PEHeader is not { Magic: PEMagic.PE32Plus, ImageBase: 0x140000000UL } ||
            pe.PEHeaders.CoffHeader.Machine != Machine.Amd64)
            throw new InvalidDataException("Depth copy expects the pinned AMD64 PE32+ image.");
        OriginalDeferredAuxiliaryDepthCopyProgram Read(int rva, int count, string hash, uint version)
        {
            var sections = pe.PEHeaders.SectionHeaders.Where(s => s.Name == ".rdata" &&
                rva >= s.VirtualAddress && (long)rva + count <= (long)s.VirtualAddress + s.SizeOfRawData).ToArray();
            if (sections.Length != 1)
                throw new InvalidDataException("Depth-copy program is outside its exact source .rdata section.");
            long offset = sections[0].PointerToRawData + (long)rva - sections[0].VirtualAddress;
            if (offset < 0 || offset > stream.Length - count)
                throw new InvalidDataException("Depth-copy program exceeds the source file.");
            stream.Position = offset;
            byte[] bytes = new byte[count]; stream.ReadExactly(bytes);
            if (Convert.ToHexString(SHA256.HashData(bytes)) != hash)
                throw new InvalidDataException("Depth-copy embedded program SHA-256 differs.");
            var container = DxbcContainerReader.Read(bytes);
            var tokens = container.Parts.Where(p => p.TokenHeader is not null).ToArray();
            if (tokens.Length != 1 || tokens[0].TokenHeader!.RawVersionToken != version)
                throw new InvalidDataException("Depth-copy embedded program stage differs.");
            var rdef = container.Parts.Single(p => p.FourCc == "RDEF").PayloadRange.Slice(bytes).ToArray();
            return new(bytes, rva, hash, container, rdef,
                version == 0x50 ? DxbcResourceDefinitionReader.Read(bytes) : ReadVertexDefinitions(bytes),
                DxbcSignatureReader.Read(bytes));
        }
        var vertex = Read(profile.Vs, VertexByteCount, VertexSha256, 0x10040);
        var pixel = Read(profile.Ps, PixelByteCount, PixelSha256, 0x50);
        var binding = pixel.Definitions.Bindings.Single();
        if (pixel.Definitions.Buffers.Count != 0 || binding.Name.Text != "t0" || binding.InputType != 2 ||
            binding.ReturnType != 5 || binding.Dimension != 4 || binding.BindPoint != 0 || binding.BindCount != 1 ||
            pixel.Signatures.Parts.Single(p => p.Kind == DxbcSignatureKind.Output).Parameters is not [var depth] ||
            depth.SemanticName.Text != "SV_Depth" || depth.RawSystemValue != 0 || depth.Register != uint.MaxValue)
            throw new InvalidDataException("Depth copy requires the exact integer-load t0 and SV_Depth source contract.");
        string uri = GameResourceId.File($"SAN14CE_{locale}.exe").ToString() + $"#rva-{profile.Ps:X}/depth-copy.ps";
        return new(locale, path, uri, executableHash, vertex, pixel);
    }

    /// <summary>Finite adapter for the single hash-pinned stock vs_4_0 RDEF.
    /// Its 28-byte header, 24-byte variable records and 16-byte type are retained.
    /// SM4 has no interface-slot or variable texture/sampler tail fields; those
    /// shared-model fields use zero counts and absent-index uint.MaxValue.</summary>
    public static DxbcResourceDefinitions ReadVertexDefinitions(ReadOnlySpan<byte> program)
    {
        if (Convert.ToHexString(SHA256.HashData(program)) != VertexSha256)
            throw new InvalidDataException("Stock VS4 reflection requires the exact pinned depth-copy vertex program.");
        var part = DxbcContainerReader.Read(program).Parts.Single(p => p.FourCc == "RDEF");
        byte[] data = part.PayloadRange.Slice(program).ToArray();
        DxbcRdefString Name(int offset)
        {
            int length = data.AsSpan(offset).IndexOf((byte)0);
            return new(Encoding.UTF8.GetString(data.AsSpan(offset, length)), new(offset, length + 1));
        }
        var globals = Name(60);
        var variables = new[]
        {
            new DxbcRdefVariable(0, new(96,24), Name(144), 0,16,2,160,null,uint.MaxValue,0,uint.MaxValue,0),
            new DxbcRdefVariable(1, new(120,24), Name(176),16,16,2,160,null,uint.MaxValue,0,uint.MaxValue,0)
        };
        var buffers = new[] { new DxbcRdefBuffer(0,new(72,24),globals,32,0,0,new(96,48),Array.AsReadOnly(variables)) };
        var bindings = new[] { new DxbcRdefBinding(0,new(28,32),globals,0,0,0,0,0,1,0) };
        var types = new[] { new DxbcRdefType(160,new(160,16),1,3,1,4,0,new(0,0),
            Array.Empty<DxbcRdefMember>(),Array.Empty<byte>(),null) };
        return new(program.Length,part.PayloadRange,4,0,0xFFFE,0x100,0,data.AsSpan(0,28).ToArray(),
            Name(191),new(72,24),new(28,32),Array.AsReadOnly(buffers),Array.AsReadOnly(bindings),Array.AsReadOnly(types));
    }
}
