using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Reflection.PortableExecutable;

namespace San14.Data;

/// <summary>Actual Main App reset fields and the primary controller's native
/// construction/reset writer. These are initialization results, not a live
/// original-process sample or authority for later App/global edits.</summary>
public sealed class LoadedOriginalMainMapCameraStartupSource
{
    internal LoadedOriginalMainMapCameraStartupSource(string locale, string exeHash, string textHash,
        ulong imageBase, uint projectionProfile, int appMode, int modeOverride, uint fovBits,
        uint maximumYFlagRva, uint maximumYAfterReset, Dictionary<string,string> windows)
    {
        Locale = locale; ExecutableSha256 = exeHash; DecodedTextSha256 = textHash; ImageBase = imageBase;
        StaticProjectionProfile = projectionProfile; AppMode288AfterReset = appMode;
        ModeOverride28CAfterReset = modeOverride; FovOverride290BitsAfterReset = fovBits;
        MaximumYFlagRva = maximumYFlagRva; MaximumYOverrideWordAfterControllerReset = maximumYAfterReset;
        SourceWindows = new ReadOnlyDictionary<string,string>(windows);
    }
    public string Locale { get; }
    public string ExecutableSha256 { get; }
    public string DecodedTextSha256 { get; }
    public ulong ImageBase { get; }
    /// <summary>The pinned image's projection profile initialization value.
    /// A later actual renderer profile change must remain a current input.</summary>
    public uint StaticProjectionProfile { get; }
    public int AppMode288AfterReset { get; }
    public int ModeOverride28CAfterReset { get; }
    public uint FovOverride290BitsAfterReset { get; }
    public float OwnerFovOverride290AfterReset => BitConverter.UInt32BitsToSingle(FovOverride290BitsAfterReset);
    public uint MaximumYFlagRva { get; }
    public uint MaximumYOverrideWordAfterControllerReset { get; }
    public bool MaximumYOverrideAfterControllerReset => MaximumYOverrideWordAfterControllerReset != 0;
    public IReadOnlyDictionary<string,string> SourceWindows { get; }
    public string SourceEvidence => $"{Locale}@{ExecutableSha256};decoded-text={DecodedTextSha256};" +
        "complete-App-singleton/constructor/reset;App290=actual10D36-reset-store;" +
        $"complete-primary-controller-C880;CB2E-zero-ESI-store-to-{MaximumYFlagRva:X8};" +
        "owned-fresh-initialization-only;later-App/global-writers-are-current-inputs";
}

public static class OriginalMainMapCameraStartupSource
{
    /// <summary>Load and accept the complete paired source functions once.
    /// Normal consumers use the immutable reset words without file access,
    /// internal hashing or replay of the native reset.</summary>
    public static LoadedOriginalMainMapCameraStartupSource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadMainMapCameraStartupSource(repository.SourceDirectory, locale);
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMainMapCameraStartupSource LoadMainMapCameraStartupSource(string directory, string locale)
    {
        var profile = Profile(locale);
        byte[] exe = File.ReadAllBytes(Path.Combine(directory, $"SAN14CE_{locale}.exe"));
        VerifyHash(exe, profile.ExeHash, "Main Camera startup executable");
        var image = new NativeImage(exe); byte[] code = image.DecodeText(out int textRva);
        VerifyHash(code, profile.TextHash, "Main Camera startup decoded text");
        bool tc = locale == "TC";
        var windows = new Dictionary<string,string>(StringComparer.Ordinal);
        void Accept(string name, int rva, int length, string hash)
        { VerifyHash(code.AsSpan(rva - textRva, length), hash, name); windows.Add(name, hash); }
        Accept("AppSingleton", 0x11040, 129, tc
            ? "2437AEE130B52D6CFF6C219D87B72D6FBE3F13E1934754480D306521F0930944"
            : "9BFDC3C3D8F3BFF4916D1A5A8258E63F192B7392620EBD28B1371918418EA94B");
        Accept("AppConstructor", tc ? 0x9A00 : 0x99F0, 490, tc
            ? "E6C7CB19A7816B07A99934F7230CD3ED6EB7554555CA4FFD36DB870E7B9801F2"
            : "7C8DC11B6E44E051AF9196C1D84BA8BF167123290EE3A14CCD7884B4DDFE9D8F");
        Accept("AppReset", 0x10CF0, 265, tc
            ? "894A90660C581C01D85F3D8A71E639E18B662B87C6804AF0D15B4059B96C6952"
            : "D94A8CBBBCE0725CB8DD4C5095C1D7B43221C1CBF0773D7B391B3C4D1BF56A0D");
        Accept("PrimaryControllerReset", 0xC880, 703, tc
            ? "6167500D4607D504D18BAA3B15086A65C6834A608EFA01C2835645588DC7BD68"
            : "A5888EE554B0DAD053CC6AEA0D9CA824CEF3FCCDAED5973C4FCDF4AFC1DA58D8");

        uint ReadResetWord(int rva, int offset)
        {
            var instruction = code.AsSpan(rva - textRva, 10);
            if (instruction[0] != 0xC7 || instruction[1] != 0x87 ||
                BinaryPrimitives.ReadInt32LittleEndian(instruction[2..6]) != offset)
                throw new InvalidDataException("Main App reset writer operand differs.");
            return BinaryPrimitives.ReadUInt32LittleEndian(instruction[6..10]);
        }
        uint mode = ReadResetWord(0x10D22, 0x288), modeOverride = ReadResetWord(0x10D2C, 0x28C),
            fov = ReadResetWord(0x10D36, 0x290);
        var zeroWriter = code.AsSpan(0xC8AC - textRva, 2);
        var globalWriter = code.AsSpan(0xCB2E - textRva, 6);
        if (!zeroWriter.SequenceEqual(Convert.FromHexString("33F6")) || globalWriter[0] != 0x89 || globalWriter[1] != 0x35)
            throw new InvalidDataException("Primary controller global MaxY reset writer differs.");
        uint maximumYFlag = checked((uint)(0xCB34 + BinaryPrimitives.ReadInt32LittleEndian(globalWriter[2..])));
        if (maximumYFlag != (tc ? 0x1B5A450u : 0x1B5B4B0u))
            throw new InvalidDataException("Primary controller reset selects another MaxY global.");

        int clipRva = tc ? 0x1A8BE70 : 0x1A8CED0;
        uint clip = BinaryPrimitives.ReadUInt32LittleEndian(image.Raw(clipRva, 4));
        if (clip != 2) throw new InvalidDataException("Main source image projection profile differs.");
        ulong imageBase;
        using (var pe = new PEReader(new MemoryStream(exe, false)))
        {
            var data = pe.PEHeaders.SectionHeaders.Single(s => s.Name == ".data");
            int app = tc ? 0x1B63610 : 0x1B64670;
            if (data.VirtualAddress != (tc ? 0x1A21000 : 0x1A22000) || data.SizeOfRawData != 0x139600 ||
                data.VirtualSize != (tc ? 0xE03F6C : 0xE0412C) ||
                app < data.VirtualAddress + data.SizeOfRawData || app + 0x434 > data.VirtualAddress + data.VirtualSize ||
                maximumYFlag < data.VirtualAddress || maximumYFlag + 4 > data.VirtualAddress + data.SizeOfRawData)
                throw new InvalidDataException("Fresh Main App zero-fill or MaxY raw-image storage extent differs.");
            imageBase = pe.PEHeaders.PEHeader?.ImageBase ?? throw new InvalidDataException("Main Camera source lacks its native PE image base.");
        }
        // The accepted complete C880 reset zeroes ESI, preserves it across
        // its ABI calls, then stores ESI to the actual RIP-relative global.
        // The reset's explicit writer, not the PE zero-fill, supplies this0.
        return new(locale, profile.ExeHash, profile.TextHash, imageBase, clip,
            unchecked((int)mode), unchecked((int)modeOverride), fov, maximumYFlag, 0, windows);
    }
}
