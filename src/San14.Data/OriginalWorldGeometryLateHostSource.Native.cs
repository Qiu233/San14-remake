using System.Numerics;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static OriginalWorldGeometryLateHostNativeSource LoadWorldGeometryLateHostNativeSource(
        string directory, string locale)
    {
        _ = RenderRuleIdentity.For("WorldGeometryLateHost",locale);
        OriginalWorldGeometryLateHostGetter[] getters =
        [
            new(0x120, 0x58, false),
            new(0x140, null, true),
            new(0x198, 0x88, false),
            new(0x1A8, 0x80, false),
        ];
        var initialColour=new OriginalWorldGeometryLateHostColourParameters(Vector4.One,Vector4.One,Vector4.One);
        return new(locale,RenderRuleIdentity.Version,Array.AsReadOnly(getters),initialColour);
    }
}
