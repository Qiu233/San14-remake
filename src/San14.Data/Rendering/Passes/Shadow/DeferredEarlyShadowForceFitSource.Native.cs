namespace San14.Data;

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedDeferredEarlyShadowForceFitSource LoadEarlyShadowForceFitSource(
        string directory,string locale)
    {
        if(locale is not ("TC" or "SC"))throw new NotSupportedException("This source layout requires SC or TC.");
        var ruleVersion=RenderRuleIdentity.For("shadow-force-fit",locale);

        return new(locale,$"{ruleVersion};"+
            "fresh-qword4A4=0;owned-fragment;not-native-live-state-or-pool-reuse",0);
    }
}
