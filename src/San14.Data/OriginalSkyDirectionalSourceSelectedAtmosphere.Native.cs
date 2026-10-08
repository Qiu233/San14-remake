using System.Buffers.Binary;
namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static string ReadDirectionalSkySourceSelectedRecipe(string sourceDirectory,string locale)
    {
        var profile=Profile(locale);byte[] exe=File.ReadAllBytes(Path.Combine(sourceDirectory,$"SAN14CE_{locale}.exe"));
        VerifyHash(exe,profile.ExeHash,"Sky source-selected feature/atmosphere executable");
        var image=new NativeImage(exe);byte[] code=image.DecodeText(out int textRva);
        VerifyHash(code,profile.TextHash,"Sky source-selected decoded text");
        (int Rva,int Bytes,string Hash)[] windows=locale switch
        {
            "TC"=>[
                (0xD3AA5C,185,"463DE0823FD2366197303E32BFC0B4480BD58B3EDAAC36A348F465521CDA25AF"),
                (0xD3AE2D,112,"B984CF1985CF16F3A772F8045C6D17534FB06ACCDD8653704EE6107E061174B6"),
                (0xD3AEC8,181,"E8C702084089EF12D6810F0A9032BDA17A8B1768BF21932308C23C944E0F7F58"),
                (0x980850,74,"6AEC92082A0A9CEB316D8933D0F788346D9B006197E01226BF567A853CCD7ACD"),
                (0x9808A0,94,"68021BFE53C1B77A486C1BE795E01837919E41116929E9C6D0F14BA94CC4E430"),
                (0x97EAF7,59,"1BB90EAF4C3693EEBE0A0FF74BE5491D43407A4917B7934357C6FE26E6EFF8C1"),
                (0xC40B86,12,"FB4F0A097FECCF266171E8D2A9787E7B4810F749CFBB5F328FB2B806871A1370"),
                (0x9768DB,58,"B41B02758734E1A701FCC1BDA1EBC1F6A5CDB413561E837F886841EC41EB56B6"),
                (0x97C1F0,1389,"1EDE800F540A5C9875209E5D85E4B6E3D8AF9CE10970BE68D149A4FA110EF7E9"),
                (0x97CCE0,645,"88FB96A0780672231F3256CDFC92D724C7ABF2D9547272742C67826468E7B87F")],
            "SC"=>[
                (0xD3B78C,185,"463DE0823FD2366197303E32BFC0B4480BD58B3EDAAC36A348F465521CDA25AF"),
                (0xD3BB5D,112,"B984CF1985CF16F3A772F8045C6D17534FB06ACCDD8653704EE6107E061174B6"),
                (0xD3BBF8,181,"E8C702084089EF12D6810F0A9032BDA17A8B1768BF21932308C23C944E0F7F58"),
                (0x9819E0,74,"6AEC92082A0A9CEB316D8933D0F788346D9B006197E01226BF567A853CCD7ACD"),
                (0x981A30,94,"B81D69B9400582D02EED5BA7DA1B45F62AD7A09268E0551C0523B6F34AE3914E"),
                (0x97FCC7,59,"8DE673B80CE090BE549D2CC568D5D565F2EF58D9D9C43F492DDA3E47CF1F0326"),
                (0xC42236,12,"FB4F0A097FECCF266171E8D2A9787E7B4810F749CFBB5F328FB2B806871A1370"),
                (0x977A6B,58,"B41B02758734E1A701FCC1BDA1EBC1F6A5CDB413561E837F886841EC41EB56B6"),
                (0x97D380,1389,"85BE28C72535BEC3CB35416024956FF2F31C3E84F00421BC27D4FD31550B1694"),
                (0x97DE70,645,"204B8A5DFA4647962C21805FFCA54C216E232DDBA41749A6506894FD1884169A")],
            _=>throw new InvalidDataException("Source-selected Sky requires the verified SC/TC pair.")
        };
        foreach(var w in windows)VerifyHash(code.AsSpan(w.Rva-textRva,w.Bytes),w.Hash,"Sky source-selected writer/math window");
        int vt=locale=="TC"?0x16E4000:0x16E4C38,matcher=locale=="TC"?0x980850:0x9819E0;
        if(BinaryPrimitives.ReadUInt64LittleEndian(image.Raw(vt+0x28,8))!=0x140000000UL+(uint)matcher)
            throw new InvalidDataException("Source-selected Sky2 actual VT28 matcher identity changed.");
        int one=locale=="TC"?0x1343B98:0x1344B98,epsilon=locale=="TC"?0x1343B54:0x1344B54;
        if(BinaryPrimitives.ReadUInt32LittleEndian(image.Raw(one,4))!=0x3F800000||
            BinaryPrimitives.ReadUInt32LittleEndian(image.Raw(epsilon,4))!=0x37480000)
            throw new InvalidDataException("Source-selected package/Fog arithmetic literal changed.");
        return $"san14://file/SAN14CE_{locale}.exe@{profile.ExeHash};D3AE2D-request48=sourceL160;"+
            "D3AEC8-copyL96->M96/D3AF58-Main1EC->M84;980850-renderer-match/97EAF7-Generic-mode-profile;"+
            "source97C1F0-mode7-finalFog/976909->97CCE0-pendingScat/97690E-clear121;not-binding-commit-or-GPU";
    }
}
