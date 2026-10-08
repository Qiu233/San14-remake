namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalArmySceneListPublicationSource LoadArmySceneListPublicationSource(
        string directory, string locale)
    {
        var profile = Profile(locale);
        byte[] executable = File.ReadAllBytes(Path.Combine(directory, $"SAN14CE_{locale}.exe"));
        VerifyHash(executable, profile.ExeHash, "Army scene publication executable");
        var image = new NativeImage(executable);
        byte[] text = image.DecodeText(out int textRva);
        VerifyHash(text, profile.TextHash, "Army scene publication decoded text");
        bool tc = locale == "TC";
        void Window(int tcRva, int scRva, int length, string tcHash, string scHash, string name)
            => VerifyHash(text.AsSpan((tc ? tcRva : scRva) - textRva, length), tc ? tcHash : scHash, name);
        Window(0x44C7B0, 0x44C9B0, 1499,
            "01541321FDD44A742619FB6AF3235A363123D65D1177DE2C15D4AA9D2D86FECC",
            "89A2522990A1657EA0CE3F9825DD28A21E4619DC1FF72EDEF714784BCBD9C49A", "complete current Game98 Unit scene caller");
        Window(0x1C7270, 0x1C7270, 464,
            "32A3A08C1E2871D44C9C3BB06DD1B74844148269505E6805695082E35E2956E2",
            "5F282C7D8FD8CDBFF51EDF4F1122B4E0A2A836C816523AEE6D11216F7207233B", "complete current UnitManager append producer");
        Window(0x1C6FD0, 0x1C6FD0, 266,
            "0DCFEC08B52941644C69346182E5EFF0B9460D780BFD413E4FDFB2A472F15BA2",
            "0DCFEC08B52941644C69346182E5EFF0B9460D780BFD413E4FDFB2A472F15BA2", "complete Army descriptor publication order");
        Window(0x1C71F0, 0x1C71F0, 119,
            "2EB88952D010E487702B2A11732394B994D9FD97049F280465A257C320521A54",
            "243A280E4FDCEF03D1848EFCA06DABC64B6E7F00CADC0399681A3C064DB846BB", "complete descriptor current group append");
        Window(0x1C6EF0, 0x1C6EF0, 213,
            "EDC2F47F0F7C8EE4FC5E57B0475F2492CB8BF8BCFF76B4CBDB388F5C4B686988",
            "0ADBAA59E46242CFCD7BB9121E25272CA2D888DAE621E1524246A1797CD46726", "complete group direct or batch route");
        Window(0x1C5040, 0x1C5040, 91,
            "16D135705D867CAC2D08AEAE964A628BA4CFD48377FAFDDB215173220ADF21BA",
            "16D135705D867CAC2D08AEAE964A628BA4CFD48377FAFDDB215173220ADF21BA", "complete source group animation batch selector");
        Window(0x1C2010, 0x1C2010, 101,
            "4AD382908B9B712AB4E023531E5069B92CD12EF638444B5383B9DCA548A87D54",
            "B8B66A5EE0BEE181FBA5D235DAD6436D26A32893F0981F2DE071281E8EE5250E", "complete retained batch ModelCPU request");
        Window(0x1C6E80, 0x1C6E80, 106,
            "19963E50A96BD0DDA05D090F888F4EEA837DE546DFE485417E5D089DB97FFC8C",
            "19963E50A96BD0DDA05D090F888F4EEA837DE546DFE485417E5D089DB97FFC8C", "complete seven batch ModelData append");
        Window(0x3FE6D0, 0x3FEC00, 1544,
            "FCEBFCF22321CB6B963A634E5D67363788E3FCD45D26A80DD24D26FDBE8BA096",
            "6F88B786A5D2C7CE932552FC029454C5C3D02B1C5417F7B30CC3B31EBC9B7A87", "complete Game98 E65D70AE source binding");
        int childGetterRva = tc ? 0xBCE3A0 : 0xFD9260;
        if (!text.AsSpan(childGetterRva - textRva, 5).SequenceEqual(Convert.FromHexString("488B4130C3")))
            throw new InvalidDataException("Army InstanceCPU child Data getter differs from its source field30 load.");
        return new(locale, $"ArmySceneListPublication/{locale}@{profile.ExeHash};" +
            "TC44C7B0/SC44C9B0->Game98.Data20->1C7270->ArmyV28:1C6FD0;" +
            "1C71F0->1C6EF0/directV8-or-1C2010/1C5040->D9F640;" +
            "then1C6E80/seven3CB8+3CF0;destinationE65D70AE;" +
            "current-group-state/batch-owners/aux580-required;not-ready-shadow-population");
    }
}
