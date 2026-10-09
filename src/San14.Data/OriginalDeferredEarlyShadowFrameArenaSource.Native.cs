namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static OriginalDeferredEarlyShadowFrameArenaNativeFields LoadEarlyShadowFrameArenaNativeSource(string directory,string locale)
    {
        string rules=RenderRuleIdentity.For("shadow-frame-arena",locale);
        const int useDeclaredBudget=-1;
        const uint firstVectorCapacity=128;
        const uint secondVectorCapacity=64;
        return new(locale,useDeclaredBudget,firstVectorCapacity,secondVectorCapacity,
            rules+";four-current-frames/declared-budget/fresh-arena-cursors");
    }
}
