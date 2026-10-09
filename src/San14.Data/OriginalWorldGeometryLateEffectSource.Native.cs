namespace San14.Data;
public static partial class OriginalDeferredSamplerSources
{

    internal static OriginalWorldGeometryLateEffectNativeSource LoadWorldGeometryLateEffectNativeSource(string directory, string locale)
    {
        _ = RenderRuleIdentity.For("WorldGeometryLateEffect",locale);
        const float affineDenominatorEpsilon=0.000011920928955078125f;
        const float affineDefaultBias=0.1f;
        return new(locale,RenderRuleIdentity.Version,affineDenominatorEpsilon,affineDefaultBias);
    }
}
