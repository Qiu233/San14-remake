using San14.Data.Formats;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static OriginalCityOpeningPostLoadNativeRecipe LoadCityOpeningPostLoadNative(string directory,string locale)
    {
        return new(locale, OriginalBaseParameterReader.ImplementationVersion,
            OriginalBaseParameterReader.ResourceUri, OriginalBaseParameterReader.ReadDefaults(locale));
    }
}
