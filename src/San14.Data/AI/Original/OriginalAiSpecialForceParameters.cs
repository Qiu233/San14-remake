using San14.Core;

namespace San14.Data;

internal static class OriginalAiSpecialForceParameters
{
    internal const string Prefix = "original-ai.special.";
    // TC initial data only. Fresh construction copies these into the saved
    // current parameter owner; queries never use this table as a fallback.
    internal static readonly (string Name, int Value)[] FreshTc =
    [
        ("defense.minimum-available-people", 1), ("defense.minimum-home-troops", 4000),
        ("defense.radius", 5), ("defense.existing-coverage-percent", 200),
        ("defense.required-strength-percent", 150), ("defense.support-radius", 5),
        ("defense.remaining-people", 1), ("defense.remaining-home-troops", 3000),
        ("defense.split-people", 4), ("defense.split-strength", 2000),
        ("defense.split-count", 3), ("defense.home-reserve", 2000), ("defense.army-maximum", 20000),
        ("army-minimum", 1000),
        ("offense.minimum-available-people", 2), ("offense.minimum-home-troops", 10000),
        ("offense.maximum-existing-attack-armies", 0), ("relation-threshold", 50),
        ("offense.required-percent-normal", 30), ("offense.required-percent-hard", 40),
        ("offense.required-percent-very-hard", 50), ("offense.required-percent-super", 60),
        ("offense.target-support-radius", 10), ("offense.remaining-people", 2),
        ("offense.remaining-home-troops", 6000), ("offense.split-people", 4),
        ("offense.split-strength", 2000), ("offense.split-count", 3),
        ("offense.home-reserve", 5000), ("offense.army-maximum", 20000),
        ("offense.minimum-periods-normal", 72), ("offense.minimum-periods-hard", 48),
        ("offense.minimum-periods-very-hard", 36), ("offense.minimum-periods-super", 18),
        ("offense.probability-normal", 3), ("offense.probability-hard", 4),
        ("offense.probability-very-hard", 5), ("offense.probability-super", 6),
        ("offense.targets-normal", 1), ("offense.targets-hard", 2),
        ("offense.targets-very-hard", 3), ("offense.targets-super", 4),
        ("offense.player-boost-probability", 50), ("offense.player-boost-normal", 70),
        ("offense.player-boost-hard", 85), ("offense.player-boost-very-hard", 100),
        ("offense.player-boost-super", 140),
        ("withdrawal.pursuit-distance", 11), ("withdrawal.enemy-radius", 5),
        ("withdrawal.strength-percent", 150), ("withdrawal.friendly-radius", 5)
    ];

    internal static int Read(GameState state, string name)
    {
        var parameters = state.NativeArmySimulation?.Parameters
            ?? throw new InvalidDataException("original_ai.special.parameters");
        string key = Prefix + name;
        if (parameters.Float32Values.ContainsKey(key) || !parameters.Int32Values.TryGetValue(key, out int value))
            throw new InvalidDataException("original_ai.special.parameter." + name);
        return value;
    }
}
