
namespace San14.Data;

/// <summary>Project mode4 writer, literal bounds/phases and original
/// logarithm tables. Dispatch is explicit; no mutable native CPU flag is read.</summary>
public sealed class LoadedSkyAtmosphereNativeSource
{
    internal LoadedSkyAtmosphereNativeSource(string locale,string provenance,
        NativeLogTables tables,NativeLogDispatch dispatch)
    {
        Locale=locale;SourceProvenance=provenance;LogTables=tables;LogDispatch=dispatch;
        ConversionLog2=NativeLogarithm.Divide(-1,
            NativeLogarithm.Log(2,tables,dispatch));
    }
    public string Locale { get; }
    public string SourceProvenance { get; }
    public NativeLogTables LogTables { get; }
    public NativeLogDispatch LogDispatch { get; }
    public float ConversionLog2 { get; }
    public float CoefficientLowerBound=>BitConverter.UInt32BitsToSingle(0xB7480000);
    public float CoefficientUpperBound=>BitConverter.UInt32BitsToSingle(0x37480000);
    public float MiePhaseFactor=>BitConverter.UInt32BitsToSingle(0x3DA2F983);
    public float RayleighPhaseFactor=>BitConverter.UInt32BitsToSingle(0x3D747645);
    public float One=>1;
    public float NegativeTwo=>-2;
}

public static class SkyAtmosphereNativeSource
{
    public static LoadedSkyAtmosphereNativeSource Load(GameResourceRepository repository,
        string locale,NativeLogDispatch explicitDispatch)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if(explicitDispatch is not(NativeLogDispatch.Sse or NativeLogDispatch.AvxFma))
            throw new InvalidDataException("Source Sky atmosphere requires an explicit original log dispatch.");
        string native=RenderSamplerSourceAdapters.ReadSkyAtmosphereNativeRecipe(repository.SourceDirectory,locale);
        var tables=NativeLogTables.Load(repository.SourceDirectory,locale);
        return new(locale,native+";log-dispatch="+explicitDispatch,tables,explicitDispatch);
    }
}

public static partial class RenderSamplerSourceAdapters
{
    internal static string ReadSkyAtmosphereNativeRecipe(string sourceDirectory,string locale)
    {
        return $"{RenderRuleIdentity.For("SkyAtmosphereNative",locale)};"+
            "mode4-scattering/4C0;lazy=-1/logf(2);"+
            "package40-Fog96/package10-directionalRGB/package30+hemi-ambient";
    }
}
