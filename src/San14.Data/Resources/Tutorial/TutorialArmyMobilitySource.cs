using San14.Core;

namespace San14.Data;

/// <summary>The tutorial's reached early return from the ordinary mobility
/// operation. A zero result means that the same ordinary calculation continues.</summary>
internal static class TutorialArmyMobilitySource
{
    internal const string OverrideParameterKey = "simulation.tutorial.designated-commander-formation-mobility";
    // Original parameter allocation, offline TC4C6F04/141A4B580. This is
    // installed by the normal saved mobility bank, never read as a fallback.
    internal const int FreshOverrideParameter = 14;

    internal static (float? Value, string? Missing) Read(GameState control, NativeArmyParameterState parameters,
        Func<ushort?> readCommander, Func<byte?> readLandFormation)
    {
        if (TacticRemakeCurrentBattleControl.ReadHeader(control) is not int header)
            return Unknown("controller_header");
        if (header == 0) return (0, null);
        if (TacticRemakeCurrentBattleControl.Read(control, 68) is not bool enabled)
            return Unknown("control68");
        if (!enabled) return (0, null);
        if (readCommander() is not ushort commander) return Unknown("commander12");
        if (commander != 656) return (0, null);
        if (readLandFormation() is not byte formation) return Unknown("land_formation1B");
        if (formation != 2) return (0, null);
        if (parameters.Float32Values.ContainsKey(OverrideParameterKey) ||
            !parameters.Int32Values.TryGetValue(OverrideParameterKey, out int value))
            return Unknown("override_parameter");
        return ((float)value, null);
    }

    private static (float?, string?) Unknown(string owner) => (null, "tutorial.mobility." + owner);
}
