namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static string LoadArmyContextDataNativeSource(string sourceDirectory,string locale)
    {
        if(locale is not ("TC" or "SC"))throw new NotSupportedException("This source layout requires SC or TC.");
        var ruleVersion=RenderRuleIdentity.For("army-context-data",locale);

        return $"{ruleVersion};D9B69B-explicit-C8-null;"+
            "D9B7B0-real-CPU18-lookup;B3B6F0/B35080-no-source-synthesis-on-MISS;D9D7DB-D9D83C-retain-null";
    }
}
