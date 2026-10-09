
namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalWorldMainDisplayParentSource LoadWorldMainDisplayParentSource(string directory, string locale)
    {
        _ = RenderRuleIdentity.For("world-main-display-parent",locale);
        return new(locale, RenderRuleIdentity.Version,
            $"{RenderRuleIdentity.For("world-main-display-parent",locale)};" +
            "locked-display-entry;current-device-surface-mode1;wrapper20-identity;" +
            "actual-format/physical-extent;Q18-count1-push;retained13C/17C/1D9;" +
            "display-outer-pop-before-present;not-RGK-named-target");
    }
}
