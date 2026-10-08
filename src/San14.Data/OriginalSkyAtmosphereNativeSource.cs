using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Hash-pinned native mode4 writer, literal bounds/phases and original
/// logarithm tables. Dispatch is explicit; no mutable native CPU flag is read.</summary>
public sealed class LoadedOriginalSkyAtmosphereNativeSource
{
    internal LoadedOriginalSkyAtmosphereNativeSource(string locale,string provenance,
        OriginalNativeLogTables tables,OriginalNativeLogDispatch dispatch)
    {
        Locale=locale;SourceProvenance=provenance;LogTables=tables;LogDispatch=dispatch;
        ConversionLog2=OriginalNativeLogarithm.Divide(-1,
            OriginalNativeLogarithm.Log(2,tables,dispatch));
    }
    public string Locale { get; }
    public string SourceProvenance { get; }
    public OriginalNativeLogTables LogTables { get; }
    public OriginalNativeLogDispatch LogDispatch { get; }
    public float ConversionLog2 { get; }
    public float CoefficientLowerBound=>BitConverter.UInt32BitsToSingle(0xB7480000);
    public float CoefficientUpperBound=>BitConverter.UInt32BitsToSingle(0x37480000);
    public float MiePhaseFactor=>BitConverter.UInt32BitsToSingle(0x3DA2F983);
    public float RayleighPhaseFactor=>BitConverter.UInt32BitsToSingle(0x3D747645);
    public float One=>1;
    public float NegativeTwo=>-2;
}

public static class OriginalSkyAtmosphereNativeSource
{
    public static LoadedOriginalSkyAtmosphereNativeSource Load(OriginalGameRepository repository,
        string locale,OriginalNativeLogDispatch explicitDispatch)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if(explicitDispatch is not(OriginalNativeLogDispatch.Sse or OriginalNativeLogDispatch.AvxFma))
            throw new InvalidDataException("Source Sky atmosphere requires an explicit original log dispatch.");
        string native=OriginalDeferredSamplerSources.ReadSkyAtmosphereNativeRecipe(repository.SourceDirectory,locale);
        var tables=OriginalNativeLogTables.Load(repository.SourceDirectory,locale);
        return new(locale,native+";log-dispatch="+explicitDispatch,tables,explicitDispatch);
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static string ReadSkyAtmosphereNativeRecipe(string sourceDirectory,string locale)
    {
        var profile=Profile(locale);string path=Path.GetFullPath(Path.Combine(sourceDirectory,$"SAN14CE_{locale}.exe"));
        byte[] exe=File.ReadAllBytes(path);VerifyHash(exe,profile.ExeHash,"Sky atmosphere source executable");
        var image=new NativeImage(exe);byte[] code=image.DecodeText(out int textRva);
        VerifyHash(code,profile.TextHash,"Sky atmosphere decoded text");
        (int Start,string Hash,int Lower,int Upper,int One,int Mie,int Rayleigh,int NegativeTwo,int LogArgument,int Numerator) p=locale switch
        {
            "TC"=>(0x97CF70,"739DD9DC91E2D142E874CB69CC2A44AF16FDE72A7BE66D632B209C5F90B7BEAE",
                0x1343CD4,0x1343B54,0x1343B98,0x14BFE5C,0x14BFE58,0x13A194C,0x1343BE0,0x1343CDC),
            "SC"=>(0x97E100,"076BDE168F847A72889741AF3DB27B6E385DA8AAE3F9A657898F25A73BDC10F9",
                0x1344CD4,0x1344B54,0x1344B98,0x14C0D3C,0x14C0D38,0x13A294C,0x1344BE0,0x1344CDC),
            _=>throw new InvalidDataException("Source Sky atmosphere requires the pinned SC or TC executable.")
        };
        VerifyHash(code.AsSpan(p.Start-textRva,0x4C0),p.Hash,"complete mode4 Scat and lazy initializer");
        foreach(var literal in new[]{(p.Lower,0xB7480000u),(p.Upper,0x37480000u),(p.One,0x3F800000u),
            (p.Mie,0x3DA2F983u),(p.Rayleigh,0x3D747645u),(p.NegativeTwo,0xC0000000u),
            (p.LogArgument,0x40000000u),(p.Numerator,0xBF800000u)})
        {
            if(BinaryPrimitives.ReadUInt32LittleEndian(image.Raw(literal.Item1,4))!=literal.Item2)
                throw new InvalidDataException("Sky atmosphere literal differs from its native source bits.");
        }
        return $"san14://file/SAN14CE_{locale}.exe@{profile.ExeHash};decoded-text={profile.TextHash};"+
            $"native-mode4-Scat={p.Start:X8}/4C0@{p.Hash};native-lazy=-1/logf(2);"+
            "source=Generic97CF70/package40-Fog96/package10-directionalRGB/package30+hemi-ambient";
    }
}
