using San14.Core;

namespace San14.Data;

internal readonly record struct AreaAdministrationUpkeepInputs(int BaseCost,
    int PolicyReductionScale, int PersonalityReductionScale);

/// <summary>CE current regional upkeep coefficients and the per-appointment
/// calculation. Fresh allocation installs the proved initial values once;
/// queries consume only the saved current parameter bank.</summary>
public static class AreaAdministrationUpkeep
{
    private const string Prefix = "ce-area-administration-upkeep:";
    public const string VersionKey = Prefix + "version";
    public const string BaseCostKey = Prefix + "base-cost";
    public const string PolicyReductionScaleKey = Prefix + "policy-reduction-scale";
    public const string PersonalityReductionScaleKey = Prefix + "personality-reduction-scale";

    public static GameState InstallFresh(GameState state)
    {
        var simulation = state.NativeArmySimulation ?? throw Missing("parameters");
        var parameters = simulation.Parameters;
        if (parameters.Language is not ("TC" or "SC") ||
            parameters.RuleVersion != SimulationRuleIdentity.ForLocale(parameters.Language) ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            parameters.Int32Values.Keys.Concat(parameters.Float32Values.Keys)
                .Any(key => key.StartsWith(Prefix, StringComparison.Ordinal)))
            throw Missing("fresh_owner");
        //Paired CE TC/SC offline profile: base50, policy/personality scales1.
        //These are allocation values, never defaults for an existing save.
        var values = new Dictionary<string, int>(parameters.Int32Values)
        {
            [VersionKey] = 1, [BaseCostKey] = 50,
            [PolicyReductionScaleKey] = 1, [PersonalityReductionScaleKey] = 1
        };
        return state with { NativeArmySimulation = simulation with
            { Parameters = parameters with { Int32Values = values } } };
    }

    internal static AreaAdministrationUpkeepInputs ReadCurrent(GameState state)
    {
        var parameters = state.NativeArmySimulation?.Parameters ?? throw Missing("parameters");
        if (parameters.Language is not ("TC" or "SC") ||
            parameters.RuleVersion != SimulationRuleIdentity.ForLocale(parameters.Language) ||
            Read(VersionKey) != 1) throw Missing("owner_version");
        return new(Read(BaseCostKey), Read(PolicyReductionScaleKey), Read(PersonalityReductionScaleKey));
        int Read(string key) => !parameters.Float32Values.ContainsKey(key) &&
            parameters.Int32Values.TryGetValue(key, out int value) ? value : throw Missing(key);
    }

    internal static int Calculate(AreaAdministrationUpkeepInputs input, int policy, int personalities)
    {
        float policyAmount = (float)unchecked(input.PolicyReductionScale * policy) * (float)input.BaseCost;
        policyAmount /= 100f;
        int policyReduction = policyAmount <= 0f ? 0 : policyAmount < 1f ? 1
            : BattleUnitSourceMath.TruncateSingleToInt32(policyAmount);
        float personalityAmount = (float)unchecked(input.PersonalityReductionScale * personalities) * (float)input.BaseCost;
        personalityAmount /= 100f;
        int personalityReduction = personalityAmount == 0f ? 0
            : personalityAmount is > 0f and < 1f ? 1
            : personalityAmount is > -1f and < 0f ? -1
            : BattleUnitSourceMath.TruncateSingleToInt32(personalityAmount);
        return Math.Max(0, unchecked(unchecked(input.BaseCost - personalityReduction) - policyReduction));
    }

    private static InvalidDataException Missing(string input) => new("area-draft.upkeep_" + input);
}
