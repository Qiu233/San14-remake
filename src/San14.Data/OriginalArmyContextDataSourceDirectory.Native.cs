namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static string LoadArmyContextDataNativeSource(string sourceDirectory,string locale)
    {
        var profile=Profile(locale);
        byte[] exe=File.ReadAllBytes(Path.Combine(sourceDirectory,$"SAN14CE_{locale}.exe"));
        VerifyHash(exe,profile.ExeHash,"Army Context Data executable");
        var image=new NativeImage(exe);byte[] code=image.DecodeText(out int codeRva);
        VerifyHash(code,profile.TextHash,"Army Context Data decoded text");
        bool tc=locale=="TC";
        void Window(string name,int tcRva,int scRva,int length,string tcHash,string scHash)
            => VerifyHash(code.AsSpan((tc?tcRva:scRva)-codeRva,length),tc?tcHash:scHash,name);
        Window("Material explicit C8 null constructor",0xD9B600,0xD9C2F0,284,
            "84647BAB3C3135AF6D1C52BC20B39541458340FA227E0DF23E2F5886DC18F420",
            "4C86571775147920550B849480FB2F9F22A05DBCCC6A5E6A787298B8C3465437");
        Window("Material actual Context Data lookup loader",0xD9B7B0,0xD9C4A0,2158,
            "E75A699CC409F82209AFE14D213835385BC181E688EEDBC425478E3A2CC10C6F",
            "7BB9691FCD0BDA54486CA714AFA172D36970C062D84AC30C8C2A1F4B3EC47CB6");
        Window("Registered resource B-tree query",0xB3B6F0,0xB3C860,283,
            "3288A03DCF415BE9A32254F579EB1E1BDBD2A3948E69D6C5E4F01DB6D4C1DCB1",
            "02FC876BFB11D35AAB25DAEDCF6C805831CA65FF63D93DF64C0FE04296C5A7D4");
        Window("Registered resource B-tree leaf query",0xB35080,0xB363B0,112,
            "9D4893E0D3A53EEF500417429C7F362C86BE06756BDDE0861ECA0086A54D9253",
            "9D4893E0D3A53EEF500417429C7F362C86BE06756BDDE0861ECA0086A54D9253");
        Window("Registered slot Context Data getter",0x14DD90,0x14DB40,223,
            "1D93BE2B54D129CC326385971F3C5578E48C16962C9A79B0B1F626AFFDE89451",
            "9C221B5E701971C44D913DCB139589C9173B47F76406D97F4FC8A6BCA2086744");
        Window("Context source type13 descriptor formatter",0xBF5FD0,0xBF74F0,220,
            "89FFFAE7F0E36A30536EFA6BE5A064E854A88133E31EF5047CD44766D37E3F38",
            "89FFFAE7F0E36A30536EFA6BE5A064E854A88133E31EF5047CD44766D37E3F38");
        Window("Material actual Context C8 ready publication",0xD9D3C0,0xD9E070,1589,
            "564BD0A08323769CBECA72EAFBD63E6AEE91A6622CAF822370E925F95E4840D3",
            "359C3805DEA0BDB44DB0D6D761EC66149FA04754DDA3F68E1EEC0933006486AE");
        return $"SAN14CE_{locale}.exe@{profile.ExeHash};D9B69B-explicit-C8-null;"+
            "D9B7B0-real-CPU18-lookup;B3B6F0/B35080-no-source-synthesis-on-MISS;D9D7DB-D9D83C-retain-null";
    }
}
