using San14.Core;

namespace San14.Data;

/// <summary>Immutable rules consumed by the normal coalition family. Mutable
/// world, random, list and invitation state stay in their existing owners.</summary>
internal static class NormalTurnCoalitionParameters
{
    private const string Prefix = "simulation.normal-turn.coalition.";

    internal static void RequireSupportedLocale(string language)
    {
        if (language is not ("TC" or "SC"))
            throw new InvalidDataException("normal.turn.coalition.formation.locale_rules");
    }

    internal static int Read(GameState state, string name, int knownTc)
    {
        var bank = state.NativeArmySimulation?.Parameters
            ?? throw new InvalidDataException("normal.turn.coalition.parameter_owner");
        string key = Prefix + name;
        if (bank.Float32Values.ContainsKey(key))
            throw new InvalidDataException("normal.turn.coalition.parameter_type_" + name);
        if (bank.Int32Values.TryGetValue(key, out int saved)) return saved;
        if (bank.Language == "TC") return knownTc;
        if (bank.Language != "SC")
            throw new InvalidDataException("normal.turn.coalition.parameter_locale_" + name);
        // Independent SC complete consumers and their actual RIP data qualify
        // these immutable defaults. No original executable is read at runtime,
        // and this query never installs or repairs a missing saved field.
        return name switch
        {
            "minimum-period-0" => 108,
            "minimum-period-1" => 90,
            "minimum-period-2" => 72,
            "minimum-period-other" => 54,
            "formation-chance-0" => 20,
            "formation-chance-1" => 30,
            "formation-chance-2" => 35,
            "formation-chance-other" => 40,
            "target-troops-0" => 500000,
            "target-troops-1" => 425000,
            "target-troops-2" => 350000,
            "target-troops-other" => 275000,
            "minimum-members" => 3,
            "member-relation-adjustment" => 20,
            "target-relation-adjustment" => -100,
            _ => throw new InvalidDataException("normal.turn.coalition.parameter_locale_" + name)
        };
    }
}
