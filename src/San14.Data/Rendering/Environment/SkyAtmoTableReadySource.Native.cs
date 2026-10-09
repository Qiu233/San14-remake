namespace San14.Data;

public static partial class RenderSamplerSourceAdapters
{
    internal static string LoadSkyAtmoTableReadyNativeSource(string directory, string locale)
    {
        _ = RenderRuleIdentity.For("SkyAtmoTableReady",locale);

        // Project rules for constructor/prepare/registry/stream and Plane factories.

        return $"{RenderRuleIdentity.For("SkyAtmoTableReady",locale)};STA-E3F770/E49EB0;" +
            "raw-B41860/B43790/B45450/14DD90;Plane-C104B0/C15440;Static-C078C0/C0EFB0;" +
            "project-rules";
    }
}
