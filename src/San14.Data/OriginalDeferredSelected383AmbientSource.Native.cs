using System.Buffers.Binary;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static string ReadSelected383AmbientNativeSource(string sourceDirectory, string locale)
    {
        if(locale is not ("TC" or "SC"))throw new NotSupportedException("This source layout requires SC or TC.");
        var ruleVersion=RenderRuleIdentity.For("sky-ambient-publication",locale);

        return $"{ruleVersion};actual-F103-Q80-v1A0/v1A8;"+
            "View19B8-gate=E8FEE0;Sky168-bit30-clear/index8..9;encoded1B-current-wrapper";
    }
}
