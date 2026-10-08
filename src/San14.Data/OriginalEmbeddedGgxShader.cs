using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The original initialization shader, reconstructed from local executable stack immediates.</summary>
public sealed record LoadedOriginalEmbeddedGgxShader(byte[] Dxbc, string ResourceUri, string Locale,
    string ExecutableSha256, string DecodedTextSha256, string StackWindowSha256, string ShaderSha256,
    int DecodedTextRva, int DecodedTextBytes, int StackStartRva, int StackByteCount, int ImmediateStoreCount)
{
    public string PhysicalPath { get; init; } = "";
}

/// <summary>Reads two verified CE revisions without loading or executing the original executable.</summary>
public static class OriginalEmbeddedGgxShader
{
    public const string ShaderSha256 = "7F0EB8A2088E538097C17E7E48ECF80441CAE5D5E18B43BA6175FEEF5E980E5A";
    public const string StackWindowSha256 = "3BD13BD6144A0335E3C9C8B1E22A21A48BF604F16C7C4AC17A4913DA388E9B29";
    public const int ShaderByteCount = 3156;
    private const int WindowBytes = 7069;

    public static LoadedOriginalEmbeddedGgxShader Load(string sourceDirectory, string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        Profile profile = GetProfile(locale);
        string path = Path.GetFullPath(Path.Combine(sourceDirectory, $"SAN14CE_{locale}.exe"));
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.SequentialScan);
        if (stream.Length is < 4096 or > 128 * 1024 * 1024)
            throw new InvalidDataException("Embedded GGX executable exceeds the supported size bound.");
        byte[] executable = new byte[checked((int)stream.Length)];
        stream.ReadExactly(executable);
        VerifyHash(executable, profile.ExecutableSha256, "executable");
        byte[] code = DecodeText(executable, out int textRva);
        VerifyHash(code, profile.TextSha256, "decoded .text");
        int offset = checked(profile.StackRva - textRva);
        if (offset < 0 || offset > code.Length - WindowBytes)
            throw new InvalidDataException("Embedded GGX stack window is outside decoded .text.");
        byte[] window = code.AsSpan(offset, WindowBytes).ToArray();
        VerifyHash(window, StackWindowSha256, "stack window");
        byte[] shader = ReconstructStackProgram(window);
        VerifyHash(shader, ShaderSha256, "DXBC");
        DxbcContainer container = DxbcContainerReader.Read(shader);
        DxbcPart[] programs = container.Parts.Where(p => p.TokenHeader is not null).ToArray();
        if (programs.Length != 1 || programs[0].TokenHeader!.RawVersionToken != 0x50)
            throw new InvalidDataException("Embedded GGX program is not the verified ps_5_0 shader.");
        // The fragment names a decoded RVA window, not an on-disk DXBC offset.
        string uri = GameResourceId.File($"SAN14CE_{locale}.exe").ToString() +
            $"#decoded-text/rva-{profile.StackRva:X}/gx-ambient-lut.ps";
        return new(shader, uri, locale, profile.ExecutableSha256, profile.TextSha256,
            StackWindowSha256, ShaderSha256, textRva, code.Length, profile.StackRva, WindowBytes, 708) { PhysicalPath = path };
    }

    private sealed record Profile(string ExecutableSha256, string TextSha256, int StackRva);
    private static Profile GetProfile(string locale) => locale switch
    {
        "SC" => new("CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88",
            "D7F3831C369A76148D87370D91EF0A8A43487D5E13A383886C68A6D50591E8F6", 0xBFB0F2),
        "TC" => new("0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464",
            "FFBD504FC3DEA6A218EF4E8530A65761296C17191F7D675AB3C49DF64D950766", 0xBF9C22),
        _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, "Embedded GGX supports verified SC and TC revisions only.")
    };

    // Observed 240-byte chained-XOR header fields and AES arrangement are checked against
    // the local offline-code-audit research. This emits only inert section bytes in memory.
    private static byte[] DecodeText(byte[] executable, out int textRva)
    {
        using var pe = new PEReader(new MemoryStream(executable, writable: false));
        PEHeaders headers = pe.PEHeaders;
        PEHeader optional = headers.PEHeader ?? throw new InvalidDataException("Missing PE optional header.");
        if (headers.CoffHeader.Machine != Machine.Amd64 || optional.Magic != PEMagic.PE32Plus || optional.ImageBase != 0x140000000UL)
            throw new InvalidDataException("Embedded GGX expects the verified AMD64 PE32+ image.");
        SectionHeader text = Section(".text"), bind = Section(".bind");
        SectionHeader Section(string name)
        {
            SectionHeader[] found = headers.SectionHeaders.Where(s => s.Name == name).ToArray();
            return found.Length == 1 ? found[0] : throw new InvalidDataException("Missing or duplicated PE section: " + name);
        }
        int Raw(int rva, int count)
        {
            SectionHeader[] found = headers.SectionHeaders.Where(s => rva >= s.VirtualAddress &&
                (long)rva + count <= (long)s.VirtualAddress + s.SizeOfRawData).ToArray();
            if (count < 0 || found.Length != 1) throw new InvalidDataException("Embedded GGX PE range is ambiguous or outside raw sections.");
            long at = found[0].PointerToRawData + (long)rva - found[0].VirtualAddress;
            if (at < 0 || at > executable.Length - (long)count) throw new InvalidDataException("Embedded GGX PE range exceeds executable.");
            return checked((int)at);
        }
        int entry = optional.AddressOfEntryPoint;
        ReadOnlySpan<byte> signature = [0xE8, 0, 0, 0, 0, 0x50, 0x53, 0x51, 0x52, 0x56, 0x57, 0x55, 0x41, 0x50];
        if (entry < bind.VirtualAddress || (long)entry + signature.Length > (long)bind.VirtualAddress + bind.SizeOfRawData ||
            !executable.AsSpan(Raw(entry, signature.Length), signature.Length).SequenceEqual(signature))
            throw new InvalidDataException("Embedded GGX static entry signature differs.");
        byte[] header = executable.AsSpan(Raw(checked(entry - 240), 240), 240).ToArray();
        uint preceding = U32(header, 0);
        for (int i = 4; i < header.Length; i += 4)
        {
            uint encoded = U32(header, i);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(i), encoded ^ preceding);
            preceding = encoded;
        }
        ulong originalEntry = U64(header, 32);
        if (U32(header, 4) != 0xC0DEC0DF || U64(header, 8) != optional.ImageBase ||
            U64(header, 72) != (ulong)text.VirtualAddress || U64(header, 80) != (ulong)text.SizeOfRawData ||
            (U32(header, 60) & 4) != 0 || originalEntry < (ulong)text.VirtualAddress ||
            originalEntry >= (ulong)text.VirtualAddress + (ulong)text.VirtualSize ||
            text.SizeOfRawData < 16 || text.SizeOfRawData % 16 != 0 || text.VirtualSize > text.SizeOfRawData)
            throw new InvalidDataException("Embedded GGX static section header differs from supported encrypted .text layout.");
        // The header carries the first encrypted block; the section contains the rest.
        byte[] ciphertext = new byte[checked(text.SizeOfRawData + 16)];
        header.AsSpan(136, 16).CopyTo(ciphertext);
        executable.AsSpan(Raw(text.VirtualAddress, text.SizeOfRawData), text.SizeOfRawData).CopyTo(ciphertext.AsSpan(16));
        using Aes aes = Aes.Create();
        aes.Key = header.AsSpan(88, 32).ToArray();
        byte[] iv = aes.DecryptEcb(header.AsSpan(120, 16), PaddingMode.None);
        byte[] plaintext = aes.DecryptCbc(ciphertext, iv, PaddingMode.None);
        textRva = text.VirtualAddress;
        return plaintext.AsSpan(0, text.SizeOfRawData).ToArray();
    }

    // Deliberately handles only the forms measured in this fixed, hash-locked window.
    // REX.W C7 /0 sign-extends imm32 to eight bytes; copying four bytes would corrupt DXBC.
    private static byte[] ReconstructStackProgram(byte[] window)
    {
        byte[] shader = new byte[ShaderByteCount];
        int input = 0, output = 0, stores4 = 0, stores8 = 0, setup = 0;
        while (input < window.Length)
        {
            ReadOnlySpan<byte> remaining = window.AsSpan(input);
            if (remaining.StartsWith(new byte[] { 0x41, 0xB8 }))
            {
                if (remaining.Length < 6 || U32(window, input + 2) != ShaderByteCount || setup != 0)
                    throw new InvalidDataException("Embedded GGX byte-count setup differs.");
                input += 6; setup = 1; continue;
            }
            if (remaining.StartsWith(new byte[] { 0x48, 0x8D, 0x55, 0xE0 }) && setup == 1)
            { input += 4; setup = 2; continue; }
            if (remaining.StartsWith(new byte[] { 0x48, 0x8B, 0xCE }) && setup == 2)
            { input += 3; setup = 3; continue; }
            int prefix = remaining[0] == 0x48 ? 1 : 0;
            int width = prefix == 1 ? 8 : 4;
            if (remaining.Length < prefix + 2 || remaining[prefix] != 0xC7 ||
                remaining[prefix + 1] is not (0x45 or 0x85))
                throw new InvalidDataException("Unsupported instruction in embedded GGX stack window.");
            int displacementBytes = remaining[prefix + 1] == 0x45 ? 1 : 4;
            int length = prefix + 2 + displacementBytes + 4;
            if (remaining.Length < length) throw new InvalidDataException("Truncated embedded GGX immediate store.");
            int displacement = displacementBytes == 1 ? unchecked((sbyte)remaining[prefix + 2]) :
                BinaryPrimitives.ReadInt32LittleEndian(remaining[(prefix + 2)..]);
            if ((long)displacement + 0x20 != output || output > shader.Length - width)
                throw new InvalidDataException("Embedded GGX writes are overlapping, noncontiguous or outside shader bytes.");
            int immediate = BinaryPrimitives.ReadInt32LittleEndian(remaining[(prefix + 2 + displacementBytes)..]);
            if (width == 8) { BinaryPrimitives.WriteInt64LittleEndian(shader.AsSpan(output), immediate); stores8++; }
            else { BinaryPrimitives.WriteInt32LittleEndian(shader.AsSpan(output), immediate); stores4++; }
            output += width; input += length;
        }
        if (output != shader.Length || stores4 != 627 || stores8 != 81 || setup != 3)
            throw new InvalidDataException("Embedded GGX complete byte coverage or instruction counts differ.");
        return shader;
    }

    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ulong U64(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset, 8));
    private static void VerifyHash(byte[] bytes, string expected, string part)
    {
        string actual = Convert.ToHexString(SHA256.HashData(bytes));
        if (actual != expected) throw new InvalidDataException($"Embedded GGX {part} SHA-256 changed: {actual}.");
    }
}
