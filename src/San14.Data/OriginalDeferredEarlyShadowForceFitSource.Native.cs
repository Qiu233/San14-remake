namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalDeferredEarlyShadowForceFitSource LoadEarlyShadowForceFitSource(
        string directory,string locale)
    {
        var profile=Profile(locale);
        byte[] exe=File.ReadAllBytes(Path.Combine(directory,$"SAN14CE_{locale}.exe"));
        VerifyHash(exe,profile.ExeHash,"Shadow forceFit fresh constructor executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int textRva);
        VerifyHash(text,profile.TextHash,"Shadow forceFit fresh constructor decoded text");
        int constructor=locale=="TC"?0x9FFCA0:0xA00CA0;
        var body=text.AsSpan(constructor-textRva,1079);
        VerifyHash(body,locale=="TC"
            ?"F7416598AE72694B326412F07A2C7D33EF2376BF8FD57C534666DB636E7F2F08"
            :"3893CE5B12B72E45C3B9635EF0DC8A871A26CFCD76CB94DA209B11F55CFD89DA",
            "complete1079B Shadow base constructor");
        // At+3 XOR R10D,R10D zero-extends the whole R10. The full source
        // reaches its+BB MOV [RAX+4A4],R10 without redefining that register.
        if(!body.Slice(3,3).SequenceEqual(Convert.FromHexString("4533D2")) ||
            !body.Slice(0xBB,7).SequenceEqual(Convert.FromHexString("4C8990A4040000")))
            throw new InvalidDataException("Original Shadow4A4 qword constructor writer differs.");
        ulong r10=0; r10^=r10;
        return new(locale,$"ShadowForceFit/{locale}@{profile.ExeHash};"+
            $"full{constructor:X8}-1079B;XOR-R10D@{constructor+3:X8};qword4A4@{constructor+0xBB:X8};"+
            "fresh-owned-fragment;not-native-live-state-or-pool-reuse",r10);
    }
}
