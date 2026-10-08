using System.Buffers.Binary;
namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static string ReadSky2MoonGeometryRecipe(string sourceDirectory,string locale)
    {
        var profile=Profile(locale);
        byte[] exe=File.ReadAllBytes(Path.Combine(sourceDirectory,$"SAN14CE_{locale}.exe"));
        VerifyHash(exe,profile.ExeHash,"Moon geometry executable");
        var image=new NativeImage(exe);byte[] code=image.DecodeText(out int textRva);
        VerifyHash(code,profile.TextHash,"Moon geometry decoded text");
        (int Start,int Bytes,string Hash)[] windows=locale switch
        {
            "TC"=>[
                (0x0118E43D,107,"C164C719E4598FC6AA83D9404B2304D387E603E40DA107343F0311AF7667E588"),
                (0x0118E4A8,229,"E27D74C4BAE78E0E9EEB9B5F3340F69868CD75E2A8CFC887C2BCF2DC6BA42940"),
                (0x0118E6B0,235,"F237DD06DE0970EA74A6A8BD6198AAF60CAE609836F818D63097A2E275C48636"),
                (0x0118E7B0,249,"284F0A2D6009FC4697326BB1F5A712D2BFDED16B8A8E5FF50ACB888F255DFB15"),
                (0x0118E8A9,50,"D41832D6DCDE900F66E14977C25E1189258DE88F34D0888161CB5786D83AEAE2"),
                (0x0118E921,28,"CB18218750AF1A131465F760FA82619B9757B6C1F660CFB1B370ACF8F1DDFDC8"),
                (0x003F71B0,197,"205AC8577F5F7EB142958110C21376FFDD596174F8D5E7DDF022D06E619BD187"),
                (0x003F7280,396,"7E3502C5D036FC955740CAA044216C924F57F28194B39D55B466ECDD8EFA0DEF"),
                (0x0101E910,621,"ED75428433CF331163CA7DA9F90B1771758581E50A60DFD7B607D2DBA53D8217"),
            ],
            "SC"=>[
                (0x0118F46D,107,"E3E81D22CA71D23AE2FB87AC3DD66E009DF5C716D5868CB07FE955C5E63E728B"),
                (0x0118F4D8,229,"EE4BBB192CEBF402106F2A0D966B8DB14CDBBB7467B793434679C94F105B850C"),
                (0x0118F6E0,235,"7575690FA1024FFC28791A31D463913696D5F14CAA5F1574EDC3A06A9E6DF0FA"),
                (0x0118F7E0,249,"C011F76ACB9967ACBA0A5957883B5D323D8A49B5ADF68962AFCF96EE83C08A70"),
                (0x0118F8D9,50,"D41832D6DCDE900F66E14977C25E1189258DE88F34D0888161CB5786D83AEAE2"),
                (0x0118F951,28,"CB18218750AF1A131465F760FA82619B9757B6C1F660CFB1B370ACF8F1DDFDC8"),
                (0x003F77F0,197,"F986568C9A09AB97BB29CE92E8C61252A5F4DE3020744B84B65B712980931E9C"),
                (0x003F78C0,396,"93B399E5D90035C615F666A85D664E3C2C87BE5E213CF8A6E869BABF87EFC617"),
                (0x0101F810,621,"3B8583D26D26F836A0665D185CE61DB94C537383C26C6E8786A64108476566A7"),
            ],
            _=>throw new InvalidDataException("Moon geometry source requires verified SC/TC.")
        };
        foreach(var w in windows)VerifyHash(code.AsSpan(w.Start-textRva,w.Bytes),w.Hash,"Moon geometry native writer/helper");
        (int TC,int SC,string Hex)[] constants=[
            (0x0173E1F8,0x0173F0B8,"1729143C"),
            (0x01343B80,0x01344B80,"0000003F"),
            (0x01698314,0x01699014,"00007AC4"),
            (0x013C440C,0x013C522C,"83F9223E"),
            (0x01343B98,0x01344B98,"0000803F"),
            (0x01343C2C,0x01344C2C,"DB0FC940"),
            (0x01343BC0,0x01344BC0,"DB0FC93F"),
            (0x01343C04,0x01344C04,"DB0F4940"),
            (0x01343CDC,0x01344CDC,"000080BF"),
            (0x013C4430,0x013C5250,"DB0FC9BF"),
            (0x01343D00,0x01344D00,"DB0F49C0"),
            (0x013C43F4,0x013C5214,"8EB83836"),
            (0x013C43EC,0x013C520C,"5B36CD32"),
            (0x013C43F0,0x013C5210,"11DD8B34"),
            (0x013C43FC,0x013C521C,"F10B5039"),
            (0x013C4404,0x013C5224,"8688083C"),
            (0x013C4410,0x013C5230,"ABAA2A3E"),
            (0x013C43F8,0x013C5218,"C2B4CF37"),
            (0x013C4400,0x013C5220,"AA09B63A"),
            (0x013C4408,0x013C5228,"A3AA2A3D"),
            (0x1343BE0,0x1344BE0,"00000040")
        ];
        foreach(var c in constants)
        {
            byte[] expected=Convert.FromHexString(c.Hex);
            if(!image.Raw(locale=="TC"?c.TC:c.SC,expected.Length).SequenceEqual(expected))
                throw new InvalidDataException("Moon geometry original arithmetic operand changed.");
        }
        return $"san14://file/SAN14CE_{locale}.exe@{profile.ExeHash};Moon118E6B0-face15C0/token11+118E7B0-screen/token13;9-native-windows/finite-acos101E910/distinct-sincos3F7280";
    }
}
