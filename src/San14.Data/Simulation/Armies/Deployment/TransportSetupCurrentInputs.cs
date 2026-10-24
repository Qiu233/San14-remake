using San14.Core;

namespace San14.Data;

/// <summary>Fresh326230 preferences and extracted transport UI parameters.
/// Installed by fresh campaign composition only. Loading never calls this
/// initializer; missing old owners remain unknown.</summary>
public static class TransportSetupCurrentInputs
{
    private const string Prefix = "transport-setup-current:";
    public const string VersionKey = Prefix + "version";
    public const string DefaultTroopsKey = Prefix + "default-troops";
    public const string AmountStepKey = Prefix + "amount-step";
    public const string TroopsBaseKey = Prefix + "troops-base";
    public const string GoldBaseKey = Prefix + "gold-base";
    public const string SuppliesBaseKey = Prefix + "supplies-base";
    public const string TroopsMultiplierKey = Prefix + "troops-multiplier";
    public const string GoldMultiplierKey = Prefix + "gold-multiplier";
    public const string SuppliesMultiplierKey = Prefix + "supplies-multiplier";
    public const string PolicyCoefficientKey = Prefix + "policy-coefficient";
    public const string TraitCoefficientKey = Prefix + "trait-coefficient";
    public const string ReturnOfficerPreferenceKey = Prefix + "return-officer-preference";
    public const string BehaviorPreferenceKey = Prefix + "behavior-preference";

    public static GameState InstallFresh(GameState state)
    {
        //Only the existing TC TSV and326230 constructor were closed here.
        //A different locale is not assigned TC-only extracted defaults.
        if (state.NativeArmySimulation?.Parameters.Language != "TC") return state;
        if (state.NativeArmySimulation is not { } native ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            native.Parameters.Int32Values.ContainsKey(VersionKey))
            throw new InvalidDataException("Transport setup inputs require an unused fresh campaign.");
        var values = new Dictionary<string, int>(native.Parameters.Int32Values)
        {
            [VersionKey] = 1, [DefaultTroopsKey] = 1000, [AmountStepKey] = 100,
            [TroopsBaseKey] = 30000, [GoldBaseKey] = 50000, [SuppliesBaseKey] = 50000,
            [TroopsMultiplierKey] = 1, [GoldMultiplierKey] = 1, [SuppliesMultiplierKey] = 1,
            [PolicyCoefficientKey] = 1, [TraitCoefficientKey] = 1,
            //326230 explicitly writes both World preferences to1.
            [ReturnOfficerPreferenceKey] = 1, [BehaviorPreferenceKey] = 1
        };
        return state with { NativeArmySimulation = native with
            { Parameters = native.Parameters with { Int32Values = values } } };
    }

    public static int? Read(GameState state, string key) =>
        state.NativeArmySimulation?.Parameters.Int32Values is { } values &&
        values.GetValueOrDefault(VersionKey) == 1 && values.TryGetValue(key, out int value) ? value : null;
}
