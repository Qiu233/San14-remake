
namespace San14.Data;

/// <summary>Project mode4 writer, literal bounds/phases and original
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
        return $"{RenderRuleIdentity.For("SkyAtmosphereNative",locale)};"+
            "mode4-scattering/4C0;lazy=-1/logf(2);"+
            "package40-Fog96/package10-directionalRGB/package30+hemi-ambient";
    }
}
