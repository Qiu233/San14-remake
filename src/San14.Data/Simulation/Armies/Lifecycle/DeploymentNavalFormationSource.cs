using San14.Core;

namespace San14.Data;

/// <summary>Normal summary's current independent naval choice. Explicit
/// confirmation packets retain their chosen value instead of rerunning this.</summary>
internal static class DeploymentNavalFormationSource
{
    internal static int Resolve(GameState state, SourceWorldContext current, int id, int physical)
    {
        bool band = unchecked((uint)(id - 4111)) <= 9 || unchecked((uint)(id - 4131)) <= 9 ||
            unchecked((uint)(id - 4151)) <= 9 || unchecked((uint)(id - 4171)) <= 9 || unchecked((uint)(id - 4191)) <= 9;
        var force = current.ReadPersonForce(physical) ?? throw Missing("person_force");
        int slot = Known(force.PhysicalSlot, "force_pointer");
        if (band && Known(current.ReadPersonFirstLocation(physical), "first_location") is >= 1 and <= 61 && slot is not (>= 46 and <= 50)) return 11;
        if (!Known(current.ReadForceNativeValidity(force), "force_validity")) return 11;
        if (state.NativeArmySimulation?.Parameters is not { } parameters ||
            !parameters.Int32Values.TryGetValue(BattleRemakeCurrentForcePolicies.ForceFieldKey(slot, 0x15E), out int value) || value is < 0 or > 255)
            throw Missing("naval_formation");
        return value;
    }
    private static T Known<T>(T? value, string input) where T : struct => value ?? throw Missing(input);
    private static InvalidDataException Missing(string input) => new("deployment.naval." + input);
}
