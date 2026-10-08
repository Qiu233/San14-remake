using System.Buffers.Binary;
using System.Collections.ObjectModel;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalArmyMaterialParameterNativeSource LoadArmyMaterialParameterNativeSource(
        string sourceDirectory,string locale)
    {
        var profile=Profile(locale);
        byte[] exe=File.ReadAllBytes(Path.Combine(sourceDirectory,$"SAN14CE_{locale}.exe"));
        VerifyHash(exe,profile.ExeHash,"Army material parameter executable");
        var image=new NativeImage(exe);byte[] code=image.DecodeText(out int codeRva);
        VerifyHash(code,profile.TextHash,"Army material parameter decoded text");
        bool tc=locale=="TC";var windows=new Dictionary<string,string>(StringComparer.Ordinal);
        void Window(string name,int tcRva,int scRva,int length,string tcHash,string scHash) {
            string hash=tc?tcHash:scHash;
            VerifyHash(code.AsSpan((tc?tcRva:scRva)-codeRva,length),hash,name);windows.Add(name,hash);
        }
        Window("MaterialParameters15",0xD9C840,0xD9D4F0,2148,
            "26AC4D6557A71CC4D3E091E7C3839C45823EC8C2A2B6BE150701B30622F5C657",
            "EBDD3E87963BD9003DCD6AD13E7A510E5841F81F5527B435B1DFD52559892926");
        Window("MaterialParameter12",0xD9C5D0,0xD9D280,624,
            "9A882FDF18846E3CEE43834495E8D79D6F6981A5598009851FE320DD8FCD3470",
            "A04D7BE4A48ECD52A5638ABFB562E0B54214550511E0E66297B7013D1568234C");
        Window("ResourceReferenceRetainEnqueue",0xD60030,0xD60D70,102,
            "A230D03BF53743E7325EECF4785015A92D789AA0B89ADBF0BA55ED69E82BE881",
            "A230D03BF53743E7325EECF4785015A92D789AA0B89ADBF0BA55ED69E82BE881");
        Window("MaterialFilenameContext13",0xD9D0B0,0xD9DD60,479,
            "3309F646ED2E03670A0B2A3C491A2F33C6D5841B1FFA7E95B99C107ACBADC38E",
            "DBF5772A19481A2945B5BC36B7B9C28598D4B73F1393BB32AAE20A893F09380A");
        Window("MaterialReadyActualCallers",0xD9D3C0,0xD9E070,1589,
            "564BD0A08323769CBECA72EAFBD63E6AEE91A6622CAF822370E925F95E4840D3",
            "359C3805DEA0BDB44DB0D6D761EC66149FA04754DDA3F68E1EEC0933006486AE");
        Window("MaterialModeParameterFields",0xD9D290,0xD9DF40,293,
            "22639C64C3F5AD43773F18ABB346ABE1BA3F92352798BB133E5C7835C611B619",
            "22639C64C3F5AD43773F18ABB346ABE1BA3F92352798BB133E5C7835C611B619");
        float Scalar(int tcRva,int scRva,string hash) {
            var bytes=image.Raw(tc?tcRva:scRva,4);VerifyHash(bytes,hash,"Material ready specular W");
            return BinaryPrimitives.ReadSingleLittleEndian(bytes);
        }
        float regular=Scalar(0x1343BE0,0x1344BE0,"D88C86F15BBEA365D658AD95A81D45367C465F7AF6F7264FB077F01747DDC77D");
        float target=Scalar(0x1343B98,0x1344B98,"E00E5EB9444182F352323374EF4E08EBCB784725FDD4FD612D7730540B3E0C8C");
        return new(locale,profile.ExeHash,profile.TextHash,new ReadOnlyDictionary<string,string>(windows),regular,target);
    }
}
