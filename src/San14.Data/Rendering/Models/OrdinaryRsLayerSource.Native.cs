
namespace San14.Data;

public static partial class RenderSamplerSourceAdapters
{
    internal static string LoadOrdinaryRsLayerNativeSource(string directory, string locale)
    {
        _ = RenderRuleIdentity.For("OrdinaryRsLayer",locale);

        return $"{RenderRuleIdentity.For("OrdinaryRsLayer",locale)};EA8D50/full-D61120-counts/D1B660;" +
            "shared-D220B0-numeric-ctor;E75060/E931A0/E96F30-current-row;" +
            "alpha8-before-SDF-mask3;retained-shared40;current-provider/View/resources-required;no-pixel-completion";
    }
}
