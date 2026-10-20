using San14.Core;

namespace San14.Data;

/// <summary>Dynamic destination cost and the first-use minimum cache used by
/// ordinary path preparation. Missing definitions remain unresolved.</summary>
public static class ArmyMarchTerrainCost
{
    public static int? Read(GameState state, SourceWorldContext current, SurfaceTypeReference surface)
    {
        if (surface.PhysicalSlot is not int physical ||
            current.ReadSurfaceTypeRawField2A(surface) is not ushort raw ||
            BattleRemakeCurrentSettings.ReadSelectorE58(state) is not int selector) return null;
        if (selector != 0 || physical is not (>= 3 and <= 14 or 24)) return raw;
        return BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 26) is int percent
            ? Math.Clamp(unchecked(raw * percent) / 100, 225, 999) : null;
    }

    /// <summary>Call only at the reached path-preparation cache initialization.
    /// Publish the returned world with the enclosing movement transaction;
    /// an already initialized saved minimum is retained even if settings change.</summary>
    public static GameState? InitializeMinimum(GameState state, SourceWorldContext current)
    {
        if (state.NativeArmySimulation is not { } simulation ||
            !simulation.Parameters.Int32Values.TryGetValue(SimulationParameterKeys.ArmyMarch.MinimumTerrainCost, out int minimum))
            return null;
        if (minimum != int.MaxValue) return state;
        for (int slot = 0; slot < 31; slot++)
        {
            if (current.ReadSurfaceTypeTableReference(slot) is not { } surface || Read(state, current, surface) is not int cost)
                return null;
            if (cost != 0 && cost < minimum) minimum = cost;
        }
        if (minimum == int.MaxValue) return state;
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values)
        {
            [SimulationParameterKeys.ArmyMarch.MinimumTerrainCost] = minimum
        };
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}
