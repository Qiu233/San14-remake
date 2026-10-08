using System.Buffers.Binary;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalDeferredEarlyShadowBiasSource LoadEarlyShadowBiasSource(string directory, string locale)
    {
        var profile = Profile(locale);
        byte[] exe = File.ReadAllBytes(Path.Combine(directory, $"SAN14CE_{locale}.exe"));
        VerifyHash(exe, profile.ExeHash, "Shadow sbias compact-cache executable");
        var image = new NativeImage(exe); byte[] text = image.DecodeText(out int textRva);
        VerifyHash(text, profile.TextHash, "Shadow sbias compact-cache decoded text");
        bool tc = locale == "TC";
        void Window(int tcRva, int scRva, int length, string tcHash, string scHash, string name)
            => VerifyHash(text.AsSpan((tc ? tcRva : scRva) - textRva, length), tc ? tcHash : scHash, name);
        Window(0xD0C8D0, 0xD0D600, 224,
            "3021CF09DDCF4F3DF066E297ECC150C1CE44276DCD0FACE3D98374451765EF7A",
            "A4D422F0EBBBFF14AAEB26C7AE6B1A6858A1D6778C8E8EA2D8AB801AB9D14A53", "complete compact pool fresh/reuse");
        Window(0xB92870, 0xB93840, 178,
            "1A9AA843F2300430D8E43FC21FEF6727E793EFB3D52E12A41AB7714EE535AD35",
            "C0FD92BECD2ECDBDE6A97676FCBBF187ED928852CC81D700FA4ADE18B658F532", "complete compact allocation/cache-R8 factory");
        Window(0x970730, 0x9718C0, 75,
            "4052BA80BCF751F3F2A31A43114B80602D9CD95FB8002A310C9A1D7E7F700027",
            "4052BA80BCF751F3F2A31A43114B80602D9CD95FB8002A310C9A1D7E7F700027", "complete prototype parameter-group forwarding leaf");
        Window(0x10571B0, 0x1058180, 125,
            "EC81ABEAA13678DC6BF0A7EB842E11F9157ED97C4BBBF8DB8BD1FCE797B20097",
            "80FF9A86F9BD4FBC2CC452C06CECACCBE1CE9242687FC288986CD169F1FBABBB", "complete compact direct constructor");
        Window(0x9FFCA0, 0xA00CA0, 1079,
            "F7416598AE72694B326412F07A2C7D33EF2376BF8FD57C534666DB636E7F2F08",
            "3893CE5B12B72E45C3B9635EF0DC8A871A26CFCD76CB94DA209B11F55CFD89DA", "complete Shadow base constructor");
        int prototypeRva = tc ? 0x14D31D0 : 0x14D3D28;
        var prototype = image.Raw(prototypeRva, 32);
        if (!prototype.SequenceEqual(Convert.FromHexString(tc
            ? "00000000000000000000000001000000C0314D4101000000D001A04001000000"
            : "00000000000000000000000001000000183D4D4101000000D011A04001000000")))
            throw new InvalidDataException("Original compact inherited0/local1 cache prototype differs.");
        int localRva = checked((int)(BinaryPrimitives.ReadUInt64LittleEndian(prototype[16..]) - 0x140000000UL));
        var local = image.Raw(localRva, 16);
        int nameRva = checked((int)(BinaryPrimitives.ReadUInt64LittleEndian(local) - 0x140000000UL));
        if (!local.SequenceEqual(Convert.FromHexString(tc
            ? "B8314D41010000000204000000000000" : "103D4D41010000000204000000000000")) ||
            !image.Raw(nameRva, 6).SequenceEqual("sbias\0"u8))
            throw new InvalidDataException("Original compact sole local sbias type2/width4 descriptor differs.");
        int baseRva = tc ? 0x9FFCA0 : 0xA00CA0;
        var body = text.AsSpan(baseRva - textRva, 1079);
        // +3D2 is the verified MOVAPS RIP-relative source writer. It targets
        // cache D0..DF immediately after the CPU138=R8 publication/checks.
        int loadAt = 0x3D2;
        if (!body.Slice(loadAt, 3).SequenceEqual(Convert.FromHexString("0F2805")) ||
            !body.Slice(loadAt + 7, 9).SequenceEqual(Convert.FromHexString("66410F7F80D0000000")))
            throw new InvalidDataException("Original Shadow sbias cache D0 vector writer differs.");
        int literalRva = checked(baseRva + loadAt + 7 + BinaryPrimitives.ReadInt32LittleEndian(body[(loadAt + 3)..]));
        byte[] literal = image.Raw(literalRva, 16).ToArray();
        VerifyHash(literal, "B1EB754D8066B041862B213B4C8AF9E2BCCA97E44BA05093CBD82364A42441A4", "source sbias constructor literal");
        return new(locale, $"SAN14CE_{locale}.exe@{profile.ExeHash};" + (tc
            ? "D0C8D0/014D31D0->B92870/cacheCPU+D30->970730->10571B0->9FFCA0;"
            : "D0D600/014D3D28->B93840/cacheCPU+D30->9718C0->1058180->A00CA0;") +
            $"prototype-local0-sbias-type2-width4;literal@{literalRva:X8};fresh-owned16-not-whole-cache-or-Shadow", literal);
    }
}
