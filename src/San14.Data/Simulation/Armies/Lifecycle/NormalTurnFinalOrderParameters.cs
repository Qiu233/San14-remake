namespace San14.Data;

/// <summary>Immutable initial parameters independently read from the SC and TC
/// final-order functions. The caller handles saved current values first; this
/// reader never installs defaults into an owner or reads original code.</summary>
internal static class NormalTurnFinalOrderParameters
{
    internal static int ReadInitial(string language, string name, int tc)
    {
        // Each SC value has its own actual source reference. The TC branch
        // retains the existing call-site value; unknown names have no fallback.
        int sc = name switch
        {
            "good-omen-chance" => 15,
            "omen-morale" => 10,
            "auspicious-sign-chance" => 30,
            "auspicious-popularity" => 150,
            "good-omen-popularity" => 100,
            "auspicious-loyalty" => 5,
            "bad-omen-chance" => 30,
            "bad-omen-popularity" => 100,
            "doctrine-bonus-chance" => 50,
            "yellow-emperor-development" => 20,
            "han-emperor-development" => 20,
            "other-emperor-development" => 20,
            "doctrine-bonus-first-threshold" => 50,
            "doctrine-bonus-second-threshold" => 75,
            "doctrine-bonus-gold" => 5000,
            "doctrine-bonus-supplies" => 20000,
            "doctrine-bonus-troops" => 3000,
            "population-decline-limit" => 30,
            "population-growth-limit" => 30,
            "annual-loyalty" => 10,
            "annual-relation" => 30,
            "annual-doctrine-reserve" => 1,
            _ => throw Missing("parameter_" + name)
        };
        return language switch
        {
            "TC" => tc,
            "SC" => sc,
            _ => throw Missing("locale_parameter_" + name)
        };
    }

    private static InvalidDataException Missing(string name)
        => new("normal-turn.final-orders." + name);
}
