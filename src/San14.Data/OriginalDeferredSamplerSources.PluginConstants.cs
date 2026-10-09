using System.Numerics;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    /// <summary>Fresh GenericOwner time parameters. Later times and plugin
    /// updates are supplied by their current owners.</summary>
    public static LoadedOriginalDeferredSelected767PluginSource LoadSelected767PluginSource(string sourceDirectory,string locale)
        => new(locale,RenderRuleIdentity.For("deferred-plugin-defaults",locale),new Vector4(0,0,3600,0));
}
