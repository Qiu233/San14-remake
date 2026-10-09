namespace San14.Data;

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedSceneCatalogListPublicationSource LoadSceneCatalogListPublicationSource(
        string directory, string locale)
    {
        _ = RenderRuleIdentity.For("SceneCatalogListPublication",locale);

        return new(locale, $"{RenderRuleIdentity.For("SceneCatalogListPublication",locale)};" +
            "TC37F050/SC37F3E0->TC37E9C0/SC37ED50;" +
            "Game98:E65D70AE/A0:F0792D7D/C0:3E3E42CD/C8:012AAC2C;" +
            "category0..3/458[820]-then-4558[200]/ListV8-bulk-append;" +
            "current-entry20/18/10-required;not-static-Army-membership-or-GPU");
    }
}
