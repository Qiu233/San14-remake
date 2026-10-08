using System.Buffers.Binary;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapGameLightQualitySource LoadGameLightQualitySource(string directory,string locale)
    {
        var profile=Profile(locale);byte[] exe=File.ReadAllBytes(Path.Combine(directory,$"SAN14CE_{locale}.exe"));
        VerifyHash(exe,profile.ExeHash,"Game Light quality executable");var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,profile.TextHash,"Game Light quality decoded text");
        LoadedOriginalMapGameLightQualitySource recipe; (int Rva,int Bytes,string Sha)[] windows;
        switch(locale)
        {
            case "SC":
                recipe=new("SC",profile.ExeHash,GameResourceId.File("SAN14CE_SC.exe").ToString(),
                    0x3E61C0,0x01A48CB0,0x01A48D1C,0x003EC1C0,
                    0x003EC400,0x003EC236,0x164180,"86F511EB7D5F7B468BC68BA1A027DBC3621FACAED82327C60F9B5DE2F1034C6B",0);
                windows=[
                    (0x3EB680,320,"A2129871DFA8879BDD866B5E01B52308830D18334286A11F57A9BB0F61003078"),
                    (0x3E61C0,279,"6ACA29F1A63B4E4A2B16479267D01B99455EF725F569F1F80AE2F84E480CB003"),
                    (0x164180,149,"86F511EB7D5F7B468BC68BA1A027DBC3621FACAED82327C60F9B5DE2F1034C6B"),
                    (0x3EB110,90,"A3729F0B347F14B971D125D938EF4B6AA47413D6A884DA786DC4EE840D1A57C2"),
                    (0x3E63D0,2556,"E8AEB57B6E8B1F4C917E01C41733CDFA4CF90E19941C83356C0B0A0F1D4661AD"),
                    (0x800C80,5,"7A64C0E82E9028AED95F03EF90DCBAC4A71B6D42A9F131BADCA5034EC0D026EC"),
                    (0x3EC236,4,"6FFEFD5A9A063CDCC897E922DB1B401A2A6C8A8FD3C47617ED3EC2FE7DD373D8"),
                    (0x3EC1C0,32,"B253C72D9907EE60187138906004FEE8E2269C0071BDCAEB8A78CCAC4B0F0C91"),
                    (0x003EC400,4,"1FD54F10A226C782F9AC0B21C265FAAFA8069112BA3F052EEBB2093F49EAE432"),
                ];
                break;
            case "TC":
                recipe=new("TC",profile.ExeHash,GameResourceId.File("SAN14CE_TC.exe").ToString(),
                    0x3E5BC0,0x01A47C80,0x01A47CEC,0x003EBB80,
                    0x003EBDC0,0x003EBBF6,0x1643D0,"5E901D59891295D836CD7FE48F2CACA0EB3893250FC5C7113649B21AE30CF00C",0);
                windows=[
                    (0x3EB070,320,"B74FB572228758CC7C0FE6288FEC2D40FE35E6342638DA90FBFDA300D898A643"),
                    (0x3E5BC0,290,"CCBA024151E1BEC621A7B3E64FCEA8574571246008C4C90DD2CB3831B7660C94"),
                    (0x1643D0,149,"5E901D59891295D836CD7FE48F2CACA0EB3893250FC5C7113649B21AE30CF00C"),
                    (0x3EAB20,90,"E81F25B080D1C8A57960723DF1F0390AFEFFE0C5D6C9B915FBD47ABE4135EDC7"),
                    (0x3E5DE0,2556,"E8AEB57B6E8B1F4C917E01C41733CDFA4CF90E19941C83356C0B0A0F1D4661AD"),
                    (0x830350,5,"7A64C0E82E9028AED95F03EF90DCBAC4A71B6D42A9F131BADCA5034EC0D026EC"),
                    (0x3EBBF6,4,"6FFEFD5A9A063CDCC897E922DB1B401A2A6C8A8FD3C47617ED3EC2FE7DD373D8"),
                    (0x3EBB80,32,"00D6D538F47532F9305764FAEB38EA50B9C112C241A6B55F80418B82632C9A0D"),
                    (0x003EBDC0,4,"627CC87789BF8C802BA5175059A94407343A9CFF911452C6EB53BDE9D0A9CA4E"),
                ];
                break;
            default:throw new NotSupportedException("Game Light quality native source requires SC/TC.");
        }
        foreach(var w in windows)VerifyHash(text.AsSpan(w.Rva-start,w.Bytes),w.Sha,"Game Light quality actual source window");
        if(BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(recipe.Option18TableEntryRva-start,4))!=recipe.Option18ArmRva)
            throw new InvalidDataException("Actual graphics option18 dispatch differs from its primary64 getter.");
        return recipe;
    }
}

internal sealed record LoadedOriginalMapGameLightQualitySource(string Locale,string ExecutableSha256,string ExecutableUri,
    int InitializerRva,uint EnvironmentHolderRva,uint PrimaryQualityRva,int OptionGetterRva,int Option18TableEntryRva,
    int Option18ArmRva,int WriterRva,string WriterSha256,int FreshQuality)
{
    public string SourceProvenance=>$"{Locale}{InitializerRva:X}-primary64-zero;fresh-before-saved-config;"+
        $"ENV={EnvironmentHolderRva:X};option18={OptionGetterRva:X}/{Option18ArmRva:X};writer={WriterRva:X}@{WriterSha256};"+
        $"{ExecutableUri}@{ExecutableSha256}";
}
