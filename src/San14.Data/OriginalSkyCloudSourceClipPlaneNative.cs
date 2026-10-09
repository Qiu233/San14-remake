using System.Numerics;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static (Vector4 Neutral, string Provenance) ReadCloudSpecialClipNativeSource(string sourceDirectory, string locale)
    {
        string identity=RenderRuleIdentity.For("SkyCloudSpecialClip",locale);
        return (Vector4.Zero,identity+
            ";source-mask0/8sourcePlanes0;neutral-owned-cache;special-VS-CB-upload/current-VS-bind;"+
            "Cloud-14C4-save/zero/restore;WORK-mask/plane0-restore;screen-mask-save0/restore;"+
            "opcode24/25-plain-Cloud/finite-Host-history;terminal-mask0/renew-neutral-not-rawplaneszero");
    }

}
