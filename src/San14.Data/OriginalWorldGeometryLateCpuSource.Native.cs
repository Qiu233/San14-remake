namespace San14.Data;

public sealed record OriginalWorldGeometryLateCpuNativeSource(string Locale, string RuleVersion);

public static partial class OriginalDeferredSamplerSources
{
    internal static OriginalWorldGeometryLateCpuNativeSource LoadWorldGeometryLateCpuNativeSource(string directory,string locale)
    {
        _ = RenderRuleIdentity.For("WorldGeometryLateCpu",locale);

        return new(locale,RenderRuleIdentity.Version);
    }
}
