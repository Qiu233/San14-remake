namespace San14.Data;

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedDeferredEarlyShadowSelectionSource LoadEarlyShadowSelectionSource(string directory,string locale)
    {
        if(locale is not ("TC" or "SC"))throw new NotSupportedException("This source layout requires SC or TC.");
        var ruleVersion=RenderRuleIdentity.For("shadow-selection",locale);

        return new(locale,$"{ruleVersion};paired-full-create/init/guard/variant/pass;"+
            "ABFA80->981F00-indices13E0..13F8=0/pass1414=-1/first-variant-dual-copy;"+
            "actual-type17+currentShadow20+frontend1BE/1C0;not-stage-or-draw-completion");
    }
}
