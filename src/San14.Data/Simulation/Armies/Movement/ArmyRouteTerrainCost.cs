using San14.Core;

namespace San14.Data;

/// <summary>The current 0BDDD0 terrain cost shared by live search and draft ETA.</summary>
internal static class ArmyRouteTerrainCost
{
    internal static int Read(GameState state, SourceWorldContext current, SurfaceTypeReference terrain)
    {
        if (terrain.PhysicalSlot is not int physical) return 0;
        int raw = Need(current.ReadSurfaceTypeRawField2A(terrain), "Terrain2A");
        if (Need(BattleRemakeCurrentSettings.ReadSelectorE58(state), "EnvironmentE58") != 0 ||
            physical is not (>= 3 and <= 14 or 24)) return raw;
        int percent = Need(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 26), "advanced26");
        return Math.Clamp(unchecked(raw * percent) / 100, 225, 999);
    }
    private static T Need<T>(T? value, string name) where T : struct =>
        value ?? throw new InvalidDataException("current.army_route_" + name);
}
