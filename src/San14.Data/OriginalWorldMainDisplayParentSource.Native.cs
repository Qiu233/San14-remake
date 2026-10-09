
namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalWorldMainDisplayParentSource LoadWorldMainDisplayParentSource(string directory, string locale)
    {
        _ = RenderRuleIdentity.For("WorldMainDisplayParent",locale);

        bool tc = locale == "TC";

        int vt = tc ? 0x13C4ED0 : 0x13C5CF0;

        return new(locale, RenderRuleIdentity.Version,
            tc ? 0x959620 : 0x95A6F0, tc ? 0x94D140 : 0x94E210,
            tc ? 0x95A9D0 : 0x95BAA0, tc ? 0x964E80 : 0x966000,
            $"{RenderRuleIdentity.For("WorldMainDisplayParent",locale)};MainVT={vt:X8};" +
            "v20-lock/vC8-entry;device-v2C8-mode1-current-surface-boundary;wrapper20-identity;" +
            "actual-format/physical-extent;Q18-count1-push;retained13C/17C/1D9;" +
            "Main-vD0-outer-pop-before-vD8-present;not-RGK-named-target");
    }
}
