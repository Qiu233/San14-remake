namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalDeferredEarlyShadowTargetRectangleSource LoadEarlyShadowTargetRectangleSource(
        string directory, string locale)
    {
        var profile = Profile(locale);
        var exe = File.ReadAllBytes(Path.Combine(directory, $"SAN14CE_{locale}.exe"));
        VerifyHash(exe, profile.ExeHash, "early shadow rectangle executable");
        var image = new NativeImage(exe);
        var text = image.DecodeText(out int textRva);
        VerifyHash(text, profile.TextHash, "early shadow rectangle decoded text");
        bool tc = locale == "TC";
        void Check(string name, int tcRva, int scRva, int count, string tcHash, string scHash)
            => VerifyHash(text.AsSpan((tc ? tcRva : scRva) - textRva, count), tc ? tcHash : scHash, name);
        Check("complete-opcode2-backend-decoder", 0x957AF0, 0x958BC0, 491,
            "B6461222F120A9CF690BE7FD87168FE3CE02460F0AE369039391F40DFEB59A9C",
            "528511B71B09FCDBAAD00E42E6CE476D19C921B917AC9A6E19DEF6D50877E926");
        Check("complete-viewport-publisher", 0x968000, 0x969180, 694,
            "DF4A85350DE58471B2D6F3EE15BF98396ECE9A5AE04514209B79E393D69AB565",
            "5BAD624474B32F46E66F8B4963158901318F10611FB3C463DE84D78651C979B0");
        Check("scissor-publisher-head", 0x95BD10, 0x95CDE0, 36,
            "7C482857C2D57280C4A6DBA47C3C92C16945C62DADD48C9ABA557103B8C14D38",
            "F6477981C60A42E5446CEDA88163CE681F1E16E9DF20A0E295B31124DFECC2ED");
        Check("scissor-publisher-body", 0x95BD34, 0x95CE04, 83,
            "8999113A705D709075FB7B2980991C1829AC250B9F4C86EC46B7C67D0D3C57A5",
            "8999113A705D709075FB7B2980991C1829AC250B9F4C86EC46B7C67D0D3C57A5");
        Check("scissor-publisher-tail", 0x95BD87, 0x95CE57, 43,
            "5B77E365894BFAD7565855F73B856D53883D145A2854EE51585B50868AF72715",
            "544720E56C82346104A81148D0BDE9B912E1B3E6A6C6DA74815A3FD530D70B7F");
        return new(locale, $"early17-opcode2/{locale}@{profile.ExeHash};" +
            $"decoder={(tc ? 0x957AF0 : 0x958BC0):X8};flag-byte5=true;" +
            "actual-row50-primary-surface50/54;viewport/scissor-full-target;" +
            "retained-frontend13C/17C-depth-and1D9-enable;no-extra-rectangle-package");
    }
}
