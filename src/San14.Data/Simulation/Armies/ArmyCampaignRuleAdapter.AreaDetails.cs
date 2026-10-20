using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Reads only the current representative cell's surface. Preview
    /// panels do not require gameplay-only Person or lifecycle owners.</summary>
    public byte? ReadAreaRepresentativeSurfaceCode(GameState state, int areaId)
    {
        if (!state.Areas.ContainsKey(areaId)) return null;
        var current = _definitions.Capture(state);
        return current?.ReadAreaHexIndex(new(areaId)) is ushort hex
            ? current.ReadHexSurfaceCode(new(hex <= 48399 ? hex : 0)) : null;
    }
}
