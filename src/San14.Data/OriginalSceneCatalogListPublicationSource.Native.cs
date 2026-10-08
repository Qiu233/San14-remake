namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalSceneCatalogListPublicationSource LoadSceneCatalogListPublicationSource(
        string directory, string locale)
    {
        var profile = Profile(locale);
        byte[] executable = File.ReadAllBytes(Path.Combine(directory, $"SAN14CE_{locale}.exe"));
        VerifyHash(executable, profile.ExeHash, "App catalog publication executable");
        var image = new NativeImage(executable);
        byte[] text = image.DecodeText(out int textRva);
        VerifyHash(text, profile.TextHash, "App catalog publication decoded text");
        bool tc = locale == "TC";
        void Window(int tcRva, int scRva, int length, string tcHash, string scHash, string name)
            => VerifyHash(text.AsSpan((tc ? tcRva : scRva) - textRva, length), tc ? tcHash : scHash, name);
        Window(0x37E9C0, 0x37ED50, 191,
            "B2AF18F54E8043BB2214F8127AD35E50EC679FCDBCB0CC5A7A0B16E174DBC334",
            "B2AF18F54E8043BB2214F8127AD35E50EC679FCDBCB0CC5A7A0B16E174DBC334", "complete current catalog append producer");
        Window(0x37F050, 0x37F3E0, 280,
            "A2BE77B9B17E75D9A5BB5746FAB5FA5EB73099C94125F8C14815781D7B7E0D2A",
            "B2541A19BD250DAB031B397DC806DB6D48EAC8C824DCE621D407914E2EBD677F", "complete four-category Game destination selector");
        Window(0x3FE6D0, 0x3FEC00, 1544,
            "FCEBFCF22321CB6B963A634E5D67363788E3FCD45D26A80DD24D26FDBE8BA096",
            "6F88B786A5D2C7CE932552FC029454C5C3D02B1C5417F7B30CC3B31EBC9B7A87", "complete Game named-list bindings");
        int orderRva = tc ? 0x13AF990 : 0x13B0990;
        var order = image.Raw(orderRva, 16);
        if (!order.SequenceEqual(Convert.FromHexString("00000000010000000200000003000000")))
            throw new InvalidDataException("App catalog category order differs from the original four-entry constant.");
        return new(locale, $"AppCatalogListPublication/{locale}@{profile.ExeHash};" +
            "TC37F050/SC37F3E0->TC37E9C0/SC37ED50;" +
            "Game98:E65D70AE/A0:F0792D7D/C0:3E3E42CD/C8:012AAC2C;" +
            "category0..3/458[820]-then-4558[200]/ListV8-bulk-append;" +
            "current-entry20/18/10-required;not-static-Army-membership-or-GPU");
    }
}
