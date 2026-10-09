using System.Security.Cryptography;

namespace San14.Data;

public static partial class RenderSamplerSourceAdapters
{
    internal static string LoadEarlyPredecessorNativeSource(string sourceDirectory, string locale)
    {
        if(locale is not ("TC" or "SC"))throw new NotSupportedException("This source layout requires SC or TC.");
        var ruleVersion=RenderRuleIdentity.For("early-predecessor",locale);

        return $"{ruleVersion};actualRender2D-CPU.v28-B0/alias;" +
            "EA-depth-only17/clear18/RenderType2-state3-queue19/pop20-flags0;" +
            "AB0-bind62/SubShadow63/own-38..78-intermediates/pop64-flags1;no-pixel-completion";
    }
}
