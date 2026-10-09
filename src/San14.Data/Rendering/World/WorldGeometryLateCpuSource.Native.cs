namespace San14.Data;

public sealed record WorldGeometryLateCpuNativeSource(string Locale, string RuleVersion);

public static partial class RenderSamplerSourceAdapters
{
    internal static WorldGeometryLateCpuNativeSource LoadWorldGeometryLateCpuNativeSource(string directory,string locale)
    {
        _ = RenderRuleIdentity.For("WorldGeometryLateCpu",locale);

        return new(locale,RenderRuleIdentity.Version);
    }
}
