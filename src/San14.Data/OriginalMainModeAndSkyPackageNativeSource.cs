using System.Buffers.Binary;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static string ReadMainModeNativeRecipe(string sourceDirectory,string locale)
    {
        var profile=Profile(locale);byte[] exe=File.ReadAllBytes(Path.Combine(sourceDirectory,$"SAN14CE_{locale}.exe"));
        VerifyHash(exe,profile.ExeHash,"Main mode source executable");var image=new NativeImage(exe);
        byte[] code=image.DecodeText(out int textRva);VerifyHash(code,profile.TextHash,"Main mode decoded text");
        (int Rva,int Count,string Hash)[] windows=locale switch{
            "TC"=>[(0x10CF0,265,"894A90660C581C01D85F3D8A71E639E18B662B87C6804AF0D15B4059B96C6952"),
                (0x15242B,36,"CFA53ECD8438F032C24C142BD1920DAAECC1045659978E3493162D10B6F236AD"),
                (0x17CFC,72,"7D948E1340E0FC07949FB854CAC6F7C592DCB60626CB946BFEC775B9B759B2EF"),
                (0x17E50,51,"DA6174BE849AC7E3B83FDB554F11AFE5297AABF217A19CB6CF9FA85313655DC5"),
                (0xED03,39,"CACB11E12AEC729BC3E02238246059C4F84B7217F4D4158636847D465817EACB")],
            "SC"=>[(0x10CF0,265,"D94A8CBBBCE0725CB8DD4C5095C1D7B43221C1CBF0773D7B391B3C4D1BF56A0D"),
                (0x1521DB,36,"125BDAD81A7FCDF563FD1FE4989555BF59BAE2EBF4CBD2F79BA0E0F2A0C26107"),
                (0x17CFC,72,"83468332B4CF7BF50C6486A130DEB4D7473DE29A695A826A26B6B3AB4C27DEE8"),
                (0x17E50,51,"70CA17BA9312E930D48DFFD4A08A3436B5C34E4113D59C6EDF940E482CE7AAE6"),
                (0xED03,39,"A89BD9B3ADF0AB8791174506BE32286DCA11C2B85DFECC65D585B7F28CA78103")],
            _=>throw new InvalidDataException("Main mode requires the pinned SC or TC source.")};
        foreach(var w in windows)VerifyHash(code.AsSpan(w.Rva-textRva,w.Count),w.Hash,"Main mode native source window");
        int d=locale=="SC"?0x1000:0;
        foreach(var v in new[]{(0x1343C00,0x40200000u),(0x1343C18,0x40600000u),(0x1343C1C,0x40900000u),(0x1343C24,0x40B00000u),(0x1343C8C,0x461C4000u)})
            if(BinaryPrimitives.ReadUInt32LittleEndian(image.Raw(v.Item1+d,4))!=v.Item2)
                throw new InvalidDataException("Main mode threshold/ECb0 denominator source literal changed.");
        return $"{locale}@{profile.ExeHash};source10CF0-reset->17D3D-classifier;thresholds2.5/3.5/4.5/5.5;primary-denominator10000";
    }
    internal static string ReadSelectedSkyPackageNativeRecipe(string sourceDirectory,string locale)
    {
        var profile=Profile(locale);byte[] exe=File.ReadAllBytes(Path.Combine(sourceDirectory,$"SAN14CE_{locale}.exe"));
        VerifyHash(exe,profile.ExeHash,"Selected Sky package source executable");var image=new NativeImage(exe);
        byte[] code=image.DecodeText(out int textRva);VerifyHash(code,profile.TextHash,"Selected Sky package decoded text");
        var p=locale switch{
            "TC"=>(0xD3ADEB,"C5EDE1CAC30B37B0B91E195C0559CDF798533FD0D298A97F2D4942F3232F98BD"),
            "SC"=>(0xD3BB1B,"67BFC887200F91D3CBCB1D99D2FEC971E0E60C134FEC5E98C445D591C1BA4030"),
            _=>throw new InvalidDataException("Selected Sky package requires SC or TC.")};
        VerifyHash(code.AsSpan(p.Item1-textRva,360),p.Item2,"D3A850 actual row0/ambient0/hemi0/Fog96 package writer");
        int delta=locale=="SC"?0x1190:0;
        foreach(var w in new[]{(0x97A90F,179,"CAAE80E1889FB3642F493E5B6044E7AD32C72D257C84C07115BDCEC98F0DB1FC"),
            (0x97CBB9,29,"11B0EEDE18528A6167D2DCFE2ED609DD1DB3E84C2460A16BB9B0573028206538"),
            (0x9768AE,103,"8C49ABF3D538F25DDCE07CA476368636A1ACE780F685FC4749AB5D50EF611DBC")})
            VerifyHash(code.AsSpan(w.Item1+delta-textRva,w.Item2),w.Item3,"Sky fresh Generic/pending source");
        VerifyHash(code.AsSpan((locale=="TC"?0x97EAF7:0x97FCC7)-textRva,59),
            locale=="TC"?"1BB90EAF4C3693EEBE0A0FF74BE5491D43407A4917B7934357C6FE26E6EFF8C1":"8DE673B80CE090BE549D2CC568D5D565F2EF58D9D9C43F492DDA3E47CF1F0326","Sky selected row pass-begin source");
        VerifyHash(code.AsSpan((locale=="TC"?0x97C1F0:0x97D380)-textRva,1552),
            locale=="TC"?"B011FBE01B38A13C5F02D8567A8DB5577E3BCA4C600E5290FBD0A30B109F7CC7":"F4056F74FFCEDBE5BD03C33D42C6D60377CC1C8BADD2D6403BBD4D85FA232E70","complete source finalFog writer");
        return $"{locale}@{profile.ExeHash};D3A850-package={p.Item1:X8}/360@{p.Item2};row0=L0..1F;ambient30=0;hemiCount=0/hemiPointer=null;Fog40=copyL150..1AF";
    }
}
