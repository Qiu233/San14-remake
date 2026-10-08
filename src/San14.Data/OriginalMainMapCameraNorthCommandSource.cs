using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Numerics;

namespace San14.Data;

/// <summary>The original Main toolbar node42/index1 command3. The request
/// changes yaw through the existing timed controller; focus, distance and
/// pitch use the original unchanged-target sentinels. This source does not
/// observe the original game's current UI or select the active camera.</summary>
public sealed class LoadedOriginalMainMapCameraNorthCommandSource
{
    private readonly OriginalMainMapCameraControllerRequest request;
    internal LoadedOriginalMainMapCameraNorthCommandSource(string locale, string exeHash, string textHash,
        int node, int icon, int duration, double yaw, double unchangedTarget, int selector,
        Dictionary<string,string> windows)
    {
        Locale = locale; ExecutableSha256 = exeHash; DecodedTextSha256 = textHash;
        LayoutNodeIndex = node; IconIndex = icon;
        request = new(duration, Vector4.Zero, unchangedTarget, yaw, unchangedTarget, selector);
        SourceWindows = new ReadOnlyDictionary<string,string>(windows);
    }
    public string Locale { get; }
    public string ExecutableSha256 { get; }
    public string DecodedTextSha256 { get; }
    public int ButtonIndex => 1;
    public int LayoutNodeIndex { get; }
    public int IconIndex { get; }
    public int NativeCommand => 3;
    public IReadOnlyDictionary<string,string> SourceWindows { get; }
    public string SourceEvidence => $"{Locale}@{ExecutableSha256};decoded-text={DecodedTextSha256};" +
        "CMainTopBtnGroupLayout.Init-node42/index1;CGameLayout.V08-command3;" +
        "CGameState44F060-native9CC0-request;primary50.V10=142A0;500ms-yaw-only-selector1";

    /// <summary>Use when the current application processes its actual north
    /// action. Existing request-mode and current controller bounds still apply.
    /// No EXE access, hash, native replay or inferred current pose occurs here.</summary>
    public OriginalMainMapCameraControllerRequest CreateNorthRequest() => request;
}

public static class OriginalMainMapCameraNorthCommandSource
{
    public static LoadedOriginalMainMapCameraNorthCommandSource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadMainMapCameraNorthCommandSource(repository.SourceDirectory, locale);
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMainMapCameraNorthCommandSource LoadMainMapCameraNorthCommandSource(string directory, string locale)
    {
        var profile = Profile(locale); bool tc = locale == "TC";
        byte[] exe = File.ReadAllBytes(Path.Combine(directory, $"SAN14CE_{locale}.exe"));
        VerifyHash(exe, profile.ExeHash, "Main north command executable");
        var image = new NativeImage(exe); byte[] code = image.DecodeText(out int textRva);
        VerifyHash(code, profile.TextHash, "Main north command decoded text");
        var windows = new Dictionary<string,string>(StringComparer.Ordinal);
        void Accept(string name, int tcRva, int scRva, int length, string tcHash, string scHash)
        {
            string hash = tc ? tcHash : scHash;
            VerifyHash(code.AsSpan((tc ? tcRva : scRva) - textRva, length), hash, name);
            windows.Add(name, hash);
        }
        Accept("TopGroupConstructor", 0x87E9D0, 0x87F420, 220,
            "109DB3035D324380D02C5859611ECB2348D63BDB26ADB56C9917C9BE3E504795",
            "2097B587F82165E080BD3B4768C55900AD8BEAE453AADD7C595C4E361816D9C2");
        Accept("TopGroupInitialize", 0x886610, 0x887060, 2010,
            "406FAAA903B6D62BE4CEE3C9D654299A2D438ECB0DED53945C622313D25D835C",
            "C7BAE965A18F448340F8493F97394859D385DCB5D9EC89EF64E0CBFDBC0E336B");
        Accept("GameLayoutInitialize", 0x444850, 0x444A40, 4691,
            "E39268FE003B162611E4761A1A1284B924222F1D21A78EF23C842D937AAF021B",
            "827962476AF439D254AED036CEEC5D79063DD5A30AFF815A278C5E3263718FD5");
        Accept("GameLayoutInput", 0x44EB30, 0x44ED30, 1056,
            "A37C19D90F2124BF151419357BA47B09204E308BC33CC5701B3010AA1209D278",
            "CED4D039D7F8DC9D14E2E21994D75A76F2DEAFE28DDD7B1407E0039354D6EA8C");
        Accept("GetTopButton", 0x882DF0, 0x883840, 20,
            "44D1543B13B0A14CF3B08E8402E201FEC18CBE5A894D54BDFC1A73242084C026",
            "44D1543B13B0A14CF3B08E8402E201FEC18CBE5A894D54BDFC1A73242084C026");
        Accept("GetCurrentInputTarget", 0x86C710, 0x86D090, 20,
            "0DB74BA0548576516A3C044E4AA092B2EBAFC4581888B3538536E8D0BB90953E",
            "0DB74BA0548576516A3C044E4AA092B2EBAFC4581888B3538536E8D0BB90953E");
        Accept("GameStateFrame", 0x44C7B0, 0x44C9B0, 1499,
            "01541321FDD44A742619FB6AF3235A363123D65D1177DE2C15D4AA9D2D86FECC",
            "89A2522990A1657EA0CE3F9825DD28A21E4619DC1FF72EDEF714784BCBD9C49A");
        Accept("ConsumeMainCommand", 0x44F060, 0x44F260, 708,
            "9885621ACB3463E73E4CE240C69FD4981FFD6EC4FDB70913FEEA050096CFD106",
            "19C0FE03F1525D0113A7967B572A040431E5D8DFA95C7B5F69DF0DB559FE4C7A");
        Accept("InitializeMainRequest", 0x9CC0, 0x9CB0, 117,
            "DDC9C990D5E659167CCD81FBF931100557014B122DEFB10745A88B4CD0086EDE",
            "DDC9C990D5E659167CCD81FBF931100557014B122DEFB10745A88B4CD0086EDE");
        Accept("SubmitPrimaryRequest", 0x142A0, 0x142A0, 95,
            "20F79FABDF51ED4051F1CB337E14DF97FF57B99ABED4639945641F61939E8D94",
            "681D04428CE9B4C4F0D6F878571729A027EFE7AC77E1BDE7D3D87A253C00424D");
        Accept("ButtonCommandJumpTable", 0x44EF50, 0x44F150, 24,
            "FDDC5BED823420328950F5BD1C4C9464EE6948E7B0326970A8DA580DC3A3F334",
            "9A6F102A29531BA664BA04E6072FE82EDBF2A449D4F6287104F0FB6B44EA988E");
        Accept("MainCommandJumpTable", 0x44F324, 0x44F524, 24,
            "5E714DF3B2E26E1693B43455D4AAC568ACFEF6249EF9F2A23243D79DC0DFDCD3",
            "EFC00944BBA1AEA7258B01D3E0073C03AAD2E96E6F1D9D6323B63B843E1C36D7");

        byte[] ReadRaw(string name, int tcRva, int scRva, int length, string hash)
        {
            byte[] raw = image.Raw(tc ? tcRva : scRva, length).ToArray();
            VerifyHash(raw, hash, name); windows.Add(name, hash); return raw;
        }
        byte[] nodes = ReadRaw("TopNodeIndexPrefix", 0x1477CA0, 0x1478AC0, 16,
            "6217691F5348B6FD395259CC0B54DC1E9AB78FA9975CF15035B4C0EB0416F896");
        byte[] icons = ReadRaw("TopIconIndexPrefix", 0x1477E20, 0x1478C40, 16,
            "64069142998E59677301F33C60FEE122CC8D3FB559803AD56AA190A4C6AEBCC8");
        byte[] yaw = ReadRaw("NorthYaw", 0x1343BF0, 0x1344BF0, 8,
            "387688F9240B7A78393541098B24AEFE3E8D688FE35170AF9C8CABCDC44F4097");
        _ = ReadRaw("TopLayoutName", 0x1477408, 0x1478228, 27,
            "8E1482B1F1B538A34F6FC98F15B7832E084481C549BD262BE1EA46AA51E27815");
        _ = ReadRaw("NorthButtonName", 0x1490F60, 0x1491E10, 18,
            "2E7F5C1531349F299CECD6780541734FBA237CB0AB0A42EDC5B8DE3DB3997C81");
        _ = ReadRaw("NorthIconName", 0x14A1370, 0x14A2220, 19,
            "AB89038A7C75A4962E3C51B3FBD99FBDAF0A2EDD2D9F3A09413604443B56C417");

        // Extract the admitted actual command3 immediates and request sentinel,
        // rather than using new double Math.PI or a current-pose approximation.
        int consumer = tc ? 0x44F060 : 0x44F260, initializer = tc ? 0x9CC0 : 0x9CB0;
        int duration = BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(consumer + 0x135 - textRva, 4));
        int selector = BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(consumer + 0x149 - textRva, 4));
        double unchanged = BinaryPrimitives.ReadDoubleLittleEndian(code.AsSpan(initializer + 0x32 - textRva, 8));
        return new(locale, profile.ExeHash, profile.TextHash,
            BinaryPrimitives.ReadInt32LittleEndian(nodes.AsSpan(4, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(icons.AsSpan(4, 4)), duration,
            BinaryPrimitives.ReadDoubleLittleEndian(yaw), unchanged, selector, windows);
    }
}
