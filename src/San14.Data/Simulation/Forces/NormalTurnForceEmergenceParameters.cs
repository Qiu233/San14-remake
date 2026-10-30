using San14.Core;

namespace San14.Data;

/// <summary>Concrete parameter data for the monthly force-emergence family.
/// Existing saved integer overrides retain the strategic-command key names.</summary>
internal static class NormalTurnForceEmergenceParameters
{
    internal static int Read(GameState state, string name, int knownTc)
    {
        var parameters = state.NativeArmySimulation?.Parameters
            ?? throw new InvalidDataException("normal.turn.force-emergence.parameter_owner");
        string key = "simulation.strategic-command." + name;
        if (parameters.Float32Values.ContainsKey(key))
            throw new InvalidDataException("normal.turn.force-emergence.parameter_type_" + name);
        if (parameters.Int32Values.TryGetValue(key, out int saved)) return saved;
        if (parameters.Language == "TC") return knownTc;
        // These four signed integers are independently read from the actual
        // SC consumers. None is a mutable or missing fresh-owner field.
        return parameters.Language == "SC" ? name switch
        {
            "normal-force-emergence.minimum-elapsed-periods" => 18,
            "normal-force-emergence.minimum-ambition" => 10,
            "force-emergence-followers.serving-loyalty-limit" => 75,
            "force-emergence-followers.affinity-limit" => 10,
            _ => throw new InvalidDataException("normal.turn.force-emergence.parameter_" + name)
        } : throw new InvalidDataException("normal.turn.force-emergence.parameter_locale");
    }
}
