using San14.Core;

namespace San14.Data;

/// <summary>Named, saved rule inputs for ordinary captive treatment. The
/// offline source is recorded with the candidate; the game reads no executable.</summary>
internal static class CaptiveTreatmentParameters
{
    internal static readonly IReadOnlyDictionary<string, int> Defaults = new Dictionary<string, int>
    {
        ["execution-relation-limit"] = 70,
        ["execution-ability-and-ruler-limit"] = 70,
        ["execution-female-reduction"] = 8,
        ["execution-base-rate"] = 2,
        ["execution-frequent-base-rate"] = 4,
        ["execution-ability-rate-addend"] = 50,
        ["release-relation-gain"] = 7,
        ["release-popularity-gain"] = 1,
        ["execute-popularity-per-rank"] = 3,
        ["execute-ruler-popularity-loss"] = 20,
        //The existing complete offline scalar-block reference/escape audit
        //qualifies these loader initial values, independently of any save.
        ["hostility-relation-floor"] = 0,
        ["execution-ruler-tendency-floor"] = 0,
    };
    internal static string Key(string name) => "captive-treatment:" + name;
    internal static int Read(GameState state, string name)
    {
        if (state.NativeArmySimulation?.Parameters is { Language: "TC" or "SC" } bank &&
            bank.RuleVersion == SimulationRuleIdentity.Version && !bank.Float32Values.ContainsKey(Key(name)))
        {
            if (bank.Int32Values.TryGetValue(Key(name), out int saved)) return saved;
            if (Defaults.TryGetValue(name, out int initial)) return initial;
        }
        throw new InvalidDataException("captive-treatment.parameter." + name);
    }
}
