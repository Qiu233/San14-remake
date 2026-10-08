using System.Buffers.Binary;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalPatternCameraResourceSource LoadPatternCameraResourceSource(string directory, string locale)
    {
        var profile = Profile(locale);
        byte[] executable = File.ReadAllBytes(Path.Combine(directory, $"SAN14CE_{locale}.exe"));
        VerifyHash(executable, profile.ExeHash, "Pattern resource source executable");
        var image = new NativeImage(executable); byte[] text = image.DecodeText(out int textRva);
        VerifyHash(text, profile.TextHash, "Pattern resource source decoded text"); bool tc = locale == "TC";
        ReadOnlySpan<byte> Check(int tcRva, int scRva, int count, string tcHash, string scHash)
        {
            var window = text.AsSpan((tc ? tcRva : scRva) - textRva, count);
            VerifyHash(window, tc ? tcHash : scHash, "complete Pattern resource source function"); return window;
        }
        var loader = Check(0xC4F6F0,0xC50CF0,35591,
            "E0A5FB47D17D9F33E29ADE0F40DEC46285DED7672F4576BE01D2BBD3F97B42A3","1DBA09CFD207CF5D9EBD99E9D20C6561858493FA6736640554D20F5E520108A9");
        Check(0xCB3210,0xCB4900,1564,
            "FF7F2695EDAA1A466A0ABDE2CADF84CE91B1098A2E3CB7A5A36881A6A05EAB68","7ADB7B896A5ABCE91219DFB8D37DFA3F343154F351E590720C72D76E27ACECC2");
        Check(0xCAAEE0,0xCAC5B0,1137,
            "82D86E5044572EF770583A2E05EBFDDFF085503BE1B9B8F67534708751513DFC","82D86E5044572EF770583A2E05EBFDDFF085503BE1B9B8F67534708751513DFC");
        Check(0xB3B6F0,0xB3C860,283,
            "3288A03DCF415BE9A32254F579EB1E1BDBD2A3948E69D6C5E4F01DB6D4C1DCB1","02FC876BFB11D35AAB25DAEDCF6C805831CA65FF63D93DF64C0FE04296C5A7D4");
        Check(0xB34660,0xB35990,15,
            "7D85B11451C5249E56DD18A7AE56FC1957A973AF68A02444F8D60D45D8219D1F","7D85B11451C5249E56DD18A7AE56FC1957A973AF68A02444F8D60D45D8219D1F");
        Check(0xB39B70,0xB3AEF0,55,
            "A21E2C82DE90B03807DB2CB61ADBB8F18276F293A454C0143A199DFDD4E4E51E","A21E2C82DE90B03807DB2CB61ADBB8F18276F293A454C0143A199DFDD4E4E51E");
        Check(0xB34D90,0xB360C0,158,
            "91EC10096A0EFB5CB311F3605DE263C832F9B42DE93C7B37B31808C88A2F69D8","B0FC90546D3BC568525652B14BB27282E7820765D877F1147094A3C77A009B28");
        Check(0xB992A0,0xB9A260,16204,
            "103404D99316AF885F6D731C66838C603CAC197058D58DEB3A813D48BF9D640E","45CB30F91FAA7BACE057CC1012991CA29A16699EF2B5C4D66F7D222098C83E09");
        int at = 0xC57F60 - 0xC4F6F0;
        if (loader[at] != 0x41 || loader[at + 1] != 0xB8)
            throw new InvalidDataException("Pattern loader has another fallback instruction.");
        uint fallback = BinaryPrimitives.ReadUInt32LittleEndian(loader.Slice(at + 2, 4));
        if (fallback != 0x80F9DB7B) throw new InvalidDataException("Pattern loader fallback key differs.");
        return new(locale, $"PatternResources/{locale}@{profile.ExeHash};full-C4F6F0/CB3210/CAAEE0;" +
            "actual-B3B6F0-handle/B34660-Data/retain/temp-release;full-B992A0-preserves3-resources;Skylight-only-context3E4.low2=1;" +
            "raw2D30-to-K1680/raw2F18-to-K1888/raw2F20-to-K1890;Main-raw-bank-separate-from-selected-embedded", fallback);
    }
}
