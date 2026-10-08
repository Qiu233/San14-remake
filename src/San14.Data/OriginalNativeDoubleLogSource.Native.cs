namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static IReadOnlyList<OriginalNativeDoubleLogRegionSource> VerifyDoubleLogNativeCode(byte[] executable,string locale="TC")
    {
        var profile=Profile(locale);VerifyHash(executable,profile.ExeHash,"double-log actual executable");
        var image=new NativeImage(executable);byte[] text=image.DecodeText(out int textRva);
        VerifyHash(text,profile.TextHash,"double-log actual decoded text");
        (int Rva,int Count,string Hash,string Role)[] windows=[
            (0x1035520,23,"608C7E33E9094939A7F469061115594418C77C8515FB85A73790ECD0DE9B3CFD","dispatch"),
            (0x1035537,492,"77B1F2D17D8BA145AA2AA6DA7248BBBBFEC2562F53452DAE7D24737A03049514","SSE-positive-normal"),
            (0x10357E0,430,"7A66AFEE08752B040E7974D92B24E1FD50AF98C54DE371F580B1BB153F055290","AVX-FMA-positive-normal")];
        return Array.AsReadOnly(windows.Select(w=>
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(locale,"OriginalNativeDoubleLogSource.Native.cs",w.Rva,w.Count,w.Hash);
            VerifyHash(text.AsSpan(actual.Rva-textRva,w.Count),actual.Sha,"double-log "+w.Role);
            return new OriginalNativeDoubleLogRegionSource($"san14://file/SAN14CE_{locale}.exe#decoded-native-doublelog/"+w.Role,actual.Rva,w.Count,actual.Sha);
        }).ToArray());
    }
}
