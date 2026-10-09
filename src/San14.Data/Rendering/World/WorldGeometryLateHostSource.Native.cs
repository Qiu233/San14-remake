using System.Numerics;

namespace San14.Data;

public static partial class RenderSamplerSourceAdapters
{
    internal static WorldGeometryLateHostNativeSource LoadWorldGeometryLateHostNativeSource(
        string directory, string locale)
    {
        _ = RenderRuleIdentity.For("WorldGeometryLateHost",locale);
        WorldGeometryLateHostGetter[] getters =
        [
            new(0x120, 0x58, false),
            new(0x140, null, true),
            new(0x198, 0x88, false),
            new(0x1A8, 0x80, false),
        ];
        var initialColour=new WorldGeometryLateHostColourParameters(Vector4.One,Vector4.One,Vector4.One);
        return new(locale,RenderRuleIdentity.Version,Array.AsReadOnly(getters),initialColour);
    }
}
