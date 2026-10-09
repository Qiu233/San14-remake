
namespace San14.Data;

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapSceneProxyNativeSource LoadMapSceneProxySource(string directory,string locale)
    {
        _ = RenderRuleIdentity.For("map-scene-proxy",locale);
        return new(locale,RenderRuleIdentity.Version);
    }
}

internal sealed record LoadedMapSceneProxyNativeSource(string Locale,string RuleVersion)
{
    public string SourceEvidence=>$"{RenderRuleIdentity.For("map-scene-proxy",Locale)};"+
        "node1/5F0A8446-typed-args5/6/8;owner-fields460/464/468;"+
        "prepared-view-table110;primary-camera-terminal1CC-clear;common-aux240;"+
        "named90B366D4;counted-install";
}
