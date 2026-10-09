namespace San14.Data;
public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedSkyPatternCurrentViewCacheSource LoadSkyPatternCurrentViewCacheSource(string directory,string locale)
    {
        _=RenderRuleIdentity.For("SkyPatternCurrentViewCache",locale);

        return new(locale,$"{RenderRuleIdentity.For("SkyPatternCurrentViewCache",locale)};"+
            "fresh-D12830/D06FA0-preparedView1B90-null;D4BD30-only-absent-currentView-cache;"+
            "D4C775/7A9-88=K1680-else1678;D4D151/16E-9E0=R148/9E8=R150;"+
            "E48920-dependency26-E33F00/E37F10-R148/11919E0-R150;D6ABE0-first-A40-allocation/copy;D5BEA0-A40-query;finite-reference-subset");
    }
}
