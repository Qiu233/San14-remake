using System.Numerics;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalDeferredEarlyShadowBiasSource LoadEarlyShadowBiasSource(string directory,string locale)
    {
        string rules=RenderRuleIdentity.For("shadow-bias",locale);
        var freshBias=new Vector4(.5f,0,1,.8f);
        return new(locale,rules+";fresh-owned-sbias/type2/four-components",
            RenderRuleNumericEncoding.Vectors(freshBias));
    }
}
