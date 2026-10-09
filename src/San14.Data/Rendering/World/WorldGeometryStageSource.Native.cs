
namespace San14.Data;

public sealed record WorldGeometryStageNativeSource(string Locale, string RuleVersion)
{
    public string SourceProvenance => $"{RenderRuleIdentity.For("world-geometry-stage",Locale)};" +
        "geometry-target-scope;fresh-HostQ7F8-zero;fresh-View1B80/1BB0-absent;" +
        "target-row-push;pop-FCFFFFFF-mask;depth-copy-before-pop;no-GPU-state-claim";
}

public static partial class RenderSamplerSourceAdapters
{
    internal static WorldGeometryStageNativeSource LoadWorldGeometryStageNativeSource(string directory, string locale)
    {
        _ = RenderRuleIdentity.For("world-geometry-stage",locale);
        return new(locale, RenderRuleIdentity.Version);
    }
}
