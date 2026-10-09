namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalSkyPatternPaletteSource LoadSkyPatternPaletteSource(string directory, string locale)
    {
        _ = RenderRuleIdentity.For("SkyPatternPalette",locale);

        return new(locale,$"{RenderRuleIdentity.For("SkyPatternPalette",locale)};"+
            "E38440-four40/60null/180zero;E3C6C0-prop3-four170keys;EB5950-ready-Sky-Q80-beforeE46EC0;"+
            "current-camera640/748/27C/1680/3AC;selector10..12;D49EE0-outputarg7=Sky180;host-reset-v1A0-null");
    }
}
