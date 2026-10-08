using System.Buffers.Binary;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static string ReadSelected383AmbientNativeSource(string sourceDirectory, string locale)
    {
        var profile = Profile(locale);
        byte[] exe = File.ReadAllBytes(Path.Combine(sourceDirectory, $"SAN14CE_{locale}.exe"));
        VerifyHash(exe, profile.ExeHash, "PS383 actual F103 ambient source executable");
        var image = new NativeImage(exe); byte[] code = image.DecodeText(out int textRva);
        VerifyHash(code, profile.TextHash, "PS383 actual F103 ambient decoded text");
        (int Rva, int Count, string Hash)[] windows = locale switch
        {
            "TC" => [(0xEACB82,112,"914EFC4818280C24A74F2AB75941ABC5B45405ABF744205D53B4E974E6DEA010"),
                (0xEADF47,119,"775ABAA03EFED253474FA23DD6D006184C821DECE4BFEE534B5B335FB5B88885"),
                (0xEADFFF,104,"67B38476BFB53446F210150E75211D370BED2BDFB41096FE0EDAD7684641FF11"),
                (0xE8FEE0,38,"F455568185C62389E5013148713705BCB1DB0F7D52AEFE5FCEBEEAEFF521AD8F"),
                (0xBFEA90,8,"8622F2826F6EFE5AB35C9BF54D69FAFFE7F12907ED0451AC4CE31906EEE8677B"),
                (0xBAAAA0,8,"F2F31F708BAC409FFC68AB28B6C5B7B7EEDF0564B482C1B8955DAB692380EAD8")],
            "SC" => [(0xEADB72,112,"914EFC4818280C24A74F2AB75941ABC5B45405ABF744205D53B4E974E6DEA010"),
                (0xEAEF37,119,"9819DF5056DA208CB54130526B50452FE4466B322E79D66558B28A9639E9140C"),
                (0xEAEFEF,104,"7C0B9306FA3D5FF3C0B7C2CDCABEFF2CBFCC3E84F1A91F3CD447995667288BFA"),
                (0xE90FD0,38,"F455568185C62389E5013148713705BCB1DB0F7D52AEFE5FCEBEEAEFF521AD8F"),
                (0xBFFF70,8,"8622F2826F6EFE5AB35C9BF54D69FAFFE7F12907ED0451AC4CE31906EEE8677B"),
                (0xBFA140,8,"F2F31F708BAC409FFC68AB28B6C5B7B7EEDF0564B482C1B8955DAB692380EAD8")],
            _ => throw new InvalidDataException("PS383 ambient source requires SC or TC.")
        };
        foreach (var w in windows) VerifyHash(code.AsSpan(w.Rva-textRva,w.Count),w.Hash,"PS383 ambient source window");
        int vt=locale=="TC"?0x13C4ED0:0x13C5CF0;
        foreach (var pair in new[] { (Slot:0x1A0,Target:locale=="TC"?0xBFEA90:0xBFFF70),
            (Slot:0x1A8,Target:locale=="TC"?0xBAAAA0:0xBFA140) })
            if (BinaryPrimitives.ReadUInt64LittleEndian(image.Raw(vt+pair.Slot,8))!=0x140000000UL+(uint)pair.Target)
                throw new InvalidDataException("CAppRenderer SkyAmbient getter/setter vtable changed.");
        return $"SAN14CE_{locale}.exe:{profile.ExeHash};decodedText={profile.TextHash};actual-F103-Q80-v1A0/v1A8;"+
            "View19B8-gate=E8FEE0;Sky168-bit30-clear/index8..9;encoded1B-current-wrapper";
    }
}
