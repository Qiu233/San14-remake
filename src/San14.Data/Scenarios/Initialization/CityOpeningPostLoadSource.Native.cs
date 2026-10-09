using San14.Data.Formats;

namespace San14.Data;

public static partial class RenderSamplerSourceAdapters
{
    internal static CityOpeningPostLoadNativeRecipe LoadCityOpeningPostLoadNative(string directory,string locale)
    {
        return new(locale, BaseParameterReader.ImplementationVersion,
            BaseParameterReader.ResourceUri, BaseParameterReader.ReadDefaults(locale));
    }
}
