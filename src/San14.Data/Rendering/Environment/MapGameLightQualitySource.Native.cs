namespace San14.Data;

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapGameLightQualitySource LoadGameLightQualitySource(string directory,string locale)
    {
        string ruleVersion=RenderRuleIdentity.For("game-light-quality",locale);

        return new(locale,ruleVersion,0);
    }
}

internal sealed record LoadedMapGameLightQualitySource(string Locale,string RuleVersion,int FreshQuality)
{
    public string SourceProvenance=>$"{RuleVersion};primary-quality-zero;fresh-before-saved-config;"+
        "settings-option18;signed-quality-four-field-overlay";
}
