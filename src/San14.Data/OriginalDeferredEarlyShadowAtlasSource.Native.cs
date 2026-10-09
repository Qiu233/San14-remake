using System.Buffers.Binary;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalDeferredEarlyShadowAtlasSource LoadEarlyShadowAtlasSource(string directory, string locale)
    {
        if(locale is not ("TC" or "SC"))throw new NotSupportedException("This source layout requires SC or TC.");
        var ruleVersion=RenderRuleIdentity.For("shadow-atlas",locale);

        return new(locale, $"{ruleVersion};" +
            $"v50-atlas={(locale == "TC" ? 0x1059500 : 0x105A430):X8};D24-normal-only;" +
            "current198-u16-40/42-or-current-fallback5B4/5B8;" +
            "current230/22C/D25-gates;CE0-interior;unit4C/50-padded;6F0-scale/offset;" +
            "not-target-pointer-publication/not-caster-or-draw-completion");
    }
}
