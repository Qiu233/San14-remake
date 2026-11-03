using San14.Core;

namespace San14.Data;

/// <summary>Owned scalar keys and the independently qualified TC/SC static
/// defaults. Existing saved values retain precedence over those defaults.</summary>
internal static class AutomaticTacticDamageCurrentParameters
{
    private const string Prefix = "simulation.tactics.troop-damage.";
    internal const string AbilityBaseline = Prefix + "ability-baseline";
    internal const string TroopDivisor = Prefix + "troop-divisor";
    internal const string SquareRootOffset = Prefix + "square-root-offset";
    internal const string CasterModifierScale = Prefix + "caster-modifier-scale";
    internal const string SpecialForcePercent = Prefix + "special-force-percent";
    internal const string OrdinaryReductionPercent = Prefix + "ordinary-reduction-percent";
    internal const string ProvokedOtherSourcePercent = Prefix + "provoked-other-source-percent";

    //TC176C10/177610 and SC176970/177370 have independently qualified
    //file-backed initial rules. Both language profiles use this same set;
    //an explicitly saved current value still wins. No original executable
    //or cached program execution is consumed by the game.
    private static readonly IReadOnlyDictionary<string, int> CeDefaults = new Dictionary<string, int>
    {
        [AbilityBaseline] = 5,
        [TroopDivisor] = 200,
        [SquareRootOffset] = 5,
        [CasterModifierScale] = 5,
        [SpecialForcePercent] = 120,
        [OrdinaryReductionPercent] = 30,
        [ProvokedOtherSourcePercent] = 130,
        [TacticRemakeCurrentContext.BaseWallTacticSmallDamageReductionPercent] = 30
    };

    internal static int? ReadCurrent(GameState state, string key)
    {
        var bank = state.NativeArmySimulation?.Parameters;
        if (bank is null || bank.Float32Values.ContainsKey(key)) return null;
        if (bank.Int32Values.TryGetValue(key, out int current)) return current;
        return bank.Language is "TC" or "SC" && bank.RuleVersion == SimulationRuleIdentity.Version &&
            CeDefaults.TryGetValue(key, out int value) ? value : null;
    }

    internal static GameState InstallFresh(GameState state)
    {
        var simulation = state.NativeArmySimulation
            ?? throw new InvalidDataException("Automatic tactic parameters have no fresh simulation owner.");
        var parameters = simulation.Parameters;
        if (parameters.Language is not ("TC" or "SC") || parameters.RuleVersion != SimulationRuleIdentity.Version) return state;
        var values = new Dictionary<string, int>(parameters.Int32Values);
        foreach (var row in CeDefaults)
        {
            if (parameters.Float32Values.ContainsKey(row.Key))
                throw new InvalidDataException("Automatic tactic parameter has a non-integer saved owner: " + row.Key);
            values.TryAdd(row.Key, row.Value);
        }
        return state with { NativeArmySimulation = simulation with
            { Parameters = parameters with { Int32Values = values } } };
    }
}

internal sealed record AutomaticArmyBaseTroopDamageMathInput(byte EffectValue,
    int SourceAbility, int AbilityBaseline, int StageProduct, int SourceTroops,
    int TroopDivisor, int SquareRootOffset, float AbilityFactor,
    float CasterModifierFactor, float WaitingFactor, int ForcePercent, float PanelFactor);

internal static class TacticArmyBaseTroopDamageMath
{
    //The existing Base recipient entry retains its genuine factor1. Army
    //recipients supply the separately queried current provocation factor.
    //Both share the exact Army-caster numeric prefix and additive literal1;
    //their target-Army trait56 query belongs after this returned truncation.
    internal static int BeforeSettings(AutomaticArmyBaseTroopDamageMathInput input)
        => BeforeSettings(input, 1f);

    internal static int BeforeSettings(AutomaticArmyBaseTroopDamageMathInput input,
        float targetProvocationFactor)
        => BeforeSettings(input, targetProvocationFactor, 1f);

    internal static int BeforeSettings(AutomaticArmyBaseTroopDamageMathInput input,
        float targetProvocationFactor, float sourceAdditive)
    {
        int ability = Math.Max(70, unchecked(input.SourceAbility + input.AbilityBaseline));
        double root = Math.Sqrt((double)(input.SourceTroops / input.TroopDivisor));
        int rootTerm = Truncate(root + (double)input.SquareRootOffset);
        float value = (float)unchecked(input.EffectValue * ability);
        value *= (float)input.StageProduct / 100f + 1f;
        value *= (float)rootTerm / 10f;
        value *= input.AbilityFactor;
        value *= targetProvocationFactor;
        value *= input.CasterModifierFactor;
        value *= input.WaitingFactor;
        value *= (float)input.ForcePercent;
        value /= 100f;
        value *= input.PanelFactor;
        value += sourceAdditive;
        return Truncate(value);
    }
    internal static int Advanced(int amount, int percent) => unchecked(amount * percent) / 100;
    internal static int OrdinaryReduced(int amount, int percent)
    {
        //Native signed32 product followed by its negative-divide-by100 magic
        //sequence, including product overflow, before the signed addition.
        int product = unchecked(amount * percent);
        int high = (int)(((long)product * -1374389535L) >> 32);
        int quotient = high >> 5;
        quotient = unchecked(quotient + (int)((uint)quotient >> 31));
        return unchecked(amount + quotient);
    }
    private static int Truncate(double value) => !double.IsFinite(value) ||
        value >= 2147483648d || value < -2147483648d ? int.MinValue : (int)value;
}
