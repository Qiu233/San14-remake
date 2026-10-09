namespace San14.Data;

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedDeferredEarlyShadowTargetRectangleSource LoadEarlyShadowTargetRectangleSource(
        string directory, string locale)
    {
        if(locale is not ("TC" or "SC"))throw new NotSupportedException("This source layout requires SC or TC.");
        var ruleVersion=RenderRuleIdentity.For("shadow-target-rectangle",locale);

        return new(locale, $"{ruleVersion};" +
            "decode-target-rectangle;flag-byte5=true;" +
            "actual-row50-primary-surface50/54;viewport/scissor-full-target;" +
            "retained-frontend13C/17C-depth-and1D9-enable;no-extra-rectangle-package");
    }
}
