using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Actual retained fresh Camera rectangle constructor literals.
/// Constructor clears and their source-loader exclusion are proven separately;
/// literals are read from the same hash-pinned native executable, not a preset.</summary>
public sealed class OriginalMapPostEffect3FallbackCameraConstructorWords
{
    internal OriginalMapPostEffect3FallbackCameraConstructorWords(string locale,string exe,string text,
        uint source1278,uint source127C,string evidence)
    {Locale=locale;ExecutableSha256=exe;DecodedTextSha256=text;Source1278Word=source1278;
     Source127CWord=source127C;SourceEvidence=evidence;}
    public string Locale{get;}
    public string ExecutableSha256{get;}
    public string DecodedTextSha256{get;}
    public uint Source1278Word{get;}
    public uint Source127CWord{get;}
    public string SourceEvidence{get;}
}

public static partial class OriginalDeferredSamplerSources
{
    public static OriginalMapPostEffect3FallbackCameraConstructorWords ReadFallbackCameraRectangleNativeSource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(native);var profile=Profile(native.Locale);
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,profile.ExeHash,"fallback Camera executable");
        var image=new NativeImage(exe);byte[] code=image.DecodeText(out int textRva);
        VerifyHash(code,profile.TextHash,"fallback Camera decoded text");
        int constructor=native.Locale=="TC"?0xC3BF60:0xC3D610;
        int literals=native.Locale=="TC"?0xC3EFA2:0xC40652;
        int loader=native.Locale=="TC"?0xC4F6F0:0xC50CF0;
        VerifyHash(code.AsSpan(constructor-textRva,12446),native.Locale=="TC"
            ?"157D48A9DF2DFBE2137F6E6390F6A2D96DA94C34DD5D8E8BB6417CA1168C2630"
            :"7EBF234E50BBE1FD0FC31BC8142306880A44C596B5666F086334054ACF18A1F8","fallback Camera full ctor");
        VerifyHash(code.AsSpan(loader-textRva,35591),native.Locale=="TC"
            ?"E0A5FB47D17D9F33E29ADE0F40DEC46285DED7672F4576BE01D2BBD3F97B42A3"
            :"1DBA09CFD207CF5D9EBD99E9D20C6561858493FA6736640554D20F5E520108A9","fallback Camera full loader");
        var words=code.AsSpan(literals-textRva,41);
        if(!words[..21].SequenceEqual(Convert.FromHexString("4C89875C1200004C8987641200004C898770120000"))||
            !words.Slice(21,6).SequenceEqual(Convert.FromHexString("C78778120000"))||
            !words.Slice(31,6).SequenceEqual(Convert.FromHexString("C7877C120000")))
            throw new InvalidDataException("Actual Camera rectangle constructor stores differ.");
        return new(native.Locale,profile.ExeHash,profile.TextHash,
            BinaryPrimitives.ReadUInt32LittleEndian(words.Slice(27,4)),
            BinaryPrimitives.ReadUInt32LittleEndian(words.Slice(37,4)),
            native.ResourceUri+";full-ctor12446/full-loader35591;actual-immediates="+literals.ToString("X8")+
            ";constructor-cleared-r8;1119-loader-pointers-exclude125C..1280");
    }
}
